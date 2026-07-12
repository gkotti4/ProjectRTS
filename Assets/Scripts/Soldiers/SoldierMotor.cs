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
/// HitReact, Death, and temporary combat-lock withdrawal delays cannot be
/// overridden by normal movement requests.
///
/// Weight pass:
/// Normal NavMesh destination movement uses MovementStats acceleration.
/// Formation-delta movement uses a local weighted velocity so slot-following no
/// longer snaps instantly to every requested delta. This gives infantry subtle
/// body weight and gives cavalry a real place to tune acceleration/deceleration
/// and turn speed later.
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

    private const float legacyInstantAccelerationThreshold = 9000f;
    private const float defaultWeightedAcceleration = 12f;
    private const float defaultWeightedDeceleration = 18f;
    private const float defaultWeightedTurnSpeed = 540f;
    private const float manualMovementVelocityValidDuration = 0.12f;
    private const float sharpTurnDotThreshold = 0.15f;

    private float baseMoveSpeed = 4f;
    private float acceleration = defaultWeightedAcceleration;
    private float deceleration = defaultWeightedDeceleration;
    private float turnSpeed = defaultWeightedTurnSpeed;
    private float velocityRotationSuppressedUntil = 0f;

    // Formation movement uses NavMeshAgent.Move rather than SetDestination.
    // NavMeshAgent.Move does not create a path, so we maintain a short-lived
    // weighted manual velocity for animation, movement-state queries, and
    // formation-delta inertia.
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
        agent.acceleration = defaultWeightedAcceleration;
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
        acceleration = ResolveAcceleration(movement.acceleration);
        deceleration = ResolveDeceleration(movement.deceleration, acceleration);
        turnSpeed = movement.turnSpeed > 0f ? movement.turnSpeed : defaultWeightedTurnSpeed;

        if (agent == null)
            return;

        agent.speed = baseMoveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = turnSpeed;
    }

    public void MoveTo(
        Vector3 position,
        float stoppingDistance = -1f,
        float speedMultiplier = 1f)
    {
        if (!CanMove())
            return;

        if (IsMovementRequestLocked())
            return;

        ClearManualMovementVelocity();

        float resolvedSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);

        agent.isStopped = false;
        agent.speed = baseMoveSpeed * resolvedSpeedMultiplier;
        agent.acceleration = acceleration * Mathf.Max(1f, resolvedSpeedMultiplier);
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

        if (IsMovementRequestLocked())
            return;

        if (agent.hasPath)
            agent.ResetPath();

        float resolvedSpeedLimit = speedLimit > 0f
            ? speedLimit
            : baseMoveSpeed;

        agent.isStopped = false;
        agent.stoppingDistance = 0f;
        agent.speed = resolvedSpeedLimit;
        agent.acceleration = acceleration;

        movementDelta.y = 0f;

        Vector3 desiredVelocity = ResolveDesiredManualVelocity(
            movementDelta,
            resolvedSpeedLimit);

        Vector3 weightedVelocity = ResolveWeightedManualVelocity(
            desiredVelocity,
            resolvedSpeedLimit);

        Vector3 weightedMovementDelta = weightedVelocity * Time.deltaTime;

        manualMovementVelocity = weightedVelocity;
        manualMovementVelocityValidUntil = Time.time + manualMovementVelocityValidDuration;

        if (weightedMovementDelta.sqrMagnitude > 0.000001f)
            agent.Move(weightedMovementDelta);

        Vector3 visualFacingDirection = fallbackFacingDirection;

        if (faceMovementDirection &&
            weightedVelocity.sqrMagnitude > 0.0001f)
        {
            visualFacingDirection = weightedVelocity;
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

        if (IsMovementRequestLocked())
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
        agent.acceleration = acceleration;

        ClearManualMovementVelocity();
    }

    public void Warp(Vector3 position)
    {
        ClearManualMovementVelocity();

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

    bool IsMovementRequestLocked()
    {
        return soldierController != null &&
               (soldierController.IsMovementLocked ||
                soldierController.IsCombatMoveLocked);
    }

    float ResolveAcceleration(float requestedAcceleration)
    {
        if (requestedAcceleration > 0f &&
            requestedAcceleration < legacyInstantAccelerationThreshold)
        {
            return requestedAcceleration;
        }

        return defaultWeightedAcceleration;
    }

    float ResolveDeceleration(
        float requestedDeceleration,
        float resolvedAcceleration)
    {
        if (requestedDeceleration > 0f)
            return requestedDeceleration;

        if (resolvedAcceleration > 0f)
            return resolvedAcceleration * 1.5f;

        return defaultWeightedDeceleration;
    }

    Vector3 ResolveDesiredManualVelocity(
        Vector3 movementDelta,
        float speedLimit)
    {
        if (Time.deltaTime <= 0f)
            return Vector3.zero;

        Vector3 desiredVelocity = movementDelta / Time.deltaTime;
        desiredVelocity.y = 0f;

        float maxSpeed = Mathf.Max(0f, speedLimit);

        if (maxSpeed > 0f && desiredVelocity.magnitude > maxSpeed)
            desiredVelocity = desiredVelocity.normalized * maxSpeed;

        return desiredVelocity;
    }

    Vector3 ResolveWeightedManualVelocity(
        Vector3 desiredVelocity,
        float speedLimit)
    {
        Vector3 currentVelocity = HasManualMovementVelocity
            ? manualMovementVelocity
            : agent.velocity;

        currentVelocity.y = 0f;
        desiredVelocity.y = 0f;

        float currentSpeed = currentVelocity.magnitude;
        float desiredSpeed = desiredVelocity.magnitude;

        float rate = GetManualVelocityChangeRate(
            currentVelocity,
            desiredVelocity,
            currentSpeed,
            desiredSpeed);

        Vector3 weightedVelocity = Vector3.MoveTowards(
            currentVelocity,
            desiredVelocity,
            rate * Time.deltaTime);

        float maxSpeed = Mathf.Max(0f, speedLimit);

        if (maxSpeed > 0f && weightedVelocity.magnitude > maxSpeed)
            weightedVelocity = weightedVelocity.normalized * maxSpeed;

        return weightedVelocity;
    }

    float GetManualVelocityChangeRate(
        Vector3 currentVelocity,
        Vector3 desiredVelocity,
        float currentSpeed,
        float desiredSpeed)
    {
        if (desiredSpeed <= 0.01f)
            return deceleration;

        if (currentSpeed <= 0.01f)
            return acceleration;

        float directionDot = Vector3.Dot(
            currentVelocity.normalized,
            desiredVelocity.normalized);

        if (directionDot < sharpTurnDotThreshold)
            return deceleration;

        if (desiredSpeed < currentSpeed)
            return deceleration;

        return acceleration;
    }

    void ClearManualMovementVelocity()
    {
        manualMovementVelocity = Vector3.zero;
        manualMovementVelocityValidUntil = 0f;
    }

    void RotateTowardVelocity()
    {
        if (Time.time < velocityRotationSuppressedUntil)
            return;

        if (IsMovementRequestLocked())
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
