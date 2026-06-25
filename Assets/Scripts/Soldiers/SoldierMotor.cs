using UnityEngine;
using UnityEngine.AI;


/// -----------------------------------------------------------------------------
/// SoldierMotor
/// -----------------------------------------------------------------------------
///
/// Movement wrapper around the soldier NavMeshAgent.
/// Provides MoveTo, Stop, Warp, speed setup, formation-delta movement, and
/// velocity/facing-based rotation.
/// Respects SoldierController movement locks so committed actions like Attack,
/// HitReact, and Death cannot be overridden by normal movement requests.
///
/// This class should not decide why the soldier moves. It only executes movement
/// requests when movement is allowed.
///
/// Design role:
/// Low-level soldier movement execution.
///
[RequireComponent(typeof(NavMeshAgent))]
public class SoldierMotor : MonoBehaviour
{
    [Header("Avoidance")]
    [SerializeField] private float agentRadius = 0.4f;
    [SerializeField] private float agentHeight = 2f;
    [SerializeField] private ObstacleAvoidanceType obstacleAvoidanceType =
        ObstacleAvoidanceType.HighQualityObstacleAvoidance;

    [SerializeField] private int avoidancePriorityMin = 40;
    [SerializeField] private int avoidancePriorityMax = 60;
    
    [SerializeField] private float defaultStoppingDistance = 0.1f;

    private NavMeshAgent agent;
    private SoldierController soldierController;

    private float baseMoveSpeed = 4f;
    private float turnSpeed = 900f;
    private float velocityRotationSuppressedUntil = 0f;

    // Formation movement uses NavMeshAgent.Move rather than SetDestination.
    // NavMeshAgent.Move does not create a path, so we cache a short-lived manual
    // velocity for animation and movement-state queries.
    private Vector3 manualMovementVelocity = Vector3.zero;
    private float manualMovementVelocityValidUntil = 0f;

    private bool HasManualMovementVelocity =>
        Time.time <= manualMovementVelocityValidUntil &&
        manualMovementVelocity.sqrMagnitude > 0.0001f;

    public NavMeshAgent Agent => agent;
    public bool HasPath => agent != null && (agent.hasPath || HasManualMovementVelocity);

    public Vector3 Velocity
    {
        get
        {
            if (HasManualMovementVelocity)
                return manualMovementVelocity;

            return agent != null ? agent.velocity : Vector3.zero;
        }
    }
    
    public float CurrentMoveSpeedLimit =>
        agent != null
            ? Mathf.Max(0.001f, agent.speed)
            : Mathf.Max(0.001f, baseMoveSpeed); // used in SoldierAnimator for calculating speed

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        soldierController = GetComponent<SoldierController>();

        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.obstacleAvoidanceType = obstacleAvoidanceType;
        agent.avoidancePriority = Random.Range(
            avoidancePriorityMin,
            avoidancePriorityMax + 1);

        agent.angularSpeed = 99999f;
        agent.acceleration = 99999f;
        agent.autoBraking = false;
        agent.updateRotation = false;
    }

    void Update()
    {
        RotateTowardVelocity();
    }

    public void Initialize(MovementStats movement)
    {
        baseMoveSpeed = movement.moveSpeed > 0f ? movement.moveSpeed : baseMoveSpeed;
        turnSpeed = movement.turnSpeed > 0f ? movement.turnSpeed : turnSpeed;

        if (agent == null)
            return;

        agent.speed = baseMoveSpeed;
        agent.acceleration = movement.acceleration > 0f
            ? movement.acceleration
            : 99999f;
    }

    public void MoveTo(
        Vector3 position,
        float stoppingDistance = -1f,
        float speedMultiplier = 1f)
    {
        if (!CanMove())
            return;

        if (soldierController != null && soldierController.IsMovementLocked)
            return;

        agent.isStopped = false;
        agent.speed = baseMoveSpeed * Mathf.Max(0.1f, speedMultiplier);
        agent.stoppingDistance = stoppingDistance >= 0f
            ? stoppingDistance
            : defaultStoppingDistance;

        agent.SetDestination(position);
    }

    /// Moves the soldier by a formation-controlled world delta instead of giving
    /// the soldier an independent path destination.
    ///
    /// This is used by SquadMovement.FormedMove so the squad can move as one
    /// shared formation body while each soldier remains a normal NavMeshAgent.
    ///
    /// Important:
    /// Formation facing and soldier visual facing are not always the same.
    /// During movement, the soldier should usually face its own movement delta.
    /// When not moving, callers can explicitly rotate the soldier toward final
    /// formation facing through FaceDirection().
    public void MoveByFormationDelta(
        Vector3 movementDelta,
        Vector3 fallbackFacingDirection,
        float speedLimit = -1f,
        bool faceMovementDirection = true)
    {
        if (!CanMove())
            return;

        if (soldierController != null && soldierController.IsMovementLocked)
            return;

        if (agent.hasPath)
            agent.ResetPath();

        float resolvedSpeedLimit = speedLimit > 0f
            ? speedLimit
            : baseMoveSpeed;

        agent.isStopped = false;
        agent.stoppingDistance = 0f;
        agent.speed = resolvedSpeedLimit;

        movementDelta.y = 0f;

        float maxDistanceThisFrame =
            Mathf.Max(0f, resolvedSpeedLimit) * Time.deltaTime;

        if (maxDistanceThisFrame > 0f &&
            movementDelta.magnitude > maxDistanceThisFrame)
        {
            movementDelta = movementDelta.normalized * maxDistanceThisFrame;
        }

        manualMovementVelocity = Time.deltaTime > 0f
            ? movementDelta / Time.deltaTime
            : Vector3.zero;

        manualMovementVelocityValidUntil = Time.time + 0.12f;

        if (movementDelta.sqrMagnitude > 0.000001f)
            agent.Move(movementDelta);

        Vector3 visualFacingDirection = fallbackFacingDirection;

        if (faceMovementDirection &&
            movementDelta.sqrMagnitude > 0.000001f)
        {
            visualFacingDirection = movementDelta;
        }

        RotateTowardDirection(visualFacingDirection);
    }
    
    /// Rotates the soldier toward a requested direction without issuing movement.
    /// Used when a soldier has reached its slot and should settle into the squad's
    /// final facing.
    public void FaceDirection(Vector3 direction)
    {
        if (!CanMove())
            return;

        if (soldierController != null && soldierController.IsMovementLocked)
            return;

        RotateTowardDirection(direction);
    }

    public void Stop()
    {
        if (!CanMove())
            return;

        agent.ResetPath();
        agent.isStopped = false;
        agent.speed = baseMoveSpeed;

        manualMovementVelocity = Vector3.zero;
        manualMovementVelocityValidUntil = 0f;
    }

    public void Warp(Vector3 position)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Warp(position);
        else
            transform.position = position;
    }

    public void SetBaseSpeed(float speed)
    {
        baseMoveSpeed = Mathf.Max(0f, speed);

        if (agent != null)
            agent.speed = baseMoveSpeed;
    }

    public void SuppressVelocityRotation(float duration = 0.12f)
    {
        velocityRotationSuppressedUntil = Mathf.Max(
            velocityRotationSuppressedUntil,
            Time.time + Mathf.Max(0f, duration));
    }

    bool CanMove()
    {
        return agent != null &&
               agent.enabled &&
               agent.isActiveAndEnabled &&
               agent.isOnNavMesh;
    }

    void RotateTowardVelocity()
    {
        if (Time.time < velocityRotationSuppressedUntil)
            return;

        if (soldierController != null && soldierController.IsMovementLocked)
            return;

        if (agent == null)
            return;

        Vector3 velocity = Velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.01f)
            return;

        RotateTowardDirection(velocity.normalized);
    }

    void RotateTowardDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            direction.normalized,
            Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);
    }
}





// using UnityEngine;
// using UnityEngine.AI;
//
//
// /// -----------------------------------------------------------------------------
// /// SoldierMotor
// /// -----------------------------------------------------------------------------
// ///
// /// Movement wrapper around the soldier NavMeshAgent.
// /// Provides MoveTo, Stop, Warp, speed setup, and velocity-based rotation.
// /// Respects SoldierController movement locks so committed actions like Attack,
// /// HitReact, and Death cannot be overridden by normal movement requests.
// ///
// /// This class should not decide why the soldier moves. It only executes movement
// /// requests when movement is allowed.
// ///
// /// Design role:
// /// Low-level soldier movement execution.
// ///
//
// [RequireComponent(typeof(NavMeshAgent))]
// public class SoldierMotor : MonoBehaviour
// {
//     [Header("Avoidance")]
//     [SerializeField] private float agentRadius = 0.4f;
//     [SerializeField] private float agentHeight = 2f;
//     [SerializeField] private ObstacleAvoidanceType obstacleAvoidanceType =
//         ObstacleAvoidanceType.HighQualityObstacleAvoidance;
//
//     [SerializeField] private int avoidancePriorityMin = 40;
//     [SerializeField] private int avoidancePriorityMax = 60;
//     
//     [SerializeField] private float defaultStoppingDistance = 0.1f;
//
//     private NavMeshAgent agent;
//     private SoldierController soldierController;
//
//     private float baseMoveSpeed = 4f;
//     private float turnSpeed = 900f;
//     private float velocityRotationSuppressedUntil = 0f;
//
//     public NavMeshAgent Agent => agent;
//     public bool HasPath => agent != null && agent.hasPath;
//     public Vector3 Velocity => agent != null ? agent.velocity : Vector3.zero;
//     
//     public float CurrentMoveSpeedLimit =>
//         agent != null
//             ? Mathf.Max(0.001f, agent.speed)
//             : Mathf.Max(0.001f, baseMoveSpeed); // used in SoldierAnimator for calculating speed
//
//     void Awake()
//     {
//         agent = GetComponent<NavMeshAgent>();
//         soldierController = GetComponent<SoldierController>();
//
//         agent.radius = agentRadius;
//         agent.height = agentHeight;
//         agent.obstacleAvoidanceType = obstacleAvoidanceType;
//         agent.avoidancePriority = Random.Range(
//             avoidancePriorityMin,
//             avoidancePriorityMax + 1);
//
//         agent.angularSpeed = 99999f;
//         agent.acceleration = 99999f;
//         agent.autoBraking = false;
//         agent.updateRotation = false;
//     }
//
//     void Update()
//     {
//         RotateTowardVelocity();
//     }
//
//     public void Initialize(MovementStats movement)
//     {
//         baseMoveSpeed = movement.moveSpeed > 0f ? movement.moveSpeed : baseMoveSpeed;
//         turnSpeed = movement.turnSpeed > 0f ? movement.turnSpeed : turnSpeed;
//
//         if (agent == null)
//             return;
//
//         agent.speed = baseMoveSpeed;
//         agent.acceleration = movement.acceleration > 0f
//             ? movement.acceleration
//             : 99999f;
//     }
//
//     public void MoveTo(
//         Vector3 position,
//         float stoppingDistance = -1f,
//         float speedMultiplier = 1f)
//     {
//         if (!CanMove())
//             return;
//
//         if (soldierController != null && soldierController.IsMovementLocked)
//             return;
//
//         agent.isStopped = false;
//         agent.speed = baseMoveSpeed * Mathf.Max(0.1f, speedMultiplier);
//         agent.stoppingDistance = stoppingDistance >= 0f
//             ? stoppingDistance
//             : defaultStoppingDistance;
//
//         agent.SetDestination(position);
//     }
//
//     public void Stop()
//     {
//         if (!CanMove())
//             return;
//
//         agent.ResetPath();
//         agent.isStopped = false;
//         agent.speed = baseMoveSpeed;
//     }
//
//     public void Warp(Vector3 position)
//     {
//         if (agent != null && agent.enabled && agent.isOnNavMesh)
//             agent.Warp(position);
//         else
//             transform.position = position;
//     }
//
//     public void SetBaseSpeed(float speed)
//     {
//         baseMoveSpeed = Mathf.Max(0f, speed);
//
//         if (agent != null)
//             agent.speed = baseMoveSpeed;
//     }
//
//     public void SuppressVelocityRotation(float duration = 0.12f)
//     {
//         velocityRotationSuppressedUntil = Mathf.Max(
//             velocityRotationSuppressedUntil,
//             Time.time + Mathf.Max(0f, duration));
//     }
//
//     bool CanMove()
//     {
//         return agent != null &&
//                agent.enabled &&
//                agent.isActiveAndEnabled &&
//                agent.isOnNavMesh;
//     }
//
//     void RotateTowardVelocity()
//     {
//         if (Time.time < velocityRotationSuppressedUntil)
//             return;
//
//         if (soldierController != null && soldierController.IsMovementLocked)
//             return;
//
//         if (agent == null)
//             return;
//
//         Vector3 velocity = agent.velocity;
//         velocity.y = 0f;
//
//         if (velocity.sqrMagnitude < 0.01f)
//             return;
//
//         Quaternion targetRotation = Quaternion.LookRotation(
//             velocity.normalized,
//             Vector3.up);
//
//         transform.rotation = Quaternion.RotateTowards(
//             transform.rotation,
//             targetRotation,
//             turnSpeed * Time.deltaTime);
//     }
// }