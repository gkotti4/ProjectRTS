using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// -----------------------------------------------------------------------------
/// SquadCombat
/// -----------------------------------------------------------------------------
///
/// Squad-level combat coordinator for the new FormationCombat base.
/// Owns squad target selection, approach, engagement start/end, simple formation
/// target assignment, and attack resolution hooks.
///
/// Removed on purpose:
/// - old formation-combat combat homes
/// - old loose-combat anchors
/// - pressure goals
/// - old row-scoring/support budgets
/// - old SoldierCombat rhythm/cohesion routing
///
/// Design role:
/// The squad decides which enemy squad is being fought and provides shared combat
/// context. Soldiers still execute through SoldierController/SoldierMotor and use
/// SoldierContactSensor for local body-space checks.
///
[DisallowMultipleComponent]
public class SquadCombat : MonoBehaviour
{
    #region Fields

    // -----------------------------------------------------------------------------
    // Component References
    // -----------------------------------------------------------------------------
    private SquadController squad;
    private SquadRoster roster;
    private SquadFormationController formation;
    private SquadMovement movement;
    private SquadData data;
    private SquadCombatProfile squadCombatProfile;

    // -----------------------------------------------------------------------------
    // Runtime Combat State
    // -----------------------------------------------------------------------------
    private SquadController targetSquad;
    private Vector3 combatContactDirection = Vector3.forward;
    private SquadCombatStyle currentCombatStyle = SquadCombatStyle.FormationCombat;
    private SquadEngagementReason currentEngagementType = SquadEngagementReason.None;

    // -----------------------------------------------------------------------------
    // Runtime Timers
    // -----------------------------------------------------------------------------
    private float scanTimer = 0f;
    private float approachRefreshTimer = 0f;
    private float approachEngagementSettleTimer = 0f;
    
    // -----------------------------------------------------------------------------
    // Formation Combat Runtime State
    // -----------------------------------------------------------------------------
    private float formationChargeTimer = 0f;

    private readonly HashSet<SoldierController> formationChargeImpactedTargets =
        new HashSet<SoldierController>();

    private readonly HashSet<SoldierController> formationChargeLeadSoldiers =
        new HashSet<SoldierController>();

    private readonly List<SoldierController> formationChargeLeadCandidates =
        new List<SoldierController>();

    // -----------------------------------------------------------------------------
    // Formation Runtime State
    // -----------------------------------------------------------------------------
    private readonly Dictionary<SoldierController, SoldierController> formationTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, float> formationTargetRefreshTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> formationAttackTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> formationReserveSideStepTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> formationReserveBlockedSitTimers =
        new Dictionary<SoldierController, float>();

    private readonly HashSet<SoldierController> formationReserveBlockedSoldiers =
        new HashSet<SoldierController>();

    private readonly Dictionary<SoldierController, Vector3> formationReserveSideStepDestinations =
        new Dictionary<SoldierController, Vector3>();

    private readonly Dictionary<SoldierController, float> formationReserveBehindFriendlySearchTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, Vector3> formationReserveBehindFriendlyDestinations =
        new Dictionary<SoldierController, Vector3>();

    private readonly Dictionary<SoldierController, SoldierController> formationActiveAttackerCombatLockTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, float> formationAttackerCombatLockTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, SoldierController> formationAttackerCombatLockTargets =
        new Dictionary<SoldierController, SoldierController>();

    private NavMeshPath formationReserveBehindFriendlyPath; // Must be initialized inside of Awake/Start, cannot be initialized in Constructor

    private readonly Dictionary<SoldierController, SoldierController> formationPendingProjectileTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, WeaponProfile> formationPendingProjectileWeapons =
        new Dictionary<SoldierController, WeaponProfile>();
    
    // Target committed when a melee attack begins.
    // The AttackImpact animation event consumes this target so target refreshes
    // during the animation cannot redirect the completed swing.
    private readonly Dictionary<SoldierController, SoldierController> formationPendingMeleeTargets =
        new Dictionary<SoldierController, SoldierController>();

    private bool hasLoggedMissingCombatProfile = false;

    // -----------------------------------------------------------------------------
    // Public Read-Only Access
    // -----------------------------------------------------------------------------
    public SquadController TargetSquad => targetSquad;
    public SquadCombatStyle CurrentCombatStyle => currentCombatStyle;
    public SquadEngagementReason CurrentEngagementType => currentEngagementType;

    #endregion

    #region Initialization

    void Awake()
    {
        formationReserveBehindFriendlyPath = new NavMeshPath();
    }

    /// Initializes squad-level combat references.
    public void Initialize(
        SquadController owner,
        SquadRoster squadRoster,
        SquadFormationController squadFormation,
        SquadMovement squadMovement,
        SquadData squadData)
    {
        squad = owner;
        roster = squadRoster;
        formation = squadFormation;
        movement = squadMovement;
        data = squadData;
        squadCombatProfile = data != null ? data.squadCombatProfile : null;
        currentCombatStyle = ResolveCombatStyle();
        currentEngagementType = SquadEngagementReason.None;

        if (!HasCombatProfile())
            enabled = false;
    }

    bool HasCombatProfile()
    {
        if (squadCombatProfile != null)
            return true;

        if (!hasLoggedMissingCombatProfile)
        {
            Debug.LogError(
                $"{name}: SquadCombat requires SquadData.squadCombatProfile. Assign a SquadCombatProfile asset before using squad combat.",
                this);

            hasLoggedMissingCombatProfile = true;
        }

        return false;
    }

    #endregion

    #region Orders / State Ticks

    /// Receives an explicit attack order.
    public void OrderAttack(SquadController target)
    {
        OrderAttack(target, SquadEngagementReason.ExplicitAttack);
    }

    /// Starts an attack-like engagement from either an explicit command or auto-scan.
    void OrderAttack(
        SquadController target,
        SquadEngagementReason engagementType)
    {
        if (!HasCombatProfile())
            return;

        if (!CanAttack(target))
            return;

        targetSquad = target;
        currentCombatStyle = ResolveCombatStyle();
        currentEngagementType = engagementType;
        approachRefreshTimer = 0f;

        ClearFormationRuntimeState(clearAttackTimers: false);

        if (IsCloseEnoughToStartEngagement(targetSquad))
        {
            BeginEngagement(notifyTarget: true);
            return;
        }

        if (ShouldUseFormationCharge() &&
            IsCloseEnoughToStartFormationCharge(targetSquad))
        {
            BeginFormationCharge();
            return;
        }

        BeginApproachingCombat();
    }

    /// Called by the enemy squad when this squad has entered melee/contact range.
    public void ReceiveEngagementRequest(SquadController attacker)
    {
        if (!HasCombatProfile())
            return;

        if (!CanRespondToEngagement(attacker))
            return;

        targetSquad = attacker;
        currentCombatStyle = ResolveCombatStyle();
        currentEngagementType = squad != null && squad.Stance == SquadStance.Hold
            ? SquadEngagementReason.DefensiveHold
            : SquadEngagementReason.PassiveContact;

        ClearFormationRuntimeState(clearAttackTimers: false);

        if (!IsCloseEnoughToStartEngagement(targetSquad))
            return;

        BeginEngagement(notifyTarget: false);
    }

    /// Clears squad-level and soldier-level combat state.
    public void ClearTargets()
    {
        targetSquad = null;
        currentEngagementType = SquadEngagementReason.None;
        approachRefreshTimer = 0f;
        approachEngagementSettleTimer = 0f;
        formationChargeTimer = 0f;
        formationChargeImpactedTargets.Clear();
        formationChargeLeadSoldiers.Clear();
        formationChargeLeadCandidates.Clear();

        ClearFormationRuntimeState(clearAttackTimers: true);
        ClearSoldierCombatStates();
    }

    /// Ticks auto-scan behavior while idle.
    public void TickIdleScan()
    {
        if (!HasCombatProfile())
            return;

        if (!ShouldScan())
            return;

        TickScan();
    }

    /// Ticks auto-scan behavior while attack-moving.
    public void TickAttackMoveScan()
    {
        if (!HasCombatProfile())
            return;

        if (!ShouldScan())
            return;

        TickScan();
    }

    /// Moves toward the current attack target until close enough to begin a melee
    /// charge or enter ranged/direct combat.
    public void TickApproachingCombat()
    {
        if (!HasCombatProfile())
            return;

        if (!CanAttack(targetSquad))
        {
            EndCombatAndReform();
            return;
        }

        movement.TickFormationFollow();

        if (IsCloseEnoughToStartEngagement(targetSquad))
        {
            if (ShouldHoldInitialEngagementForApproachSettle(targetSquad))
                return;

            BeginEngagement(notifyTarget: true);
            return;
        }

        if (ShouldUseFormationCharge() &&
            IsCloseEnoughToStartFormationCharge(targetSquad))
        {
            BeginFormationCharge();
            return;
        }

        approachEngagementSettleTimer = 0f;
        TickCombatApproachRefresh();
    }

    /// Ticks the shared melee charge phase. The full formation surges forward while
    /// individual front soldiers naturally become the first contacts. Charge ends
    /// when a meaningful fraction reaches personal melee range or the safety timer
    /// expires.
    public void TickCharging()
    {
        if (!HasCombatProfile())
            return;

        if (!CanAttack(targetSquad))
        {
            EndCombatAndReform();
            return;
        }

        if (!ShouldUseFormationCharge())
        {
            BeginApproachingCombat();
            return;
        }

        formationChargeTimer -= Time.deltaTime;

        RefreshFormationChargeLeadSoldiers();

        movement.TickFormationFollow(
            squadCombatProfile.formationChargeSpeedMultiplier,
            formationChargeLeadSoldiers,
            squadCombatProfile.formationChargeLeadSpeedMultiplier);

        TickFormationChargeImpulseEmitters();

        if (HasFormationChargeReachedContactRatio(targetSquad) ||
            formationChargeTimer <= 0f)
        {
            BeginEngagement(notifyTarget: true);
            return;
        }

        TickCombatApproachRefresh();
    }

    void TickCombatApproachRefresh()
    {
        approachRefreshTimer -= Time.deltaTime;

        if (approachRefreshTimer > 0f)
            return;

        approachRefreshTimer = Mathf.Max(
            0.01f,
            squadCombatProfile.combatApproachRefreshInterval);

        MoveTowardCombatTarget();
    }

    /// Ticks active squad combat.
    public void TickCombat()
    {
        TickFormationCombat();
    }

    #endregion

    #region Formation Combat

    /// FormationCombat base:
    /// - no combat homes
    /// - no formation combat slots
    /// - no old pressure/old row-scoring/support logic
    /// - simple target crowding
    /// - attack, advance, or wait if a friendly body blocks the forward lane
    void TickFormationCombat()
    {
        if (!HasCombatProfile())
            return;

        if (!CanAttack(targetSquad) || !IsWithinCombatBreakRange(targetSquad))
        {
            if (!TrySwitchPrimaryCombatTarget())
            {
                EndCombatAndReform();
                return;
            }
        }

        if (roster == null ||
            targetSquad == null ||
            targetSquad.Roster == null)
        {
            EndCombatAndReform();
            return;
        }

        combatContactDirection = GetContactDirection();

        foreach (SoldierController soldier in roster.Soldiers)
        {
            TickFormationSoldier(soldier);
        }
    }

    void TickFormationSoldier(SoldierController soldier)
    {
        if (soldier == null || !soldier.IsAlive)
            return;

        // -------------------------------------------------------------------------
        // Shared Soldier Combat Setup
        // -------------------------------------------------------------------------
        EnsureFormationTimers(soldier);
        TickFormationTimers(soldier);

        SoldierController currentTarget =
            RefreshFormationSoldierTargetIfNeeded(soldier);

        if (currentTarget == null)
        {
            soldier.Stop();
            soldier.SetCombatRole(SoldierRole.None);
            soldier.ClearCombatTarget();
            ClearFormationReserveBlockedState(soldier);
            return;
        }

        WeaponProfile weaponProfile = GetWeaponProfile(soldier);
        bool isRangedWeapon = IsRangedWeapon(weaponProfile);

        GetFormationAttackValues(
            soldier,
            weaponProfile,
            isRangedWeapon,
            out MeleeCombatStats meleeStats,
            out RangedCombatStats rangedStats,
            out float attackRange,
            out float attackInterval,
            out float stoppingDistance);

        if (soldier.IsMovementLocked)
        {
            soldier.FaceToward(currentTarget.transform.position);
            return;
        }

        if (!isRangedWeapon &&
            TryFindImmediateFormationContactTarget(
                soldier,
                currentTarget,
                attackRange,
                out SoldierController immediateContactTarget))
        {
            currentTarget = immediateContactTarget;
            formationTargets[soldier] = currentTarget;
            soldier.SetCombatTarget(currentTarget);
            ClearFormationReserveBlockedState(soldier);
        }

        Vector3 toTarget = currentTarget.transform.position - soldier.transform.position;
        toTarget.y = 0f;

        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget <= 0.001f)
        {
            soldier.Stop();
            return;
        }
        
        soldier.FaceToward(currentTarget.transform.position, soldier.Stats != null ? soldier.Stats.movement.turnSpeed : soldier.Data.movement.turnSpeed, false);

        // -------------------------------------------------------------------------
        // Active Soldier Logic
        // -------------------------------------------------------------------------
        // Active means this soldier is currently in its personal 1v1 / attack range
        // and can directly fight its assigned target. Later this can become a real
        // soldier combat state. For now, it is only a clear branch in the tick.
        if (IsFormationActiveSoldier(distanceToTarget, attackRange))
        {
            ClearFormationReserveBlockedState(soldier);

            if (!isRangedWeapon)
                MarkFormationActiveAttackerCombatLockCandidate(soldier, currentTarget);
            else
                ClearFormationActiveAttackerCombatLockCandidate(soldier);

            TickFormationActiveSoldier(
                soldier,
                currentTarget,
                weaponProfile,
                meleeStats,
                rangedStats,
                isRangedWeapon,
                attackInterval);

            return;
        }

        // -------------------------------------------------------------------------
        // Reserve Soldier Logic
        // -------------------------------------------------------------------------
        // Reserve means this soldier has a valid combat target, but is not currently
        // in direct active combat / 1v1 range. For now reserves simply try to move
        // toward a useful combat point, or wait when a friendly body blocks the lane.
        TickFormationReserveSoldier(
            soldier,
            currentTarget,
            isRangedWeapon,
            attackRange,
            stoppingDistance);
    }

    SoldierController RefreshFormationSoldierTargetIfNeeded(SoldierController soldier)
    {
        formationTargets.TryGetValue(
            soldier,
            out SoldierController currentTarget);

        bool shouldRefreshTarget =
            formationTargetRefreshTimers[soldier] <= 0f ||
            !IsValidFormationTarget(currentTarget);

        if (!shouldRefreshTarget)
            return currentTarget;

        formationTargetRefreshTimers[soldier] = Mathf.Max(
            0.01f,
            squadCombatProfile.formationTargetRefreshInterval);

        currentTarget = FindBestFormationTarget(soldier, currentTarget);

        formationTargets[soldier] = currentTarget;
        soldier.SetCombatTarget(currentTarget);

        return currentTarget;
    }

    bool IsFormationActiveSoldier(float distanceToTarget, float attackRange)
    {
        return distanceToTarget <= attackRange;
    }

    void TickFormationActiveSoldier(
        SoldierController soldier,
        SoldierController currentTarget,
        WeaponProfile weaponProfile,
        MeleeCombatStats meleeStats,
        RangedCombatStats rangedStats,
        bool isRangedWeapon,
        float attackInterval)
    {
        soldier.SetCombatRole(isRangedWeapon ? SoldierRole.Ranged : SoldierRole.Frontline);
        soldier.Stop();

        if (formationAttackTimers[soldier] > 0f)
            return;

        TryFormationAttack(
            soldier,
            currentTarget,
            weaponProfile,
            meleeStats,
            rangedStats,
            isRangedWeapon,
            attackInterval);
    }

    void TickFormationReserveSoldier(
        SoldierController soldier,
        SoldierController currentTarget,
        bool isRangedWeapon,
        float attackRange,
        float stoppingDistance)
    {
        soldier.SetCombatRole(SoldierRole.Reserve);
        ClearFormationActiveAttackerCombatLockCandidate(soldier);

        if (currentTarget == null)
        {
            soldier.Stop();
            soldier.ClearCombatTarget();
            return;
        }

        Vector3 moveDestination = isRangedWeapon
            ? GetRangedMoveDestination(soldier, currentTarget, attackRange)
            : currentTarget.transform.position;

        Vector3 desiredMoveDirection = moveDestination - soldier.transform.position;
        desiredMoveDirection.y = 0f;

        if (desiredMoveDirection.sqrMagnitude <= 0.0001f)
        {
            soldier.Stop();
            return;
        }

        desiredMoveDirection.Normalize();

        SoldierContactSensor contactSensor = soldier.ContactSensor;

        if (contactSensor != null)
        {
            bool hasForwardGap = contactSensor.IsForwardFriendlyGapOpen(
                soldier,
                desiredMoveDirection,
                squadCombatProfile.formationReserveForwardGapDistance,
                squadCombatProfile.formationReserveForwardGapRadius);

            if (!hasForwardGap)
            {
                MarkFormationReserveBlocked(soldier);

                if (formationReserveBlockedSitTimers[soldier] > 0f)
                {
                    soldier.Stop();
                    return;
                }

                if (TryTickFormationReserveBehindFriendlyReposition(
                        soldier,
                        contactSensor,
                        currentTarget,
                        attackRange))
                {
                    return;
                }

                if (TryTickFormationReserveSideStep(
                        soldier,
                        contactSensor,
                        desiredMoveDirection,
                        stoppingDistance))
                {
                    return;
                }

                soldier.Stop();
                return;
            }

            if (IsFormationReserveStillSitting(soldier))
            {
                soldier.Stop();
                return;
            }
        }

        ClearFormationReserveBlockedState(soldier);

        soldier.MoveToCombatPoint(
            moveDestination,
            stoppingDistance,
            squadCombatProfile.formationCombatMoveSpeedMultiplier);
    }

    void MarkFormationActiveAttackerCombatLockCandidate(
        SoldierController soldier,
        SoldierController currentTarget)
    {
        if (!squadCombatProfile.formationAttackerCombatLockEnabled)
            return;

        if (soldier == null || !soldier.IsAlive)
            return;

        if (currentTarget == null || !currentTarget.IsAlive)
            return;

        formationActiveAttackerCombatLockTargets[soldier] = currentTarget;
    }

    void ClearFormationActiveAttackerCombatLockCandidate(SoldierController soldier)
    {
        if (soldier == null)
            return;

        formationActiveAttackerCombatLockTargets.Remove(soldier);
    }

    public void BeginCombatLockedMoveOrder()
    {
        if (!squadCombatProfile.formationAttackerCombatLockEnabled)
        {
            ClearTargets();
            return;
        }

        BuildFormationAttackerCombatLocksFromActiveAttackers();

        targetSquad = null;
        currentEngagementType = SquadEngagementReason.None;
        approachRefreshTimer = 0f;
        approachEngagementSettleTimer = 0f;
        formationChargeTimer = 0f;

        ClearFormationRuntimeState(
            clearAttackTimers: false,
            clearCombatLocks: false);

        ClearSoldierCombatStates(preserveCombatLockedSoldiers: true);
    }

    void BuildFormationAttackerCombatLocksFromActiveAttackers()
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            if (!formationActiveAttackerCombatLockTargets.TryGetValue(
                    soldier,
                    out SoldierController lockTarget))
            {
                continue;
            }

            if (lockTarget == null || !lockTarget.IsAlive)
                continue;

            formationAttackerCombatLockTargets[soldier] = lockTarget;
            formationAttackerCombatLockTimers[soldier] = Random.Range(
                squadCombatProfile.formationAttackerCombatLockTimeMin,
                squadCombatProfile.formationAttackerCombatLockTimeMax);
        }
    }

    public void TickCombatLocks()
    {
        if (!squadCombatProfile.formationAttackerCombatLockEnabled)
            return;

        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            TickFormationAttackerCombatLock(soldier);
        }
    }

    void TickFormationAttackerCombatLock(SoldierController soldier)
    {
        if (soldier == null)
            return;

        if (!formationAttackerCombatLockTimers.ContainsKey(soldier) &&
            !formationAttackerCombatLockTargets.ContainsKey(soldier))
        {
            return;
        }

        if (!IsSoldierCombatLocked(soldier))
        {
            ClearFormationAttackerCombatLock(soldier);
            return;
        }

        formationAttackerCombatLockTimers[soldier] -= Time.deltaTime;

        if (!IsSoldierCombatLocked(soldier))
        {
            ClearFormationAttackerCombatLock(soldier);
            return;
        }

        SoldierController lockTarget = formationAttackerCombatLockTargets[soldier];

        soldier.SetCombatRole(SoldierRole.Frontline);
        soldier.SetCombatTarget(lockTarget);
        soldier.Stop();
        soldier.FaceToward(lockTarget.transform.position, soldier.Stats != null ? soldier.Stats.movement.turnSpeed : soldier.Data.movement.turnSpeed);
    }

    public bool IsSoldierCombatLocked(SoldierController soldier)
    {
        if (!squadCombatProfile.formationAttackerCombatLockEnabled)
            return false;

        if (soldier == null || !soldier.IsAlive) // PERFORMANCE
            return false;

        if (!formationAttackerCombatLockTimers.TryGetValue(
                soldier,
                out float lockTimer) ||
            lockTimer <= 0f)
        {
            return false;
        }

        return formationAttackerCombatLockTargets.TryGetValue(
                   soldier,
                   out SoldierController lockTarget) &&
               lockTarget != null &&
               lockTarget.IsAlive; // PERFORMANCE
    }

    void ClearFormationAttackerCombatLock(SoldierController soldier)
    {
        if (soldier == null)
            return;

        formationAttackerCombatLockTimers.Remove(soldier);
        formationAttackerCombatLockTargets.Remove(soldier);

        if (soldier.IsAlive)
        {
            soldier.SetCombatRole(SoldierRole.None);
            soldier.ClearCombatTarget();
        }
    }

    bool TryTickFormationReserveBehindFriendlyReposition(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        float attackRange)
    {
        if (!squadCombatProfile.formationReserveBehindFriendlyRepositionEnabled)
            return false;

        if (soldier == null || contactSensor == null || currentTarget == null)
            return false;

        if (TryUseCachedFormationReserveBehindFriendlyPoint(
                soldier,
                contactSensor,
                currentTarget,
                attackRange))
        {
            return true;
        }

        if (formationReserveBehindFriendlySearchTimers.TryGetValue(
                soldier,
                out float searchTimer) &&
            searchTimer > 0f)
        {
            return false;
        }

        formationReserveBehindFriendlySearchTimers[soldier] =
            squadCombatProfile.formationReserveBehindFriendlySearchInterval;

        if (!TryFindFormationReserveBehindFriendlyPoint(
                soldier,
                contactSensor,
                currentTarget,
                attackRange,
                out Vector3 reservePoint))
        {
            return false;
        }

        formationReserveBehindFriendlyDestinations[soldier] = reservePoint;

        soldier.MoveToCombatPoint(
            reservePoint,
            squadCombatProfile.formationReserveBehindFriendlyReachDistance,
            squadCombatProfile.formationReserveBehindFriendlySpeedMultiplier);

        return true;
    }

    bool TryUseCachedFormationReserveBehindFriendlyPoint(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        float attackRange)
    {
        if (!formationReserveBehindFriendlyDestinations.TryGetValue(
                soldier,
                out Vector3 reservePoint))
        {
            return false;
        }

        if (!IsFormationReserveBehindFriendlyPointStillUseful(
                soldier,
                contactSensor,
                currentTarget,
                reservePoint,
                attackRange))
        {
            formationReserveBehindFriendlyDestinations.Remove(soldier);
            return false;
        }

        if (!Calc.OutOfRange(
                soldier.transform.position,
                reservePoint,
                squadCombatProfile.formationReserveBehindFriendlyReachDistance))
        {
            formationReserveBehindFriendlyDestinations.Remove(soldier);
            formationTargetRefreshTimers[soldier] = 0f;
            soldier.Stop();
            return true;
        }

        soldier.MoveToCombatPoint(
            reservePoint,
            squadCombatProfile.formationReserveBehindFriendlyReachDistance,
            squadCombatProfile.formationReserveBehindFriendlySpeedMultiplier);

        return true;
    }

    bool TryFindFormationReserveBehindFriendlyPoint(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        float attackRange,
        out Vector3 bestReservePoint)
    {
        bestReservePoint = Vector3.zero;

        if (soldier == null || contactSensor == null || currentTarget == null || roster == null)
            return false;

        bool foundPoint = false;
        float bestScore = float.PositiveInfinity;

        foreach (SoldierController friendly in roster.Soldiers)
        {
            if (!IsValidFormationReserveBehindFriendlyAnchor(
                    soldier,
                    currentTarget,
                    friendly))
            {
                continue;
            }

            if (!TryEvaluateFormationReserveBehindFriendlyAnchor(
                    soldier,
                    contactSensor,
                    currentTarget,
                    friendly,
                    attackRange,
                    out Vector3 candidatePoint,
                    out float candidateScore))
            {
                continue;
            }

            if (candidateScore >= bestScore)
                continue;

            bestScore = candidateScore;
            bestReservePoint = candidatePoint;
            foundPoint = true;
        }

        return foundPoint;
    }

    bool IsValidFormationReserveBehindFriendlyAnchor(
        SoldierController soldier,
        SoldierController currentTarget,
        SoldierController friendly)
    {
        if (soldier == null || currentTarget == null || friendly == null)
            return false;

        if (friendly == soldier)
            return false;

        if (!friendly.IsAlive)
            return false;

        if (friendly.Squad != squad)
            return false;

        float anchorDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(friendly.transform.position));

        if (anchorDistance > squadCombatProfile.formationReserveBehindFriendlyAnchorSearchRadius)
            return false;

        float soldierTargetDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(currentTarget.transform.position));

        float friendlyTargetDistance = Vector3.Distance(
            Flatten(friendly.transform.position),
            Flatten(currentTarget.transform.position));

        return friendlyTargetDistance <=
               soldierTargetDistance - squadCombatProfile.formationReserveBehindFriendlyMinAnchorForwardGain;
    }

    bool TryEvaluateFormationReserveBehindFriendlyAnchor(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        SoldierController friendlyAnchor,
        float attackRange,
        out Vector3 bestPoint,
        out float bestScore)
    {
        bestPoint = Vector3.zero;
        bestScore = float.PositiveInfinity;

        if (soldier == null || contactSensor == null || currentTarget == null || friendlyAnchor == null)
            return false;

        Vector3 awayFromTarget =
            friendlyAnchor.transform.position - currentTarget.transform.position;

        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude <= 0.0001f)
            awayFromTarget = -combatContactDirection;

        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude <= 0.0001f)
            return false;

        awayFromTarget.Normalize();

        Vector3 side = new Vector3(
            awayFromTarget.z,
            0f,
            -awayFromTarget.x);

        Vector3 centerPoint =
            friendlyAnchor.transform.position +
            awayFromTarget * squadCombatProfile.formationReserveBehindFriendlyBackOffset;

        bool foundPoint = false;

        TryReplaceBestFormationReserveBehindFriendlyCandidate(
            soldier,
            contactSensor,
            currentTarget,
            centerPoint,
            attackRange,
            ref bestPoint,
            ref bestScore,
            ref foundPoint);

        if (squadCombatProfile.formationReserveBehindFriendlySideOffset > 0f)
        {
            TryReplaceBestFormationReserveBehindFriendlyCandidate(
                soldier,
                contactSensor,
                currentTarget,
                centerPoint + side * squadCombatProfile.formationReserveBehindFriendlySideOffset,
                attackRange,
                ref bestPoint,
                ref bestScore,
                ref foundPoint);

            TryReplaceBestFormationReserveBehindFriendlyCandidate(
                soldier,
                contactSensor,
                currentTarget,
                centerPoint - side * squadCombatProfile.formationReserveBehindFriendlySideOffset,
                attackRange,
                ref bestPoint,
                ref bestScore,
                ref foundPoint);
        }

        return foundPoint;
    }

    void TryReplaceBestFormationReserveBehindFriendlyCandidate(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        Vector3 rawPoint,
        float attackRange,
        ref Vector3 bestPoint,
        ref float bestScore,
        ref bool foundPoint)
    {
        if (!TryScoreFormationReserveBehindFriendlyCandidate(
                soldier,
                contactSensor,
                currentTarget,
                rawPoint,
                attackRange,
                out Vector3 projectedPoint,
                out float score))
        {
            return;
        }

        if (score >= bestScore)
            return;

        bestPoint = projectedPoint;
        bestScore = score;
        foundPoint = true;
    }

    bool TryScoreFormationReserveBehindFriendlyCandidate(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        Vector3 rawPoint,
        float attackRange,
        out Vector3 projectedPoint,
        out float score)
    {
        projectedPoint = Vector3.zero;
        score = float.PositiveInfinity;

        if (soldier == null || contactSensor == null || currentTarget == null)
            return false;

        if (!NavMesh.SamplePosition(
                rawPoint,
                out NavMeshHit navHit,
                squadCombatProfile.formationReserveBehindFriendlyNavMeshProjectionRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        projectedPoint = navHit.position;

        float moveDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(projectedPoint));

        if (moveDistance > squadCombatProfile.formationReserveBehindFriendlyMaxMoveDistance)
            return false;

        float currentTargetDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(currentTarget.transform.position));

        float candidateTargetDistance = Vector3.Distance(
            Flatten(projectedPoint),
            Flatten(currentTarget.transform.position));

        float targetProgress = currentTargetDistance - candidateTargetDistance;

        if (targetProgress < squadCombatProfile.formationReserveBehindFriendlyMinTargetProgress)
            return false;

        // Do not step into attack range through a reserve reposition. Once the
        // soldier is that close, direct contact/attack logic should own behavior.
        float minimumEnemyDistance = Mathf.Max(0.1f, attackRange * 0.85f);

        if (candidateTargetDistance < minimumEnemyDistance)
            return false;

        if (contactSensor.IsPointOccupiedByLivingSoldier(
                soldier,
                projectedPoint,
                squadCombatProfile.formationReserveBehindFriendlyOccupancyRadius))
        {
            return false;
        }

        int nearbyBodies = CountLivingSoldiersNearFormationPoint(
            soldier,
            projectedPoint,
            squadCombatProfile.formationReserveBehindFriendlyCrowdRadius);

        if (nearbyBodies > squadCombatProfile.formationReserveBehindFriendlyMaxNearbyBodies)
            return false;

        if (!HasCompleteFormationReserveBehindFriendlyPath(
                soldier.transform.position,
                projectedPoint))
        {
            return false;
        }

        score =
            moveDistance +
            nearbyBodies * squadCombatProfile.formationReserveBehindFriendlyCrowdScoreWeight -
            targetProgress * squadCombatProfile.formationReserveBehindFriendlyProgressScoreWeight;

        return true;
    }

    bool IsFormationReserveBehindFriendlyPointStillUseful(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        Vector3 reservePoint,
        float attackRange)
    {
        if (soldier == null || contactSensor == null || currentTarget == null)
            return false;

        if (!currentTarget.IsAlive)
            return false;

        float currentTargetDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(currentTarget.transform.position));

        float reserveTargetDistance = Vector3.Distance(
            Flatten(reservePoint),
            Flatten(currentTarget.transform.position));

        float targetProgress = currentTargetDistance - reserveTargetDistance;

        if (targetProgress < -0.1f)
            return false;

        float minimumEnemyDistance = Mathf.Max(0.1f, attackRange * 0.85f);

        if (reserveTargetDistance < minimumEnemyDistance)
            return false;

        if (contactSensor.IsPointOccupiedByLivingSoldier(
                soldier,
                reservePoint,
                squadCombatProfile.formationReserveBehindFriendlyOccupancyRadius))
        {
            return false;
        }

        int nearbyBodies = CountLivingSoldiersNearFormationPoint(
            soldier,
            reservePoint,
            squadCombatProfile.formationReserveBehindFriendlyCrowdRadius);

        return nearbyBodies <= squadCombatProfile.formationReserveBehindFriendlyMaxNearbyBodies;
    }

    int CountLivingSoldiersNearFormationPoint(
        SoldierController ignoredSoldier,
        Vector3 point,
        float radius)
    {
        radius = Mathf.Max(0.01f, radius);
        float radiusSqr = radius * radius;
        int count = 0;

        if (SquadManager.Instance != null)
        {
            foreach (SquadController candidateSquad in SquadManager.Instance.Squads)
            {
                if (candidateSquad == null || candidateSquad.Roster == null)
                    continue;

                count += CountLivingSoldiersNearFormationPointFromRoster(
                    ignoredSoldier,
                    candidateSquad.Roster,
                    point,
                    radiusSqr);
            }

            return count;
        }

        count += CountLivingSoldiersNearFormationPointFromRoster(
            ignoredSoldier,
            roster,
            point,
            radiusSqr);

        if (targetSquad != null)
        {
            count += CountLivingSoldiersNearFormationPointFromRoster(
                ignoredSoldier,
                targetSquad.Roster,
                point,
                radiusSqr);
        }

        return count;
    }

    int CountLivingSoldiersNearFormationPointFromRoster(
        SoldierController ignoredSoldier,
        SquadRoster sourceRoster,
        Vector3 point,
        float radiusSqr)
    {
        if (sourceRoster == null)
            return 0;

        int count = 0;
        Vector3 flatPoint = Flatten(point);

        foreach (SoldierController soldier in sourceRoster.Soldiers)
        {
            if (soldier == null || soldier == ignoredSoldier || !soldier.IsAlive)
                continue;

            float distanceSqr = Vector3.SqrMagnitude(
                Flatten(soldier.transform.position) - flatPoint);

            if (distanceSqr <= radiusSqr)
                count++;
        }

        return count;
    }

    bool HasCompleteFormationReserveBehindFriendlyPath(
        Vector3 startPoint,
        Vector3 endPoint)
    {
        if (!NavMesh.CalculatePath(
                startPoint,
                endPoint,
                NavMesh.AllAreas,
                formationReserveBehindFriendlyPath))
        {
            return false;
        }

        return formationReserveBehindFriendlyPath.status == NavMeshPathStatus.PathComplete;
    }

    bool TryTickFormationReserveSideStep(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        Vector3 desiredMoveDirection,
        float stoppingDistance)
    {
        if (!squadCombatProfile.formationReserveSideStepEnabled)
            return false;

        if (soldier == null || contactSensor == null)
            return false;

        if (formationReserveSideStepDestinations.TryGetValue(
                soldier,
                out Vector3 sideStepDestination))
        {
            if (!Calc.OutOfRange(
                    soldier.transform.position,
                    sideStepDestination,
                    0.18f))
            {
                formationReserveSideStepDestinations.Remove(soldier);
                formationTargetRefreshTimers[soldier] = 0f;
                return false;
            }

            soldier.MoveToCombatPoint(
                sideStepDestination,
                Mathf.Min(stoppingDistance, 0.12f),
                squadCombatProfile.formationReserveSideStepSpeedMultiplier);

            return true;
        }

        if (formationReserveSideStepTimers[soldier] > 0f)
            return false;

        formationReserveSideStepTimers[soldier] = Random.Range(
            squadCombatProfile.formationReserveSideStepIntervalMin,
            squadCombatProfile.formationReserveSideStepIntervalMax);

        if (!TryFindFormationReserveSideStepPoint(
                soldier,
                contactSensor,
                desiredMoveDirection,
                out sideStepDestination))
        {
            return false;
        }

        formationReserveSideStepDestinations[soldier] = sideStepDestination;

        soldier.MoveToCombatPoint(
            sideStepDestination,
            Mathf.Min(stoppingDistance, 0.12f),
            squadCombatProfile.formationReserveSideStepSpeedMultiplier);

        return true;
    }

    bool TryFindFormationReserveSideStepPoint(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        Vector3 desiredMoveDirection,
        out Vector3 sideStepPoint)
    {
        sideStepPoint = soldier != null ? soldier.transform.position : transform.position;

        if (soldier == null || contactSensor == null)
            return false;

        Vector3 right = Vector3.Cross(Vector3.up, desiredMoveDirection).normalized;

        if (right.sqrMagnitude <= 0.0001f)
            return false;

        bool leftBlocked = contactSensor.IsSideBlockedByFriendly(soldier, -right);
        bool rightBlocked = contactSensor.IsSideBlockedByFriendly(soldier, right);

        if (leftBlocked && rightBlocked)
            return false;

        Vector3 firstSide;
        Vector3 secondSide = Vector3.zero;
        bool hasSecondSide = false;

        if (!leftBlocked && !rightBlocked)
        {
            bool chooseRightFirst = Random.value >= 0.5f;
            firstSide = chooseRightFirst ? right : -right;
            secondSide = chooseRightFirst ? -right : right;
            hasSecondSide = true;
        }
        else
        {
            firstSide = !rightBlocked ? right : -right;
        }

        if (TryBuildFormationReserveSideStepPoint(
                soldier,
                contactSensor,
                firstSide,
                out sideStepPoint))
        {
            return true;
        }

        return hasSecondSide &&
               TryBuildFormationReserveSideStepPoint(
                   soldier,
                   contactSensor,
                   secondSide,
                   out sideStepPoint);
    }

    bool TryBuildFormationReserveSideStepPoint(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        Vector3 sideDirection,
        out Vector3 sideStepPoint)
    {
        sideStepPoint = soldier.transform.position;

        Vector3 rawPoint =
            soldier.transform.position +
            sideDirection.normalized * squadCombatProfile.formationReserveSideStepDistance;

        if (!NavMesh.SamplePosition(
                rawPoint,
                out NavMeshHit navHit,
                squadCombatProfile.formationReserveSideStepDistance,
                NavMesh.AllAreas))
        {
            return false;
        }

        if (contactSensor.IsPointOccupiedByLivingSoldier(
                soldier,
                navHit.position,
                squadCombatProfile.formationReserveSideStepOccupancyRadius))
        {
            return false;
        }

        sideStepPoint = navHit.position;
        return true;
    }

    void MarkFormationReserveBlocked(SoldierController soldier)
    {
        if (soldier == null)
            return;

        if (!formationReserveBlockedSoldiers.Add(soldier))
            return;

        formationReserveBlockedSitTimers[soldier] = Random.Range(squadCombatProfile.formationReserveMinimumBlockedSitTimeMin, squadCombatProfile.formationReserveMinimumBlockedSitTimeMax); // chcek
        formationReserveSideStepDestinations.Remove(soldier);
        formationReserveBehindFriendlyDestinations.Remove(soldier);
    }

    bool IsFormationReserveStillSitting(SoldierController soldier)
    {
        return soldier != null &&
               formationReserveBlockedSoldiers.Contains(soldier) &&
               formationReserveBlockedSitTimers.TryGetValue(
                   soldier,
                   out float sitTimer) &&
               sitTimer > 0f;
    }

    void ClearFormationReserveBlockedState(SoldierController soldier)
    {
        if (soldier == null)
            return;

        formationReserveBlockedSoldiers.Remove(soldier);

        if (formationReserveBlockedSitTimers.ContainsKey(soldier))
            formationReserveBlockedSitTimers[soldier] = 0f;

        formationReserveSideStepDestinations.Remove(soldier);
        formationReserveBehindFriendlyDestinations.Remove(soldier);

        if (formationReserveBehindFriendlySearchTimers.ContainsKey(soldier))
            formationReserveBehindFriendlySearchTimers[soldier] = 0f;
    }

    void EnsureFormationTimers(SoldierController soldier)
    {
        if (!formationTargetRefreshTimers.ContainsKey(soldier))
            formationTargetRefreshTimers[soldier] = 0f;

        if (!formationAttackTimers.ContainsKey(soldier))
            formationAttackTimers[soldier] = 0f;

        if (!formationReserveSideStepTimers.ContainsKey(soldier))
        {
            formationReserveSideStepTimers[soldier] = Random.Range(
                squadCombatProfile.formationReserveSideStepIntervalMin,
                squadCombatProfile.formationReserveSideStepIntervalMax);
        }

        if (!formationReserveBlockedSitTimers.ContainsKey(soldier))
            formationReserveBlockedSitTimers[soldier] = 0f;

        if (!formationReserveBehindFriendlySearchTimers.ContainsKey(soldier))
            formationReserveBehindFriendlySearchTimers[soldier] = 0f;

    }

    void TickFormationTimers(SoldierController soldier)
    {
        formationTargetRefreshTimers[soldier] -= Time.deltaTime;
        formationAttackTimers[soldier] -= Time.deltaTime;
        formationReserveSideStepTimers[soldier] -= Time.deltaTime;
        formationReserveBlockedSitTimers[soldier] -= Time.deltaTime;
        formationReserveBehindFriendlySearchTimers[soldier] -= Time.deltaTime;

    }

    bool TryFindImmediateFormationContactTarget(
        SoldierController soldier,
        SoldierController currentTarget,
        float attackRange,
        out SoldierController contactTarget)
    {
        contactTarget = null;

        if (!squadCombatProfile.formationImmediateContactOverrideEnabled)
            return false;

        if (soldier == null || !soldier.IsAlive)
            return false;

        float contactRange = Mathf.Max(
            0.1f,
            attackRange + squadCombatProfile.formationImmediateContactRangePadding);

        float bestDistanceSqr = contactRange * contactRange;

        if (squadCombatProfile.formationMultiSquadLocalTargetingEnabled && SquadManager.Instance != null)
        {
            foreach (SquadController candidateSquad in SquadManager.Instance.Squads)
            {
                if (!CanAttack(candidateSquad))
                    continue;

                FindImmediateFormationContactTargetFromSquad(
                    soldier,
                    candidateSquad,
                    ref contactTarget,
                    ref bestDistanceSqr);
            }
        }
        else
        {
            FindImmediateFormationContactTargetFromSquad(
                soldier,
                targetSquad,
                ref contactTarget,
                ref bestDistanceSqr);
        }

        return contactTarget != null;
    }

    void FindImmediateFormationContactTargetFromSquad(
        SoldierController soldier,
        SquadController candidateSquad,
        ref SoldierController contactTarget,
        ref float bestDistanceSqr)
    {
        if (soldier == null || candidateSquad == null || candidateSquad.Roster == null)
            return;

        Vector3 soldierPosition = Flatten(soldier.transform.position);

        foreach (SoldierController enemy in candidateSquad.Roster.Soldiers)
        {
            if (!IsValidFormationTarget(enemy))
                continue;

            float distanceSqr = Vector3.SqrMagnitude(
                soldierPosition - Flatten(enemy.transform.position));

            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            contactTarget = enemy;
        }
    }

    SoldierController FindBestFormationTarget(
        SoldierController soldier,
        SoldierController currentTarget)
    {
        if (soldier == null || targetSquad == null || targetSquad.Roster == null)
            return null;

        SoldierController bestTarget = null;
        float bestScore = float.PositiveInfinity;

        if (squadCombatProfile.formationMultiSquadLocalTargetingEnabled && SquadManager.Instance != null)
        {
            foreach (SquadController candidateSquad in SquadManager.Instance.Squads)
            {
                if (!CanAttack(candidateSquad))
                    continue;

                ScoreFormationTargetsFromSquad(
                    soldier,
                    currentTarget,
                    candidateSquad,
                    candidateSquad == targetSquad,
                    ref bestTarget,
                    ref bestScore);
            }
        }
        else
        {
            ScoreFormationTargetsFromSquad(
                soldier,
                currentTarget,
                targetSquad,
                true,
                ref bestTarget,
                ref bestScore);
        }

        return bestTarget;
    }

    void ScoreFormationTargetsFromSquad(
        SoldierController soldier,
        SoldierController currentTarget,
        SquadController candidateSquad,
        bool isPrimaryTargetSquad,
        ref SoldierController bestTarget,
        ref float bestScore)
    {
        if (soldier == null || candidateSquad == null || candidateSquad.Roster == null)
            return;

        foreach (SoldierController enemy in candidateSquad.Roster.Soldiers)
        {
            if (!IsValidFormationTarget(enemy))
                continue;

            float distance = Vector3.Distance(
                Flatten(soldier.transform.position),
                Flatten(enemy.transform.position));

            // Non-primary enemies are local reactions only. This lets soldiers turn
            // into flankers without turning the whole squad into global free-chase.
            if (!isPrimaryTargetSquad && distance > squadCombatProfile.formationLocalEnemyTargetSearchRadius)
                continue;

            int currentAttackers = CountFormationAttackers(enemy, soldier);

            float score =
                distance +
                currentAttackers * squadCombatProfile.formationTargetCrowdingPenalty;

            if (!isPrimaryTargetSquad)
                score += squadCombatProfile.formationNonPrimaryTargetPenalty;

            if (enemy == currentTarget)
                score -= squadCombatProfile.formationCurrentTargetStickinessBonus;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }
    }

    int CountFormationAttackers(
        SoldierController target,
        SoldierController ignoredSoldier)
    {
        if (target == null)
            return 0;

        int count = 0;

        foreach (KeyValuePair<SoldierController, SoldierController> pair in formationTargets)
        {
            SoldierController attacker = pair.Key;
            SoldierController assignedTarget = pair.Value;

            if (attacker == null || attacker == ignoredSoldier || !attacker.IsAlive)
                continue;

            if (assignedTarget == target)
                count++;
        }

        return count;
    }

    bool IsValidFormationTarget(SoldierController target)
    {
        return target != null &&
               target.IsAlive &&
               target.Squad != null &&
               CanAttack(target.Squad);
    }

    void GetFormationAttackValues(
        SoldierController soldier,
        WeaponProfile weaponProfile,
        bool isRangedWeapon,
        out MeleeCombatStats meleeStats,
        out RangedCombatStats rangedStats,
        out float attackRange,
        out float attackInterval,
        out float stoppingDistance)
    {
        meleeStats = soldier != null && soldier.Stats != null
            ? soldier.Stats.melee
            : weaponProfile != null
                ? weaponProfile.melee
                : MeleeCombatStats.Default;

        rangedStats = soldier != null && soldier.Stats != null
            ? soldier.Stats.ranged
            : weaponProfile != null
                ? weaponProfile.ranged
                : RangedCombatStats.Default;

        if (isRangedWeapon)
        {
            attackRange = Mathf.Max(0.1f, rangedStats.attackRange);
            attackInterval = Mathf.Max(0.05f, rangedStats.attackInterval);
            stoppingDistance = Mathf.Max(
                0.05f,
                attackRange * squadCombatProfile.formationRangedStoppingDistanceMultiplier);
            return;
        }

        attackRange = Mathf.Max(
            0.1f,
            weaponProfile != null
                ? meleeStats.attackRange
                : squadCombatProfile.formationFallbackMeleeAttackRange);

        attackInterval = Mathf.Max(
            0.05f,
            weaponProfile != null
                ? meleeStats.attackInterval
                : squadCombatProfile.formationFallbackMeleeAttackInterval);

        stoppingDistance = Mathf.Max(
            0.05f,
            attackRange * squadCombatProfile.formationMeleeStoppingDistanceMultiplier);
    }

    Vector3 GetRangedMoveDestination(
        SoldierController soldier,
        SoldierController target,
        float attackRange)
    {
        Vector3 toTarget = target.transform.position - soldier.transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
            return soldier.transform.position;

        Vector3 directionToTarget = toTarget.normalized;

        float preferredDistance = Mathf.Max(
            0.1f,
            attackRange * squadCombatProfile.formationRangedPreferredDistanceMultiplier);

        return target.transform.position - directionToTarget * preferredDistance;
    }

    // void TryFormationAttack(
    //     SoldierController attacker,
    //     SoldierController target,
    //     WeaponProfile weaponProfile,
    //     MeleeCombatStats meleeStats,
    //     RangedCombatStats rangedStats,
    //     bool isRangedWeapon,
    //     float attackInterval)
    // {
    //     if (attacker == null || target == null)
    //         return;
    //
    //     bool beganAttack = attacker.TryBeginAction(SoldierActionState.Attack);
    //
    //     if (!beganAttack)
    //         return;
    //
    //     formationAttackTimers[attacker] = Mathf.Max(0.05f, attackInterval);
    //
    //     if (isRangedWeapon)
    //     {
    //         BeginFormationRangedAttack(
    //             attacker,
    //             target,
    //             weaponProfile,
    //             rangedStats);
    //         return;
    //     }
    //
    //     ResolveFormationCombatHit(
    //         attacker,
    //         target,
    //         meleeStats);
    // }
    
    void TryFormationAttack(
        SoldierController attacker,
        SoldierController target,
        WeaponProfile weaponProfile,
        MeleeCombatStats meleeStats,
        RangedCombatStats rangedStats,
        bool isRangedWeapon,
        float attackInterval)
    {
        if (attacker == null || target == null)
            return;

        bool beganAttack =
            attacker.TryBeginAction(SoldierActionState.Attack);

        if (!beganAttack)
            return;

        float randInterval = Random.Range(squadCombatProfile.formationAttackIntervalRandomMin, squadCombatProfile.formationAttackIntervalRandomMax);
        
        formationAttackTimers[attacker] =
            Mathf.Max(0.05f, attackInterval + randInterval); // NEW: added randomized attack interval

        if (isRangedWeapon)
        {
            BeginFormationRangedAttack(
                attacker,
                target,
                weaponProfile,
                rangedStats);

            return;
        }

        // Melee damage is not resolved here.
        // Snapshot the committed target and wait for AttackImpact.
        formationPendingMeleeTargets[attacker] = target;
    }

    void ResolveFormationCombatHit(
        SoldierController attacker,
        SoldierController target,
        MeleeCombatStats meleeStats)
    {
        if (attacker == null || target == null || target.Health == null)
            return;

        CombatDefenseStats defenderStats = target.Stats != null
            ? target.Stats.defense
            : CombatDefenseStats.Default;

        DamageResult damageResult = CombatResolver.ResolveMeleeHit(
            meleeStats,
            defenderStats);

        if (!damageResult.didHit)
            return;

        target.Health.TakeDamage(
            damageResult.normalDamage,
            damageResult.armorPiercingDamage);

        ApplyFormationCombatHitImpulse(attacker, target);

        if (target.IsAlive)
            target.TryBeginAction(SoldierActionState.HitReact);
    }

    void ApplyFormationCombatHitImpulse(
        SoldierController attacker,
        SoldierController target)
    {
        if (!squadCombatProfile.formationMeleeHitImpulseEnabled)
            return;

        if (attacker == null || target == null || target.Motor == null)
            return;

        Vector3 impactDirection =
            target.transform.position - attacker.transform.position;

        impactDirection.y = 0f;

        if (impactDirection.sqrMagnitude <= 0.0001f)
            impactDirection = attacker.transform.forward;

        target.Motor.ApplyExternalImpulse(
            impactDirection,
            squadCombatProfile.formationMeleeHitImpulseMagnitude,
            squadCombatProfile.formationMeleeHitImpulseDuration);
    }

    void BeginFormationRangedAttack(
        SoldierController attacker,
        SoldierController target,
        WeaponProfile weaponProfile,
        RangedCombatStats rangedStats)
    {
        if (attacker == null || target == null)
            return;

        if (weaponProfile != null && rangedStats.projectilePrefab != null)
        {
            formationPendingProjectileTargets[attacker] = target;
            formationPendingProjectileWeapons[attacker] = weaponProfile;
            return;
        }

        ResolveFormationRangedHit(
            attacker,
            target,
            rangedStats);
    }

    void ResolveFormationRangedHit(
        SoldierController attacker,
        SoldierController target,
        RangedCombatStats rangedStats)
    {
        if (attacker == null || target == null || target.Health == null)
            return;

        CombatDefenseStats defenderStats = target.Stats != null
            ? target.Stats.defense
            : CombatDefenseStats.Default;

        DamageResult damageResult = CombatResolver.ResolveRangedHit(
            rangedStats,
            defenderStats);

        if (!damageResult.didHit)
            return;

        target.Health.TakeDamage(
            damageResult.normalDamage,
            damageResult.armorPiercingDamage);

        if (target.IsAlive)
            target.TryBeginAction(SoldierActionState.HitReact);
    }

    /// Called by SoldierCombat from an animation event bridge.
    public void ResolveSoldierProjectileRelease(SoldierController attacker)
    {
        if (attacker == null)
            return;

        if (!formationPendingProjectileTargets.TryGetValue(
                attacker,
                out SoldierController target))
        {
            return;
        }

        if (!formationPendingProjectileWeapons.TryGetValue(
                attacker,
                out WeaponProfile weaponProfile))
        {
            ClearPendingProjectile(attacker);
            return;
        }

        ClearPendingProjectile(attacker);

        if (target == null || !target.IsAlive)
            return;

        if (weaponProfile == null ||
            weaponProfile.ranged.projectilePrefab == null)
            return;

        Transform attackOrigin = attacker.AttackOrigin;
        Vector3 spawnPosition = attackOrigin != null
            ? attackOrigin.position
            : attacker.transform.position;

        Quaternion spawnRotation = attackOrigin != null
            ? attackOrigin.rotation
            : attacker.transform.rotation;

        GameObject projectileObject = Instantiate(
            weaponProfile.ranged.projectilePrefab,
            spawnPosition,
            spawnRotation);

        ProjectileController projectile =
            projectileObject.GetComponent<ProjectileController>();

        if (projectile == null)
        {
            Debug.LogWarning(
                $"{name}: Ranged projectile prefab has no ProjectileController.",
                projectileObject);
            return;
        }

        projectile.Initialize(
            attacker,
            target,
            weaponProfile);
    }

    /// Called by SoldierCombat from the AttackImpact animation event.
    /// Resolves melee damage, impulse, and HitReact on the authored impact frame.
    public void ResolveSoldierAttackImpact(SoldierController attacker)
    {
        if (attacker == null)
            return;

        if (attacker.ActionState != SoldierActionState.Attack)
        {
            ClearPendingMeleeAttack(attacker);
            return;
        }

        if (!formationPendingMeleeTargets.TryGetValue(
                attacker,
                out SoldierController target))
        {
            return;
        }

        // Consume first so duplicate AttackImpact animation events cannot hit twice.
        ClearPendingMeleeAttack(attacker);

        if (target == null || !target.IsAlive)
            return;

        WeaponProfile weaponProfile = GetWeaponProfile(attacker);

        MeleeCombatStats meleeStats = attacker.Stats != null
            ? attacker.Stats.melee
            : weaponProfile != null
                ? weaponProfile.melee
                : MeleeCombatStats.Default;

        ResolveFormationCombatHit(
            attacker,
            target,
            meleeStats);
    }
    
    void ClearPendingMeleeAttack(SoldierController soldier)
    {
        if (soldier == null)
            return;

        formationPendingMeleeTargets.Remove(soldier);
    }

    /// Called by SoldierCombat when the soldier action ends.
    // public void HandleSoldierActionCompleted(
    //     SoldierController soldier,
    //     SoldierActionState completedAction)
    // {
    //     if (completedAction == SoldierActionState.Attack)
    //         ClearPendingProjectile(soldier);
    // }
    
    public void HandleSoldierActionCompleted(
        SoldierController soldier,
        SoldierActionState completedAction)
    {
        if (completedAction != SoldierActionState.Attack)
            return;

        ClearPendingMeleeAttack(soldier);

        // Leave your existing projectile cleanup here unchanged.
        ClearPendingProjectile(soldier);
    }

    /// Called by SoldierCombat when an action is interrupted.
    // public void HandleSoldierActionInterrupted(
    //     SoldierController soldier,
    //     SoldierActionState interruptedAction,
    //     SoldierActionState newAction)
    // {
    //     if (interruptedAction == SoldierActionState.Attack)
    //         ClearPendingProjectile(soldier);
    // }
    
    public void HandleSoldierActionInterrupted(
        SoldierController soldier,
        SoldierActionState interruptedAction,
        SoldierActionState newAction)
    {
        if (interruptedAction != SoldierActionState.Attack)
            return;

        ClearPendingMeleeAttack(soldier);

        // Leave your existing projectile cleanup here unchanged.
        ClearPendingProjectile(soldier);
    }

    void ClearPendingProjectile(SoldierController soldier)
    {
        if (soldier == null)
            return;

        formationPendingProjectileTargets.Remove(soldier);
        formationPendingProjectileWeapons.Remove(soldier);
    }

    void ClearFormationRuntimeState(
        bool clearAttackTimers,
        bool clearCombatLocks = true)
    {
        formationTargets.Clear();
        formationTargetRefreshTimers.Clear();
        formationReserveSideStepTimers.Clear();
        formationReserveBlockedSitTimers.Clear();
        formationReserveBlockedSoldiers.Clear();
        formationReserveSideStepDestinations.Clear();
        formationReserveBehindFriendlySearchTimers.Clear();
        formationReserveBehindFriendlyDestinations.Clear();

        formationActiveAttackerCombatLockTargets.Clear();

        if (clearCombatLocks)
        {
            formationAttackerCombatLockTimers.Clear();
            formationAttackerCombatLockTargets.Clear();
        }

        formationPendingMeleeTargets.Clear();
        
        formationPendingProjectileTargets.Clear();
        formationPendingProjectileWeapons.Clear();

        if (clearAttackTimers)
            formationAttackTimers.Clear();
    }

    #endregion

    #region Engagement Flow

    void TickScan()
    {
        scanTimer -= Time.deltaTime;

        if (scanTimer > 0f)
            return;

        scanTimer = Mathf.Max(0.01f, squadCombatProfile.autoTargetScanInterval);

        if (TryFindTarget(out SquadController target))
        {
            SquadEngagementReason scanEngagementType =
                squad != null && squad.State == SquadState.AttackMoving
                    ? SquadEngagementReason.AttackMoveContact
                    : SquadEngagementReason.PassiveContact;

            OrderAttack(target, scanEngagementType);
        }
    }

    void BeginApproachingCombat()
    {
        ClearSoldierCombatStates();
        approachEngagementSettleTimer = 0f;

        if (squad != null)
            squad.SetState(SquadState.ApproachingCombat);

        MoveTowardCombatTarget();
    }

    bool ShouldHoldInitialEngagementForApproachSettle(SquadController target)
    {
        if (!squadCombatProfile.formationApproachSettleGateEnabled)
            return false;

        if (IsRangedCombatStyle())
            return false;

        if (!CanAttack(target))
            return false;

        if (HasEnoughSoldiersReadyForInitialEngagement(target))
        {
            approachEngagementSettleTimer = 0f;
            return false;
        }

        approachEngagementSettleTimer += Time.deltaTime;
        return approachEngagementSettleTimer < squadCombatProfile.formationApproachSettleDuration;
    }

    bool HasEnoughSoldiersReadyForInitialEngagement(SquadController target)
    {
        if (roster == null || target == null || target.Roster == null)
            return true;

        int livingSoldiers = 0;
        int readySoldiers = 0;

        float readyRange = Mathf.Max(
            squadCombatProfile.formationApproachSettleMinimumReadyRange,
            GetSquadWeaponAttackRange() + squadCombatProfile.formationApproachSettleReadyRangePadding);

        float readyRangeSqr = readyRange * readyRange;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            livingSoldiers++;

            if (IsSoldierNearAnyLivingEnemyInSquad(
                    soldier,
                    target.Roster,
                    readyRangeSqr))
            {
                readySoldiers++;
            }
        }

        if (livingSoldiers <= 0)
            return true;

        int requiredReadySoldiers = Mathf.Clamp(
            Mathf.CeilToInt(livingSoldiers * squadCombatProfile.formationApproachSettleReadyRatio),
            1,
            livingSoldiers);

        return readySoldiers >= requiredReadySoldiers;
    }

    bool IsSoldierNearAnyLivingEnemyInSquad(
        SoldierController soldier,
        SquadRoster enemyRoster,
        float readyRangeSqr)
    {
        if (soldier == null || enemyRoster == null)
            return false;

        Vector3 soldierPosition = Flatten(soldier.transform.position);

        foreach (SoldierController enemy in enemyRoster.Soldiers)
        {
            if (enemy == null || !enemy.IsAlive)
                continue;

            float distanceSqr = Vector3.SqrMagnitude(
                soldierPosition - Flatten(enemy.transform.position));

            if (distanceSqr <= readyRangeSqr)
                return true;
        }

        return false;
    }

    bool ShouldUseFormationCharge()
    {
        return squadCombatProfile.formationChargeEnabled &&
               !IsRangedCombatStyle();
    }

    bool IsCloseEnoughToStartFormationCharge(SquadController target)
    {
        if (!ShouldUseFormationCharge() || target == null)
            return false;

        if (!TryGetClosestLivingSoldierDistanceSqr(
                roster,
                target.Roster,
                out float distanceSqr))
        {
            return false;
        }

        float chargeStartDistance = Mathf.Max(
            GetEffectiveCombatStartRange(),
            squadCombatProfile.formationChargeStartDistance);

        return distanceSqr <= chargeStartDistance * chargeStartDistance;
    }

    void BeginFormationCharge()
    {
        if (!ShouldUseFormationCharge() ||
            targetSquad == null ||
            !CanAttack(targetSquad))
        {
            BeginApproachingCombat();
            return;
        }

        approachEngagementSettleTimer = 0f;
        approachRefreshTimer = 0f;
        formationChargeImpactedTargets.Clear();
        formationChargeLeadSoldiers.Clear();
        formationChargeLeadCandidates.Clear();
        formationChargeTimer = Mathf.Max(
            0.01f,
            squadCombatProfile.formationChargeMaximumDuration);

        if (squad != null)
            squad.SetState(SquadState.Charging);

        MoveTowardCombatTarget();
    }

    void RefreshFormationChargeLeadSoldiers()
    {
        formationChargeLeadSoldiers.Clear();
        formationChargeLeadCandidates.Clear();

        if (!squadCombatProfile.formationChargeLeadSpeedEnabled ||
            roster == null ||
            targetSquad == null ||
            targetSquad.Roster == null)
        {
            return;
        }

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            if (IsRangedWeapon(GetWeaponProfile(soldier)))
                continue;

            formationChargeLeadCandidates.Add(soldier);
        }

        formationChargeLeadCandidates.Sort((left, right) =>
        {
            float leftDistance = GetClosestLivingEnemyDistanceSqr(
                left,
                targetSquad.Roster);

            float rightDistance = GetClosestLivingEnemyDistanceSqr(
                right,
                targetSquad.Roster);

            int distanceComparison = leftDistance.CompareTo(rightDistance);

            if (distanceComparison != 0)
                return distanceComparison;

            return left.gameObject.GetInstanceID().CompareTo(
                right.gameObject.GetInstanceID());
        });

        int leadCount = Mathf.Clamp(
            Mathf.CeilToInt(
                formationChargeLeadCandidates.Count *
                squadCombatProfile.formationChargeLeadSoldierRatio),
            0,
            formationChargeLeadCandidates.Count);

        for (int index = 0; index < leadCount; index++)
            formationChargeLeadSoldiers.Add(
                formationChargeLeadCandidates[index]);
    }

    float GetClosestLivingEnemyDistanceSqr(
        SoldierController soldier,
        SquadRoster enemyRoster)
    {
        if (soldier == null || enemyRoster == null)
            return float.PositiveInfinity;

        float closestDistanceSqr = float.PositiveInfinity;
        Vector3 soldierPosition = Flatten(soldier.transform.position);

        foreach (SoldierController enemy in enemyRoster.Soldiers)
        {
            if (enemy == null || !enemy.IsAlive)
                continue;

            float distanceSqr = Vector3.SqrMagnitude(
                Flatten(enemy.transform.position) - soldierPosition);

            if (distanceSqr < closestDistanceSqr)
                closestDistanceSqr = distanceSqr;
        }

        return closestDistanceSqr;
    }

    void TickFormationChargeImpulseEmitters()
    {
        if (!squadCombatProfile.formationChargeImpulseEnabled ||
            roster == null ||
            targetSquad == null)
        {
            return;
        }

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive || soldier.Motor == null)
                continue;

            if (IsRangedWeapon(GetWeaponProfile(soldier)))
                continue;

            Vector3 chargeDirection = soldier.Motor.Velocity;
            chargeDirection.y = 0f;

            if (chargeDirection.sqrMagnitude <= 0.0001f)
                chargeDirection = combatContactDirection;

            chargeDirection.y = 0f;

            if (chargeDirection.sqrMagnitude <= 0.0001f)
                chargeDirection = soldier.transform.forward;

            chargeDirection.Normalize();

            Vector3 capsuleStart =
                soldier.transform.position +
                chargeDirection * 0.15f;

            Vector3 capsuleEnd =
                capsuleStart +
                chargeDirection * squadCombatProfile.formationChargeImpulseForwardDistance;

            ImpulseEmitter.EmitDirectionalCapsule(
                capsuleStart,
                capsuleEnd,
                squadCombatProfile.formationChargeImpulseRadius,
                chargeDirection,
                squadCombatProfile.formationChargeImpulseMagnitude,
                squadCombatProfile.formationChargeImpulseDuration,
                sourceSoldier: soldier,
                affectFriendlies: false,
                radialBlend: squadCombatProfile.formationChargeImpulseRadialBlend,
                minimumFalloff: 0.65f,
                excludedTargets: formationChargeImpactedTargets,
                affectedTargets: formationChargeImpactedTargets);
        }
    }

    bool HasFormationChargeReachedContactRatio(SquadController target)
    {
        if (roster == null || target == null || target.Roster == null)
            return false;

        int livingMeleeSoldiers = 0;
        int contactReadySoldiers = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            WeaponProfile weaponProfile = GetWeaponProfile(soldier);

            if (IsRangedWeapon(weaponProfile))
                continue;

            livingMeleeSoldiers++;

            GetFormationAttackValues(
                soldier,
                weaponProfile,
                false,
                out _,
                out _,
                out float attackRange,
                out _,
                out _);

            if (IsSoldierNearAnyLivingEnemyInSquad(
                    soldier,
                    target.Roster,
                    attackRange * attackRange))
            {
                contactReadySoldiers++;
            }
        }

        if (livingMeleeSoldiers <= 0)
            return true;

        int requiredContactSoldiers = Mathf.Clamp(
            Mathf.CeilToInt(
                livingMeleeSoldiers * squadCombatProfile.formationChargeContactReadyRatio),
            1,
            livingMeleeSoldiers);

        return contactReadySoldiers >= requiredContactSoldiers;
    }

    void BeginEngagement(bool notifyTarget)
    {
        if (targetSquad == null)
            return;

        movement.OrderStop();
        currentCombatStyle = ResolveCombatStyle();
        combatContactDirection = GetContactDirection();
        approachEngagementSettleTimer = 0f;

        ClearFormationRuntimeState(clearAttackTimers: false);

        if (squad != null)
            squad.SetState(SquadState.InCombat);

        if (notifyTarget && targetSquad.Combat != null)
            targetSquad.Combat.ReceiveEngagementRequest(squad);
    }

    void MoveTowardCombatTarget()
    {
        if (targetSquad == null || movement == null)
            return;

        Vector3 myCenter = TryGetLivingSoldierCenter(roster, out Vector3 resolvedMyCenter)
            ? resolvedMyCenter
            : transform.position;

        Vector3 targetCenter = TryGetLivingSoldierCenter(targetSquad.Roster, out Vector3 resolvedTargetCenter)
            ? resolvedTargetCenter
            : targetSquad.transform.position;

        Vector3 fromTargetToMe = myCenter - targetCenter;
        fromTargetToMe.y = 0f;

        if (fromTargetToMe.sqrMagnitude <= 0.0001f)
            fromTargetToMe = -(movement != null ? movement.DesiredFacing : transform.forward);

        fromTargetToMe.y = 0f;

        if (fromTargetToMe.sqrMagnitude <= 0.0001f)
            fromTargetToMe = -Vector3.forward;

        fromTargetToMe.Normalize();

        Vector3 approachPoint =
            targetCenter +
            fromTargetToMe * GetEffectiveApproachStopDistance();

        Vector3 facing = -fromTargetToMe;

        movement.OrderMove(
            approachPoint,
            facing);
    }

    void EndCombatAndReform()
    {
        ClearTargets();

        approachRefreshTimer = 0f;
        scanTimer = 0f;

        if (squad == null || roster == null || !roster.HasLivingSoldiers)
            return;

        if (formation != null)
            formation.Rebuild();

        if (movement != null)
            movement.BeginReform(recenterFromSoldiers: true);

        squad.SetState(SquadState.Reforming);
    }

    #endregion

    #region Validation / Targeting Helpers

    void ClearSoldierCombatStates(bool preserveCombatLockedSoldiers = false)
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null)
                continue;

            if (preserveCombatLockedSoldiers &&
                IsSoldierCombatLocked(soldier) &&
                formationAttackerCombatLockTargets.TryGetValue(
                    soldier,
                    out SoldierController lockTarget))
            {
                soldier.SetCombatRole(SoldierRole.Frontline);
                soldier.SetCombatTarget(lockTarget);
                soldier.Stop();
                soldier.FaceToward(lockTarget.transform.position, soldier.Stats != null ? soldier.Stats.movement.turnSpeed : soldier.Data.movement.turnSpeed);
                continue;
            }

            soldier.SetCombatRole(SoldierRole.None);
            soldier.ClearCombatTarget();
            soldier.Combat?.ClearCombat();
        }
    }

    bool CanAttack(SquadController target)
    {
        if (target == null || target == squad)
            return false;

        if (squad == null || !squad.IsInitialized)
            return false;

        if (!target.IsInitialized)
            return false;

        if (roster == null || !roster.HasLivingSoldiers)
            return false;

        if (target.Roster == null || !target.Roster.HasLivingSoldiers)
            return false;

        if (squad.Faction == null || target.Faction == null)
            return false;

        return squad.Faction.teamId != target.Faction.teamId;
    }

    bool CanRespondToEngagement(SquadController attacker)
    {
        return CanAttack(attacker);
    }

    bool IsCloseEnoughToStartEngagement(SquadController target)
    {
        if (target == null)
            return false;

        if (!TryGetClosestLivingSoldierDistanceSqr(
                roster,
                target.Roster,
                out float distanceSqr))
        {
            return false;
        }

        float range = GetEffectiveCombatStartRange();
        return distanceSqr <= range * range;
    }

    bool IsWithinCombatBreakRange(SquadController target)
    {
        if (target == null)
            return false;

        if (!TryGetClosestLivingSoldierDistanceSqr(
                roster,
                target.Roster,
                out float distanceSqr))
        {
            return false;
        }

        float range = GetEffectiveCombatBreakRange();
        return distanceSqr <= range * range;
    }

    bool ShouldScan()
    {
        if (squadCombatProfile == null || !squadCombatProfile.autoTargetScanEnabled)
            return false;

        if (squad == null || roster == null || !roster.HasLivingSoldiers)
            return false;

        return true;
    }

    bool TryFindTarget(out SquadController bestTarget)
    {
        return TryFindTargetWithinRange(
            GetEffectiveScanRange(),
            out bestTarget);
    }

    bool TryFindTargetWithinRange(
        float range,
        out SquadController bestTarget)
    {
        bestTarget = null;

        if (SquadManager.Instance == null)
            return false;

        if (range <= 0f)
            return false;

        float bestDistanceSqr = range * range;

        foreach (SquadController candidate in SquadManager.Instance.Squads)
        {
            if (!CanAttack(candidate))
                continue;

            if (!TryGetClosestLivingSoldierDistanceSqr(
                    roster,
                    candidate.Roster,
                    out float distanceSqr))
            {
                continue;
            }

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTarget = candidate;
            }
        }

        return bestTarget != null;
    }

    bool TrySwitchPrimaryCombatTarget()
    {
        if (!TryFindTargetWithinRange(
                GetEffectiveCombatBreakRange(),
                out SquadController newTarget))
        {
            return false;
        }

        if (newTarget == targetSquad)
            return true;

        targetSquad = newTarget;
        combatContactDirection = GetContactDirection();

        // Force soldiers to reconsider local enemy assignments under the new
        // primary target while preserving attack cooldowns/action state.
        formationTargets.Clear();
        formationTargetRefreshTimers.Clear();

        return true;
    }

    bool TryGetClosestLivingSoldierDistanceSqr(
        SquadRoster sourceRoster,
        SquadRoster targetRoster,
        out float closestDistanceSqr)
    {
        closestDistanceSqr = float.PositiveInfinity;

        if (sourceRoster == null || targetRoster == null)
            return false;

        bool foundPair = false;

        foreach (SoldierController sourceSoldier in sourceRoster.Soldiers)
        {
            if (sourceSoldier == null || !sourceSoldier.IsAlive)
                continue;

            Vector3 sourcePosition = Flatten(sourceSoldier.transform.position);

            foreach (SoldierController targetSoldier in targetRoster.Soldiers)
            {
                if (targetSoldier == null || !targetSoldier.IsAlive)
                    continue;

                float distanceSqr = Vector3.SqrMagnitude(
                    sourcePosition - Flatten(targetSoldier.transform.position));

                if (distanceSqr >= closestDistanceSqr)
                    continue;

                closestDistanceSqr = distanceSqr;
                foundPair = true;
            }
        }

        return foundPair;
    }

    bool TryGetLivingSoldierCenter(
        SquadRoster sourceRoster,
        out Vector3 center)
    {
        center = Vector3.zero;

        if (sourceRoster == null)
            return false;

        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (SoldierController soldier in sourceRoster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            sum += soldier.transform.position;
            count++;
        }

        if (count <= 0)
            return false;

        center = sum / count;
        return true;
    }

    #endregion

    #region Range / Style Helpers

    SquadCombatStyle ResolveCombatStyle()
    {
        if (data == null)
            return SquadCombatStyle.FormationCombat;

        if (data.defaultCombatStyle == SquadCombatStyle.RangedLine)
            return SquadCombatStyle.RangedLine;

        WeaponProfile weaponProfile = GetSquadWeaponProfile();

        if (weaponProfile != null && weaponProfile.weaponKind == WeaponKind.Ranged)
            return SquadCombatStyle.RangedLine;

        if (data.category == SquadCategory.Ranged)
            return SquadCombatStyle.RangedLine;

        return SquadCombatStyle.FormationCombat;
    }

    bool IsRangedCombatStyle()
    {
        return ResolveCombatStyle() == SquadCombatStyle.RangedLine;
    }

    float GetEffectiveScanRange()
    {
        if (squad == null || squadCombatProfile == null)
            return 0f;

        float baseRange = squad.Stance == SquadStance.Hold
            ? squadCombatProfile.holdStanceAutoTargetScanRange
            : squadCombatProfile.engageStanceAutoTargetScanRange;

        if (!IsRangedCombatStyle() || !squadCombatProfile.rangedUseWeaponRangeForTacticalRanges)
            return baseRange;

        return Mathf.Max(
            baseRange,
            GetSquadWeaponAttackRange() + squadCombatProfile.rangedScanRangePadding);
    }

    float GetEffectiveCombatStartRange()
    {
        if (!IsRangedCombatStyle() || !squadCombatProfile.rangedUseWeaponRangeForTacticalRanges)
            return Mathf.Max(0f, squadCombatProfile.defaultCombatStartRange);

        return Mathf.Max(
            0.1f,
            GetSquadWeaponAttackRange() * squadCombatProfile.rangedCombatStartRangeMultiplier);
    }

    float GetEffectiveApproachStopDistance()
    {
        if (!IsRangedCombatStyle() || !squadCombatProfile.rangedUseWeaponRangeForTacticalRanges)
            return Mathf.Max(0f, squadCombatProfile.defaultApproachStopDistance);

        return Mathf.Max(
            0.1f,
            GetSquadWeaponAttackRange() * squadCombatProfile.rangedPreferredRangeMultiplier);
    }

    float GetEffectiveCombatBreakRange()
    {
        if (!IsRangedCombatStyle() || !squadCombatProfile.rangedUseWeaponRangeForTacticalRanges)
        {
            return Mathf.Max(
                squadCombatProfile.defaultCombatStartRange,
                squadCombatProfile.defaultCombatBreakRange);
        }

        return Mathf.Max(
            GetEffectiveCombatStartRange(),
            GetSquadWeaponAttackRange() + squadCombatProfile.rangedCombatBreakRangePadding);
    }

    float GetSquadWeaponAttackRange()
    {
        WeaponProfile weaponProfile = GetSquadWeaponProfile();

        if (weaponProfile == null)
            return squadCombatProfile != null
                ? squadCombatProfile.formationFallbackMeleeAttackRange
                : 1.5f;

        return weaponProfile.weaponKind == WeaponKind.Ranged
            ? Mathf.Max(0.1f, weaponProfile.ranged.attackRange)
            : Mathf.Max(0.1f, weaponProfile.melee.attackRange);
    }

    WeaponProfile GetSquadWeaponProfile()
    {
        if (data == null || data.soldierData == null)
            return null;

        return data.soldierData.weaponProfile;
    }

    WeaponProfile GetWeaponProfile(SoldierController soldier)
    {
        return soldier != null && soldier.Stats != null
            ? soldier.Stats.weaponProfile
            : soldier != null && soldier.Data != null
                ? soldier.Data.weaponProfile
                : null;
    }

    bool IsRangedWeapon(WeaponProfile weaponProfile)
    {
        return weaponProfile != null && weaponProfile.weaponKind == WeaponKind.Ranged;
    }

    Vector3 GetContactDirection()
    {
        if (targetSquad == null)
            return movement != null ? movement.DesiredFacing : transform.forward;

        Vector3 myCenter = TryGetLivingSoldierCenter(roster, out Vector3 resolvedMyCenter)
            ? resolvedMyCenter
            : transform.position;

        Vector3 targetCenter = TryGetLivingSoldierCenter(targetSquad.Roster, out Vector3 resolvedTargetCenter)
            ? resolvedTargetCenter
            : targetSquad.transform.position;

        Vector3 direction = targetCenter - myCenter;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = movement != null ? movement.DesiredFacing : transform.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.forward;

        return direction.normalized;
    }

    Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    #endregion
}
