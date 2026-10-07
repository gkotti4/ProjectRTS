using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// -----------------------------------------------------------------------------
/// SquadCombat
/// -----------------------------------------------------------------------------
///
/// Squad-level combat coordinator.
///
/// Combat is modeled on two independent axes:
/// - SquadCombatStyle: Melee, Ranged, or Siege (what family owns the fight).
/// - SquadCombatExecutionMode: Formed, Loose, Skirmish, or Deployed (how that
///   family organizes itself spatially).
///
/// This partial MonoBehaviour remains the single runtime authority for target,
/// engagement, approach, charge, and combat-family/execution-mode state. Family
/// behavior is split across MeleeCombat.cs, RangedCombat.cs, and SiegeCombat.cs.
///
[DisallowMultipleComponent]
public partial class SquadCombat : MonoBehaviour
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
    private SquadCombatStyle currentCombatStyle = SquadCombatStyle.Melee;
    private SquadCombatExecutionMode currentCombatExecutionMode = SquadCombatExecutionMode.Formed;
    private SquadEngagementReason currentEngagementType = SquadEngagementReason.None;

    // -----------------------------------------------------------------------------
    // Runtime Timers
    // -----------------------------------------------------------------------------
    private float scanTimer = 0f;

    // A normal move order temporarily owns the squad over passive auto-targeting.
    // The grace timer is consumed only after the squad reaches Idle, so long move
    // orders do not lose their disengage window while traveling or reforming.
    private const float autoTargetMoveOrderGraceDuration = 0.50f;
    private bool autoTargetMoveOrderSuppressed = false;
    private float autoTargetMoveOrderGraceTimer = 0f;
    private float approachRefreshTimer = 0f;
    private float approachEngagementSettleTimer = 0f;
    // -----------------------------------------------------------------------------
    // Formation Charge Runtime State
    // -----------------------------------------------------------------------------
    private bool formationChargeEnabled = true;
    private bool formationChargeContactReached = false;
    private float formationChargeTimer = 0f;

    private Vector3 formationChargeLockedDirection = Vector3.forward;
    private Vector3 formationChargeFollowThroughStartCenter = Vector3.zero;
    private Vector3 formationChargeFollowThroughDestination = Vector3.zero;

    private sealed class FormationChargeSoldierRuntime
    {
        public float penetrationRemaining = 0f;
        public bool spent = false;

        public readonly HashSet<SoldierController> impactedEnemies =
            new HashSet<SoldierController>();
    }

    private readonly Dictionary<SoldierController, FormationChargeSoldierRuntime>
        formationChargeSoldierRuntime =
            new Dictionary<SoldierController, FormationChargeSoldierRuntime>();

    private readonly HashSet<SoldierController> formationChargeCurrentContacts =
        new HashSet<SoldierController>();

    private readonly HashSet<SoldierController> formationChargeImpactedTargets =
        new HashSet<SoldierController>();

    // FullCharge shock damage is applied at most once per enemy soldier per charge,
    // independently from per-rider penetration accounting.
    private readonly HashSet<SoldierController> formationChargeShockDamagedTargets =
        new HashSet<SoldierController>();

    // A contacted enemy squad is notified once when the charge physically reaches it.
    // This lets defenders enter combat/facing without converting their response into
    // an OrderedAttack or counter-charge.
    private readonly HashSet<SquadController> formationChargeNotifiedSquads =
        new HashSet<SquadController>();

    private readonly HashSet<SoldierController> formationChargeLeadSoldiers =
        new HashSet<SoldierController>();

    // Each soldier may perform at most one moving melee strike during one charge.
    // The strike reuses the normal AttackImpact/AttackEnd pipeline; only movement
    // locking differs while the charge is active.
    private readonly HashSet<SoldierController> formationChargeAttackStartedSoldiers =
        new HashSet<SoldierController>();

    private readonly List<SoldierController> formationChargeLeadCandidates =
        new List<SoldierController>();

    private bool hasLoggedMissingCombatProfile = false;

    // -----------------------------------------------------------------------------
    // Public Read-Only Access
    // -----------------------------------------------------------------------------
    public SquadController TargetSquad => targetSquad;
    public SquadCombatStyle CurrentCombatStyle => currentCombatStyle;
    public SquadCombatExecutionMode CurrentCombatExecutionMode => currentCombatExecutionMode;
    public SquadEngagementReason CurrentEngagementType => currentEngagementType;
    public bool FormationChargeEnabled => formationChargeEnabled;


    #endregion

    #region Initialization

    void Awake()
    {
        formedReserveBehindFriendlyPath = new NavMeshPath();
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
        ResetActiveCombatModeToAuthoredDefaults();
        currentEngagementType = SquadEngagementReason.None;
        rangedVolleyEnabled =
            squadCombatProfile != null &&
            squadCombatProfile.rangedVolleyEnabledByDefault;

        rangedAvoidanceEnabled =
            squadCombatProfile != null &&
            squadCombatProfile.rangedAvoidanceEnabledByDefault;

        formationChargeEnabled =
            squadCombatProfile != null &&
            squadCombatProfile.chargeEnabledByDefault;

        rangedAmmunitionStartingSoldierCount = CountLivingRangedSoldiers();
        InitializeRangedAmmunition();


        if (!HasCombatProfile())
            enabled = false;
    }


    public void SetFormationChargeEnabled(bool enabled)
    {
        formationChargeEnabled = enabled;

        // Turning charge off during the charge phase immediately returns the squad
        // to its normal ordered-attack approach rather than leaving stale charge state.
        if (!formationChargeEnabled &&
            squad != null &&
            squad.State == SquadState.Charging)
        {
            BeginApproachingCombat();
        }
    }

    public void ToggleFormationCharge()
    {
        SetFormationChargeEnabled(!formationChargeEnabled);
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

    /// Receives a direct ordered attack against a specific squad.
    public void OrderAttack(SquadController target)
    {
        // An explicit attack order supersedes any temporary suppression created by
        // a previous move order. Passive scans still respect that suppression.
        ClearMoveOrderAutoTargetSuppression();
        OrderAttack(target, SquadEngagementReason.OrderedAttack);
    }

    /// Starts an attack-like engagement from either an ordered attack or auto-scan.
    void OrderAttack(
        SquadController target,
        SquadEngagementReason engagementType)
    {
        if (!HasCombatProfile())
            return;

        if (!CanAttack(target))
            return;

        targetSquad = target;
        ResetActiveCombatModeToAuthoredDefaults();
        currentEngagementType = engagementType;
        approachRefreshTimer = 0f;

        ClearFormedCombatRuntimeState(clearAttackTimers: false);

        // Ordered melee attacks must get first chance to enter the charge state.
        // Normal engagement range can overlap the authored charge-start range, so
        // checking engagement first would bypass Charging and go straight to InCombat.
        if (ShouldUseFormationCharge() &&
            IsCloseEnoughToStartFormationCharge(targetSquad))
        {
            BeginFormationCharge();
            return;
        }

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
        ResetActiveCombatModeToAuthoredDefaults();
        currentEngagementType = squad != null && squad.Stance == SquadStance.Hold
            ? SquadEngagementReason.DefensiveHold
            : SquadEngagementReason.PassiveContact;

        ClearFormedCombatRuntimeState(clearAttackTimers: false);

        if (!IsCloseEnoughToStartEngagement(targetSquad))
            return;

        BeginEngagement(notifyTarget: false);
    }

    /// Clears squad-level and soldier-level combat state.
    public void ClearTargets()
    {
        targetSquad = null;
        ResetActiveCombatModeToAuthoredDefaults();
        currentEngagementType = SquadEngagementReason.None;
        rangedUsingMeleeFallback = false;
        rangedAvoidanceThreatSquad = null;
        approachRefreshTimer = 0f;
        approachEngagementSettleTimer = 0f;
        rangedInitialFireSettleTimer = 0f;
        rangedSetupRequired = false;
        rangedSetupInitialized = false;
        formationChargeContactReached = false;
        formationChargeTimer = 0f;
        formationChargeLockedDirection = Vector3.forward;
        formationChargeFollowThroughStartCenter = Vector3.zero;
        formationChargeFollowThroughDestination = Vector3.zero;
        formationChargeSoldierRuntime.Clear();
        formationChargeCurrentContacts.Clear();
        formationChargeImpactedTargets.Clear();
        formationChargeShockDamagedTargets.Clear();
        formationChargeNotifiedSquads.Clear();
        formationChargeLeadSoldiers.Clear();
        formationChargeLeadCandidates.Clear();
        formationChargeAttackStartedSoldiers.Clear();

        ClearFormedCombatRuntimeState(clearAttackTimers: true);
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

        // Ranged avoidance can trigger while the formation is still approaching,
        // so a close melee threat does not need to wait for InCombat first.
        if (TryBeginRangedAvoidance())
            return;

        movement.TickFormationFollow();

        // Ordered melee attacks must get first chance to transition into Charging.
        // The charge-start range is allowed to overlap (or exceed) normal engagement
        // range, so evaluating normal engagement first can make Charging unreachable.
        if (ShouldUseFormationCharge() &&
            IsCloseEnoughToStartFormationCharge(targetSquad))
        {
            BeginFormationCharge();
            return;
        }

        if (IsCloseEnoughToStartEngagement(targetSquad))
        {
            if (ShouldHoldInitialEngagementForApproachSettle(targetSquad))
                return;

            BeginEngagement(notifyTarget: true);
            return;
        }

        approachEngagementSettleTimer = 0f;
        TickCombatApproachRefresh();
    }

    /// Ticks the ordered melee charge phase.
    ///
    /// Charge is intentionally different from normal approach and contains two
    /// explicit internal behaviors:
    /// - RunUp: lightweight final rush, opening moving attack, settle on contact
    /// - FullCharge: momentum/shock/penetration/follow-through before normal melee
    /// Only an OrderedAttack may enter either behavior.
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

        switch (squadCombatProfile.chargeMode)
        {
            case ChargeMode.FullCharge:
                TickFormationFullCharge();
                break;

            case ChargeMode.RunUp:
            default:
                TickFormationRunUpCharge();
                break;
        }
    }

    /// <summary>
    /// Lightweight infantry-style charge. Soldiers get the authored charge movement
    /// speed and moving opening attack, but contact immediately hands ownership to
    /// normal melee. No penetration, charge-through, shock capsule, or follow-through.
    /// </summary>
    void TickFormationRunUpCharge()
    {
        formationChargeTimer -= Time.deltaTime;

        RefreshFormationChargeLeadSoldiers();
        TickFormationChargeAttacks();

        movement.TickFormationFollow(
            squadCombatProfile.chargeSpeedMultiplier,
            formationChargeLeadSoldiers,
            squadCombatProfile.chargeLeadSpeedMultiplier);

        if (HasFormationChargeReachedContactRatio(targetSquad))
        {
            formationChargeContactReached = true;
            ApplyFormationChargeMoraleShock();
            BeginEngagement(notifyTarget: true);
            return;
        }

        if (formationChargeTimer <= 0f)
        {
            BeginEngagement(notifyTarget: true);
            return;
        }

        TickCombatApproachRefresh();
    }

    /// <summary>
    /// Momentum-driven charge used by cavalry/monsters/chariots. This owns the
    /// physical contact capsule, shock damage, penetration, locked-direction
    /// follow-through, and spent-rider exit rules.
    /// </summary>
    void TickFormationFullCharge()
    {
        formationChargeTimer -= Time.deltaTime;

        RefreshFormationChargeLeadSoldiers();
        TickFormationChargeAttacks();

        movement.TickFormationFollow(
            squadCombatProfile.chargeSpeedMultiplier,
            formationChargeLeadSoldiers,
            squadCombatProfile.chargeLeadSpeedMultiplier);

        TickFormationChargeImpulseEmitters();

        if (!formationChargeContactReached &&
            HasFormationChargeReachedContactRatio(targetSquad))
        {
            formationChargeContactReached = true;
            LockFormationChargeFollowThrough();
            ApplyFormationChargeMoraleShock();

            // Once meaningful contact happens, target-relative steering ends. The
            // charge follows its locked entry vector so passing the enemy center can
            // never reverse the formation back toward the original target.
            approachRefreshTimer = 0f;
            MoveAlongFormationChargeFollowThrough();
        }

        if (formationChargeContactReached)
        {
            if (HasFormationChargeExhausted() ||
                HasFormationChargeReachedMaximumFollowThroughDistance() ||
                formationChargeTimer <= 0f)
            {
                BeginEngagement(notifyTarget: true);
                return;
            }

            TickCombatApproachRefresh();
            return;
        }

        // Safety cap for a charge that never achieves valid contact.
        if (formationChargeTimer <= 0f)
        {
            BeginEngagement(notifyTarget: true);
            return;
        }

        TickCombatApproachRefresh();
    }

    void ApplyFormationChargeMoraleShock()
    {
        if (targetSquad == null ||
            targetSquad.Morale == null ||
            squadCombatProfile.chargeMoraleShock <= 0f)
        {
            return;
        }

        targetSquad.Morale.ApplyMoraleLoss(
            squadCombatProfile.chargeMoraleShock);
    }

    void TickCombatApproachRefresh()
    {
        approachRefreshTimer -= Time.deltaTime;

        if (approachRefreshTimer > 0f)
            return;

        approachRefreshTimer = Mathf.Max(
            0.01f,
            squadCombatProfile.combatApproachRefreshInterval);

        if (squad != null &&
            squad.State == SquadState.Charging &&
            squadCombatProfile.chargeMode == ChargeMode.FullCharge &&
            formationChargeContactReached)
        {
            MoveAlongFormationChargeFollowThrough();
            return;
        }

        MoveTowardCombatTarget(
            chargeThroughTarget: squad != null && squad.State == SquadState.Charging);
    }

    /// Ticks active squad combat through the selected combat family.
    public void TickCombat()
    {
        switch (currentCombatStyle)
        {
            case SquadCombatStyle.Ranged:
                TickRangedCombat();
                return;

            case SquadCombatStyle.Siege:
                TickSiegeCombat();
                return;

            case SquadCombatStyle.Melee:
            default:
                TickMeleeCombat();
                return;
        }
    }

    #endregion

    #region Move Order Combat Ownership

    /// <summary>
    /// Gives an explicit normal move order temporary priority over passive auto-target
    /// scanning. Suppression lasts for the entire move/reform and then for a short
    /// grace period after the squad reaches Idle.
    /// </summary>
    public void BeginMoveOrderAutoTargetSuppression()
    {
        autoTargetMoveOrderSuppressed = true;
        autoTargetMoveOrderGraceTimer = autoTargetMoveOrderGraceDuration;
    }

    void ClearMoveOrderAutoTargetSuppression()
    {
        autoTargetMoveOrderSuppressed = false;
        autoTargetMoveOrderGraceTimer = 0f;
    }

    bool IsMoveOrderAutoTargetSuppressed()
    {
        if (!autoTargetMoveOrderSuppressed)
            return false;

        if (squad == null)
        {
            ClearMoveOrderAutoTargetSuppression();
            return false;
        }

        // TickIdleScan also runs while Reforming. Keep suppression fully active until
        // the normal move/reform pipeline has genuinely settled back to Idle.
        if (squad.State != SquadState.Idle)
            return true;

        autoTargetMoveOrderGraceTimer -= Time.deltaTime;

        if (autoTargetMoveOrderGraceTimer > 0f)
            return true;

        ClearMoveOrderAutoTargetSuppression();
        return false;
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

        if (IsAuthoredRangedCombat())
        {
            rangedSetupRequired = true;
            rangedSetupInitialized = false;
            rangedInitialFireSettleTimer =
                squadCombatProfile != null
                    ? squadCombatProfile.rangedInitialFireSettleTime
                    : 0f;
        }

        if (squad != null)
            squad.SetState(SquadState.ApproachingCombat);

        MoveTowardCombatTarget();
    }

    bool ShouldHoldInitialEngagementForApproachSettle(SquadController target)
    {
        if (!squadCombatProfile.approachSettleGateEnabled)
            return false;

        if (IsRangedCombat())
            return false;

        if (!CanAttack(target))
            return false;

        if (HasEnoughSoldiersReadyForInitialEngagement(target))
        {
            approachEngagementSettleTimer = 0f;
            return false;
        }

        approachEngagementSettleTimer += Time.deltaTime;
        return approachEngagementSettleTimer < squadCombatProfile.approachSettleDuration;
    }

    bool HasEnoughSoldiersReadyForInitialEngagement(SquadController target)
    {
        if (roster == null || target == null || target.Roster == null)
            return true;

        int livingSoldiers = 0;
        int readySoldiers = 0;

        float readyRange = Mathf.Max(
            squadCombatProfile.approachSettleMinimumReadyRange,
            GetSquadWeaponAttackRange() + squadCombatProfile.approachSettleReadyRangePadding);

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
            Mathf.CeilToInt(livingSoldiers * squadCombatProfile.approachSettleReadyRatio),
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
        return squadCombatProfile.chargeEnabled &&
               formationChargeEnabled &&
               currentEngagementType == SquadEngagementReason.OrderedAttack &&
               squad != null &&
               !IsRangedCombat();
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
            squadCombatProfile.chargeStartDistance);

        float minimumStartDistance = Mathf.Clamp(
            squadCombatProfile.chargeMinimumStartDistance,
            0f,
            chargeStartDistance);

        // Charging is a run-up window, not merely a maximum range. If the player
        // issues an attack while already inside the minimum run-up distance, normal
        // engagement wins instead of manufacturing a point-blank charge.
        return distanceSqr <= chargeStartDistance * chargeStartDistance &&
               distanceSqr >= minimumStartDistance * minimumStartDistance;
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
        formationChargeShockDamagedTargets.Clear();
        formationChargeNotifiedSquads.Clear();
        formationChargeCurrentContacts.Clear();
        formationChargeLeadSoldiers.Clear();
        formationChargeLeadCandidates.Clear();
        formationChargeAttackStartedSoldiers.Clear();
        formationChargeSoldierRuntime.Clear();
        formationChargeContactReached = false;
        formationChargeLockedDirection = Vector3.forward;
        formationChargeFollowThroughStartCenter = Vector3.zero;
        formationChargeFollowThroughDestination = Vector3.zero;
        formationChargeTimer = Mathf.Max(
            0.01f,
            squadCombatProfile.chargeMaximumDuration);

        if (squadCombatProfile.chargeMode == ChargeMode.FullCharge)
            InitializeFormationChargeSoldierRuntime();

        if (squad != null)
            squad.SetState(SquadState.Charging);

        MoveTowardCombatTarget(chargeThroughTarget: true);
    }

    void InitializeFormationChargeSoldierRuntime()
    {
        if (roster == null)
            return;

        float penetrationMultiplier = Mathf.Max(
            0f,
            squadCombatProfile.chargePenetrationMultiplier);

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null ||
                !soldier.IsAlive ||
                soldier.Motor == null ||
                IsRangedWeapon(GetWeaponProfile(soldier)))
            {
                continue;
            }

            float initialPenetration =
                soldier.Motor.BodyMass * penetrationMultiplier;

            formationChargeSoldierRuntime[soldier] =
                new FormationChargeSoldierRuntime
                {
                    penetrationRemaining = Mathf.Max(0f, initialPenetration),
                    spent = initialPenetration <= 0f
                };
        }
    }

    bool IsFormationChargeSoldierSpent(SoldierController soldier)
    {
        return soldier != null &&
               formationChargeSoldierRuntime.TryGetValue(
                   soldier,
                   out FormationChargeSoldierRuntime runtime) &&
               runtime.spent;
    }

    void LockFormationChargeFollowThrough()
    {
        Vector3 direction = Vector3.zero;

        if (roster != null)
        {
            foreach (SoldierController soldier in roster.Soldiers)
            {
                if (soldier == null ||
                    !soldier.IsAlive ||
                    soldier.Motor == null ||
                    IsRangedWeapon(GetWeaponProfile(soldier)))
                {
                    continue;
                }

                Vector3 velocity = soldier.Motor.Velocity;
                velocity.y = 0f;

                if (velocity.sqrMagnitude > 0.0001f)
                    direction += velocity.normalized;
            }
        }

        if (direction.sqrMagnitude <= 0.0001f && movement != null)
            direction = movement.DesiredFacing;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = combatContactDirection;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        formationChargeLockedDirection = direction.normalized;

        formationChargeFollowThroughStartCenter =
            TryGetLivingSoldierCenter(roster, out Vector3 resolvedCenter)
                ? resolvedCenter
                : transform.position;

        formationChargeFollowThroughDestination =
            formationChargeFollowThroughStartCenter +
            formationChargeLockedDirection *
            Mathf.Max(
                0f,
                squadCombatProfile.chargeFollowThroughMaximumDistance);
    }

    void MoveAlongFormationChargeFollowThrough()
    {
        if (movement == null)
            return;

        movement.OrderMove(
            formationChargeFollowThroughDestination,
            formationChargeLockedDirection);
    }

    bool HasFormationChargeReachedMaximumFollowThroughDistance()
    {
        float maximumDistance = Mathf.Max(
            0f,
            squadCombatProfile.chargeFollowThroughMaximumDistance);

        if (maximumDistance <= 0f)
            return true;

        Vector3 currentCenter =
            TryGetLivingSoldierCenter(roster, out Vector3 resolvedCenter)
                ? resolvedCenter
                : transform.position;

        Vector3 travel = currentCenter - formationChargeFollowThroughStartCenter;
        travel.y = 0f;

        float forwardDistance = Vector3.Dot(
            travel,
            formationChargeLockedDirection);

        return forwardDistance >= maximumDistance;
    }

    bool HasFormationChargeExhausted()
    {
        int activeChargers = 0;
        int spentChargers = 0;

        foreach (KeyValuePair<SoldierController, FormationChargeSoldierRuntime> pair
                 in formationChargeSoldierRuntime)
        {
            SoldierController soldier = pair.Key;

            if (soldier == null || !soldier.IsAlive)
                continue;

            activeChargers++;

            if (pair.Value.spent)
                spentChargers++;
        }

        if (activeChargers <= 0)
            return true;

        float spentRatio = spentChargers / (float)activeChargers;

        return spentRatio >= Mathf.Clamp01(
            squadCombatProfile.chargeEndSpentRatio);
    }

    void RegisterFormationChargeContact(
        SoldierController charger,
        SoldierController enemy)
    {
        if (charger == null || enemy == null || enemy.Motor == null)
            return;

        NotifySquadOfFormationChargeContact(enemy);
        TryApplyFullChargeShockDamage(charger, enemy);

        if (!formationChargeSoldierRuntime.TryGetValue(
                charger,
                out FormationChargeSoldierRuntime runtime))
        {
            return;
        }

        if (runtime.spent || !runtime.impactedEnemies.Add(enemy))
            return;

        Vector3 chargeDirection = ResolveFormationChargeImpactDirection(charger);
        float speedRatio = ResolveFullChargeSpeedRatio(charger, chargeDirection);
        float minimumSpeedRatio = Mathf.Clamp01(
            squadCombatProfile.fullChargeMinimumImpactSpeedRatio);

        float penetrationSpeedStrength = speedRatio < minimumSpeedRatio
            ? 0f
            : minimumSpeedRatio >= 0.999f
                ? 1f
                : Mathf.InverseLerp(minimumSpeedRatio, 1f, speedRatio);

        if (penetrationSpeedStrength <= 0f)
        {
            runtime.penetrationRemaining = 0f;
            runtime.spent = true;
            return;
        }

        // Lower-speed FullCharge contacts burn through the same mass budget faster;
        // a fully built charge uses the authored mass budget at full efficiency.
        float penetrationCost = enemy.Motor.BodyMass /
                                Mathf.Max(0.25f, penetrationSpeedStrength);

        runtime.penetrationRemaining -= penetrationCost;

        if (runtime.penetrationRemaining <= 0f)
        {
            runtime.penetrationRemaining = 0f;
            runtime.spent = true;
        }
    }

    void NotifySquadOfFormationChargeContact(SoldierController contactedEnemy)
    {
        if (contactedEnemy == null ||
            contactedEnemy.Squad == null ||
            contactedEnemy.Squad == squad ||
            contactedEnemy.Squad.Combat == null)
        {
            return;
        }

        SquadController contactedSquad = contactedEnemy.Squad;

        if (!formationChargeNotifiedSquads.Add(contactedSquad))
            return;

        // ReceiveEngagementRequest deliberately creates PassiveContact /
        // DefensiveHold rather than OrderedAttack, so being hit by a charge never
        // causes the defender to enter its own charge approach.
        contactedSquad.Combat.ReceiveEngagementRequest(squad);
    }

    void TryApplyFullChargeShockDamage(
        SoldierController charger,
        SoldierController enemy)
    {
        if (squadCombatProfile.chargeMode != ChargeMode.FullCharge ||
            charger == null ||
            charger.Motor == null ||
            enemy == null ||
            enemy.Health == null ||
            enemy.Motor == null ||
            !enemy.IsAlive)
        {
            return;
        }

        Vector3 chargeDirection = ResolveFormationChargeImpactDirection(charger);
        float impactStrength = ResolveFullChargeImpactStrength(
            charger,
            enemy,
            chargeDirection);

        if (impactStrength <= 0f)
            return;

        if (!formationChargeShockDamagedTargets.Add(enemy))
            return;

        int normalDamage = Mathf.RoundToInt(
            squadCombatProfile.fullChargeImpactDamage *
            impactStrength);

        int armorPiercingDamage = Mathf.RoundToInt(
            squadCombatProfile.fullChargeImpactArmorPiercingDamage *
            impactStrength);

        if (normalDamage <= 0 && armorPiercingDamage <= 0)
            return;

        int appliedDamage = enemy.Health.TakeDamage(
            normalDamage,
            armorPiercingDamage);

        if (appliedDamage > 0)
            GameEvents.CombatDamageDealt(charger, enemy, appliedDamage);

        if (enemy.IsAlive)
            enemy.TryBeginAction(SoldierActionState.HitReact);
    }

    Vector3 ResolveFormationChargeImpactDirection(SoldierController charger)
    {
        Vector3 direction = formationChargeContactReached
            ? formationChargeLockedDirection
            : charger != null && charger.Motor != null
                ? charger.Motor.Velocity
                : Vector3.zero;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = combatContactDirection;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f && charger != null)
            direction = charger.transform.forward;

        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.forward;
    }

    float ResolveFullChargeSpeedRatio(
        SoldierController charger,
        Vector3 chargeDirection)
    {
        if (charger == null || charger.Motor == null)
            return 0f;

        Vector3 velocity = charger.Motor.Velocity;
        velocity.y = 0f;

        float forwardSpeed = Mathf.Max(
            0f,
            Vector3.Dot(velocity, chargeDirection));

        float authoredChargeSpeed = Mathf.Max(
            0.01f,
            charger.Motor.BaseMoveSpeed *
            Mathf.Max(0.01f, squadCombatProfile.chargeSpeedMultiplier));

        return Mathf.Clamp01(forwardSpeed / authoredChargeSpeed);
    }

    float ResolveFullChargeImpactStrength(
        SoldierController charger,
        SoldierController enemy,
        Vector3 chargeDirection)
    {
        if (charger == null || charger.Motor == null ||
            enemy == null || enemy.Motor == null)
        {
            return 0f;
        }

        float speedRatio = ResolveFullChargeSpeedRatio(
            charger,
            chargeDirection);

        float minimumSpeedRatio = Mathf.Clamp01(
            squadCombatProfile.fullChargeMinimumImpactSpeedRatio);

        if (speedRatio < minimumSpeedRatio)
            return 0f;

        float speedStrength = minimumSpeedRatio >= 0.999f
            ? 1f
            : Mathf.InverseLerp(minimumSpeedRatio, 1f, speedRatio);

        float massRatio = charger.Motor.BodyMass /
                          Mathf.Max(0.01f, enemy.Motor.BodyMass);

        // Square-root mass scaling keeps body mass meaningful without allowing
        // extreme profiles to multiply shock damage uncontrollably.
        float massStrength = Mathf.Clamp(
            Mathf.Sqrt(Mathf.Max(0.01f, massRatio)),
            0.50f,
            1.50f);

        return speedStrength * massStrength;
    }

    void RefreshFormationChargeLeadSoldiers()
    {
        formationChargeLeadSoldiers.Clear();
        formationChargeLeadCandidates.Clear();

        if (!squadCombatProfile.chargeLeadSpeedEnabled ||
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

            if (IsRangedWeapon(GetWeaponProfile(soldier)) ||
                IsFormationChargeSoldierSpent(soldier))
            {
                continue;
            }

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
                squadCombatProfile.chargeLeadSoldierRatio),
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

    void TickFormationChargeAttacks()
    {
        if (roster == null || targetSquad == null || targetSquad.Roster == null)
            return;

        foreach (SoldierController attacker in roster.Soldiers)
        {
            if (attacker == null ||
                !attacker.IsAlive ||
                attacker.IsActionLocked ||
                IsFormationChargeSoldierSpent(attacker) ||
                formationChargeAttackStartedSoldiers.Contains(attacker))
            {
                continue;
            }

            WeaponProfile weaponProfile = GetWeaponProfile(attacker);

            if (IsRangedWeapon(weaponProfile))
                continue;

            GetCombatAttackValues(
                attacker,
                weaponProfile,
                false,
                out MeleeCombatStats meleeStats,
                out _,
                out float attackRange,
                out float attackInterval,
                out _);

            SoldierController target = FindClosestChargeTargetWithinRange(
                attacker,
                targetSquad.Roster,
                attackRange);

            if (target == null)
                continue;

            bool beganAttack = attacker.TryBeginAction(
                SoldierActionState.Attack,
                allowMovementDuringAttack: true);

            if (!beganAttack)
                continue;

            formationChargeAttackStartedSoldiers.Add(attacker);
            meleePendingTargets[attacker] = target;

            // Preserve normal melee cadence after the opening charge strike so the
            // soldier does not immediately chain a second normal attack on settle.
            float randomInterval = Random.Range(
                squadCombatProfile.attackIntervalRandomMin,
                squadCombatProfile.attackIntervalRandomMax);

            formedAttackTimers[attacker] = Mathf.Max(
                0.05f,
                attackInterval + randomInterval);
        }
    }

    SoldierController FindClosestChargeTargetWithinRange(
        SoldierController attacker,
        SquadRoster enemyRoster,
        float attackRange)
    {
        if (attacker == null || enemyRoster == null)
            return null;

        float bestDistanceSqr = Mathf.Max(0.1f, attackRange);
        bestDistanceSqr *= bestDistanceSqr;

        SoldierController bestTarget = null;
        Vector3 attackerPosition = Flatten(attacker.transform.position);

        foreach (SoldierController enemy in enemyRoster.Soldiers)
        {
            if (!IsValidFormedTarget(enemy))
                continue;

            float distanceSqr = Vector3.SqrMagnitude(
                Flatten(enemy.transform.position) - attackerPosition);

            if (distanceSqr > bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            bestTarget = enemy;
        }

        return bestTarget;
    }

    void TickFormationChargeImpulseEmitters()
    {
        if (roster == null || targetSquad == null)
            return;

        bool applyImpulse = squadCombatProfile.chargeImpulseEnabled;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive || soldier.Motor == null)
                continue;

            if (IsRangedWeapon(GetWeaponProfile(soldier)) ||
                IsFormationChargeSoldierSpent(soldier))
            {
                continue;
            }

            Vector3 chargeDirection =
                ResolveFormationChargeImpactDirection(soldier);

            Vector3 capsuleStart =
                soldier.transform.position +
                chargeDirection * 0.15f;

            Vector3 capsuleEnd =
                capsuleStart +
                chargeDirection * squadCombatProfile.chargeImpulseForwardDistance;

            formationChargeCurrentContacts.Clear();

            float chargeSpeedRatio = ResolveFullChargeSpeedRatio(
                soldier,
                chargeDirection);

            float minimumImpactSpeedRatio = Mathf.Clamp01(
                squadCombatProfile.fullChargeMinimumImpactSpeedRatio);

            float impulseSpeedStrength = chargeSpeedRatio < minimumImpactSpeedRatio
                ? 0f
                : minimumImpactSpeedRatio >= 0.999f
                    ? 1f
                    : Mathf.InverseLerp(
                        minimumImpactSpeedRatio,
                        1f,
                        chargeSpeedRatio);

            // FullCharge physical impulse is momentum-like: current charge-speed
            // buildup and body mass both contribute. Receiver mass is still handled
            // by SoldierMotor.ApplyExternalImpulse.
            float resolvedImpulseMagnitude =
                squadCombatProfile.chargeImpulseMagnitude *
                soldier.Motor.BodyMass *
                impulseSpeedStrength;

            ImpulseEmitter.EmitDirectionalCapsule(
                capsuleStart,
                capsuleEnd,
                squadCombatProfile.chargeImpulseRadius,
                chargeDirection,
                applyImpulse
                    ? resolvedImpulseMagnitude
                    : 0f,
                squadCombatProfile.chargeImpulseDuration,
                sourceSoldier: soldier,
                affectFriendlies: false,
                radialBlend: squadCombatProfile.chargeImpulseRadialBlend,
                minimumFalloff: 0.65f,
                excludedTargets: applyImpulse
                    ? formationChargeImpactedTargets
                    : null,
                affectedTargets: applyImpulse
                    ? formationChargeImpactedTargets
                    : null,
                contactedTargets: formationChargeCurrentContacts);

            foreach (SoldierController contactedEnemy in formationChargeCurrentContacts)
                RegisterFormationChargeContact(soldier, contactedEnemy);
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

            GetCombatAttackValues(
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
                livingMeleeSoldiers * squadCombatProfile.chargeContactReadyRatio),
            1,
            livingMeleeSoldiers);

        return contactReadySoldiers >= requiredContactSoldiers;
    }

    void BeginEngagement(bool notifyTarget)
    {
        if (targetSquad == null)
            return;

        movement.OrderStop();
        ResetActiveCombatModeToAuthoredDefaults();
        combatContactDirection = GetContactDirection();
        approachEngagementSettleTimer = 0f;

        if (IsRangedCombat())
        {
            // Entering a new ranged firing position is one of the few times the
            // formation is allowed to compact around current survivors. Once setup
            // completes, casualties leave holes until another deliberate formation
            // event (approach/reface/reform) occurs.
            formation?.Rebuild();
            rangedSetupRequired = true;
            rangedSetupInitialized = false;
            rangedInitialFireSettleTimer =
                squadCombatProfile.rangedInitialFireSettleTime;
        }

        ClearFormedCombatRuntimeState(clearAttackTimers: false);

        if (squad != null)
            squad.SetState(SquadState.InCombat);

        if (notifyTarget && targetSquad.Combat != null)
            targetSquad.Combat.ReceiveEngagementRequest(squad);
    }

    void MoveTowardCombatTarget(bool chargeThroughTarget = false)
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

        Vector3 facing = -fromTargetToMe;

        // Normal melee approach deliberately stops outside the enemy center. A
        // charge does the opposite: its temporary formation destination is placed
        // beyond the target center so body contact does not immediately erase the
        // squad's forward movement intent.
        Vector3 approachPoint;

        if (chargeThroughTarget && !IsRangedCombat())
        {
            approachPoint =
                targetCenter +
                facing * Mathf.Max(
                    0f,
                    squadCombatProfile.chargeFollowThroughMaximumDistance);
        }
        else
        {
            // Ranged approach has no preferred-distance repositioning. The formation
            // simply advances toward the enemy; TickApproachingCombat enters combat as
            // soon as the ranged combat-start range is satisfied. If already in range,
            // ranged squads never back away unless Ranged Avoidance explicitly does it.
            approachPoint = IsRangedCombat()
                ? targetCenter
                : targetCenter + fromTargetToMe * GetEffectiveApproachStopDistance();
        }

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
                meleeAttackerCombatLockTargets.TryGetValue(
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
            soldier.UseDefaultWeapon();
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

        if (target.Morale != null && target.Morale.HasRoutedOffField)
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
        if (squad != null && squad.Morale != null && squad.Morale.IsRoutingOrRouted)
            return false;

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

        if (IsMoveOrderAutoTargetSuppressed())
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
        formedTargets.Clear();
        formedTargetRefreshTimers.Clear();

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

    #region Range / Combat Mode Helpers

    void ResetActiveCombatModeToAuthoredDefaults()
    {
        currentCombatStyle = ResolveCombatStyle();
        currentCombatExecutionMode = ResolveCombatExecutionMode();
    }

    SquadCombatStyle ResolveCombatStyle()
    {
        return data != null
            ? data.defaultCombatStyle
            : SquadCombatStyle.Melee;
    }

    SquadCombatExecutionMode ResolveCombatExecutionMode()
    {
        return data != null
            ? data.defaultCombatExecutionMode
            : SquadCombatExecutionMode.Formed;
    }


    float GetEffectiveApproachStopDistance()
    {
        return Mathf.Max(0f, squadCombatProfile.defaultApproachStopDistance);
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


