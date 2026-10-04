using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// -----------------------------------------------------------------------------
/// FormationCombat
/// -----------------------------------------------------------------------------
///
/// Formation/melee behavior module for SquadCombat.
///
/// This is intentionally a partial of the single SquadCombat MonoBehaviour rather
/// than a second component. It owns the formation soldier executor, local target
/// assignment, reserve movement, combat locks, attack timers, and melee impact
/// resolution while sharing one authoritative squad combat state with the coordinator.
///
public partial class SquadCombat
{
    #region Formation Combat Module

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

    // Target committed when a melee attack begins.
    // The AttackImpact animation event consumes this target so target refreshes
    // during the animation cannot redirect the completed swing.
    private readonly Dictionary<SoldierController, SoldierController> formationPendingMeleeTargets =
        new Dictionary<SoldierController, SoldierController>();

    void TickFormationCombat()
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
        if (TryBeginFormationRangedAvoidance())
            return;

        // Ranged/melee fallback is a squad decision, not a per-soldier decision.
        // Update it before range/break checks so the correct combat mode owns them.
        UpdateFormationSquadCombatMode();

        if (!CanAttack(targetSquad))
        {
            if (!TrySwitchPrimaryCombatTarget())
            {
                EndCombatAndReform();
                return;
            }

            UpdateFormationSquadCombatMode();
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

            UpdateFormationSquadCombatMode();
        }

        // Ranged squads pursue their current squad target as a formation.
        // If the target leaves the normal ranged engagement distance, reuse the
        // existing approach/preferred-range behavior instead of letting individual
        // soldiers chase or waiting until the larger combat break range is exceeded.
        if (IsRangedCombatStyle() &&
            !IsCloseEnoughToStartEngagement(targetSquad))
        {
            BeginApproachingCombat();
            return;
        }

        combatContactDirection = GetContactDirection();

        // Ranged combat is formation-owned. The whole squad turns and settles
        // before individual soldiers are allowed to fire.
        if (IsRangedCombatStyle() &&
            !TickFormationRangedSetup())
        {
            return;
        }
        
        // Ranged Volley (or Synced Attack) MVP
        bool waitToAttack = false;
        bool shouldSynchronizeRangedVolley =
            rangedVolleyEnabled &&
            IsRangedCombatStyle() &&
            !formationRangedSquadUsingMeleeFallback;

        if (shouldSynchronizeRangedVolley &&
            !IsAllSoldierAttackTimersReady())
        {
            waitToAttack = true;
        }
        
        foreach (SoldierController soldier in roster.Soldiers)
        {
            TickFormationSoldier(soldier, waitToAttack);
        }
    }
    void TickFormationSoldier(SoldierController soldier, bool waitToAttack = false)
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

        if (weaponProfile == null)
        {
            soldier.Stop();
            soldier.SetCombatRole(SoldierRole.None);
            return;
        }

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

            // Ranged Volley (Attack Sync) MVP
            if (waitToAttack)
                return;
            
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

        TickFormationRangedReleaseTimer(soldier);
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
    bool IsAllSoldierAttackTimersReady()
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

            if (formationAttackTimers.TryGetValue(
                    soldier,
                    out float timer) &&
                timer > 0f)
            {
                return false;
            }
        }

        return true;
    }
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

        int appliedDamage = target.Health.TakeDamage(
            damageResult.normalDamage,
            damageResult.armorPiercingDamage);

        if (appliedDamage > 0)
            GameEvents.CombatDamageDealt(attacker, target, appliedDamage);

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
        formationRangedReleaseTimers.Clear();

        if (clearAttackTimers)
            formationAttackTimers.Clear();
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
