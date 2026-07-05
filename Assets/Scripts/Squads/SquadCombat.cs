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

    // -----------------------------------------------------------------------------
    // Prototype Reserve MVP Tuning
    // -----------------------------------------------------------------------------
    // Temporary hardcoded values for the first blocked-reserve experiment.
    // If this feels good, these can move into SquadCombatProfile later.
    // NEW: Reserve Movement
    private const bool prototypeReserveEnabled = true;
    private const float prototypeReserveBlockedDelay = 1.0f;
    private const float prototypeReserveSearchInterval = 1.5f;
    private const float prototypeReserveAnchorSearchRadius = 8f;
    private const float prototypeReserveBackOffset = 1.0f;
    private const float prototypeReserveSideOffset = 1.0f;
    private const float prototypeReserveOccupancyRadius = 0.55f;
    private const float prototypeReserveNavMeshProjectionRadius = 0.65f;
    private const float prototypeReserveMinimumTargetGain = 0.35f;
    private const float prototypeReserveMaxMoveDistance = prototypeReserveAnchorSearchRadius + 0.5f; // was 4.5f
    private const float prototypeReserveProgressScoreWeight = 1.15f;

    // -----------------------------------------------------------------------------
    // Prototype Runtime State
    // -----------------------------------------------------------------------------
    private readonly Dictionary<SoldierController, SoldierController> prototypeTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, float> prototypeTargetRefreshTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> prototypeAttackTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> prototypeBlockedTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> prototypeReserveSearchTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, Vector3> prototypeReserveDestinations =
        new Dictionary<SoldierController, Vector3>();

    // NEW: Reserve Movement
    private NavMeshPath prototypeReservePath; // can't be readonly, navmesh can't be initialized in constructor - must initialize somewhere else.

    private readonly Dictionary<SoldierController, SoldierController> prototypePendingProjectileTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, WeaponProfile> prototypePendingProjectileWeapons =
        new Dictionary<SoldierController, WeaponProfile>();
    // End Reserve Movement

    private bool hasLoggedMissingCombatProfile = false;

    // -----------------------------------------------------------------------------
    // Public Read-Only Access
    // -----------------------------------------------------------------------------
    public SquadController TargetSquad => targetSquad;
    public SquadCombatStyle CurrentCombatStyle => currentCombatStyle;
    public SquadEngagementReason CurrentEngagementType => currentEngagementType;

    #endregion



    void Awake()
    {
        prototypeReservePath = new NavMeshPath(); // NEW: Reserve Movement
    }
    
    
    #region Initialization

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
            BeginEngagement(notifyTarget: true);
            return;
        }

        approachRefreshTimer -= Time.deltaTime;

        if (approachRefreshTimer > 0f)
            return;

        approachRefreshTimer = Mathf.Max(0.01f, squadCombatProfile.combatApproachRefreshInterval);

        MoveTowardCombatTarget();
    }

    /// Ticks active squad combat.
    public void TickCombat()
    {
        if (!HasCombatProfile())
            return;

        if (!CanAttack(targetSquad))
        {
            EndCombatAndReform();
            return;
        }

        if (!IsWithinCombatBreakRange(targetSquad))
        {
            EndCombatAndReform();
            return;
        }
        
        if (roster == null ||
            targetSquad == null ||
            targetSquad.Roster == null)
        {
            EndCombatAndReform();
            return;
        }
        
        // Ranged should go here ...
        
        
        TickPrototypeCombat();
        
        movement.SyncRootToLivingSoldierCenter(); // NEW: make squad virtually move with combat
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

        EnsurePrototypeTimers(soldier);
        TickPrototypeTimers(soldier);

        prototypeTargets.TryGetValue(
            soldier,
            out SoldierController currentTarget);

        bool shouldRefreshTarget =
            prototypeTargetRefreshTimers[soldier] <= 0f ||
            !IsValidPrototypeTarget(currentTarget);

        if (shouldRefreshTarget)
        {
            prototypeTargetRefreshTimers[soldier] = Mathf.Max(
                0.01f,
                squadCombatProfile.prototypeTargetRefreshInterval);

            currentTarget = FindBestPrototypeTarget(soldier, currentTarget);

            prototypeTargets[soldier] = currentTarget;
            soldier.SetCombatTarget(currentTarget);
        }

        if (currentTarget == null)
        {
            soldier.Stop();
            soldier.ClearCombatTarget();
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
            soldier.FaceToward(currentTarget.transform.position);
            return;
        }

        Vector3 toTarget = currentTarget.transform.position - soldier.transform.position;
        toTarget.y = 0f;

        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget <= 0.001f)
        {
            soldier.Stop();
            return;
        }

        soldier.FaceToward(currentTarget.transform.position);

        if (distanceToTarget <= attackRange)
        {
            soldier.Stop();

            if (prototypeAttackTimers[soldier] <= 0f)
            {
                TryPrototypeAttack(
                    soldier,
                    currentTarget,
                    weaponProfile,
                    meleeStats,
                    rangedStats,
                    isRangedWeapon,
                    attackInterval);
            }

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

        if (contactSensor != null &&
            contactSensor.TryGetForwardBlockingFriendly(
                soldier,
                desiredMoveDirection,
                out SoldierController blockingFriendly))
        {
            TickPrototypeBlockedByFriendly(
                soldier,
                currentTarget,
                blockingFriendly,
                attackRange,
                stoppingDistance);

            return;
        }

        ClearPrototypeReserveState(soldier);

        soldier.MoveToCombatPoint(
            moveDestination,
            stoppingDistance,
            squadCombatProfile.prototypeCombatMoveSpeedMultiplier);
    }

    void EnsurePrototypeTimers(SoldierController soldier)
    {
        if (!prototypeTargetRefreshTimers.ContainsKey(soldier))
            prototypeTargetRefreshTimers[soldier] = 0f;

        if (!prototypeAttackTimers.ContainsKey(soldier))
            prototypeAttackTimers[soldier] = 0f;

        if (!prototypeBlockedTimers.ContainsKey(soldier))
            prototypeBlockedTimers[soldier] = 0f;

        if (!prototypeReserveSearchTimers.ContainsKey(soldier))
            prototypeReserveSearchTimers[soldier] = 0f;
    }

    void TickPrototypeTimers(SoldierController soldier)
    {
        prototypeTargetRefreshTimers[soldier] -= Time.deltaTime;
        prototypeAttackTimers[soldier] -= Time.deltaTime;
        prototypeReserveSearchTimers[soldier] -= Time.deltaTime;
    }

    /// Handles the simple MVP reserve behavior for a soldier whose direct combat
    /// lane is blocked by another friendly body.
    ///
    /// The goal is not to rebuild formation combat. The soldier waits briefly,
    /// then periodically looks for a useful point behind a friendly that is already
    /// closer to the current enemy target.
    void TickPrototypeBlockedByFriendly(
        SoldierController soldier,
        SoldierController currentTarget,
        SoldierController blockingFriendly,
        float attackRange,
        float stoppingDistance)
    {
        if (soldier == null || currentTarget == null)
            return;

        if (!prototypeReserveEnabled)
        {
            soldier.Stop();
            return;
        }

        prototypeBlockedTimers[soldier] += Time.deltaTime;

        soldier.FaceToward(currentTarget.transform.position);

        if (prototypeBlockedTimers[soldier] < prototypeReserveBlockedDelay)
        {
            soldier.Stop();
            return;
        }

        if (TryUseCachedPrototypeReserveDestination(
                soldier,
                currentTarget,
                attackRange,
                stoppingDistance))
        {
            return;
        }

        if (prototypeReserveSearchTimers[soldier] > 0f)
        {
            soldier.Stop();
            return;
        }

        prototypeReserveSearchTimers[soldier] = prototypeReserveSearchInterval;

        if (TryFindPrototypeReservePointBehindFriendly(
                soldier,
                currentTarget,
                blockingFriendly,
                attackRange,
                out Vector3 reservePoint))
        {
            prototypeReserveDestinations[soldier] = reservePoint;

            soldier.MoveToCombatPoint(
                reservePoint,
                stoppingDistance,
                squadCombatProfile.prototypeCombatMoveSpeedMultiplier);

            return;
        }

        prototypeReserveDestinations.Remove(soldier);
        soldier.Stop();
    }

    bool TryUseCachedPrototypeReserveDestination(
        SoldierController soldier,
        SoldierController currentTarget,
        float attackRange,
        float stoppingDistance)
    {
        if (!prototypeReserveDestinations.TryGetValue(
                soldier,
                out Vector3 reservePoint))
        {
            return false;
        }

        if (!IsPrototypeReserveDestinationStillUseful(
                soldier,
                currentTarget,
                reservePoint,
                attackRange))
        {
            prototypeReserveDestinations.Remove(soldier);
            return false;
        }

        soldier.MoveToCombatPoint(
            reservePoint,
            stoppingDistance,
            squadCombatProfile.prototypeCombatMoveSpeedMultiplier);

        return true;
    }

    bool TryFindPrototypeReservePointBehindFriendly(
        SoldierController soldier,
        SoldierController currentTarget,
        SoldierController blockingFriendly,
        float attackRange,
        out Vector3 bestReservePoint)
    {
        bestReservePoint = Vector3.zero;

        if (soldier == null || currentTarget == null || roster == null)
            return false;

        float bestScore = float.PositiveInfinity;
        bool foundPoint = false;

        foreach (SoldierController friendly in roster.Soldiers)
        {
            if (!IsValidPrototypeReserveAnchor(
                    soldier,
                    currentTarget,
                    friendly,
                    blockingFriendly))
            {
                continue;
            }

            if (!TryEvaluatePrototypeReserveAnchor(
                    soldier,
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

    bool IsValidPrototypeReserveAnchor(
        SoldierController soldier,
        SoldierController currentTarget,
        SoldierController friendly,
        SoldierController blockingFriendly)
    {
        if (soldier == null || currentTarget == null || friendly == null)
            return false;

        if (friendly == soldier)
            return false;

        if (!friendly.IsAlive)
            return false;

        if (friendly.Squad != squad)
            return false;

        float soldierTargetDistance = PrototypeFlatDistance(
            soldier.transform.position,
            currentTarget.transform.position);

        float friendlyTargetDistance = PrototypeFlatDistance(
            friendly.transform.position,
            currentTarget.transform.position);

        bool isBlockingFriendly = friendly == blockingFriendly;

        if (!isBlockingFriendly)
        {
            float anchorDistance = PrototypeFlatDistance(
                soldier.transform.position,
                friendly.transform.position);

            if (anchorDistance > prototypeReserveAnchorSearchRadius)
                return false;
        }

        // Only stand behind friendlies that are already meaningfully ahead.
        return friendlyTargetDistance <
               soldierTargetDistance - prototypeReserveMinimumTargetGain;
    }

    bool TryEvaluatePrototypeReserveAnchor(
        SoldierController soldier,
        SoldierController currentTarget,
        SoldierController friendlyAnchor,
        float attackRange,
        out Vector3 bestPoint,
        out float bestScore)
    {
        bestPoint = Vector3.zero;
        bestScore = float.PositiveInfinity;

        Vector3 awayFromTarget =
            friendlyAnchor.transform.position - currentTarget.transform.position;

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
            awayFromTarget * prototypeReserveBackOffset;

        Vector3 leftPoint =
            centerPoint + side * prototypeReserveSideOffset;

        Vector3 rightPoint =
            centerPoint - side * prototypeReserveSideOffset;

        bool foundPoint = false;

        TryReplaceBestPrototypeReserveCandidate(
            soldier,
            currentTarget,
            centerPoint,
            attackRange,
            ref bestPoint,
            ref bestScore,
            ref foundPoint);

        TryReplaceBestPrototypeReserveCandidate(
            soldier,
            currentTarget,
            leftPoint,
            attackRange,
            ref bestPoint,
            ref bestScore,
            ref foundPoint);

        TryReplaceBestPrototypeReserveCandidate(
            soldier,
            currentTarget,
            rightPoint,
            attackRange,
            ref bestPoint,
            ref bestScore,
            ref foundPoint);

        return foundPoint;
    }

    void TryReplaceBestPrototypeReserveCandidate(
        SoldierController soldier,
        SoldierController currentTarget,
        Vector3 rawCandidatePoint,
        float attackRange,
        ref Vector3 bestPoint,
        ref float bestScore,
        ref bool foundPoint)
    {
        if (!TryScorePrototypeReserveCandidate(
                soldier,
                currentTarget,
                rawCandidatePoint,
                attackRange,
                out Vector3 projectedPoint,
                out float candidateScore))
        {
            return;
        }

        if (candidateScore >= bestScore)
            return;

        bestPoint = projectedPoint;
        bestScore = candidateScore;
        foundPoint = true;
    }

    bool TryScorePrototypeReserveCandidate(
        SoldierController soldier,
        SoldierController currentTarget,
        Vector3 rawCandidatePoint,
        float attackRange,
        out Vector3 projectedPoint,
        out float score)
    {
        projectedPoint = Vector3.zero;
        score = float.PositiveInfinity;

        if (soldier == null || currentTarget == null)
            return false;

        if (!TryProjectPrototypeReservePointToNavMesh(
                rawCandidatePoint,
                out projectedPoint))
        {
            return false;
        }

        float moveDistance = PrototypeFlatDistance(
            soldier.transform.position,
            projectedPoint);

        if (moveDistance > prototypeReserveMaxMoveDistance)
            return false;

        float currentTargetDistance = PrototypeFlatDistance(
            soldier.transform.position,
            currentTarget.transform.position);

        float candidateTargetDistance = PrototypeFlatDistance(
            projectedPoint,
            currentTarget.transform.position);

        float targetGain = currentTargetDistance - candidateTargetDistance;

        if (targetGain < prototypeReserveMinimumTargetGain)
            return false;

        // Do not reserve-step practically inside the enemy. If the candidate is
        // this close, normal attack logic should take over instead.
        float minimumEnemyDistance = Mathf.Max(
            0.1f,
            attackRange * 0.75f);

        if (candidateTargetDistance < minimumEnemyDistance)
            return false;

        SoldierContactSensor contactSensor = soldier.ContactSensor;

        if (contactSensor != null &&
            contactSensor.IsPointOccupiedByLivingSoldier(
                soldier,
                projectedPoint,
                prototypeReserveOccupancyRadius))
        {
            return false;
        }

        if (!HasCompletePrototypeReservePath(
                soldier.transform.position,
                projectedPoint))
        {
            return false;
        }

        // Lower is better: prefer short moves, but reward forward progress.
        score = moveDistance - targetGain * prototypeReserveProgressScoreWeight;
        return true;
    }

    bool IsPrototypeReserveDestinationStillUseful(
        SoldierController soldier,
        SoldierController currentTarget,
        Vector3 reservePoint,
        float attackRange)
    {
        if (soldier == null || currentTarget == null)
            return false;

        float currentTargetDistance = PrototypeFlatDistance(
            soldier.transform.position,
            currentTarget.transform.position);

        float reserveTargetDistance = PrototypeFlatDistance(
            reservePoint,
            currentTarget.transform.position);

        float targetGain = currentTargetDistance - reserveTargetDistance;

        if (targetGain < prototypeReserveMinimumTargetGain * 0.5f)
            return false;

        float minimumEnemyDistance = Mathf.Max(
            0.1f,
            attackRange * 0.75f);

        if (reserveTargetDistance < minimumEnemyDistance)
            return false;

        SoldierContactSensor contactSensor = soldier.ContactSensor;

        if (contactSensor != null &&
            contactSensor.IsPointOccupiedByLivingSoldier(
                soldier,
                reservePoint,
                prototypeReserveOccupancyRadius))
        {
            return false;
        }

        return true;
    }

    bool TryProjectPrototypeReservePointToNavMesh(
        Vector3 rawPoint,
        out Vector3 projectedPoint)
    {
        projectedPoint = rawPoint;

        if (!NavMesh.SamplePosition(
                rawPoint,
                out NavMeshHit navHit,
                prototypeReserveNavMeshProjectionRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        projectedPoint = navHit.position;
        return true;
    }

    bool HasCompletePrototypeReservePath(
        Vector3 startPoint,
        Vector3 endPoint)
    {
        if (!NavMesh.CalculatePath(
                startPoint,
                endPoint,
                NavMesh.AllAreas,
                prototypeReservePath))
        {
            return false;
        }

        return prototypeReservePath.status == NavMeshPathStatus.PathComplete;
    }

    void ClearPrototypeReserveState(SoldierController soldier)
    {
        if (soldier == null)
            return;

        prototypeBlockedTimers[soldier] = 0f;
        prototypeReserveSearchTimers[soldier] = 0f;
        prototypeReserveDestinations.Remove(soldier);
    }

    Vector3 PrototypeFlat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    float PrototypeFlatDistance(
        Vector3 a,
        Vector3 b)
    {
        return Vector3.Distance(
            PrototypeFlat(a),
            PrototypeFlat(b));
    }

    SoldierController FindBestPrototypeTarget(
        SoldierController soldier,
        SoldierController currentTarget)
    {
        if (soldier == null || targetSquad == null || targetSquad.Roster == null)
            return null;

        SoldierController bestTarget = null;
        float bestScore = float.PositiveInfinity;

        foreach (SoldierController enemy in targetSquad.Roster.Soldiers)
        {
            if (!IsValidPrototypeTarget(enemy))
                continue;

            float distance = Vector3.Distance(
                Flatten(soldier.transform.position),
                Flatten(enemy.transform.position));

            int currentAttackers = CountPrototypeAttackers(enemy, soldier);

            float score =
                distance +
                currentAttackers * squadCombatProfile.prototypeTargetCrowdingPenalty;

            if (enemy == currentTarget)
                score -= squadCombatProfile.prototypeCurrentTargetStickinessBonus;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }

        return bestTarget;
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
               target.Squad == targetSquad;
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

    void ClearPrototypeRuntimeState(bool clearAttackTimers)
    {
        prototypeTargets.Clear();
        prototypeTargetRefreshTimers.Clear();
        prototypeBlockedTimers.Clear();
        prototypeReserveSearchTimers.Clear();
        prototypeReserveDestinations.Clear();

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

        if (squad != null)
            squad.SetState(SquadState.ApproachingCombat);

        MoveTowardCombatTarget();
    }

    void BeginEngagement(bool notifyTarget)
    {
        if (targetSquad == null)
            return;

        movement.OrderStop();
        currentCombatStyle = ResolveCombatStyle();
        combatContactDirection = GetContactDirection();

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

        Vector3 fromTargetToMe = transform.position - targetSquad.transform.position;
        fromTargetToMe.y = 0f;

        if (fromTargetToMe.sqrMagnitude <= 0.0001f)
            fromTargetToMe = -(movement != null ? movement.DesiredFacing : transform.forward);

        fromTargetToMe.y = 0f;

        if (fromTargetToMe.sqrMagnitude <= 0.0001f)
            fromTargetToMe = -Vector3.forward;

        fromTargetToMe.Normalize();

        Vector3 approachPoint =
            targetSquad.transform.position +
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

    void ClearSoldierCombatStates()
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null)
                continue;

            soldier.SetCombatRole(SoldierRole.None);
            soldier.ClearCombatTarget();
            soldier.Combat?.ClearCombat();
            
            //soldier.CancelCurrentAction(notifyCombat: false); // DEBUG: Soldiers getting stuck/locked during combat (thought is that is is an action without release)

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

        return Vector3.Distance(
            transform.position,
            target.transform.position) <= GetEffectiveCombatStartRange();
    }

    bool IsWithinCombatBreakRange(SquadController target)
    {
        if (target == null)
            return false;

        return Vector3.Distance(
            transform.position,
            target.transform.position) <= GetEffectiveCombatBreakRange();
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
        bestTarget = null;

        if (SquadManager.Instance == null)
            return false;

        float range = GetEffectiveScanRange();

        if (range <= 0f)
            return false;

        float bestDistanceSqr = range * range;

        foreach (SquadController candidate in SquadManager.Instance.Squads)
        {
            if (!CanAttack(candidate))
                continue;

            float distanceSqr = Vector3.SqrMagnitude(
                candidate.transform.position - transform.position);

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTarget = candidate;
            }
        }

        return bestTarget != null;
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

        Vector3 direction = targetSquad.transform.position - transform.position;
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
