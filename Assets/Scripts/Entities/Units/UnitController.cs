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
    protected ControlState controlState = ControlState.AIControlled;

    protected UnitState state = UnitState.Idle;
    public UnitState State => state;
    protected UnitState prevState = UnitState.Idle;

    // Movement
    protected Vector3 homePosition;

    // Combat
    protected EntityController attackTarget;
    public EntityController AttackTarget => attackTarget;
    protected float attackTimer;

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
        mainCamera = Camera.main;
        homePosition = transform.position;
        if (selectionDecal != null) selectionDecal.enabled = false;
    }

    protected virtual void Update()
    {
        HandleState();
        UpdateRotation();
    }

    
    #region Handle State
    protected virtual void HandleState()
    {
        if (attackTimer > 0f) attackTimer -= Time.deltaTime;
        
        switch (state)
        {
            case UnitState.Idle: break;
            case UnitState.Moving: HandleMovingState(); break;
            case UnitState.Attacking: HandleAttackingState(); break;
        }
    }

    protected virtual void HandleMovingState()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (attackTarget != null) state = UnitState.Attacking;
            else state = UnitState.Idle;
        }
    }

    // Basic attack state — subclasses override for stance logic
    protected virtual void HandleAttackingState()
    {
        if (attackTarget == null || !attackTarget.Stats.IsAlive)
        {
            attackTarget = null;
            state = UnitState.Idle;
            return;
        }

        if (Calc.WithinRange(attackTarget.transform.position, transform.position, stats.attackRange))
        {
            if (attackTimer <= 0f)
                TriggerAttack();
        }
        else
        {
            agent.SetDestination(attackTarget.transform.position);
            state = UnitState.Moving;
        }
    }
    #endregion

    
    #region Attack
    protected void TriggerAttack()
    {
        unitAnimator.TriggerAttack();
        attackTimer = stats.attackInterval; // check
        controlState = ControlState.Locked; 
        //Debug.Log("trigger attack");
    }

    // Called by animation event
    public void DealAttackDamage()
    {
        if (attackTarget == null) return;
        attackTarget.TakeDamage(stats.attackDamage);
        //Debug.Log(attackTarget.name + " was dealt " + stats.attackDamage + " attack damage");
    }
    #endregion
    
    
    #region Animation Events
    public virtual void OnAttackImpact()
    {
        // Base damage - works for any unit that attacks - called by UnitAnimator
        if (attackTarget == null) return;
        DealAttackDamage();
    }
    
    //public virtual void OnAttackEnd() { /* Only Military Units need this for now */ }
    public virtual void OnAttackEnd()
    {
        controlState = ControlState.AIControlled; // flow: if in Player control state attack -> TriggerAttack -> Locked control state -> now AI Controlled Attacking State
    }
    #endregion

    protected void SetUnitState(UnitState newState)
    {
        prevState = state;
        state = newState;
    }
    
    #region Orders
    public virtual void OrderMove(Vector3 destination)
    {
        attackTarget = null;
        homePosition = destination;
        agent.stoppingDistance = 0.1f;
        agent.SetDestination(destination);
        state = UnitState.Moving;
    }

    public virtual void OrderAttack(EntityController target)
    {
        if (target == null) return;
        attackTarget = target;
        agent.stoppingDistance = stats.attackRange;
        agent.SetDestination(attackTarget.transform.position);
        state = UnitState.Moving;

        Vector3 dir = Calc.Dir(transform.position, target.transform.position);
        if (Calc.WithinRange(dir, stats.attackRange))
            transform.rotation = SnapToYAngles(dir);
    }

    public virtual void OrderStop()
    {
        attackTarget = null;
        agent.ResetPath();
        state = UnitState.Idle;
    }
    #endregion
    
    
    #region Rotation
    protected virtual void UpdateRotation()
    {
        switch (state)
        {
            case UnitState.Returning:
            case UnitState.Patrolling:
            case UnitState.Moving: RotateTowardVelocity(); break;
            case UnitState.Attacking: RotateTowardIfNeeded(attackTarget?.transform.position); break;
        }
        
    }

    protected void RotateTowardVelocity()
    {
        if (agent.velocity.sqrMagnitude < 0.1f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            SnapToYAngles(agent.velocity),
            900f * Time.deltaTime);
    }

    protected void RotateToward(Vector3? targetPos)
    {
        if (targetPos == null) return;
        Vector3 dir = targetPos.Value - transform.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;
        transform.rotation = SnapToYAngles(dir);
    }

    protected void RotateTowardIfNeeded(Vector3? targetPos, float threshold = 22.5f)
    {
        if (targetPos == null) return;
        Vector3 dir = targetPos.Value - transform.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;
        if (Vector3.Angle(transform.forward, dir) > threshold)
            transform.rotation = SnapToYAngles(dir);
    }

    protected Quaternion SnapToYAngles(Vector3 direction)
    {
        float angleDiv = 22.5f;
        direction.y = 0f;
        if (direction == Vector3.zero) return transform.rotation;
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / angleDiv) * angleDiv;
        return Quaternion.Euler(0f, snapped, 0f);
    }
    #endregion
}




