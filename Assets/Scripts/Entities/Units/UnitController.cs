using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class UnitController : EntityController
{
    protected NavMeshAgent agent;
    public NavMeshAgent Agent => agent;
    protected Camera mainCamera;
    protected Rigidbody rb;
    protected UnitAnimator unitAnimator;
    public UnitAnimator UnitAnimator => unitAnimator;

    // State
    protected UnitState state = UnitState.Idle;
    public UnitState State => state;

    // Combat
    protected EntityController attackTarget;
    public EntityController AttackTarget => attackTarget;
    protected float attackTimer;

    // Gathering
    protected ResourceNode nodeTarget;
    protected float gatherTimer;

    public override bool IsDragSelectable => true;

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();
        agent.angularSpeed = 99999f;
        agent.acceleration = 99999f;
        agent.autoBraking = false;
        agent.updateRotation = false;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.freezeRotation = true;
        rb.useGravity = false;
        unitAnimator = GetComponentInChildren<UnitAnimator>();
        if (unitAnimator == null) Debug.LogWarning("No UnitAnimator on " + gameObject.name);
    }

    protected override void Start()
    {
        base.Start();
        agent.speed = stats.moveSpeed;
        attackTimer = stats.attackInterval;
        gatherTimer = stats.gatherInterval;
        mainCamera = Camera.main;
        if (selectionDecal != null) selectionDecal.enabled = false;
    }

    protected virtual void Update()
    {
        HandleState();
        HandleRotation();
        if(attackTimer > 0f) 
            attackTimer -= Time.deltaTime;
    }
    

    // Routes to correct state handler each frame
    protected virtual void HandleState()
    {
        switch (state)
        {
            case UnitState.Idle: break;
            case UnitState.Moving: HandleMovingState(); break;
            case UnitState.Attacking: HandleAttackingState(); break;
            case UnitState.Gathering: HandleGatheringState(); break;
        }
    }

    // Checks NavMesh arrival and transitions to next state
    protected virtual void HandleMovingState()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (nodeTarget != null) state = UnitState.Gathering;
            else if (attackTarget != null) state = UnitState.Attacking;
            else state = UnitState.Idle;
        }
    }

    // Handles combat — chases, attacks, cleans up dead targets
    protected virtual void HandleAttackingState()
    {
        if (attackTarget == null)
        {
            state = UnitState.Idle;
            return;
        }

        Vector3 toTarget = attackTarget.transform.position - transform.position;
        float sqrDistance = toTarget.sqrMagnitude;
        float sqrAttackRange = stats.attackRange * stats.attackRange;

        if (sqrDistance > sqrAttackRange)
        {
            agent.SetDestination(attackTarget.transform.position);
            state = UnitState.Moving;
            return;
        }

        if (attackTimer <= 0f)
        {
            unitAnimator.TriggerAttack();
            attackTimer = stats.attackInterval;
        }
    }

    // Called by animation event — deals damage to current attack target
    public void DealAttackDamage()
    {
        if (attackTarget == null) return;
        attackTarget.TakeDamage(stats.attackDamage);
    }

    // Ticks gather timer and pulls resources from node
    protected virtual void HandleGatheringState()
    {
        if (nodeTarget == null || !nodeTarget.HasResources())
        {
            state = UnitState.Idle;
            nodeTarget = null;
            return;
        }

        gatherTimer -= Time.deltaTime;
        if (gatherTimer <= 0f)
        {
            int harvested = nodeTarget.Harvest(stats.gatherAmount);
            GameManager.Instance.AddResources(nodeTarget.resourceNodeData.resourceType, harvested, stats.faction);
            gatherTimer = stats.gatherInterval;
        }
    }

    // Orders unit to attack a target
    public void OrderAttack(EntityController target)
    {
        if (target == null) return;
        attackTarget = target;
        nodeTarget = null;
        agent.stoppingDistance = stats.attackRange;
        agent.SetDestination(attackTarget.transform.position);
        state = UnitState.Moving;

        // Snap rotation if already in range
        Vector3 dir = (target.transform.position - transform.position);
        if (dir.sqrMagnitude < stats.attackRange * stats.attackRange)
            transform.rotation = SnapToXDirections(dir);
    }

    // Orders unit to gather from a resource node
    public void OrderGather(ResourceNode node)
    {
        if (node == null || stats.gatherAmount <= 0) return;
        nodeTarget = node;
        attackTarget = null;
        agent.stoppingDistance = stats.gatherRange;
        agent.SetDestination(nodeTarget.transform.position);
        state = UnitState.Moving;

        // Snap rotation if already in range
        Vector3 dir = (node.transform.position - transform.position);
        if (dir.sqrMagnitude < stats.gatherRange * stats.gatherRange)
            transform.rotation = SnapToXDirections(dir);
    }

    // Orders unit to move to a position
    public void OrderMove(Vector3 destination)
    {
        attackTarget = null;
        nodeTarget = null;
        agent.stoppingDistance = 0.1f;
        agent.SetDestination(destination);
        state = UnitState.Moving;
    }

    // Orders unit to stop immediately
    public void OrderStop()
    {
        attackTarget = null;
        nodeTarget = null;
        agent.ResetPath();
        state = UnitState.Idle;
    }

    void HandleRotation()
    {
        switch (state)
        {
            case UnitState.Moving: RotateTowardVelocity(); break;
            case UnitState.Attacking: RotateToward(attackTarget?.transform.position); break;
            case UnitState.Gathering: RotateTowardIfNeeded(nodeTarget?.transform.position); break;
            case UnitState.Idle: break;
        }
    }

    void RotateTowardVelocity()
    {
        if (agent.velocity.sqrMagnitude < 0.1f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            SnapToXDirections(agent.velocity),
            900f * Time.deltaTime);
    }

    void RotateToward(Vector3? targetPos)
    {
        if (targetPos == null) return;
        Vector3 dir = targetPos.Value - transform.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;
        transform.rotation = SnapToXDirections(dir);
    }

    void RotateTowardIfNeeded(Vector3? targetPos, float threshold = 22.5f)
    {
        if (targetPos == null) return;
        Vector3 dir = targetPos.Value - transform.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;
        if (Vector3.Angle(transform.forward, dir) > threshold)
            transform.rotation = SnapToXDirections(dir);
    }

    Quaternion SnapToXDirections(Vector3 direction)
    {
        float angleDiv = 22.5f;
        direction.y = 0f;
        if (direction == Vector3.zero) return transform.rotation;
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / angleDiv) * angleDiv;
        return Quaternion.Euler(0f, snapped, 0f);
    }
}