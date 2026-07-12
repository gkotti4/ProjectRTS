using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// -----------------------------------------------------------------------------
/// SquadCombat
/// -----------------------------------------------------------------------------
///
/// Squad-level combat coordinator for the new PrototypeMelee base.
/// Owns squad target selection, approach, engagement start/end, simple prototype
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
    private SquadCombatStyle currentCombatStyle = SquadCombatStyle.PrototypeMelee;
    private SquadEngagementReason currentEngagementType = SquadEngagementReason.None;

    // -----------------------------------------------------------------------------
    // Runtime Timers
    // -----------------------------------------------------------------------------
    private float scanTimer = 0f;
    private float approachRefreshTimer = 0f;
    private float approachEngagementSettleTimer = 0f;

    // -----------------------------------------------------------------------------
    // Prototype Reserve Settle / Side-Step MVP Tuning
    // -----------------------------------------------------------------------------
    // Super simple pass: reserve soldiers only move forward when there is a real
    // friendly-body gap. If they get blocked, they sit for a minimum time before
    // any side-step escape is allowed.
    private const float prototypeReserveForwardGapDistance = 1.15f; // How far ahead a reserve checks for a friendly-body gap before moving forward.
    private const float prototypeReserveForwardGapRadius = 0.65f; // Width/radius of the forward gap check; higher means reserves need a wider lane.
    private const float prototypeReserveMinimumBlockedSitTimeMin = 0.35f; // Shortest randomized time a newly blocked reserve must wait before repositioning.
    private const float prototypeReserveMinimumBlockedSitTimeMax = 1.25f; // Longest randomized time a newly blocked reserve must wait before repositioning.

    private const bool prototypeReserveSideStepEnabled = false; // Enables the small local side-step fallback for blocked reserve soldiers.
    private const float prototypeReserveSideStepIntervalMin = 5.0f; // Shortest randomized cooldown before a reserve can attempt another side-step.
    private const float prototypeReserveSideStepIntervalMax = 10.0f; // Longest randomized cooldown before a reserve can attempt another side-step.
    private const float prototypeReserveSideStepDistance = 1.20f; // How far sideways a reserve tries to step when using the side-step fallback.
    private const float prototypeReserveSideStepOccupancyRadius = 0.85f; // Radius used to reject side-step points already occupied by living soldiers.
    private const float prototypeReserveSideStepSpeedMultiplier = 0.50f; // Movement speed multiplier used while performing a reserve side-step.

    // -----------------------------------------------------------------------------
    // Prototype Local Enemy Targeting MVP Tuning
    // -----------------------------------------------------------------------------
    // targetSquad remains the primary squad-level order target. Individual soldiers
    // can also select nearby enemy soldiers from other hostile squads so flanks and
    // multi-squad pileups are answered locally.
    private const bool prototypeMultiSquadLocalTargetingEnabled = true; // Allows soldiers to locally target nearby enemies from non-primary hostile squads.
    private const float prototypeLocalEnemyTargetSearchRadius = 8.0f; // Max distance for considering non-primary enemy soldiers as local reaction targets.
    private const float prototypeNonPrimaryTargetPenalty = 1.25f; // Score penalty for non-primary enemies so soldiers still prefer the ordered target squad.

    // -----------------------------------------------------------------------------
    // Prototype Immediate Contact Guard MVP Tuning
    // -----------------------------------------------------------------------------
    // Prevents melee soldiers from running past a clearly reachable enemy because
    // their current assigned target is farther away or less crowded. Once a hostile
    // body is already in personal contact range, that body wins.
    private const bool prototypeImmediateContactOverrideEnabled = true; // Forces melee soldiers to prefer obvious nearby enemies over farther assigned targets.
    private const float prototypeImmediateContactRangePadding = 0.55f; // Extra range added to attack range when checking for immediate contact enemies.

    // -----------------------------------------------------------------------------
    // Prototype Approach Settle Gate MVP Tuning
    // -----------------------------------------------------------------------------
    // Prevents one corner soldier from instantly releasing the whole squad into
    // active melee before a meaningful chunk of the unit has arrived. This is not a
    // full formation-staging system; it is only a short initial contact buffer.
    private const bool prototypeApproachSettleGateEnabled = true; // Enables the short initial delay before full melee release when only a few soldiers arrive.
    private const float prototypeApproachSettleDuration = 0.45f; // How long the squad may wait at first contact for more soldiers to arrive.
    private const float prototypeApproachSettleReadyRatio = 0.45f; // Fraction of living soldiers that must be near the enemy to skip/finish the settle gate.
    private const float prototypeApproachSettleReadyRangePadding = 0.95f; // Extra range added to combat start range when counting soldiers as approach-ready.
    private const float prototypeApproachSettleMinimumReadyRange = 2.75f; // Minimum ready-check radius so very small combat start ranges still count nearby soldiers.

    // -----------------------------------------------------------------------------
    // Prototype Engagement Run-Up MVP Tuning
    // -----------------------------------------------------------------------------
    // Gives each melee soldier one short final rush toward its current target before
    // entering personal attack range. This is only an engagement presentation /
    // movement pass; it does not add charge damage, momentum, push, or knockdown.
    private const bool prototypeEngagementRunUpEnabled = true; // Enables the short per-soldier melee rush before first contact with a target.
    private const float prototypeEngagementRunUpStartDistance = 4.5f; // Maximum target distance at which a melee soldier can begin its run-up.
    private const float prototypeEngagementRunUpSpeedMultiplier = 1.20f; // Absolute multiplier of the soldier's base move speed while the run-up is active.
    private const float prototypeEngagementRunUpMaximumDuration = 1.50f; // Safety cap so a blocked or fleeing target cannot keep a soldier in run-up speed indefinitely.

    // -----------------------------------------------------------------------------
    // Prototype Engagement Run-Up Runtime State
    // -----------------------------------------------------------------------------
    // Keeping the target after the timer reaches zero marks that soldier/target pair
    // as consumed, preventing the run-up from restarting every frame or after contact.
    private readonly Dictionary<SoldierController, float> prototypeEngagementRunUpTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, SoldierController> prototypeEngagementRunUpTargets =
        new Dictionary<SoldierController, SoldierController>();

    // -----------------------------------------------------------------------------
    // Prototype Active Attacker Combat Lock MVP Tuning
    // -----------------------------------------------------------------------------
    // When a move order is given during melee, active melee attackers stay locked
    // for a short random time before they are allowed to peel away. Reserves and
    // non-active soldiers still leave quickly so the whole squad does not feel stuck.
    private const bool prototypeAttackerCombatLockEnabled = true; // Enables temporary movement lock for active melee attackers after a move/withdraw order.
    private const float prototypeAttackerCombatLockTimeMin = 0.75f; // Shortest time an active melee attacker stays committed after the squad receives a move order.
    private const float prototypeAttackerCombatLockTimeMax = 1.75f; // Longest time an active melee attacker stays committed after the squad receives a move order.

    // -----------------------------------------------------------------------------
    // Prototype Reserve Behind-Friendly Reposition MVP Tuning
    // -----------------------------------------------------------------------------
    // Conservative reserve repositioning: a blocked reserve can move into a small
    // uncrowded pocket behind another friendly who is already closer to the fight.
    // This replaces the old enemy-ring access point idea. The soldier never tries
    // to orbit the enemy; it only queues behind a friendly body.
    private const bool prototypeReserveBehindFriendlyRepositionEnabled = true; // Enables blocked reserves to move into an open pocket behind a better-positioned friendly.
    private const float prototypeReserveBehindFriendlySearchInterval = 2.35f; // Cooldown between behind-friendly reposition searches for each reserve soldier.
    private const float prototypeReserveBehindFriendlyAnchorSearchRadius = 5.5f; // Max distance for finding friendly anchors that the reserve can queue behind.
    private const float prototypeReserveBehindFriendlyBackOffset = 1.45f; // Distance behind the chosen friendly anchor where the reserve tries to move.
    private const float prototypeReserveBehindFriendlySideOffset = 0.65f; // Optional left/right offset from the behind point if side probes are enabled.
    private const float prototypeReserveBehindFriendlyNavMeshProjectionRadius = 1.05f; // Max distance allowed when projecting the candidate pocket onto the NavMesh.
    private const float prototypeReserveBehindFriendlyOccupancyRadius = 1.05f; // Radius used to reject candidate pockets already occupied by a living soldier.
    private const float prototypeReserveBehindFriendlyCrowdRadius = 1.35f; // Radius used to count nearby bodies around a candidate pocket.
    private const int prototypeReserveBehindFriendlyMaxNearbyBodies = 1; // Maximum nearby living bodies allowed before a candidate pocket is considered crowded.
    private const float prototypeReserveBehindFriendlyReachDistance = 0.18f; // Distance from the pocket at which the reserve considers the reposition complete.
    private const float prototypeReserveBehindFriendlyMaxMoveDistance = 10.0f; // Maximum distance a reserve is allowed to travel for this behind-friendly reposition.
    private const float prototypeReserveBehindFriendlyMinAnchorForwardGain = 0.45f; // Required amount the friendly anchor must be closer to the target than the reserve.
    private const float prototypeReserveBehindFriendlyMinTargetProgress = 0.05f; // Required amount the candidate point must move the reserve closer to its target.
    private const float prototypeReserveBehindFriendlySpeedMultiplier = 0.65f; // Movement speed multiplier used while moving to a behind-friendly pocket.
    private const float prototypeReserveBehindFriendlyCrowdScoreWeight = 1.25f; // Score penalty per nearby body when ranking behind-friendly candidate pockets.
    private const float prototypeReserveBehindFriendlyProgressScoreWeight = 0.75f; // Score bonus for candidate pockets that make better progress toward the target.

    // -----------------------------------------------------------------------------
    // Prototype Runtime State
    // -----------------------------------------------------------------------------
    private readonly Dictionary<SoldierController, SoldierController> prototypeTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, float> prototypeTargetRefreshTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> prototypeAttackTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> prototypeReserveSideStepTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> prototypeReserveBlockedSitTimers =
        new Dictionary<SoldierController, float>();

    private readonly HashSet<SoldierController> prototypeReserveBlockedSoldiers =
        new HashSet<SoldierController>();

    private readonly Dictionary<SoldierController, Vector3> prototypeReserveSideStepDestinations =
        new Dictionary<SoldierController, Vector3>();

    private readonly Dictionary<SoldierController, float> prototypeReserveBehindFriendlySearchTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, Vector3> prototypeReserveBehindFriendlyDestinations =
        new Dictionary<SoldierController, Vector3>();

    private readonly Dictionary<SoldierController, SoldierController> prototypeActiveAttackerCombatLockTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, float> prototypeAttackerCombatLockTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, SoldierController> prototypeAttackerCombatLockTargets =
        new Dictionary<SoldierController, SoldierController>();

    private NavMeshPath prototypeReserveBehindFriendlyPath; // Must be initialized inside of Awake/Start, cannot be initialized in Constructor

    private readonly Dictionary<SoldierController, SoldierController> prototypePendingProjectileTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, WeaponProfile> prototypePendingProjectileWeapons =
        new Dictionary<SoldierController, WeaponProfile>();

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
        prototypeReserveBehindFriendlyPath = new NavMeshPath();
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

        ClearPrototypeRuntimeState(clearAttackTimers: false);

        if (IsCloseEnoughToStartEngagement(targetSquad))
        {
            BeginEngagement(notifyTarget: true);
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

        ClearPrototypeRuntimeState(clearAttackTimers: false);

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

        ClearPrototypeRuntimeState(clearAttackTimers: true);
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

    /// Moves toward the current attack target until close enough to enter combat.
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

        approachEngagementSettleTimer = 0f;
        approachRefreshTimer -= Time.deltaTime;

        if (approachRefreshTimer > 0f)
            return;

        approachRefreshTimer = Mathf.Max(0.01f, squadCombatProfile.combatApproachRefreshInterval);

        MoveTowardCombatTarget();
    }

    /// Ticks active squad combat.
    public void TickCombat()
    {
        TickPrototypeCombat();
    }

    #endregion

    #region Prototype Combat

    /// PrototypeMelee base:
    /// - no combat homes
    /// - no formation combat slots
    /// - no old pressure/old row-scoring/support logic
    /// - simple target crowding
    /// - attack, advance, or wait if a friendly body blocks the forward lane
    void TickPrototypeCombat()
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
            TickPrototypeSoldier(soldier);
        }
    }

    void TickPrototypeSoldier(SoldierController soldier)
    {
        if (soldier == null || !soldier.IsAlive)
            return;

        // -------------------------------------------------------------------------
        // Shared Soldier Combat Setup
        // -------------------------------------------------------------------------
        EnsurePrototypeTimers(soldier);
        TickPrototypeTimers(soldier);

        SoldierController currentTarget =
            RefreshPrototypeSoldierTargetIfNeeded(soldier);

        if (currentTarget == null)
        {
            ClearPrototypeEngagementRunUpState(soldier);
            soldier.Stop();
            soldier.SetCombatRole(SoldierRole.None);
            soldier.ClearCombatTarget();
            ClearPrototypeReserveBlockedState(soldier);
            return;
        }

        WeaponProfile weaponProfile = GetWeaponProfile(soldier);
        bool isRangedWeapon = IsRangedWeapon(weaponProfile);

        GetPrototypeAttackValues(
            weaponProfile,
            isRangedWeapon,
            out MeleeCombatStats meleeStats,
            out RangedCombatStats rangedStats,
            out float attackRange,
            out float attackInterval,
            out float stoppingDistance);

        if (soldier.IsMovementLocked)
        {
            CompletePrototypeEngagementRunUp(soldier, currentTarget);
            soldier.FaceToward(currentTarget.transform.position);
            return;
        }

        if (!isRangedWeapon &&
            TryFindImmediatePrototypeContactTarget(
                soldier,
                currentTarget,
                attackRange,
                out SoldierController immediateContactTarget))
        {
            currentTarget = immediateContactTarget;
            prototypeTargets[soldier] = currentTarget;
            soldier.SetCombatTarget(currentTarget);
            ClearPrototypeReserveBlockedState(soldier);
        }

        Vector3 toTarget = currentTarget.transform.position - soldier.transform.position;
        toTarget.y = 0f;

        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget <= 0.001f)
        {
            CompletePrototypeEngagementRunUp(soldier, currentTarget);
            soldier.Stop();
            return;
        }

        soldier.FaceToward(currentTarget.transform.position);

        // -------------------------------------------------------------------------
        // Active Soldier Logic
        // -------------------------------------------------------------------------
        // Active means this soldier is currently in its personal 1v1 / attack range
        // and can directly fight its assigned target. Later this can become a real
        // soldier combat state. For now, it is only a clear branch in the tick.
        if (IsPrototypeActiveSoldier(distanceToTarget, attackRange))
        {
            CompletePrototypeEngagementRunUp(soldier, currentTarget);
            ClearPrototypeReserveBlockedState(soldier);

            if (!isRangedWeapon)
                MarkPrototypeActiveAttackerCombatLockCandidate(soldier, currentTarget);
            else
                ClearPrototypeActiveAttackerCombatLockCandidate(soldier);

            TickPrototypeActiveSoldier(
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
        TickPrototypeReserveSoldier(
            soldier,
            currentTarget,
            isRangedWeapon,
            distanceToTarget,
            attackRange,
            stoppingDistance);
    }

    SoldierController RefreshPrototypeSoldierTargetIfNeeded(SoldierController soldier)
    {
        prototypeTargets.TryGetValue(
            soldier,
            out SoldierController currentTarget);

        bool shouldRefreshTarget =
            prototypeTargetRefreshTimers[soldier] <= 0f ||
            !IsValidPrototypeTarget(currentTarget);

        if (!shouldRefreshTarget)
            return currentTarget;

        prototypeTargetRefreshTimers[soldier] = Mathf.Max(
            0.01f,
            squadCombatProfile.prototypeTargetRefreshInterval);

        currentTarget = FindBestPrototypeTarget(soldier, currentTarget);

        prototypeTargets[soldier] = currentTarget;
        soldier.SetCombatTarget(currentTarget);

        return currentTarget;
    }

    bool IsPrototypeActiveSoldier(float distanceToTarget, float attackRange)
    {
        return distanceToTarget <= attackRange;
    }

    void TickPrototypeActiveSoldier(
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

        if (prototypeAttackTimers[soldier] > 0f)
            return;

        TryPrototypeAttack(
            soldier,
            currentTarget,
            weaponProfile,
            meleeStats,
            rangedStats,
            isRangedWeapon,
            attackInterval);
    }

    void TickPrototypeReserveSoldier(
        SoldierController soldier,
        SoldierController currentTarget,
        bool isRangedWeapon,
        float distanceToTarget,
        float attackRange,
        float stoppingDistance)
    {
        soldier.SetCombatRole(SoldierRole.Reserve);
        ClearPrototypeActiveAttackerCombatLockCandidate(soldier);

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

        bool useEngagementRunUp = ShouldUsePrototypeEngagementRunUp(
            soldier,
            currentTarget,
            isRangedWeapon,
            distanceToTarget,
            attackRange);

        SoldierContactSensor contactSensor = soldier.ContactSensor;

        if (contactSensor != null)
        {
            bool hasForwardGap = contactSensor.IsForwardFriendlyGapOpen(
                soldier,
                desiredMoveDirection,
                prototypeReserveForwardGapDistance,
                prototypeReserveForwardGapRadius);

            if (!hasForwardGap)
            {
                MarkPrototypeReserveBlocked(soldier);

                if (prototypeReserveBlockedSitTimers[soldier] > 0f)
                {
                    soldier.Stop();
                    return;
                }

                if (TryTickPrototypeReserveBehindFriendlyReposition(
                        soldier,
                        contactSensor,
                        currentTarget,
                        attackRange))
                {
                    return;
                }

                if (TryTickPrototypeReserveSideStep(
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

            if (IsPrototypeReserveStillSitting(soldier))
            {
                soldier.Stop();
                return;
            }
        }

        ClearPrototypeReserveBlockedState(soldier);

        float movementSpeedMultiplier = useEngagementRunUp
            ? prototypeEngagementRunUpSpeedMultiplier
            : squadCombatProfile.prototypeCombatMoveSpeedMultiplier;

        soldier.MoveToCombatPoint(
            moveDestination,
            stoppingDistance,
            movementSpeedMultiplier);
    }

    bool ShouldUsePrototypeEngagementRunUp(
        SoldierController soldier,
        SoldierController currentTarget,
        bool isRangedWeapon,
        float distanceToTarget,
        float attackRange)
    {
        if (!prototypeEngagementRunUpEnabled || isRangedWeapon)
        {
            ClearPrototypeEngagementRunUpState(soldier);
            return false;
        }

        if (soldier == null || !soldier.IsAlive ||
            currentTarget == null || !currentTarget.IsAlive)
        {
            ClearPrototypeEngagementRunUpState(soldier);
            return false;
        }

        if (soldier.IsMovementLocked || distanceToTarget <= attackRange)
        {
            CompletePrototypeEngagementRunUp(soldier, currentTarget);
            return false;
        }

        if (prototypeEngagementRunUpTargets.TryGetValue(
                soldier,
                out SoldierController trackedTarget))
        {
            if (trackedTarget != currentTarget)
            {
                ClearPrototypeEngagementRunUpState(soldier);
            }
            else
            {
                return prototypeEngagementRunUpTimers.TryGetValue(
                           soldier,
                           out float existingTimer) &&
                       existingTimer > 0f;
            }
        }

        if (distanceToTarget > prototypeEngagementRunUpStartDistance)
            return false;

        prototypeEngagementRunUpTargets[soldier] = currentTarget;
        prototypeEngagementRunUpTimers[soldier] = Mathf.Max(
            0.01f,
            prototypeEngagementRunUpMaximumDuration);

        return true;
    }

    void CompletePrototypeEngagementRunUp(
        SoldierController soldier,
        SoldierController currentTarget)
    {
        if (soldier == null)
            return;

        if (!prototypeEngagementRunUpEnabled ||
            currentTarget == null || !currentTarget.IsAlive)
        {
            ClearPrototypeEngagementRunUpState(soldier);
            return;
        }

        // Timer zero means this soldier has already spent its run-up against this
        // target. Retaining the pair prevents repeated speed bursts after contact.
        prototypeEngagementRunUpTargets[soldier] = currentTarget;
        prototypeEngagementRunUpTimers[soldier] = 0f;
    }

    void ClearPrototypeEngagementRunUpState(SoldierController soldier)
    {
        if (soldier == null)
            return;

        prototypeEngagementRunUpTimers.Remove(soldier);
        prototypeEngagementRunUpTargets.Remove(soldier);
    }

    public bool IsSoldierInEngagementRunUp(SoldierController soldier)
    {
        if (!prototypeEngagementRunUpEnabled ||
            soldier == null || !soldier.IsAlive ||
            soldier.IsMovementLocked)
        {
            return false;
        }

        if (!prototypeEngagementRunUpTimers.TryGetValue(
                soldier,
                out float runUpTimer) ||
            runUpTimer <= 0f)
        {
            return false;
        }

        return prototypeEngagementRunUpTargets.TryGetValue(
                   soldier,
                   out SoldierController runUpTarget) &&
               runUpTarget != null &&
               runUpTarget.IsAlive;
    }

    void MarkPrototypeActiveAttackerCombatLockCandidate(
        SoldierController soldier,
        SoldierController currentTarget)
    {
        if (!prototypeAttackerCombatLockEnabled)
            return;

        if (soldier == null || !soldier.IsAlive)
            return;

        if (currentTarget == null || !currentTarget.IsAlive)
            return;

        prototypeActiveAttackerCombatLockTargets[soldier] = currentTarget;
    }

    void ClearPrototypeActiveAttackerCombatLockCandidate(SoldierController soldier)
    {
        if (soldier == null)
            return;

        prototypeActiveAttackerCombatLockTargets.Remove(soldier);
    }

    public void BeginCombatLockedMoveOrder()
    {
        if (!prototypeAttackerCombatLockEnabled)
        {
            ClearTargets();
            return;
        }

        BuildPrototypeAttackerCombatLocksFromActiveAttackers();

        targetSquad = null;
        currentEngagementType = SquadEngagementReason.None;
        approachRefreshTimer = 0f;
        approachEngagementSettleTimer = 0f;

        ClearPrototypeRuntimeState(
            clearAttackTimers: false,
            clearCombatLocks: false);

        ClearSoldierCombatStates(preserveCombatLockedSoldiers: true);
    }

    void BuildPrototypeAttackerCombatLocksFromActiveAttackers()
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            if (!prototypeActiveAttackerCombatLockTargets.TryGetValue(
                    soldier,
                    out SoldierController lockTarget))
            {
                continue;
            }

            if (lockTarget == null || !lockTarget.IsAlive)
                continue;

            prototypeAttackerCombatLockTargets[soldier] = lockTarget;
            prototypeAttackerCombatLockTimers[soldier] = Random.Range(
                prototypeAttackerCombatLockTimeMin,
                prototypeAttackerCombatLockTimeMax);
        }
    }

    public void TickCombatLocks()
    {
        if (!prototypeAttackerCombatLockEnabled)
            return;

        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            TickPrototypeAttackerCombatLock(soldier);
        }
    }

    void TickPrototypeAttackerCombatLock(SoldierController soldier)
    {
        if (soldier == null)
            return;

        if (!prototypeAttackerCombatLockTimers.ContainsKey(soldier) &&
            !prototypeAttackerCombatLockTargets.ContainsKey(soldier))
        {
            return;
        }

        if (!IsSoldierCombatLocked(soldier))
        {
            ClearPrototypeAttackerCombatLock(soldier);
            return;
        }

        prototypeAttackerCombatLockTimers[soldier] -= Time.deltaTime;

        if (!IsSoldierCombatLocked(soldier))
        {
            ClearPrototypeAttackerCombatLock(soldier);
            return;
        }

        SoldierController lockTarget = prototypeAttackerCombatLockTargets[soldier];

        soldier.SetCombatRole(SoldierRole.Frontline);
        soldier.SetCombatTarget(lockTarget);
        soldier.Stop();
        soldier.FaceToward(lockTarget.transform.position);
    }

    public bool IsSoldierCombatLocked(SoldierController soldier)
    {
        if (!prototypeAttackerCombatLockEnabled)
            return false;

        if (soldier == null || !soldier.IsAlive)
            return false;

        if (!prototypeAttackerCombatLockTimers.TryGetValue(
                soldier,
                out float lockTimer) ||
            lockTimer <= 0f)
        {
            return false;
        }

        return prototypeAttackerCombatLockTargets.TryGetValue(
                   soldier,
                   out SoldierController lockTarget) &&
               lockTarget != null &&
               lockTarget.IsAlive;
    }

    void ClearPrototypeAttackerCombatLock(SoldierController soldier)
    {
        if (soldier == null)
            return;

        prototypeAttackerCombatLockTimers.Remove(soldier);
        prototypeAttackerCombatLockTargets.Remove(soldier);

        if (soldier.IsAlive)
        {
            soldier.SetCombatRole(SoldierRole.None);
            soldier.ClearCombatTarget();
        }
    }

    bool TryTickPrototypeReserveBehindFriendlyReposition(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        float attackRange)
    {
        if (!prototypeReserveBehindFriendlyRepositionEnabled)
            return false;

        if (soldier == null || contactSensor == null || currentTarget == null)
            return false;

        if (TryUseCachedPrototypeReserveBehindFriendlyPoint(
                soldier,
                contactSensor,
                currentTarget,
                attackRange))
        {
            return true;
        }

        if (prototypeReserveBehindFriendlySearchTimers.TryGetValue(
                soldier,
                out float searchTimer) &&
            searchTimer > 0f)
        {
            return false;
        }

        prototypeReserveBehindFriendlySearchTimers[soldier] =
            prototypeReserveBehindFriendlySearchInterval;

        if (!TryFindPrototypeReserveBehindFriendlyPoint(
                soldier,
                contactSensor,
                currentTarget,
                attackRange,
                out Vector3 reservePoint))
        {
            return false;
        }

        prototypeReserveBehindFriendlyDestinations[soldier] = reservePoint;

        soldier.MoveToCombatPoint(
            reservePoint,
            prototypeReserveBehindFriendlyReachDistance,
            prototypeReserveBehindFriendlySpeedMultiplier);

        return true;
    }

    bool TryUseCachedPrototypeReserveBehindFriendlyPoint(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        float attackRange)
    {
        if (!prototypeReserveBehindFriendlyDestinations.TryGetValue(
                soldier,
                out Vector3 reservePoint))
        {
            return false;
        }

        if (!IsPrototypeReserveBehindFriendlyPointStillUseful(
                soldier,
                contactSensor,
                currentTarget,
                reservePoint,
                attackRange))
        {
            prototypeReserveBehindFriendlyDestinations.Remove(soldier);
            return false;
        }

        if (!Calc.OutOfRange(
                soldier.transform.position,
                reservePoint,
                prototypeReserveBehindFriendlyReachDistance))
        {
            prototypeReserveBehindFriendlyDestinations.Remove(soldier);
            prototypeTargetRefreshTimers[soldier] = 0f;
            soldier.Stop();
            return true;
        }

        soldier.MoveToCombatPoint(
            reservePoint,
            prototypeReserveBehindFriendlyReachDistance,
            prototypeReserveBehindFriendlySpeedMultiplier);

        return true;
    }

    bool TryFindPrototypeReserveBehindFriendlyPoint(
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
            if (!IsValidPrototypeReserveBehindFriendlyAnchor(
                    soldier,
                    currentTarget,
                    friendly))
            {
                continue;
            }

            if (!TryEvaluatePrototypeReserveBehindFriendlyAnchor(
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

    bool IsValidPrototypeReserveBehindFriendlyAnchor(
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

        if (anchorDistance > prototypeReserveBehindFriendlyAnchorSearchRadius)
            return false;

        float soldierTargetDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(currentTarget.transform.position));

        float friendlyTargetDistance = Vector3.Distance(
            Flatten(friendly.transform.position),
            Flatten(currentTarget.transform.position));

        return friendlyTargetDistance <=
               soldierTargetDistance - prototypeReserveBehindFriendlyMinAnchorForwardGain;
    }

    bool TryEvaluatePrototypeReserveBehindFriendlyAnchor(
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
            awayFromTarget * prototypeReserveBehindFriendlyBackOffset;

        bool foundPoint = false;

        TryReplaceBestPrototypeReserveBehindFriendlyCandidate(
            soldier,
            contactSensor,
            currentTarget,
            centerPoint,
            attackRange,
            ref bestPoint,
            ref bestScore,
            ref foundPoint);

        if (prototypeReserveBehindFriendlySideOffset > 0f)
        {
            TryReplaceBestPrototypeReserveBehindFriendlyCandidate(
                soldier,
                contactSensor,
                currentTarget,
                centerPoint + side * prototypeReserveBehindFriendlySideOffset,
                attackRange,
                ref bestPoint,
                ref bestScore,
                ref foundPoint);

            TryReplaceBestPrototypeReserveBehindFriendlyCandidate(
                soldier,
                contactSensor,
                currentTarget,
                centerPoint - side * prototypeReserveBehindFriendlySideOffset,
                attackRange,
                ref bestPoint,
                ref bestScore,
                ref foundPoint);
        }

        return foundPoint;
    }

    void TryReplaceBestPrototypeReserveBehindFriendlyCandidate(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        Vector3 rawPoint,
        float attackRange,
        ref Vector3 bestPoint,
        ref float bestScore,
        ref bool foundPoint)
    {
        if (!TryScorePrototypeReserveBehindFriendlyCandidate(
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

    bool TryScorePrototypeReserveBehindFriendlyCandidate(
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
                prototypeReserveBehindFriendlyNavMeshProjectionRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        projectedPoint = navHit.position;

        float moveDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(projectedPoint));

        if (moveDistance > prototypeReserveBehindFriendlyMaxMoveDistance)
            return false;

        float currentTargetDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(currentTarget.transform.position));

        float candidateTargetDistance = Vector3.Distance(
            Flatten(projectedPoint),
            Flatten(currentTarget.transform.position));

        float targetProgress = currentTargetDistance - candidateTargetDistance;

        if (targetProgress < prototypeReserveBehindFriendlyMinTargetProgress)
            return false;

        // Do not step into attack range through a reserve reposition. Once the
        // soldier is that close, direct contact/attack logic should own behavior.
        float minimumEnemyDistance = Mathf.Max(0.1f, attackRange * 0.85f);

        if (candidateTargetDistance < minimumEnemyDistance)
            return false;

        if (contactSensor.IsPointOccupiedByLivingSoldier(
                soldier,
                projectedPoint,
                prototypeReserveBehindFriendlyOccupancyRadius))
        {
            return false;
        }

        int nearbyBodies = CountLivingSoldiersNearPrototypePoint(
            soldier,
            projectedPoint,
            prototypeReserveBehindFriendlyCrowdRadius);

        if (nearbyBodies > prototypeReserveBehindFriendlyMaxNearbyBodies)
            return false;

        if (!HasCompletePrototypeReserveBehindFriendlyPath(
                soldier.transform.position,
                projectedPoint))
        {
            return false;
        }

        score =
            moveDistance +
            nearbyBodies * prototypeReserveBehindFriendlyCrowdScoreWeight -
            targetProgress * prototypeReserveBehindFriendlyProgressScoreWeight;

        return true;
    }

    bool IsPrototypeReserveBehindFriendlyPointStillUseful(
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
                prototypeReserveBehindFriendlyOccupancyRadius))
        {
            return false;
        }

        int nearbyBodies = CountLivingSoldiersNearPrototypePoint(
            soldier,
            reservePoint,
            prototypeReserveBehindFriendlyCrowdRadius);

        return nearbyBodies <= prototypeReserveBehindFriendlyMaxNearbyBodies;
    }

    int CountLivingSoldiersNearPrototypePoint(
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

                count += CountLivingSoldiersNearPrototypePointFromRoster(
                    ignoredSoldier,
                    candidateSquad.Roster,
                    point,
                    radiusSqr);
            }

            return count;
        }

        count += CountLivingSoldiersNearPrototypePointFromRoster(
            ignoredSoldier,
            roster,
            point,
            radiusSqr);

        if (targetSquad != null)
        {
            count += CountLivingSoldiersNearPrototypePointFromRoster(
                ignoredSoldier,
                targetSquad.Roster,
                point,
                radiusSqr);
        }

        return count;
    }

    int CountLivingSoldiersNearPrototypePointFromRoster(
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

    bool HasCompletePrototypeReserveBehindFriendlyPath(
        Vector3 startPoint,
        Vector3 endPoint)
    {
        if (!NavMesh.CalculatePath(
                startPoint,
                endPoint,
                NavMesh.AllAreas,
                prototypeReserveBehindFriendlyPath))
        {
            return false;
        }

        return prototypeReserveBehindFriendlyPath.status == NavMeshPathStatus.PathComplete;
    }

    bool TryTickPrototypeReserveSideStep(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        Vector3 desiredMoveDirection,
        float stoppingDistance)
    {
        if (!prototypeReserveSideStepEnabled)
            return false;

        if (soldier == null || contactSensor == null)
            return false;

        if (prototypeReserveSideStepDestinations.TryGetValue(
                soldier,
                out Vector3 sideStepDestination))
        {
            if (!Calc.OutOfRange(
                    soldier.transform.position,
                    sideStepDestination,
                    0.18f))
            {
                prototypeReserveSideStepDestinations.Remove(soldier);
                prototypeTargetRefreshTimers[soldier] = 0f;
                return false;
            }

            soldier.MoveToCombatPoint(
                sideStepDestination,
                Mathf.Min(stoppingDistance, 0.12f),
                prototypeReserveSideStepSpeedMultiplier);

            return true;
        }

        if (prototypeReserveSideStepTimers[soldier] > 0f)
            return false;

        prototypeReserveSideStepTimers[soldier] = Random.Range(
            prototypeReserveSideStepIntervalMin,
            prototypeReserveSideStepIntervalMax);

        if (!TryFindPrototypeReserveSideStepPoint(
                soldier,
                contactSensor,
                desiredMoveDirection,
                out sideStepDestination))
        {
            return false;
        }

        prototypeReserveSideStepDestinations[soldier] = sideStepDestination;

        soldier.MoveToCombatPoint(
            sideStepDestination,
            Mathf.Min(stoppingDistance, 0.12f),
            prototypeReserveSideStepSpeedMultiplier);

        return true;
    }

    bool TryFindPrototypeReserveSideStepPoint(
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

        if (TryBuildPrototypeReserveSideStepPoint(
                soldier,
                contactSensor,
                firstSide,
                out sideStepPoint))
        {
            return true;
        }

        return hasSecondSide &&
               TryBuildPrototypeReserveSideStepPoint(
                   soldier,
                   contactSensor,
                   secondSide,
                   out sideStepPoint);
    }

    bool TryBuildPrototypeReserveSideStepPoint(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        Vector3 sideDirection,
        out Vector3 sideStepPoint)
    {
        sideStepPoint = soldier.transform.position;

        Vector3 rawPoint =
            soldier.transform.position +
            sideDirection.normalized * prototypeReserveSideStepDistance;

        if (!NavMesh.SamplePosition(
                rawPoint,
                out NavMeshHit navHit,
                prototypeReserveSideStepDistance,
                NavMesh.AllAreas))
        {
            return false;
        }

        if (contactSensor.IsPointOccupiedByLivingSoldier(
                soldier,
                navHit.position,
                prototypeReserveSideStepOccupancyRadius))
        {
            return false;
        }

        sideStepPoint = navHit.position;
        return true;
    }

    void MarkPrototypeReserveBlocked(SoldierController soldier)
    {
        if (soldier == null)
            return;

        if (!prototypeReserveBlockedSoldiers.Add(soldier))
            return;

        prototypeReserveBlockedSitTimers[soldier] = Random.Range(prototypeReserveMinimumBlockedSitTimeMin, prototypeReserveMinimumBlockedSitTimeMax); // chcek
        prototypeReserveSideStepDestinations.Remove(soldier);
        prototypeReserveBehindFriendlyDestinations.Remove(soldier);
    }

    bool IsPrototypeReserveStillSitting(SoldierController soldier)
    {
        return soldier != null &&
               prototypeReserveBlockedSoldiers.Contains(soldier) &&
               prototypeReserveBlockedSitTimers.TryGetValue(
                   soldier,
                   out float sitTimer) &&
               sitTimer > 0f;
    }

    void ClearPrototypeReserveBlockedState(SoldierController soldier)
    {
        if (soldier == null)
            return;

        prototypeReserveBlockedSoldiers.Remove(soldier);

        if (prototypeReserveBlockedSitTimers.ContainsKey(soldier))
            prototypeReserveBlockedSitTimers[soldier] = 0f;

        prototypeReserveSideStepDestinations.Remove(soldier);
        prototypeReserveBehindFriendlyDestinations.Remove(soldier);

        if (prototypeReserveBehindFriendlySearchTimers.ContainsKey(soldier))
            prototypeReserveBehindFriendlySearchTimers[soldier] = 0f;
    }

    void EnsurePrototypeTimers(SoldierController soldier)
    {
        if (!prototypeTargetRefreshTimers.ContainsKey(soldier))
            prototypeTargetRefreshTimers[soldier] = 0f;

        if (!prototypeAttackTimers.ContainsKey(soldier))
            prototypeAttackTimers[soldier] = 0f;

        if (!prototypeReserveSideStepTimers.ContainsKey(soldier))
        {
            prototypeReserveSideStepTimers[soldier] = Random.Range(
                prototypeReserveSideStepIntervalMin,
                prototypeReserveSideStepIntervalMax);
        }

        if (!prototypeReserveBlockedSitTimers.ContainsKey(soldier))
            prototypeReserveBlockedSitTimers[soldier] = 0f;

        if (!prototypeReserveBehindFriendlySearchTimers.ContainsKey(soldier))
            prototypeReserveBehindFriendlySearchTimers[soldier] = 0f;

    }

    void TickPrototypeTimers(SoldierController soldier)
    {
        prototypeTargetRefreshTimers[soldier] -= Time.deltaTime;
        prototypeAttackTimers[soldier] -= Time.deltaTime;
        prototypeReserveSideStepTimers[soldier] -= Time.deltaTime;
        prototypeReserveBlockedSitTimers[soldier] -= Time.deltaTime;
        prototypeReserveBehindFriendlySearchTimers[soldier] -= Time.deltaTime;

        if (prototypeEngagementRunUpTimers.TryGetValue(
                soldier,
                out float runUpTimer) &&
            runUpTimer > 0f)
        {
            prototypeEngagementRunUpTimers[soldier] = Mathf.Max(
                0f,
                runUpTimer - Time.deltaTime);
        }
    }

    bool TryFindImmediatePrototypeContactTarget(
        SoldierController soldier,
        SoldierController currentTarget,
        float attackRange,
        out SoldierController contactTarget)
    {
        contactTarget = null;

        if (!prototypeImmediateContactOverrideEnabled)
            return false;

        if (soldier == null || !soldier.IsAlive)
            return false;

        float contactRange = Mathf.Max(
            0.1f,
            attackRange + prototypeImmediateContactRangePadding);

        float bestDistanceSqr = contactRange * contactRange;

        if (prototypeMultiSquadLocalTargetingEnabled && SquadManager.Instance != null)
        {
            foreach (SquadController candidateSquad in SquadManager.Instance.Squads)
            {
                if (!CanAttack(candidateSquad))
                    continue;

                FindImmediatePrototypeContactTargetFromSquad(
                    soldier,
                    candidateSquad,
                    ref contactTarget,
                    ref bestDistanceSqr);
            }
        }
        else
        {
            FindImmediatePrototypeContactTargetFromSquad(
                soldier,
                targetSquad,
                ref contactTarget,
                ref bestDistanceSqr);
        }

        return contactTarget != null;
    }

    void FindImmediatePrototypeContactTargetFromSquad(
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
            if (!IsValidPrototypeTarget(enemy))
                continue;

            float distanceSqr = Vector3.SqrMagnitude(
                soldierPosition - Flatten(enemy.transform.position));

            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            contactTarget = enemy;
        }
    }

    SoldierController FindBestPrototypeTarget(
        SoldierController soldier,
        SoldierController currentTarget)
    {
        if (soldier == null || targetSquad == null || targetSquad.Roster == null)
            return null;

        SoldierController bestTarget = null;
        float bestScore = float.PositiveInfinity;

        if (prototypeMultiSquadLocalTargetingEnabled && SquadManager.Instance != null)
        {
            foreach (SquadController candidateSquad in SquadManager.Instance.Squads)
            {
                if (!CanAttack(candidateSquad))
                    continue;

                ScorePrototypeTargetsFromSquad(
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
            ScorePrototypeTargetsFromSquad(
                soldier,
                currentTarget,
                targetSquad,
                true,
                ref bestTarget,
                ref bestScore);
        }

        return bestTarget;
    }

    void ScorePrototypeTargetsFromSquad(
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
            if (!IsValidPrototypeTarget(enemy))
                continue;

            float distance = Vector3.Distance(
                Flatten(soldier.transform.position),
                Flatten(enemy.transform.position));

            // Non-primary enemies are local reactions only. This lets soldiers turn
            // into flankers without turning the whole squad into global free-chase.
            if (!isPrimaryTargetSquad && distance > prototypeLocalEnemyTargetSearchRadius)
                continue;

            int currentAttackers = CountPrototypeAttackers(enemy, soldier);

            float score =
                distance +
                currentAttackers * squadCombatProfile.prototypeTargetCrowdingPenalty;

            if (!isPrimaryTargetSquad)
                score += prototypeNonPrimaryTargetPenalty;

            if (enemy == currentTarget)
                score -= squadCombatProfile.prototypeCurrentTargetStickinessBonus;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }
    }

    int CountPrototypeAttackers(
        SoldierController target,
        SoldierController ignoredSoldier)
    {
        if (target == null)
            return 0;

        int count = 0;

        foreach (KeyValuePair<SoldierController, SoldierController> pair in prototypeTargets)
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

    bool IsValidPrototypeTarget(SoldierController target)
    {
        return target != null &&
               target.IsAlive &&
               target.Squad != null &&
               CanAttack(target.Squad);
    }

    void GetPrototypeAttackValues(
        WeaponProfile weaponProfile,
        bool isRangedWeapon,
        out MeleeCombatStats meleeStats,
        out RangedCombatStats rangedStats,
        out float attackRange,
        out float attackInterval,
        out float stoppingDistance)
    {
        meleeStats = weaponProfile != null
            ? weaponProfile.melee
            : MeleeCombatStats.Default;

        rangedStats = weaponProfile != null
            ? weaponProfile.ranged
            : RangedCombatStats.Default;

        if (isRangedWeapon)
        {
            attackRange = Mathf.Max(0.1f, rangedStats.attackRange);
            attackInterval = Mathf.Max(0.05f, rangedStats.attackInterval);
            stoppingDistance = Mathf.Max(
                0.05f,
                attackRange * squadCombatProfile.prototypeRangedStoppingDistanceMultiplier);
            return;
        }

        attackRange = Mathf.Max(
            0.1f,
            weaponProfile != null
                ? meleeStats.attackRange
                : squadCombatProfile.prototypeFallbackMeleeAttackRange);

        attackInterval = Mathf.Max(
            0.05f,
            weaponProfile != null
                ? meleeStats.attackInterval
                : squadCombatProfile.prototypeFallbackMeleeAttackInterval);

        stoppingDistance = Mathf.Max(
            0.05f,
            attackRange * squadCombatProfile.prototypeMeleeStoppingDistanceMultiplier);
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
            attackRange * squadCombatProfile.prototypeRangedPreferredDistanceMultiplier);

        return target.transform.position - directionToTarget * preferredDistance;
    }

    void TryPrototypeAttack(
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

        bool beganAttack = attacker.TryBeginAction(SoldierActionState.Attack);

        if (!beganAttack)
            return;

        prototypeAttackTimers[attacker] = Mathf.Max(0.05f, attackInterval);

        if (isRangedWeapon)
        {
            BeginPrototypeRangedAttack(
                attacker,
                target,
                weaponProfile,
                rangedStats);
            return;
        }

        ResolvePrototypeMeleeHit(
            attacker,
            target,
            meleeStats);
    }

    void ResolvePrototypeMeleeHit(
        SoldierController attacker,
        SoldierController target,
        MeleeCombatStats meleeStats)
    {
        if (attacker == null || target == null || target.Health == null)
            return;

        CombatDefenseStats defenderStats = target.Data != null
            ? target.Data.defense
            : CombatDefenseStats.Default;

        DamageResult damageResult = CombatResolver.ResolveMeleeHit(
            meleeStats,
            defenderStats);

        if (!damageResult.didHit)
            return;

        target.Health.TakeDamage(
            damageResult.normalDamage,
            damageResult.armorPiercingDamage);

        if (target.IsAlive)
            target.TryBeginAction(SoldierActionState.HitReact);
    }

    void BeginPrototypeRangedAttack(
        SoldierController attacker,
        SoldierController target,
        WeaponProfile weaponProfile,
        RangedCombatStats rangedStats)
    {
        if (attacker == null || target == null)
            return;

        if (weaponProfile != null && rangedStats.projectilePrefab != null)
        {
            prototypePendingProjectileTargets[attacker] = target;
            prototypePendingProjectileWeapons[attacker] = weaponProfile;
            return;
        }

        ResolvePrototypeRangedHit(
            attacker,
            target,
            rangedStats);
    }

    void ResolvePrototypeRangedHit(
        SoldierController attacker,
        SoldierController target,
        RangedCombatStats rangedStats)
    {
        if (attacker == null || target == null || target.Health == null)
            return;

        CombatDefenseStats defenderStats = target.Data != null
            ? target.Data.defense
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

        if (!prototypePendingProjectileTargets.TryGetValue(
                attacker,
                out SoldierController target))
        {
            return;
        }

        if (!prototypePendingProjectileWeapons.TryGetValue(
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

    /// Melee is currently resolved immediately when the attack begins.
    /// This hook exists so SoldierCombat can safely forward animation events.
    public void ResolveSoldierAttackImpact(SoldierController attacker)
    {
        // Intentionally empty for this PrototypeMelee base.
    }

    /// Called by SoldierCombat when the soldier action ends.
    public void HandleSoldierActionCompleted(
        SoldierController soldier,
        SoldierActionState completedAction)
    {
        if (completedAction == SoldierActionState.Attack)
            ClearPendingProjectile(soldier);
    }

    /// Called by SoldierCombat when an action is interrupted.
    public void HandleSoldierActionInterrupted(
        SoldierController soldier,
        SoldierActionState interruptedAction,
        SoldierActionState newAction)
    {
        if (interruptedAction == SoldierActionState.Attack)
            ClearPendingProjectile(soldier);
    }

    void ClearPendingProjectile(SoldierController soldier)
    {
        if (soldier == null)
            return;

        prototypePendingProjectileTargets.Remove(soldier);
        prototypePendingProjectileWeapons.Remove(soldier);
    }

    void ClearPrototypeRuntimeState(
        bool clearAttackTimers,
        bool clearCombatLocks = true)
    {
        prototypeTargets.Clear();
        prototypeTargetRefreshTimers.Clear();
        prototypeReserveSideStepTimers.Clear();
        prototypeReserveBlockedSitTimers.Clear();
        prototypeReserveBlockedSoldiers.Clear();
        prototypeReserveSideStepDestinations.Clear();
        prototypeReserveBehindFriendlySearchTimers.Clear();
        prototypeReserveBehindFriendlyDestinations.Clear();

        prototypeEngagementRunUpTimers.Clear();
        prototypeEngagementRunUpTargets.Clear();

        prototypeActiveAttackerCombatLockTargets.Clear();

        if (clearCombatLocks)
        {
            prototypeAttackerCombatLockTimers.Clear();
            prototypeAttackerCombatLockTargets.Clear();
        }

        prototypePendingProjectileTargets.Clear();
        prototypePendingProjectileWeapons.Clear();

        if (clearAttackTimers)
            prototypeAttackTimers.Clear();
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
        if (!prototypeApproachSettleGateEnabled)
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
        return approachEngagementSettleTimer < prototypeApproachSettleDuration;
    }

    bool HasEnoughSoldiersReadyForInitialEngagement(SquadController target)
    {
        if (roster == null || target == null || target.Roster == null)
            return true;

        int livingSoldiers = 0;
        int readySoldiers = 0;

        float readyRange = Mathf.Max(
            prototypeApproachSettleMinimumReadyRange,
            GetSquadWeaponAttackRange() + prototypeApproachSettleReadyRangePadding);

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
            Mathf.CeilToInt(livingSoldiers * prototypeApproachSettleReadyRatio),
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

    void BeginEngagement(bool notifyTarget)
    {
        if (targetSquad == null)
            return;

        movement.OrderStop();
        currentCombatStyle = ResolveCombatStyle();
        combatContactDirection = GetContactDirection();
        approachEngagementSettleTimer = 0f;

        ClearPrototypeRuntimeState(clearAttackTimers: false);

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
                prototypeAttackerCombatLockTargets.TryGetValue(
                    soldier,
                    out SoldierController lockTarget))
            {
                soldier.SetCombatRole(SoldierRole.Frontline);
                soldier.SetCombatTarget(lockTarget);
                soldier.Stop();
                soldier.FaceToward(lockTarget.transform.position);
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
        prototypeTargets.Clear();
        prototypeTargetRefreshTimers.Clear();

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
            return SquadCombatStyle.PrototypeMelee;

        if (data.defaultCombatStyle == SquadCombatStyle.RangedLine)
            return SquadCombatStyle.RangedLine;

        WeaponProfile weaponProfile = GetSquadWeaponProfile();

        if (weaponProfile != null && weaponProfile.weaponKind == WeaponKind.Ranged)
            return SquadCombatStyle.RangedLine;

        if (data.category == SquadCategory.Ranged)
            return SquadCombatStyle.RangedLine;

        return SquadCombatStyle.PrototypeMelee;
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
                ? squadCombatProfile.prototypeFallbackMeleeAttackRange
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
        return soldier != null && soldier.Data != null
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
