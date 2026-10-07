using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// -----------------------------------------------------------------------------
/// MeleeCombat
/// -----------------------------------------------------------------------------
///
/// Melee-family behavior plus the current shared Formed execution baseline.
///
/// This is intentionally a partial of the single SquadCombat MonoBehaviour rather
/// than a second component. Melee-specific impact/commitment behavior lives here,
/// while the existing formed soldier executor remains shared with Ranged combat.
/// Future Loose execution can branch from the family entry point without creating
/// another combat-family enum value.
///
public partial class SquadCombat
{
    #region Melee / Formed Combat Module

    // -----------------------------------------------------------------------------
    // Formed Execution Runtime State
    // -----------------------------------------------------------------------------
    private readonly Dictionary<SoldierController, SoldierController> formedTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, float> formedTargetRefreshTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> formedAttackTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> formedReserveSideStepTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> formedReserveBlockedSitTimers =
        new Dictionary<SoldierController, float>();

    private readonly HashSet<SoldierController> formedReserveBlockedSoldiers =
        new HashSet<SoldierController>();

    private readonly Dictionary<SoldierController, Vector3> formedReserveSideStepDestinations =
        new Dictionary<SoldierController, Vector3>();

    private readonly Dictionary<SoldierController, float> formedReserveBehindFriendlySearchTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, Vector3> formedReserveBehindFriendlyDestinations =
        new Dictionary<SoldierController, Vector3>();

    private readonly Dictionary<SoldierController, SoldierController> meleeActiveAttackerCombatLockTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, float> meleeAttackerCombatLockTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, SoldierController> meleeAttackerCombatLockTargets =
        new Dictionary<SoldierController, SoldierController>();

    private NavMeshPath formedReserveBehindFriendlyPath; // Must be initialized inside of Awake/Start, cannot be initialized in Constructor

    // Target committed when a melee attack begins.
    // The AttackImpact animation event consumes this target so target refreshes
    // during the animation cannot redirect the completed swing.
    private readonly Dictionary<SoldierController, SoldierController> meleePendingTargets =
        new Dictionary<SoldierController, SoldierController>();

    void TickMeleeCombat()
    {
        switch (currentCombatExecutionMode)
        {
            case SquadCombatExecutionMode.Formed:
                TickFormedCombat();
                return;

            // Loose is intentionally a real authored mode already, but its own
            // executor is not implemented yet. Preserve current gameplay by
            // falling back to the stable Formed path until that pass is built.
            case SquadCombatExecutionMode.Loose:
            default:
                TickFormedCombat();
                return;
        }
    }

    void TickFormedCombat()
    {
        if (!HasCombatProfile())
            return;

        if (roster == null ||
            targetSquad == null ||
            targetSquad.Roster == null)
        {
            EndCombatAndReform();
            return;
        }

        // Active combat can move soldiers independently of the virtual formation
        // anchor. Keep the squad root/banner locked to the living body center so
        // later movement/reform orders never inherit a stale combat anchor.
        movement?.SyncRootToLivingSoldierCenter();

        // Avoidance gets first refusal while the squad still has ammunition. This is
        // intentionally a simple one-step retreat, not full skirmisher AI.
        if (TryBeginRangedAvoidance())
            return;

        // Ranged/melee fallback is a squad decision, not a per-soldier decision.
        // Update it before range/break checks so the correct combat mode owns them.
        UpdateRangedCombatMode();

        if (!CanAttack(targetSquad))
        {
            if (!TrySwitchPrimaryCombatTarget())
            {
                EndCombatAndReform();
                return;
            }

            UpdateRangedCombatMode();
        }
        else if (!IsWithinCombatBreakRange(targetSquad))
        {
            // A direct ordered attack is persistent: if the living target opens a
            // large gap, return to formation approach instead of abandoning it.
            // Autonomous/passive engagements still respect the normal combat leash.
            if (currentEngagementType == SquadEngagementReason.OrderedAttack)
            {
                BeginApproachingCombat();
                return;
            }

            if (!TrySwitchPrimaryCombatTarget())
            {
                EndCombatAndReform();
                return;
            }

            UpdateRangedCombatMode();
        }

        // Ranged squads pursue their current squad target as a formation.
        // If the target leaves the normal ranged engagement distance, reuse the
        // existing approach/preferred-range behavior instead of letting individual
        // soldiers chase or waiting until the larger combat break range is exceeded.
        if (IsRangedCombat() &&
            !IsCloseEnoughToStartEngagement(targetSquad))
        {
            BeginApproachingCombat();
            return;
        }

        combatContactDirection = GetContactDirection();

        // Ranged combat is formation-owned. The whole squad turns and settles
        // before individual soldiers are allowed to fire.
        if (IsRangedCombat() &&
            !TickFormedRangedSetup())
        {
            return;
        }
        
        // Ranged Volley (or Synced Attack) MVP
        bool waitToAttack = false;
        bool shouldSynchronizeRangedVolley =
            rangedVolleyEnabled &&
            IsRangedCombat() &&
            !rangedUsingMeleeFallback;

        if (shouldSynchronizeRangedVolley &&
            !AreAllFormedAttackTimersReady())
        {
            waitToAttack = true;
        }
        
        foreach (SoldierController soldier in roster.Soldiers)
        {
            TickFormedCombatSoldier(soldier, waitToAttack);
        }
    }
    void TickFormedCombatSoldier(SoldierController soldier, bool waitToAttack = false)
    {
        if (soldier == null || !soldier.IsAlive)
            return;

        // -------------------------------------------------------------------------
        // Shared Soldier Combat Setup
        // -------------------------------------------------------------------------
        EnsureFormedCombatTimers(soldier);
        TickFormedCombatTimers(soldier);

        SoldierController currentTarget =
            RefreshFormedSoldierTargetIfNeeded(soldier);

        if (currentTarget == null)
        {
            soldier.Stop();
            soldier.SetCombatRole(SoldierRole.None);
            soldier.ClearCombatTarget();
            ClearFormedReserveBlockedState(soldier);
            return;
        }

        WeaponProfile weaponProfile = GetWeaponProfile(soldier);

        if (weaponProfile == null)
        {
            soldier.Stop();
            soldier.SetCombatRole(SoldierRole.None);
            return;
        }

        bool isRangedWeapon = IsRangedWeapon(weaponProfile);

        GetCombatAttackValues(
            soldier,
            weaponProfile,
            isRangedWeapon,
            out MeleeCombatStats meleeStats,
            out RangedCombatStats rangedStats,
            out float attackRange,
            out float attackInterval,
            out float stoppingDistance);

        if (isRangedWeapon)
        {
            TryCancelInvalidPendingRangedAttack(
                soldier,
                rangedStats);
        }

        if (soldier.IsMovementLocked)
        {
            soldier.FaceToward(currentTarget.transform.position);
            return;
        }

        if (!isRangedWeapon &&
            TryFindImmediateMeleeContactTarget(
                soldier,
                currentTarget,
                attackRange,
                out SoldierController immediateContactTarget))
        {
            currentTarget = immediateContactTarget;
            formedTargets[soldier] = currentTarget;
            soldier.SetCombatTarget(currentTarget);
            ClearFormedReserveBlockedState(soldier);
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
        if (IsFormedActiveSoldier(distanceToTarget, attackRange))
        {
            ClearFormedReserveBlockedState(soldier);

            if (!isRangedWeapon)
                MarkMeleeActiveAttackerCombatLockCandidate(soldier, currentTarget);
            else
                ClearMeleeActiveAttackerCombatLockCandidate(soldier);

            // Ranged Volley (Attack Sync) MVP
            if (waitToAttack)
                return;
            
            TickFormedActiveSoldier(
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
        TickFormedReserveSoldier(
            soldier,
            currentTarget,
            isRangedWeapon,
            attackRange,
            stoppingDistance);
    }
    SoldierController RefreshFormedSoldierTargetIfNeeded(SoldierController soldier)
    {
        formedTargets.TryGetValue(
            soldier,
            out SoldierController currentTarget);

        bool shouldRefreshTarget =
            formedTargetRefreshTimers[soldier] <= 0f ||
            !IsValidFormedTarget(currentTarget);

        if (!shouldRefreshTarget)
            return currentTarget;

        formedTargetRefreshTimers[soldier] = Mathf.Max(
            0.01f,
            squadCombatProfile.meleeTargetRefreshInterval);

        currentTarget = FindBestFormedTarget(soldier, currentTarget);

        formedTargets[soldier] = currentTarget;
        soldier.SetCombatTarget(currentTarget);

        return currentTarget;
    }
    bool IsFormedActiveSoldier(float distanceToTarget, float attackRange)
    {
        return distanceToTarget <= attackRange;
    }
    void TickFormedActiveSoldier(
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

        if (formedAttackTimers[soldier] > 0f)
            return;

        TryFormedAttack(
            soldier,
            currentTarget,
            weaponProfile,
            meleeStats,
            rangedStats,
            isRangedWeapon,
            attackInterval);
    }
    void TickFormedReserveSoldier(
        SoldierController soldier,
        SoldierController currentTarget,
        bool isRangedWeapon,
        float attackRange,
        float stoppingDistance)
    {
        soldier.SetCombatRole(SoldierRole.Reserve);
        ClearMeleeActiveAttackerCombatLockCandidate(soldier);

        if (currentTarget == null)
        {
            soldier.Stop();
            soldier.ClearCombatTarget();
            return;
        }

        Vector3 moveDestination;

        if (isRangedWeapon &&
            formation != null &&
            formation.TryGetSlotForSoldier(soldier, out Vector3 rangedFormationSlot))
        {
            // Ranged soldiers hold the squad formation instead of individually
            // chasing their personal targets.
            moveDestination = rangedFormationSlot;
        }
        else
        {
            moveDestination = currentTarget.transform.position;
        }

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
                squadCombatProfile.reserveForwardGapDistance,
                squadCombatProfile.reserveForwardGapRadius);

            if (!hasForwardGap)
            {
                MarkFormedReserveBlocked(soldier);

                if (formedReserveBlockedSitTimers[soldier] > 0f)
                {
                    soldier.Stop();
                    return;
                }

                if (TryTickFormedReserveBehindFriendlyReposition(
                        soldier,
                        contactSensor,
                        currentTarget,
                        attackRange))
                {
                    return;
                }

                if (TryTickFormedReserveSideStep(
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

            if (IsFormedReserveStillSitting(soldier))
            {
                soldier.Stop();
                return;
            }
        }

        ClearFormedReserveBlockedState(soldier);

        soldier.MoveToCombatPoint(
            moveDestination,
            stoppingDistance,
            squadCombatProfile.meleeCombatMoveSpeedMultiplier);
    }
    void MarkMeleeActiveAttackerCombatLockCandidate(
        SoldierController soldier,
        SoldierController currentTarget)
    {
        if (!squadCombatProfile.attackerCombatLockEnabled)
            return;

        if (soldier == null || !soldier.IsAlive)
            return;

        if (currentTarget == null || !currentTarget.IsAlive)
            return;

        meleeActiveAttackerCombatLockTargets[soldier] = currentTarget;
    }
    void ClearMeleeActiveAttackerCombatLockCandidate(SoldierController soldier)
    {
        if (soldier == null)
            return;

        meleeActiveAttackerCombatLockTargets.Remove(soldier);
    }
    public void BeginCombatLockedMoveOrder()
    {
        if (!squadCombatProfile.attackerCombatLockEnabled)
        {
            ClearTargets();
            return;
        }

        BuildMeleeAttackerCombatLocksFromActiveAttackers();

        targetSquad = null;
        currentEngagementType = SquadEngagementReason.None;
        approachRefreshTimer = 0f;
        approachEngagementSettleTimer = 0f;
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

        ClearFormedCombatRuntimeState(
            clearAttackTimers: false,
            clearCombatLocks: false);

        ClearSoldierCombatStates(preserveCombatLockedSoldiers: true);
    }
    void BuildMeleeAttackerCombatLocksFromActiveAttackers()
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            if (!meleeActiveAttackerCombatLockTargets.TryGetValue(
                    soldier,
                    out SoldierController lockTarget))
            {
                continue;
            }

            if (lockTarget == null || !lockTarget.IsAlive)
                continue;

            meleeAttackerCombatLockTargets[soldier] = lockTarget;
            meleeAttackerCombatLockTimers[soldier] = Random.Range(
                squadCombatProfile.attackerCombatLockTimeMin,
                squadCombatProfile.attackerCombatLockTimeMax);
        }
    }
    public void TickCombatLocks()
    {
        if (!squadCombatProfile.attackerCombatLockEnabled)
            return;

        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            TickMeleeAttackerCombatLock(soldier);
        }
    }
    void TickMeleeAttackerCombatLock(SoldierController soldier)
    {
        if (soldier == null)
            return;

        if (!meleeAttackerCombatLockTimers.ContainsKey(soldier) &&
            !meleeAttackerCombatLockTargets.ContainsKey(soldier))
        {
            return;
        }

        if (!IsSoldierCombatLocked(soldier))
        {
            ClearMeleeAttackerCombatLock(soldier);
            return;
        }

        meleeAttackerCombatLockTimers[soldier] -= Time.deltaTime;

        if (!IsSoldierCombatLocked(soldier))
        {
            ClearMeleeAttackerCombatLock(soldier);
            return;
        }

        SoldierController lockTarget = meleeAttackerCombatLockTargets[soldier];

        soldier.SetCombatRole(SoldierRole.Frontline);
        soldier.SetCombatTarget(lockTarget);
        soldier.Stop();
        soldier.FaceToward(lockTarget.transform.position, soldier.Stats != null ? soldier.Stats.movement.turnSpeed : soldier.Data.movement.turnSpeed);
    }
    public bool IsSoldierCombatLocked(SoldierController soldier)
    {
        if (!squadCombatProfile.attackerCombatLockEnabled)
            return false;

        if (soldier == null || !soldier.IsAlive) // PERFORMANCE
            return false;

        if (!meleeAttackerCombatLockTimers.TryGetValue(
                soldier,
                out float lockTimer) ||
            lockTimer <= 0f)
        {
            return false;
        }

        return meleeAttackerCombatLockTargets.TryGetValue(
                   soldier,
                   out SoldierController lockTarget) &&
               lockTarget != null &&
               lockTarget.IsAlive; // PERFORMANCE
    }
    void ClearMeleeAttackerCombatLock(SoldierController soldier)
    {
        if (soldier == null)
            return;

        meleeAttackerCombatLockTimers.Remove(soldier);
        meleeAttackerCombatLockTargets.Remove(soldier);

        if (soldier.IsAlive)
        {
            soldier.SetCombatRole(SoldierRole.None);
            soldier.ClearCombatTarget();
        }
    }
    bool TryTickFormedReserveBehindFriendlyReposition(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        float attackRange)
    {
        if (!squadCombatProfile.reserveBehindFriendlyRepositionEnabled)
            return false;

        if (soldier == null || contactSensor == null || currentTarget == null)
            return false;

        if (TryUseCachedFormedReserveBehindFriendlyPoint(
                soldier,
                contactSensor,
                currentTarget,
                attackRange))
        {
            return true;
        }

        if (formedReserveBehindFriendlySearchTimers.TryGetValue(
                soldier,
                out float searchTimer) &&
            searchTimer > 0f)
        {
            return false;
        }

        formedReserveBehindFriendlySearchTimers[soldier] =
            squadCombatProfile.reserveBehindFriendlySearchInterval;

        if (!TryFindFormedReserveBehindFriendlyPoint(
                soldier,
                contactSensor,
                currentTarget,
                attackRange,
                out Vector3 reservePoint))
        {
            return false;
        }

        formedReserveBehindFriendlyDestinations[soldier] = reservePoint;

        soldier.MoveToCombatPoint(
            reservePoint,
            squadCombatProfile.reserveBehindFriendlyReachDistance,
            squadCombatProfile.reserveBehindFriendlySpeedMultiplier);

        return true;
    }
    bool TryUseCachedFormedReserveBehindFriendlyPoint(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        float attackRange)
    {
        if (!formedReserveBehindFriendlyDestinations.TryGetValue(
                soldier,
                out Vector3 reservePoint))
        {
            return false;
        }

        if (!IsFormedReserveBehindFriendlyPointStillUseful(
                soldier,
                contactSensor,
                currentTarget,
                reservePoint,
                attackRange))
        {
            formedReserveBehindFriendlyDestinations.Remove(soldier);
            return false;
        }

        if (!Calc.OutOfRange(
                soldier.transform.position,
                reservePoint,
                squadCombatProfile.reserveBehindFriendlyReachDistance))
        {
            formedReserveBehindFriendlyDestinations.Remove(soldier);
            formedTargetRefreshTimers[soldier] = 0f;
            soldier.Stop();
            return true;
        }

        soldier.MoveToCombatPoint(
            reservePoint,
            squadCombatProfile.reserveBehindFriendlyReachDistance,
            squadCombatProfile.reserveBehindFriendlySpeedMultiplier);

        return true;
    }
    bool TryFindFormedReserveBehindFriendlyPoint(
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
            if (!IsValidFormedReserveBehindFriendlyAnchor(
                    soldier,
                    currentTarget,
                    friendly))
            {
                continue;
            }

            if (!TryEvaluateFormedReserveBehindFriendlyAnchor(
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
    bool IsValidFormedReserveBehindFriendlyAnchor(
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

        if (anchorDistance > squadCombatProfile.reserveBehindFriendlyAnchorSearchRadius)
            return false;

        float soldierTargetDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(currentTarget.transform.position));

        float friendlyTargetDistance = Vector3.Distance(
            Flatten(friendly.transform.position),
            Flatten(currentTarget.transform.position));

        return friendlyTargetDistance <=
               soldierTargetDistance - squadCombatProfile.reserveBehindFriendlyMinAnchorForwardGain;
    }
    bool TryEvaluateFormedReserveBehindFriendlyAnchor(
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
            awayFromTarget * squadCombatProfile.reserveBehindFriendlyBackOffset;

        bool foundPoint = false;

        TryReplaceBestFormedReserveBehindFriendlyCandidate(
            soldier,
            contactSensor,
            currentTarget,
            centerPoint,
            attackRange,
            ref bestPoint,
            ref bestScore,
            ref foundPoint);

        if (squadCombatProfile.reserveBehindFriendlySideOffset > 0f)
        {
            TryReplaceBestFormedReserveBehindFriendlyCandidate(
                soldier,
                contactSensor,
                currentTarget,
                centerPoint + side * squadCombatProfile.reserveBehindFriendlySideOffset,
                attackRange,
                ref bestPoint,
                ref bestScore,
                ref foundPoint);

            TryReplaceBestFormedReserveBehindFriendlyCandidate(
                soldier,
                contactSensor,
                currentTarget,
                centerPoint - side * squadCombatProfile.reserveBehindFriendlySideOffset,
                attackRange,
                ref bestPoint,
                ref bestScore,
                ref foundPoint);
        }

        return foundPoint;
    }
    void TryReplaceBestFormedReserveBehindFriendlyCandidate(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        SoldierController currentTarget,
        Vector3 rawPoint,
        float attackRange,
        ref Vector3 bestPoint,
        ref float bestScore,
        ref bool foundPoint)
    {
        if (!TryScoreFormedReserveBehindFriendlyCandidate(
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
    bool TryScoreFormedReserveBehindFriendlyCandidate(
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
                squadCombatProfile.reserveBehindFriendlyNavMeshProjectionRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        projectedPoint = navHit.position;

        float moveDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(projectedPoint));

        if (moveDistance > squadCombatProfile.reserveBehindFriendlyMaxMoveDistance)
            return false;

        float currentTargetDistance = Vector3.Distance(
            Flatten(soldier.transform.position),
            Flatten(currentTarget.transform.position));

        float candidateTargetDistance = Vector3.Distance(
            Flatten(projectedPoint),
            Flatten(currentTarget.transform.position));

        float targetProgress = currentTargetDistance - candidateTargetDistance;

        if (targetProgress < squadCombatProfile.reserveBehindFriendlyMinTargetProgress)
            return false;

        // Do not step into attack range through a reserve reposition. Once the
        // soldier is that close, direct contact/attack logic should own behavior.
        float minimumEnemyDistance = Mathf.Max(0.1f, attackRange * 0.85f);

        if (candidateTargetDistance < minimumEnemyDistance)
            return false;

        if (contactSensor.IsPointOccupiedByLivingSoldier(
                soldier,
                projectedPoint,
                squadCombatProfile.reserveBehindFriendlyOccupancyRadius))
        {
            return false;
        }

        int nearbyBodies = CountLivingSoldiersNearFormedPoint(
            soldier,
            projectedPoint,
            squadCombatProfile.reserveBehindFriendlyCrowdRadius);

        if (nearbyBodies > squadCombatProfile.reserveBehindFriendlyMaxNearbyBodies)
            return false;

        if (!HasCompleteFormedReserveBehindFriendlyPath(
                soldier.transform.position,
                projectedPoint))
        {
            return false;
        }

        score =
            moveDistance +
            nearbyBodies * squadCombatProfile.reserveBehindFriendlyCrowdScoreWeight -
            targetProgress * squadCombatProfile.reserveBehindFriendlyProgressScoreWeight;

        return true;
    }
    bool IsFormedReserveBehindFriendlyPointStillUseful(
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
                squadCombatProfile.reserveBehindFriendlyOccupancyRadius))
        {
            return false;
        }

        int nearbyBodies = CountLivingSoldiersNearFormedPoint(
            soldier,
            reservePoint,
            squadCombatProfile.reserveBehindFriendlyCrowdRadius);

        return nearbyBodies <= squadCombatProfile.reserveBehindFriendlyMaxNearbyBodies;
    }
    int CountLivingSoldiersNearFormedPoint(
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

                count += CountLivingSoldiersNearFormedPointFromRoster(
                    ignoredSoldier,
                    candidateSquad.Roster,
                    point,
                    radiusSqr);
            }

            return count;
        }

        count += CountLivingSoldiersNearFormedPointFromRoster(
            ignoredSoldier,
            roster,
            point,
            radiusSqr);

        if (targetSquad != null)
        {
            count += CountLivingSoldiersNearFormedPointFromRoster(
                ignoredSoldier,
                targetSquad.Roster,
                point,
                radiusSqr);
        }

        return count;
    }
    int CountLivingSoldiersNearFormedPointFromRoster(
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
    bool HasCompleteFormedReserveBehindFriendlyPath(
        Vector3 startPoint,
        Vector3 endPoint)
    {
        if (!NavMesh.CalculatePath(
                startPoint,
                endPoint,
                NavMesh.AllAreas,
                formedReserveBehindFriendlyPath))
        {
            return false;
        }

        return formedReserveBehindFriendlyPath.status == NavMeshPathStatus.PathComplete;
    }
    bool TryTickFormedReserveSideStep(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        Vector3 desiredMoveDirection,
        float stoppingDistance)
    {
        if (!squadCombatProfile.reserveSideStepEnabled)
            return false;

        if (soldier == null || contactSensor == null)
            return false;

        if (formedReserveSideStepDestinations.TryGetValue(
                soldier,
                out Vector3 sideStepDestination))
        {
            if (!Calc.OutOfRange(
                    soldier.transform.position,
                    sideStepDestination,
                    0.18f))
            {
                formedReserveSideStepDestinations.Remove(soldier);
                formedTargetRefreshTimers[soldier] = 0f;
                return false;
            }

            soldier.MoveToCombatPoint(
                sideStepDestination,
                Mathf.Min(stoppingDistance, 0.12f),
                squadCombatProfile.reserveSideStepSpeedMultiplier);

            return true;
        }

        if (formedReserveSideStepTimers[soldier] > 0f)
            return false;

        formedReserveSideStepTimers[soldier] = Random.Range(
            squadCombatProfile.reserveSideStepIntervalMin,
            squadCombatProfile.reserveSideStepIntervalMax);

        if (!TryFindFormedReserveSideStepPoint(
                soldier,
                contactSensor,
                desiredMoveDirection,
                out sideStepDestination))
        {
            return false;
        }

        formedReserveSideStepDestinations[soldier] = sideStepDestination;

        soldier.MoveToCombatPoint(
            sideStepDestination,
            Mathf.Min(stoppingDistance, 0.12f),
            squadCombatProfile.reserveSideStepSpeedMultiplier);

        return true;
    }
    bool TryFindFormedReserveSideStepPoint(
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

        if (TryBuildFormedReserveSideStepPoint(
                soldier,
                contactSensor,
                firstSide,
                out sideStepPoint))
        {
            return true;
        }

        return hasSecondSide &&
               TryBuildFormedReserveSideStepPoint(
                   soldier,
                   contactSensor,
                   secondSide,
                   out sideStepPoint);
    }
    bool TryBuildFormedReserveSideStepPoint(
        SoldierController soldier,
        SoldierContactSensor contactSensor,
        Vector3 sideDirection,
        out Vector3 sideStepPoint)
    {
        sideStepPoint = soldier.transform.position;

        Vector3 rawPoint =
            soldier.transform.position +
            sideDirection.normalized * squadCombatProfile.reserveSideStepDistance;

        if (!NavMesh.SamplePosition(
                rawPoint,
                out NavMeshHit navHit,
                squadCombatProfile.reserveSideStepDistance,
                NavMesh.AllAreas))
        {
            return false;
        }

        if (contactSensor.IsPointOccupiedByLivingSoldier(
                soldier,
                navHit.position,
                squadCombatProfile.reserveSideStepOccupancyRadius))
        {
            return false;
        }

        sideStepPoint = navHit.position;
        return true;
    }
    void MarkFormedReserveBlocked(SoldierController soldier)
    {
        if (soldier == null)
            return;

        if (!formedReserveBlockedSoldiers.Add(soldier))
            return;

        formedReserveBlockedSitTimers[soldier] = Random.Range(squadCombatProfile.reserveMinimumBlockedSitTimeMin, squadCombatProfile.reserveMinimumBlockedSitTimeMax); // chcek
        formedReserveSideStepDestinations.Remove(soldier);
        formedReserveBehindFriendlyDestinations.Remove(soldier);
    }
    bool IsFormedReserveStillSitting(SoldierController soldier)
    {
        return soldier != null &&
               formedReserveBlockedSoldiers.Contains(soldier) &&
               formedReserveBlockedSitTimers.TryGetValue(
                   soldier,
                   out float sitTimer) &&
               sitTimer > 0f;
    }
    void ClearFormedReserveBlockedState(SoldierController soldier)
    {
        if (soldier == null)
            return;

        formedReserveBlockedSoldiers.Remove(soldier);

        if (formedReserveBlockedSitTimers.ContainsKey(soldier))
            formedReserveBlockedSitTimers[soldier] = 0f;

        formedReserveSideStepDestinations.Remove(soldier);
        formedReserveBehindFriendlyDestinations.Remove(soldier);

        if (formedReserveBehindFriendlySearchTimers.ContainsKey(soldier))
            formedReserveBehindFriendlySearchTimers[soldier] = 0f;
    }
    void EnsureFormedCombatTimers(SoldierController soldier)
    {
        if (!formedTargetRefreshTimers.ContainsKey(soldier))
            formedTargetRefreshTimers[soldier] = 0f;

        if (!formedAttackTimers.ContainsKey(soldier))
            formedAttackTimers[soldier] = 0f;

        if (!formedReserveSideStepTimers.ContainsKey(soldier))
        {
            formedReserveSideStepTimers[soldier] = Random.Range(
                squadCombatProfile.reserveSideStepIntervalMin,
                squadCombatProfile.reserveSideStepIntervalMax);
        }

        if (!formedReserveBlockedSitTimers.ContainsKey(soldier))
            formedReserveBlockedSitTimers[soldier] = 0f;

        if (!formedReserveBehindFriendlySearchTimers.ContainsKey(soldier))
            formedReserveBehindFriendlySearchTimers[soldier] = 0f;

    }
    void TickFormedCombatTimers(SoldierController soldier)
    {
        formedTargetRefreshTimers[soldier] -= Time.deltaTime;
        formedAttackTimers[soldier] -= Time.deltaTime;
        formedReserveSideStepTimers[soldier] -= Time.deltaTime;
        formedReserveBlockedSitTimers[soldier] -= Time.deltaTime;
        formedReserveBehindFriendlySearchTimers[soldier] -= Time.deltaTime;

        TickRangedReleaseTimer(soldier);
    }
    bool TryFindImmediateMeleeContactTarget(
        SoldierController soldier,
        SoldierController currentTarget,
        float attackRange,
        out SoldierController contactTarget)
    {
        contactTarget = null;

        if (!squadCombatProfile.immediateContactOverrideEnabled)
            return false;

        if (soldier == null || !soldier.IsAlive)
            return false;

        float contactRange = Mathf.Max(
            0.1f,
            attackRange + squadCombatProfile.immediateContactRangePadding);

        float bestDistanceSqr = contactRange * contactRange;

        if (squadCombatProfile.multiSquadLocalTargetingEnabled && SquadManager.Instance != null)
        {
            foreach (SquadController candidateSquad in SquadManager.Instance.Squads)
            {
                if (!CanAttack(candidateSquad))
                    continue;

                FindImmediateMeleeContactTargetFromSquad(
                    soldier,
                    candidateSquad,
                    ref contactTarget,
                    ref bestDistanceSqr);
            }
        }
        else
        {
            FindImmediateMeleeContactTargetFromSquad(
                soldier,
                targetSquad,
                ref contactTarget,
                ref bestDistanceSqr);
        }

        return contactTarget != null;
    }
    void FindImmediateMeleeContactTargetFromSquad(
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
            if (!IsValidFormedTarget(enemy))
                continue;

            float distanceSqr = Vector3.SqrMagnitude(
                soldierPosition - Flatten(enemy.transform.position));

            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            contactTarget = enemy;
        }
    }
    SoldierController FindBestFormedTarget(
        SoldierController soldier,
        SoldierController currentTarget)
    {
        if (soldier == null || targetSquad == null || targetSquad.Roster == null)
            return null;

        SoldierController bestTarget = null;
        float bestScore = float.PositiveInfinity;

        if (squadCombatProfile.multiSquadLocalTargetingEnabled && SquadManager.Instance != null)
        {
            foreach (SquadController candidateSquad in SquadManager.Instance.Squads)
            {
                if (!CanAttack(candidateSquad))
                    continue;

                ScoreFormedTargetsFromSquad(
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
            ScoreFormedTargetsFromSquad(
                soldier,
                currentTarget,
                targetSquad,
                true,
                ref bestTarget,
                ref bestScore);
        }

        return bestTarget;
    }
    void ScoreFormedTargetsFromSquad(
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
            if (!IsValidFormedTarget(enemy))
                continue;

            float distance = Vector3.Distance(
                Flatten(soldier.transform.position),
                Flatten(enemy.transform.position));

            // Non-primary enemies are local reactions only. This lets soldiers turn
            // into flankers without turning the whole squad into global free-chase.
            if (!isPrimaryTargetSquad && distance > squadCombatProfile.localEnemyTargetSearchRadius)
                continue;

            int currentAttackers = CountFormedAttackers(enemy, soldier);

            float score =
                distance +
                currentAttackers * squadCombatProfile.meleeTargetCrowdingPenalty;

            if (!isPrimaryTargetSquad)
                score += squadCombatProfile.nonPrimaryTargetPenalty;

            if (enemy == currentTarget)
                score -= squadCombatProfile.meleeCurrentTargetStickinessBonus;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }
    }
    int CountFormedAttackers(
        SoldierController target,
        SoldierController ignoredSoldier)
    {
        if (target == null)
            return 0;

        int count = 0;

        foreach (KeyValuePair<SoldierController, SoldierController> pair in formedTargets)
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
    bool IsValidFormedTarget(SoldierController target)
    {
        return target != null &&
               target.IsAlive &&
               target.Squad != null &&
               CanAttack(target.Squad);
    }
    void GetCombatAttackValues(
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
                attackRange * squadCombatProfile.rangedStoppingDistanceMultiplier);
            return;
        }

        attackRange = Mathf.Max(
            0.1f,
            weaponProfile != null
                ? meleeStats.attackRange
                : squadCombatProfile.fallbackMeleeAttackRange);

        attackInterval = Mathf.Max(
            0.05f,
            weaponProfile != null
                ? meleeStats.attackInterval
                : squadCombatProfile.fallbackMeleeAttackInterval);

        stoppingDistance = Mathf.Max(
            0.05f,
            attackRange * squadCombatProfile.meleeStoppingDistanceMultiplier);
    }
    bool AreAllFormedAttackTimersReady()
    {
        if (roster == null)
            return false;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null ||
                !soldier.IsAlive)
                // || !soldier.IsUsingRangedWeapon)
            {
                continue;
            }

            if (formedAttackTimers.TryGetValue(
                    soldier,
                    out float timer) &&
                timer > 0f)
            {
                return false;
            }
        }

        return true;
    }
    void TryFormedAttack(
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

        if (isRangedWeapon)
        {
            if (!HasRangedAmmunition)
                return;

            if (!IsTargetSquadWithinRangedFiringArc(rangedStats.attackRangeArc))
                return;
        }

        bool beganAttack =
            attacker.TryBeginAction(SoldierActionState.Attack);

        if (!beganAttack)
            return;

        float randInterval = Random.Range(squadCombatProfile.attackIntervalRandomMin, squadCombatProfile.attackIntervalRandomMax);
        
        formedAttackTimers[attacker] =
            Mathf.Max(0.05f, attackInterval + randInterval); // NEW: added randomized attack interval

        if (isRangedWeapon)
        {
            BeginRangedAttack(
                attacker,
                target,
                weaponProfile,
                rangedStats);

            return;
        }

        // Melee damage is not resolved here.
        // Snapshot the committed target and wait for AttackImpact.
        meleePendingTargets[attacker] = target;
    }
    void ResolveMeleeHit(
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

        int appliedDamage = target.Health.TakeDamage(
            damageResult.normalDamage,
            damageResult.armorPiercingDamage);

        if (appliedDamage > 0)
            GameEvents.CombatDamageDealt(attacker, target, appliedDamage);

        ApplyMeleeHitImpulse(attacker, target);

        if (target.IsAlive)
            target.TryBeginAction(SoldierActionState.HitReact);
    }
    void ApplyMeleeHitImpulse(
        SoldierController attacker,
        SoldierController target)
    {
        if (!squadCombatProfile.meleeHitImpulseEnabled)
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
            squadCombatProfile.meleeHitImpulseMagnitude,
            squadCombatProfile.meleeHitImpulseDuration);
    }
    public void ResolveSoldierAttackImpact(SoldierController attacker)
    {
        if (attacker == null)
            return;

        if (attacker.ActionState != SoldierActionState.Attack)
        {
            ClearPendingMeleeAttack(attacker);
            return;
        }

        if (!meleePendingTargets.TryGetValue(
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

        ResolveMeleeHit(
            attacker,
            target,
            meleeStats);
    }
    void ClearPendingMeleeAttack(SoldierController soldier)
    {
        if (soldier == null)
            return;

        meleePendingTargets.Remove(soldier);
    }
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
    void ClearFormedCombatRuntimeState(
        bool clearAttackTimers,
        bool clearCombatLocks = true)
    {
        formedTargets.Clear();
        formedTargetRefreshTimers.Clear();
        formedReserveSideStepTimers.Clear();
        formedReserveBlockedSitTimers.Clear();
        formedReserveBlockedSoldiers.Clear();
        formedReserveSideStepDestinations.Clear();
        formedReserveBehindFriendlySearchTimers.Clear();
        formedReserveBehindFriendlyDestinations.Clear();

        meleeActiveAttackerCombatLockTargets.Clear();

        if (clearCombatLocks)
        {
            meleeAttackerCombatLockTimers.Clear();
            meleeAttackerCombatLockTargets.Clear();
        }

        meleePendingTargets.Clear();
        
        rangedPendingProjectileTargets.Clear();
        rangedPendingProjectileWeapons.Clear();
        rangedReleaseTimers.Clear();

        if (clearAttackTimers)
            formedAttackTimers.Clear();
    }
    WeaponProfile GetWeaponProfile(SoldierController soldier)
    {
        if (soldier == null)
            return null;

        if (soldier.ActiveWeaponProfile != null)
            return soldier.ActiveWeaponProfile;

        return soldier.RangedWeaponProfile != null
            ? soldier.RangedWeaponProfile
            : soldier.MeleeWeaponProfile;
    }
    bool IsRangedWeapon(WeaponProfile weaponProfile)
    {
        return weaponProfile != null && weaponProfile.weaponKind == WeaponKind.Ranged;
    }

    #endregion
}


