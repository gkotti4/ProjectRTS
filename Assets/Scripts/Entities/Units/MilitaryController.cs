// SESSION: Squad Control Refactor

using UnityEngine;

[RequireComponent(typeof(SquadMemberController))]
public class MilitaryController : UnitController
{
    #region Fields

    [Header("Legacy Standalone Military AI")]
    [SerializeField] private SquadStance stance = SquadStance.Aggressive;
    [SerializeField] private float scanInterval = 0.5f;

    private float scanTimer = 0f;
    private readonly Collider[] scanBuffer = new Collider[20];

    private UnitControlState unitControlState = UnitControlState.AIControlled;

    public SquadMemberController SquadMember { get; private set; }
    public bool IsSquadControlled => SquadMember != null && SquadMember.IsInSquad;

    #endregion

    #region Selection

    public override bool IsDragSelectable => !IsSquadControlled;

    public override void OnSelect()
    {
        // When squad-controlled, this is only a visual call from SquadController.
        // The member itself is not truly selected.
        if (IsSquadControlled)
        {
            isSelected = true;

            if (selectionDecal != null)
                selectionDecal.enabled = true;

            if (healthBar != null)
                healthBar.OnSelected();

            return;
        }

        base.OnSelect();
    }

    public override void OnDeselect()
    {
        if (IsSquadControlled)
        {
            isSelected = false;

            if (selectionDecal != null)
                selectionDecal.enabled = false;

            if (healthBar != null)
                healthBar.OnDeselected();

            return;
        }

        base.OnDeselect();
    }

    #endregion

    #region Unity Lifecycle

    protected override void Update()
    {
        // SquadController owns behavior while this unit is a squad member.
        if (IsSquadControlled)
            return;

        HandleState();
        UpdateRotation();
    }

    #endregion

    #region Squad Membership

    public void SetSquadMember(SquadMemberController member)
    {
        SquadMember = member;

        attackTarget = null;
        unitControlState = UnitControlState.AIControlled;
        SetUnitState(UnitState.Idle);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    public void ClearSquadMember(SquadMemberController member)
    {
        if (SquadMember != member)
            return;

        SquadMember = null;

        attackTarget = null;
        unitControlState = UnitControlState.AIControlled;
        SetUnitState(UnitState.Idle);
    }

    #endregion

    #region State Handling

    protected override void HandleState()
    {
        if (IsSquadControlled)
            return;

        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        if (stance == SquadStance.NoAttack && attackTarget != null)
        {
            ClearAttackAndStop();
            return;
        }

        if (unitControlState == UnitControlState.Locked)
            return;

        if (unitControlState == UnitControlState.PlayerControlled)
        {
            HandlePlayerControlled();
            return;
        }

        HandleAIControlled();
    }

    void HandlePlayerControlled()
    {
        switch (state)
        {
            case UnitState.Moving:
                HandlePlayerMoving();
                break;

            case UnitState.Attacking:
                HandlePlayerAttacking();
                break;

            case UnitState.AttackMoving:
                HandlePlayerAttackMoving();
                break;
        }
    }

    void HandlePlayerMoving()
    {
        if (agent.pathPending)
            return;

        if (agent.remainingDistance > agent.stoppingDistance)
            return;

        if (attackTarget != null)
        {
            SetUnitState(UnitState.Attacking);
            return;
        }

        SetUnitState(UnitState.Idle);
        unitControlState = UnitControlState.AIControlled;
    }

    void HandlePlayerAttacking()
    {
        if (!HasValidAttackTarget())
        {
            ClearAttackTarget();
            SetUnitState(UnitState.Idle);
            unitControlState = UnitControlState.AIControlled;
            return;
        }

        ExecuteAttack();
    }

    void HandlePlayerAttackMoving()
    {
        if (attackTarget == null)
        {
            EntityController target = FindTarget();

            if (target != null)
            {
                attackTarget = target;
                SetUnitState(UnitState.Attacking);
                unitControlState = UnitControlState.AIControlled;
                return;
            }
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            SetUnitState(UnitState.Idle);
            unitControlState = UnitControlState.AIControlled;
        }
    }

    void HandleAIControlled()
    {
        switch (state)
        {
            case UnitState.Idle:
                HandleAIIdle();
                break;

            case UnitState.Moving:
                HandleAIMoving();
                break;

            case UnitState.Attacking:
                HandleAIAttacking();
                break;

            case UnitState.Returning:
                HandleAIReturning();
                break;
        }
    }

    void HandleAIIdle()
    {
        EntityController target = TickScanForTarget();

        if (target == null)
            return;

        attackTarget = target;
        agent.stoppingDistance = stats.attackRange;
        agent.SetDestination(attackTarget.transform.position);
        SetUnitState(UnitState.Moving);
    }

    void HandleAIMoving()
    {
        if (!HasValidAttackTarget())
        {
            ClearAttackTarget();
            SetUnitState(UnitState.Idle);
            return;
        }

        if (stance == SquadStance.Defensive && IsTooFarFromHome())
        {
            ClearAttackTarget();
            SetUnitState(UnitState.Returning);
            agent.SetDestination(homePosition);
            return;
        }

        agent.SetDestination(attackTarget.transform.position);

        if (IsInAttackRange())
            SetUnitState(UnitState.Attacking);
    }

    void HandleAIAttacking()
    {
        if (!HasValidAttackTarget())
        {
            attackTarget = FindTarget();

            if (attackTarget == null)
            {
                if (prevState == UnitState.AttackMoving)
                    OrderAttackMove(homePosition);
                else
                    SetUnitState(UnitState.Idle);
            }

            return;
        }

        if (!IsInAttackRange())
        {
            if (stance == SquadStance.StandGround)
            {
                ClearAttackTarget();
                SetUnitState(UnitState.Idle);
                return;
            }

            SetUnitState(UnitState.Moving);
            return;
        }

        ExecuteAttack();
    }

    void HandleAIReturning()
    {
        if (IsNearHome())
        {
            SetUnitState(UnitState.Idle);
            return;
        }

        if (stance != SquadStance.Aggressive)
            return;

        EntityController target = FindTarget();

        if (target == null)
            return;

        attackTarget = target;
        SetUnitState(UnitState.Moving);
        agent.SetDestination(attackTarget.transform.position);
    }

    #endregion

    #region Combat

    void ExecuteAttack()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();

        if (attackTimer <= 0f)
            TriggerAttack();
    }

    EntityController TickScanForTarget()
    {
        scanTimer -= Time.deltaTime;

        if (scanTimer > 0f)
            return null;

        scanTimer = scanInterval;
        return FindTarget();
    }

    EntityController FindTarget()
    {
        if (stance == SquadStance.NoAttack)
            return null;

        float scanRange = stats.lineOfSight;

        if (stance == SquadStance.StandGround)
            scanRange = stats.attackRange + 1f;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            scanRange,
            scanBuffer);

        EntityController nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            if (!scanBuffer[i].TryGetComponent(out EntityController entity))
                continue;

            if (entity == this)
                continue;

            if (entity.Stats == null)
                continue;

            if (!entity.Stats.IsEnemy(stats))
                continue;

            float dist = Calc.SqrDistance(transform.position, entity.transform.position);

            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = entity;
            }
        }

        return nearest;
    }

    bool HasValidAttackTarget()
    {
        return attackTarget != null &&
               attackTarget.Stats != null &&
               attackTarget.Stats.IsAlive;
    }

    bool IsInAttackRange()
    {
        return HasValidAttackTarget() &&
               Calc.WithinRange(
                   attackTarget.transform.position,
                   transform.position,
                   stats.attackRange);
    }

    #endregion

    #region Distance Checks

    bool IsTooFarFromHome()
    {
        return !Calc.WithinRange(
            transform.position,
            homePosition,
            stats.defensiveChaseRange);
    }

    bool IsNearHome(float range = 1f)
    {
        return Calc.WithinRange(transform.position, homePosition, range);
    }

    #endregion

    #region Orders

    public override void OrderAttack(EntityController target)
    {
        if (IsSquadControlled)
            return;

        if (target == null)
            return;

        attackTarget = target;
        unitControlState = UnitControlState.PlayerControlled;
        SetUnitState(UnitState.Moving);

        agent.stoppingDistance = stats.attackRange;
        agent.SetDestination(target.transform.position);

        homePosition = transform.position;
    }

    public override void OrderMove(Vector3 destination)
    {
        if (IsSquadControlled)
            return;

        ClearAttackTarget();

        unitControlState = UnitControlState.PlayerControlled;
        SetUnitState(UnitState.Moving);

        homePosition = destination;

        agent.stoppingDistance = 0.1f;
        agent.SetDestination(destination);
    }

    public override void OrderStop()
    {
        if (IsSquadControlled)
            return;

        ClearAttackAndStop();
    }

    public void OrderSetStance(SquadStance newStance)
    {
        if (IsSquadControlled)
            return;

        stance = newStance;
        homePosition = transform.position;
    }

    public void OrderAttackMove(Vector3 destination)
    {
        if (IsSquadControlled)
            return;

        OrderMove(destination);

        unitControlState = UnitControlState.PlayerControlled;
        SetUnitState(UnitState.AttackMoving);
    }

    void ClearAttackAndStop()
    {
        ClearAttackTarget();

        unitControlState = UnitControlState.AIControlled;
        SetUnitState(UnitState.Idle);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    void ClearAttackTarget()
    {
        attackTarget = null;
    }

    #endregion
}

