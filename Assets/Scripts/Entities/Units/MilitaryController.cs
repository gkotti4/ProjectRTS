// SESSION: Squad Control Refactor

using UnityEngine;

[RequireComponent(typeof(SquadMemberController))]
public class MilitaryController : UnitController
{
    #region Fields

    [Header("Legacy Standalone Military AI")]
    [SerializeField] private CombatStance stance = CombatStance.Aggressive;
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

        if (stance == CombatStance.NoAttack && attackTarget != null)
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

        if (stance == CombatStance.Defensive && IsTooFarFromHome())
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
            if (stance == CombatStance.StandGround)
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

        if (stance != CombatStance.Aggressive)
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
        if (stance == CombatStance.NoAttack)
            return null;

        float scanRange = stats.lineOfSight;

        if (stance == CombatStance.StandGround)
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

    public void OrderSetStance(CombatStance newStance)
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





// using TMPro.SpriteAssetUtilities;
// using UnityEngine;
//
// public enum ControlState { PlayerControlled, AIControlled, Locked } // do we want Locked as state or bool
//
// public class MilitaryController : UnitController
// {
//     #region Fields
//     
//     // Stances
//     protected UnitStance stance = UnitStance.Aggressive;
//
//     // Scanning
//     private float scanInterval = 0.5f;
//     private float scanTimer = 0f;
//     private Collider[] scanBuffer = new Collider[20];
//     
//     // Control Group FORMATIONS (needed for anchored moving as formation)
//     public Vector3 formationSlot; // current world slot position
//     public int offsetIndex = -1; // index into control group offsets
//     public Vector3 lastSlotPos; // last slot position we set destination to
//     
//     #endregion
//
//     #region Unity Lifecycle Methods
//     protected override void Update()
//     {
//         HandleState();
//         UpdateRotation();
//         
//     }
//     #endregion
//     
//     #region FormationMode
//     /// Returns true if this unit is in a control group with formation mode active
//     bool InFormationMode()
//     {
//         if (controlGroup < 0) return false;
//         ControlGroup cg = SelectionManager.Instance.GetControlGroup(controlGroup);
//         return cg != null && cg.formationMode;
//     }
//     
//     
//     #endregion
//     
//     
//     #region State Handlers
//     protected override void HandleState()
//     {
//         if (attackTimer > 0f) attackTimer -= Time.deltaTime;
//         
//         // Formation mode — anchor drives movement, unit only handles combat near slot
//         if (InFormationMode())
//         {
//             HandleFormationMode();
//
//             return;
//         }
//         
//         // Individual mode — full state machine
//         if (stance == UnitStance.NoAttack && attackTarget != null) // check
//         {
//             attackTarget = null;
//             controlState = ControlState.AIControlled;
//             SetUnitState(UnitState.Idle);
//             agent.ResetPath();
//             return;
//         }
//
//         // Control State
//         if (controlState == ControlState.Locked) return;
//
//         if (controlState == ControlState.PlayerControlled)
//         {
//             HandlePlayerControlled();
//             return;
//         }
//
//         HandleAIControlled();
//         
//     }
//
//     void HandleFormationMode()
//     {
//         ControlGroup cg = SelectionManager.Instance.GetControlGroup(controlGroup);
//         if (cg == null) return;
//             
//         UnitStance activeStance = SelectionManager.Instance
//             .GetControlGroup(controlGroup).formationStance;
//             
//         if (activeStance == UnitStance.NoAttack)
//         {
//             attackTarget = null;
//             return;
//         }
//             
//         // Scan for targets
//         if (attackTarget == null)
//         {
//             scanTimer -= Time.deltaTime;
//             if (scanTimer <= 0f)
//             {
//                 scanTimer = scanInterval;
//                 attackTarget = FindTarget();
//             }
//         }
//             
//         // No target - return to slot
//         if (attackTarget == null || !attackTarget.Stats.IsAlive)
//         {
//             attackTarget = null;
//             if (formationSlot != Vector3.zero &&
//                 Calc.OutOfRange(transform.position, formationSlot, 1f))
//                 agent.SetDestination(formationSlot);
//             return;
//         }
//             
//         // Has target
//         if (IsInAttackRange())
//         {
//             agent.ResetPath();
//             if (attackTimer <= 0f) TriggerAttack();
//         }
//         else
//         {
//
//             bool tooFarFromSlot = Calc.OutOfRange(transform.position, formationSlot,
//                 SelectionManager.Instance.GetControlGroup(controlGroup).formationChaseRange);
//
//             if (activeStance == UnitStance.StandGround || 
//                 activeStance == UnitStance.NoAttack ||
//                 (activeStance == UnitStance.Defensive && tooFarFromSlot))
//             {
//                 attackTarget = null;
//                 if (formationSlot != Vector3.zero)
//                     agent.SetDestination(formationSlot);
//                 return;
//             }
//
//             // Aggressive or Defensive within range — chase
//             agent.SetDestination(attackTarget.transform.position);
//         }
//     }
//
//     void HandlePlayerControlled()
//     {
//         switch (state)
//         {
//             case UnitState.Moving:
//                 if (agent.pathPending) return;
//                 if (agent.remainingDistance <= agent.stoppingDistance)
//                 {
//                     // Order complete — hand back to AI
//                     if (attackTarget != null)
//                         SetUnitState(UnitState.Attacking);
//                     else
//                     {
//                         SetUnitState(UnitState.Idle);
//                         controlState = ControlState.AIControlled;
//                     }
//                 }
//
//                 break;
//
//             case UnitState.Attacking:
//                 if (attackTarget == null || !attackTarget.Stats.IsAlive)
//                 {
//                     attackTarget = null;
//                     SetUnitState(UnitState.Idle);
//                     controlState = ControlState.AIControlled;
//                     return;
//                 }
//
//                 ExecuteAttack();
//                 break;
//             
//             case UnitState.AttackMoving:
//                 // Scan for enemies while moving
//                 if (attackTarget == null)
//                 {
//                     EntityController target = FindTarget();
//                     if (target != null)
//                     {
//                         attackTarget = target;
//                         SetUnitState(UnitState.Attacking);
//                         controlState = ControlState.AIControlled;
//                         return;
//                     }
//                 }
//                 
//                 // Arrived at destination
//                 if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
//                 {
//                     SetUnitState(UnitState.Idle);
//                     controlState = ControlState.AIControlled;
//                 }
//                 break;
//         }
//     }
//
//     void HandleAIControlled()
//     {
//         switch (state)
//         {
//             case UnitState.Idle:
//                 // Scan for enemies
//                 scanTimer -= Time.deltaTime;
//                 if (scanTimer <= 0f)
//                 {
//                     scanTimer = scanInterval;
//                     EntityController target = FindTarget();
//                     if (target != null)
//                     {
//                         attackTarget = target;
//                         SetUnitState(UnitState.Moving);
//                         agent.stoppingDistance = stats.attackRange;
//                         agent.SetDestination(attackTarget.transform.position);
//                     }
//                 }
//
//                 break;
//
//             case UnitState.Moving:
//                 if (attackTarget == null || !attackTarget.Stats.IsAlive)
//                 {
//                     attackTarget = null;
//                     SetUnitState(UnitState.Idle);
//                     return;
//                 }
//                 
//                 // Defensive - stop chasing if too far from home - seems to be working fine
//                 if (stance == UnitStance.Defensive && IsTooFarFromHome())
//                 {
//                     attackTarget = null;
//                     SetUnitState(UnitState.Returning);
//                     agent.SetDestination(homePosition);
//                     return;
//                 }
//
//                 agent.SetDestination(attackTarget.transform.position);
//                 if (IsInAttackRange())
//                     SetUnitState(UnitState.Attacking);
//                 break;
//
//             case UnitState.Attacking:
//                 if (attackTarget == null || !attackTarget.Stats.IsAlive)
//                 {
//                     attackTarget = FindTarget(); // Target Switching - immediately look for next target
//                     if (attackTarget == null)
//                     {
//                         if (prevState == UnitState.AttackMoving)
//                             OrderAttackMove(homePosition);
//                         else
//                             SetUnitState(UnitState.Idle);
//                     }
//                     return;
//                 }
//
//                 if (!IsInAttackRange())
//                 {
//                     if (stance == UnitStance.StandGround)
//                     {
//                         attackTarget = null;
//                         SetUnitState(UnitState.Idle);
//                         return;
//                     }
//
//                     SetUnitState(UnitState.Moving);
//                     return;
//                 }
//
//                 ExecuteAttack();
//                 break;
//
//             case UnitState.Returning:
//                 if (IsNearHome())
//                 {
//                     SetUnitState(UnitState.Idle);
//                     return;
//                 }
//
//                 // Aggressive can break return to attack
//                 if (stance == UnitStance.Aggressive)
//                 {
//                     EntityController target = FindTarget();
//                     if (target != null)
//                     {
//                         attackTarget = target;
//                         SetUnitState(UnitState.Moving);
//                         agent.SetDestination(attackTarget.transform.position);
//                     }
//                 }
//
//                 break;
//         }
//
//     }
//     #endregion
//
//     #region Combat
//     void ExecuteAttack()
//     {
//         agent.ResetPath();
//         if (attackTimer <= 0f)
//             TriggerAttack();
//     }
//     #endregion
//     
//     #region Targetting
//     EntityController FindTarget()
//     {
//         if (stance == UnitStance.NoAttack) return null;
//     
//         float scanRange = Stats.lineOfSight;
//     
//         // StandGround only targets enemies already in attack range
//         if (stance == UnitStance.StandGround)
//             scanRange = Stats.attackRange + 1f;
//     
//         int count = Physics.OverlapSphereNonAlloc(transform.position, scanRange, scanBuffer);
//     
//         EntityController nearest = null;
//         float nearestDist = float.MaxValue;
//     
//         for (int i = 0; i < count; i++)
//         {
//             if (!scanBuffer[i].TryGetComponent(out EntityController entity)) continue;
//             if (!entity.Stats.IsEnemy(Stats)) continue;
//     
//             float dist = (entity.transform.position - transform.position).sqrMagnitude;
//             if (dist < nearestDist)
//             {
//                 nearestDist = dist;
//                 nearest = entity;
//             }
//         }
//         return nearest;
//     }
//     #endregion
//     
//     
//     #region Distance Checks
//     bool IsInAttackRange()
//     {
//         if (attackTarget && Calc.WithinRange(attackTarget.transform.position, transform.position, Stats.attackRange))
//             return true;
//         return false;
//     }
//     
//     bool IsTooFarFromHome()
//     {
//         return !Calc.WithinRange(transform.position, homePosition, Stats.defensiveChaseRange);
//     }
//     
//     bool IsNearHome(float range=1f)
//     {
//         return Calc.WithinRange(transform.position, homePosition, range);
//     }
//     
//     // bool IsNearHome()
//     // {
//     //     bool hasFormationSlot = controlGroup >= 0 && formationSlot != Vector3.zero;
//     //     Vector3 home = hasFormationSlot ? formationSlot : homePosition;
//     //     return Calc.WithinRange(transform.position, home, 2f);
//     // }
//     //
//     // bool IsTooFarFromHome()
//     // {
//     //     bool hasFormationSlot = controlGroup >= 0 && formationSlot != Vector3.zero;
//     //     Vector3 home = hasFormationSlot ? formationSlot : homePosition;
//     //     return !Calc.WithinRange(transform.position, home, Stats.defensiveChaseRange);
//     // }
//     #endregion
//     
//     
//     #region Player Orders
//     public override void OrderAttack(EntityController target)
//     {
//         if (target == null) return;
//         attackTarget = target;
//         controlState = ControlState.PlayerControlled;
//         SetUnitState(UnitState.Moving);
//         agent.stoppingDistance = stats.attackRange;
//         agent.SetDestination(target.transform.position);
//         homePosition = transform.position;
//     }
//     
//     // public override void OrderAttack(EntityController target)
//     // {
//     //     if (target == null) return;
//     //     attackTarget = target;
//     //     controlState = ControlState.PlayerControlled;
//     //     SetUnitState(UnitState.Moving);
//     //     agent.stoppingDistance = stats.attackRange;
//     //     agent.SetDestination(target.transform.position);
//     //
//     //     // Only update homePosition if not in a control group
//     //     if (controlGroup < 0)
//     //         homePosition = transform.position;
//     // }
//
//     public override void OrderMove(Vector3 destination)
//     {
//         attackTarget = null;
//         controlState = ControlState.PlayerControlled;
//         SetUnitState(UnitState.Moving);
//         homePosition = destination;
//         agent.stoppingDistance = 0.1f;
//         agent.SetDestination(destination);
//     }
//     
//     // public override void OrderMove(Vector3 destination)
//     // {
//     //     attackTarget = null;
//     //     controlState = ControlState.PlayerControlled;
//     //     SetUnitState(UnitState.Moving);
//     //
//     //     // Only update homePosition if not in a control group
//     //     // Control group anchor manages home position for grouped units
//     //     if (controlGroup < 0)
//     //         homePosition = destination;
//     //
//     //     agent.stoppingDistance = 0.1f;
//     //     agent.SetDestination(destination);
//     // }
//
//     public override void OrderStop()
//     {
//         attackTarget = null;
//         controlState = ControlState.AIControlled;
//         SetUnitState(UnitState.Idle);
//         agent.ResetPath();
//     }
//
//     public void OrderSetStance(UnitStance newStance)
//     {
//         stance = newStance;
//         homePosition = transform.position; // eh
//     }
//
//     public void OrderAttackMove(Vector3 destination)
//     {
//         OrderMove(destination);
//         controlState = ControlState.PlayerControlled;
//         SetUnitState(UnitState.AttackMoving);
//     }
//     #endregion
//     
//     
//     
// }


// using UnityEngine;
//
// // SESSION: Squad Control
// [RequireComponent(typeof(SquadMemberController))]
//
// public class MilitaryController : UnitController
// {
//     // SESSION: Squad Control
//     public SquadMemberController SquadMember { get; private set; }
//     public bool IsSquadControlled => SquadMember != null && SquadMember.IsInSquad;
//
//
//     protected UnitStance stance = UnitStance.Aggressive;
//
//     // Scanning
//     private float scanInterval = 0.5f;
//     private float scanTimer = 0f;
//     private Collider[] scanBuffer = new Collider[20];
//     
//     // Formation
//     public Vector3 formationSlot;
//     public int offsetIndex = -1;
//     public Vector3 lastSlotPos;
//
//     
//     
//     #region ISelectable // SESSION: Squad Control
//     public override bool IsDragSelectable => !IsSquadControlled;
//     
//     public override void OnSelect()
//     {
//         if (IsSquadControlled)
//         {
//             if (selectionDecal) selectionDecal.enabled = true;
//             if (healthBar) healthBar.OnSelected();
//         }
//         else
//             base.OnSelect(); 
//     }
//     
//     public override void OnDeselect()
//     {
//         if (IsSquadControlled)
//         {
//             if (selectionDecal) selectionDecal.enabled = false;
//             if(healthBar) healthBar.OnDeselected();
//         }
//         else
//             base.OnDeselect();
//     }
//     #endregion
//     
//     #region Unity Lifecycle
//     protected override void Update()
//     {
//         if (IsSquadControlled) return;
//         
//         HandleState();
//         UpdateRotation();
//     }
//     #endregion
//
//     
//     
//     #region Squad
//     
//     // SESSION: Squad Control
//     public void SetSquadMember(SquadMemberController member)
//     {
//         SquadMember = member;
//
//         attackTarget = null;
//         UnitControlState = UnitControlState.AIControlled;
//         SetUnitState(UnitState.Idle);
//
//         if (agent != null && agent.enabled)
//             agent.ResetPath();
//     }
//
//     public void ClearSquadMember(SquadMemberController member)
//     {
//         if (SquadMember != member)
//             return;
//
//         SquadMember = null;
//
//         attackTarget = null;
//         UnitControlState = UnitControlState.AIControlled;
//         SetUnitState(UnitState.Idle);
//     }
//     
//     #endregion
//     
//     
//     // SESSION: Squad Control
//     // #region Formation Mode
//     // /// Returns true if this unit is in a control group with formation mode active
//     // bool InFormationMode()
//     // {
//     //     if (controlGroup < 0) return false;
//     //     ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(controlGroup);
//     //     return cg != null && cg.formationMode;
//     // }
//     //
//     // void HandleFormationMode()
//     // {
//     //     ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(controlGroup);
//     //     if (cg == null) return;
//     //
//     //     UnitStance activeStance = cg.formationStance;
//     //     if (activeStance == UnitStance.NoAttack) { attackTarget = null; return; }
//     //
//     //     if (UnitControlState == UnitControlState.Locked) return;
//     //
//     //     // Scan for targets — only when AI controlled
//     //     if (UnitControlState == UnitControlState.AIControlled)
//     //     {
//     //         if (!attackTarget) // WAS: == null
//     //         {
//     //             scanTimer -= Time.deltaTime;
//     //             if (scanTimer <= 0f)
//     //             {
//     //                 scanTimer = scanInterval;
//     //                 attackTarget = FindTarget();
//     //             }
//     //         }
//     //     }
//     //
//     //     // No target — return to slot if AI controlled
//     //     if (attackTarget == null || !attackTarget.Stats.IsAlive)
//     //     {
//     //         attackTarget = null;
//     //         if (UnitControlState == UnitControlState.AIControlled &&
//     //             formationSlot != Vector3.zero &&
//     //             Calc.OutOfRange(transform.position, formationSlot, 1f))
//     //             agent.SetDestination(formationSlot);
//     //         return;
//     //     }
//     //
//     //     // Has target — attack logic runs regardless of control state
//     //     if (IsInAttackRange())
//     //     {
//     //         agent.ResetPath();
//     //         if (attackTimer <= 0f) TriggerAttack();
//     //     }
//     //     else
//     //     {
//     //         bool tooFarFromSlot = Calc.OutOfRange(
//     //             transform.position, formationSlot, cg.formationChaseRange);
//     //
//     //         if (activeStance == UnitStance.StandGround ||
//     //             (activeStance == UnitStance.Defensive || tooFarFromSlot)) // && tooFarFromSlot)) 6/5
//     //         {
//     //             attackTarget = null;
//     //             if (formationSlot != Vector3.zero)
//     //                 agent.SetDestination(formationSlot);
//     //             return;
//     //         }
//     //
//     //         agent.SetDestination(attackTarget.transform.position);
//     //     }
//     // }
//     // #endregion
//
//     #region State Handlers
//     protected override void HandleState()
//     {
//         if (attackTimer > 0f) attackTimer -= Time.deltaTime;
//
//         // SESSION: Squad Control
//         // Formation mode — anchor drives movement, unit only handles combat near slot
//         // if (InFormationMode())
//         // {
//         //     HandleFormationMode();
//         //     return;
//         // }
//
//         // Individual mode — full state machine
//         if (stance == UnitStance.NoAttack && attackTarget != null)
//         {
//             attackTarget = null;
//             UnitControlState = UnitControlState.AIControlled;
//             SetUnitState(UnitState.Idle);
//             agent.ResetPath();
//             return;
//         }
//
//         if (UnitControlState == UnitControlState.Locked) return;
//         if (UnitControlState == UnitControlState.PlayerControlled) { HandlePlayerControlled(); return; }
//         HandleAIControlled();
//     }
//
//     void HandlePlayerControlled()
//     {
//         switch (state)
//         {
//             case UnitState.Moving:
//                 if (agent.pathPending) return;
//                 if (agent.remainingDistance <= agent.stoppingDistance)
//                 {
//                     if (attackTarget != null)
//                         SetUnitState(UnitState.Attacking);
//                     else
//                     {
//                         SetUnitState(UnitState.Idle);
//                         UnitControlState = UnitControlState.AIControlled;
//                     }
//                 }
//                 break;
//
//             case UnitState.Attacking:
//                 if (attackTarget == null || !attackTarget.Stats.IsAlive)
//                 {
//                     attackTarget = null;
//                     SetUnitState(UnitState.Idle);
//                     UnitControlState = UnitControlState.AIControlled;
//                     return;
//                 }
//                 ExecuteAttack();
//                 break;
//
//             case UnitState.AttackMoving:
//                 if (attackTarget == null)
//                 {
//                     EntityController target = FindTarget();
//                     if (target != null)
//                     {
//                         attackTarget = target;
//                         SetUnitState(UnitState.Attacking);
//                         UnitControlState = UnitControlState.AIControlled;
//                         return;
//                     }
//                 }
//                 if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
//                 {
//                     SetUnitState(UnitState.Idle);
//                     UnitControlState = UnitControlState.AIControlled;
//                 }
//                 break;
//         }
//     }
//
//     void HandleAIControlled()
//     {
//         switch (state)
//         {
//             case UnitState.Idle:
//                 scanTimer -= Time.deltaTime;
//                 if (scanTimer <= 0f)
//                 {
//                     scanTimer = scanInterval;
//                     EntityController target = FindTarget();
//                     if (target != null)
//                     {
//                         attackTarget = target;
//                         SetUnitState(UnitState.Moving);
//                         agent.stoppingDistance = stats.attackRange;
//                         agent.SetDestination(attackTarget.transform.position);
//                     }
//                 }
//                 break;
//
//             case UnitState.Moving:
//                 if (attackTarget == null || !attackTarget.Stats.IsAlive)
//                 {
//                     attackTarget = null;
//                     SetUnitState(UnitState.Idle);
//                     return;
//                 }
//                 if (stance == UnitStance.Defensive && IsTooFarFromHome())
//                 {
//                     attackTarget = null;
//                     SetUnitState(UnitState.Returning);
//                     agent.SetDestination(homePosition);
//                     return;
//                 }
//                 agent.SetDestination(attackTarget.transform.position);
//                 if (IsInAttackRange())
//                     SetUnitState(UnitState.Attacking);
//                 break;
//
//             case UnitState.Attacking:
//                 if (attackTarget == null || !attackTarget.Stats.IsAlive)
//                 {
//                     attackTarget = FindTarget();
//                     if (attackTarget == null)
//                     {
//                         if (prevState == UnitState.AttackMoving)
//                             OrderAttackMove(homePosition);
//                         else
//                             SetUnitState(UnitState.Idle);
//                     }
//                     return;
//                 }
//                 if (!IsInAttackRange())
//                 {
//                     if (stance == UnitStance.StandGround)
//                     {
//                         attackTarget = null;
//                         SetUnitState(UnitState.Idle);
//                         return;
//                     }
//                     SetUnitState(UnitState.Moving);
//                     return;
//                 }
//                 ExecuteAttack();
//                 break;
//
//             case UnitState.Returning:
//                 if (IsNearHome())
//                 {
//                     SetUnitState(UnitState.Idle);
//                     return;
//                 }
//                 if (stance == UnitStance.Aggressive)
//                 {
//                     EntityController target = FindTarget();
//                     if (target != null)
//                     {
//                         attackTarget = target;
//                         SetUnitState(UnitState.Moving);
//                         agent.SetDestination(attackTarget.transform.position);
//                     }
//                 }
//                 break;
//         }
//     }
//     #endregion
//
//     #region Combat
//     void ExecuteAttack()
//     {
//         agent.ResetPath();
//         if (attackTimer <= 0f)
//             TriggerAttack();
//     }
//     #endregion
//
//     #region Targeting
//     EntityController FindTarget()
//     {
//         if (stance == UnitStance.NoAttack) return null;
//
//         float scanRange = Stats.lineOfSight;
//         if (stance == UnitStance.StandGround)
//             scanRange = Stats.attackRange + 1f;
//
//         int count = Physics.OverlapSphereNonAlloc(transform.position, scanRange, scanBuffer);
//
//         EntityController nearest = null;
//         float nearestDist = float.MaxValue;
//
//         for (int i = 0; i < count; i++)
//         {
//             if (!scanBuffer[i].TryGetComponent(out EntityController entity)) continue;
//             if (!entity.Stats.IsEnemy(Stats)) continue;
//
//             float dist = (entity.transform.position - transform.position).sqrMagnitude;
//             if (dist < nearestDist) { nearestDist = dist; nearest = entity; }
//         }
//         return nearest;
//     }
//     #endregion
//
//     #region Distance Checks
//     bool IsInAttackRange()
//     {
//         return attackTarget != null && 
//                Calc.WithinRange(attackTarget.transform.position, transform.position, Stats.attackRange);
//     }
//
//     bool IsTooFarFromHome()
//     {
//         return !Calc.WithinRange(transform.position, homePosition, Stats.defensiveChaseRange);
//     }
//
//     bool IsNearHome(float range = 1f)
//     {
//         return Calc.WithinRange(transform.position, homePosition, range);
//     }
//     #endregion
//
//     #region Player Orders
//     public override void OrderAttack(EntityController target)
//     {
//         // SESSION: Squad Control
//         if (IsSquadControlled) return;
//         
//         if (target == null) return;
//         attackTarget = target;
//         UnitControlState = UnitControlState.PlayerControlled;
//         SetUnitState(UnitState.Moving);
//         agent.stoppingDistance = stats.attackRange;
//         agent.SetDestination(target.transform.position);
//         homePosition = transform.position;
//     }
//
//     public override void OrderMove(Vector3 destination)
//     {
//         // SESSION: Squad Control
//         if (IsSquadControlled) return;
//         
//         attackTarget = null;
//         UnitControlState = UnitControlState.PlayerControlled;
//         SetUnitState(UnitState.Moving);
//         homePosition = destination;
//         agent.stoppingDistance = 0.1f;
//         agent.SetDestination(destination);
//     }
//
//     public override void OrderStop()
//     {
//         // SESSION: Squad Control
//         if (IsSquadControlled) return;
//         
//         attackTarget = null;
//         UnitControlState = UnitControlState.AIControlled;
//         SetUnitState(UnitState.Idle);
//         agent.ResetPath();
//     }
//
//     public void OrderSetStance(UnitStance newStance)
//     {
//         // SESSION: Squad Control
//         if (IsSquadControlled) return;
//         
//         stance = newStance;
//         homePosition = transform.position;
//     }
//
//     public void OrderAttackMove(Vector3 destination) // REFACTOR: still not complete
//     {
//         // SESSION: Squad Control
//         if (IsSquadControlled) return;
//         
//         OrderMove(destination);
//         UnitControlState = UnitControlState.PlayerControlled;
//         SetUnitState(UnitState.AttackMoving);
//     }
//     #endregion
//     
//     
//     
//     
//     protected override void UpdateRotation()
//     {
//         // SESSION: Squad Control
//         // if (InFormationMode())
//         // {
//         //     // Formation mode — rotate by velocity or toward target
//         //     if (agent.velocity.sqrMagnitude > 0.1f)
//         //         RotateTowardVelocity();
//         //     else if (attackTarget != null)
//         //         RotateTowardIfNeeded(attackTarget.transform.position);
//         //     return;
//         // }
//         base.UpdateRotation();
//     }
//     
//     
//     
// }