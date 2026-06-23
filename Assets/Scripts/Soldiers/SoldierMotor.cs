using UnityEngine;
using UnityEngine.AI;


/// -----------------------------------------------------------------------------
/// SoldierMotor
/// -----------------------------------------------------------------------------
///
/// Movement wrapper around the soldier NavMeshAgent.
/// Provides MoveTo, Stop, Warp, speed setup, and velocity-based rotation.
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

    public NavMeshAgent Agent => agent;
    public bool HasPath => agent != null && agent.hasPath;
    public Vector3 Velocity => agent != null ? agent.velocity : Vector3.zero;
    
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

    public void Stop()
    {
        if (!CanMove())
            return;

        agent.ResetPath();
        agent.isStopped = false;
        agent.speed = baseMoveSpeed;
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

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            velocity.normalized,
            Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);
    }
}