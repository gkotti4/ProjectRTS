using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierCombat
/// -----------------------------------------------------------------------------
///
/// Local combat brain for one soldier.
/// Chooses nearby targets, checks attack range, requests Attack/HitReact actions,
/// handles attack impact timing, receives hit reaction requests, manages combat
/// rhythm, and keeps the soldier within squad-provided cohesion/pressure limits.
///
/// This class decides what the soldier wants to do next, but committed action
/// locking belongs to SoldierController and visual playback belongs to SoldierAnimator.
///
/// Design role:
/// Individual melee decision-making inside squad combat.
///

public class SoldierCombat : MonoBehaviour
{
    enum SoldierCombatRhythmState
    {
        Seeking,
        Engaged,
        Recovering,
        Waiting,
        Repositioning
    }

    #region Fields

    // -----------------------------------------------------------------------------
    // Component References
    // -----------------------------------------------------------------------------
    private SoldierController soldier;

    // -----------------------------------------------------------------------------
    // Runtime Target / Attack State
    // -----------------------------------------------------------------------------
    private SoldierController currentTarget;
    private float attackTimer = 0f;

    // -----------------------------------------------------------------------------
    // Local Attack Impact Fallbacks
    // -----------------------------------------------------------------------------
    // This is intentionally not serialized.
    // Could move to SoldierCombatProfile later if you want it designer-tunable.
    private float attackImpactRangeGrace = 0.35f;

    private SoldierController pendingAttackTarget;
    private bool pendingAttackImpactResolved = false;

    // -----------------------------------------------------------------------------
    // Profile Reference
    // -----------------------------------------------------------------------------
    // SoldierCombatProfile is the single source of truth for designer-tunable
    // combat values. Runtime fields below are only for actual combat state.
    private SoldierCombatProfile soldierCombatProfile;
    private bool hasLoggedMissingCombatProfile = false;

    // -----------------------------------------------------------------------------
    // Runtime Rhythm State
    // -----------------------------------------------------------------------------
    private SoldierCombatRhythmState rhythmState = SoldierCombatRhythmState.Seeking;

    // -----------------------------------------------------------------------------
    // Runtime Recovery State
    // -----------------------------------------------------------------------------
    private float combatRecoveryTimer = 0f;
    private bool hasCombatRecoveryPoint = false;
    private Vector3 combatRecoveryPoint = Vector3.zero;

    // -----------------------------------------------------------------------------
    // Runtime Pressure Waiting State
    // -----------------------------------------------------------------------------
    private float pressureWaitTimer = 0f;
    private bool hasPressureShufflePoint = false;
    private Vector3 pressureShufflePoint = Vector3.zero;

    // -----------------------------------------------------------------------------
    // Runtime Hit Reaction State
    // -----------------------------------------------------------------------------
    private float hitReactionCooldownTimer = 0f;
    private SoldierController lastHitAttacker;

    // -----------------------------------------------------------------------------
    // Runtime Combat Context Cache
    // -----------------------------------------------------------------------------
    private bool hasLastCombatContext = false;
    private Vector3 lastCohesionOrigin = Vector3.zero;
    private Vector3 lastPressureGoal = Vector3.zero;

    // -----------------------------------------------------------------------------
    // Public Read-Only Access
    // -----------------------------------------------------------------------------
    public SoldierController CurrentTarget => currentTarget;
    public bool HasTarget => currentTarget != null && currentTarget.IsAlive;
    public bool IsUsingRangedWeapon => IsRangedWeapon(GetWeaponProfile(soldier));

    public bool IsActiveAttacker =>
        HasTarget &&
        soldier != null &&
        soldier.ActionState != SoldierActionState.HitReact &&
        combatRecoveryTimer <= 0f &&
        (rhythmState == SoldierCombatRhythmState.Engaged ||
         soldier.ActionState == SoldierActionState.Attack);

    public bool IsAttacking =>
        soldier != null && soldier.ActionState == SoldierActionState.Attack;

    public bool IsRecovering => combatRecoveryTimer > 0f;

    public bool IsHitReacting =>
        soldier != null && soldier.ActionState == SoldierActionState.HitReact;

    public bool IsWaiting => rhythmState == SoldierCombatRhythmState.Waiting;

    #endregion

    /// Caches the owning SoldierController.
    /// SoldierCombat is the local combat brain for one soldier.
    void Awake()
    {
        soldier = GetComponent<SoldierController>();
    }

    /// Initializes this combat component from SoldierController.
    /// This keeps setup explicit when soldiers are spawned by SquadRoster.
    public void Initialize(SoldierController owner)
    {
        soldier = owner;
        ApplyProfileFromSquad();

        currentTarget = null;
        attackTimer = 0f;
        ClearPendingAttack();
        combatRecoveryTimer = 0f;
        hasCombatRecoveryPoint = false;
        pressureWaitTimer = 0f;
        hasPressureShufflePoint = false;
        hitReactionCooldownTimer = 0f;
        lastHitAttacker = null;
        hasLastCombatContext = false;
        lastCohesionOrigin = Vector3.zero;
        lastPressureGoal = Vector3.zero;
        rhythmState = SoldierCombatRhythmState.Seeking;
    }

    public void ApplyProfileFromSquad()
    {
        SoldierCombatProfile profile = null;

        if (soldier != null &&
            soldier.Squad != null &&
            soldier.Squad.Data != null)
        {
            profile = soldier.Squad.Data.soldierCombatProfile;
        }

        ApplyProfile(profile);
    }

    public void ApplyProfile(SoldierCombatProfile profile)
    {
        soldierCombatProfile = profile;

        enabled = HasCombatProfile();
    }

    bool HasCombatProfile()
    {
        if (soldierCombatProfile != null)
            return true;

        if (!hasLoggedMissingCombatProfile)
        {
            Debug.LogError(
                $"{name}: SoldierCombat requires SquadData.soldierCombatProfile. Assign a SoldierCombatProfile asset before using soldier combat.",
                this);

            hasLoggedMissingCombatProfile = true;
        }

        return false;
    }

    /// Clears this soldier's local combat target and temporary rhythm state.
    /// The attack timer is intentionally not reset so clearing/reacquiring
    /// targets cannot create free instant attacks.
    public void ClearCombat()
    {
        currentTarget = null;
        ClearPendingAttack();
        combatRecoveryTimer = 0f;
        hasCombatRecoveryPoint = false;
        pressureWaitTimer = 0f;
        hasPressureShufflePoint = false;
        hitReactionCooldownTimer = 0f;
        lastHitAttacker = null;
        hasLastCombatContext = false;
        lastCohesionOrigin = Vector3.zero;
        lastPressureGoal = Vector3.zero;
        rhythmState = SoldierCombatRhythmState.Seeking;

        if (soldier != null)
        {
            soldier.CancelCurrentAction(false);
            soldier.ClearCombatTarget();
        }
    }
    

    /// Legacy compatibility overload.
    /// New combat should call the overload with a pressure goal and soft cohesion ranges.
    public void TickCombat(
        SquadController enemySquad,
        Vector3 leashOrigin,
        float combatLeashRange,
        float localTargetScanRange,
        float speedMultiplier)
    {
        TickCombat(
            enemySquad,
            leashOrigin,
            leashOrigin,
            combatLeashRange,
            combatLeashRange + localTargetScanRange,
            combatLeashRange + localTargetScanRange + GetAttackRange(),
            localTargetScanRange,
            speedMultiplier,
            0.1f,
            true);
    }

    /// Ticks autonomous local melee behavior for this soldier.
    /// Priority order:
    /// 1. Continue recovery/reset rhythm if active.
    /// 2. Keep valid combat lock.
    /// 3. Acquire nearby valid enemy.
    /// 4. Press toward the squad combat pressure goal.
    /// 5. Wait/shuffle near the pressure line.
    /// 6. Fall back toward cohesion origin if too far or combat ends.
    public void TickCombat(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        Vector3 pressureGoal,
        float freeEngageDistance,
        float disengageDistance,
        float forceRejoinDistance,
        float localTargetScanRange,
        float speedMultiplier,
        float pressureStoppingDistance,
        bool canStartNewEngagement = true,
        float preferredAttackDistance = -1f)
    {
        if (soldier == null || !soldier.IsAlive)
            return;

        if (!HasCombatProfile())
            return;

        freeEngageDistance = Mathf.Max(0.1f, freeEngageDistance);
        disengageDistance = Mathf.Max(freeEngageDistance, disengageDistance);
        forceRejoinDistance = Mathf.Max(disengageDistance, forceRejoinDistance);
        localTargetScanRange = Mathf.Max(0f, localTargetScanRange);

        TickAttackTimer();
        TickCombatRecoveryTimer();
        TickPressureWaitTimer();
        TickHitReactionTimers();

        if (enemySquad == null ||
            enemySquad.Roster == null ||
            !enemySquad.Roster.HasLivingSoldiers)
        {
            ClearCombat();
            MoveTowardFallback(cohesionOrigin, cohesionOrigin, pressureStoppingDistance, 1f);
            return;
        }

        if (IsFarOutsideCohesion(cohesionOrigin, forceRejoinDistance) &&
            !HasImmediateAttackOpportunity())
        {
            ClearCombatTargetOnly();
            MoveTowardFallback(cohesionOrigin, cohesionOrigin, pressureStoppingDistance, 1f);
            return;
        }

        Vector3 clampedPressureGoal = ClampPointToRange(
            pressureGoal,
            cohesionOrigin,
            freeEngageDistance);

        RememberCombatContext(cohesionOrigin, clampedPressureGoal);

        if (soldier.IsMovementLocked)
        {
            TickActionLock(
                enemySquad,
                clampedPressureGoal);

            return;
        }

        if (combatRecoveryTimer > 0f)
        {
            TickCombatRecovery(
                enemySquad,
                cohesionOrigin,
                clampedPressureGoal,
                freeEngageDistance,
                speedMultiplier);

            return;
        }

        if (!IsValidCurrentTarget(
                enemySquad,
                cohesionOrigin,
                disengageDistance))
        {
            ClearCombatTargetOnly();

            if (canStartNewEngagement)
            {
                currentTarget = FindBestLocalTarget(
                    enemySquad,
                    cohesionOrigin,
                    freeEngageDistance,
                    localTargetScanRange);

                soldier.SetCombatTarget(currentTarget);
            }
        }

        if (currentTarget != null)
        {
            rhythmState = SoldierCombatRhythmState.Engaged;

            TickTargetMovementAndAttack(
                cohesionOrigin,
                forceRejoinDistance,
                speedMultiplier,
                preferredAttackDistance);

            return;
        }

        if (Vector3.Distance(transform.position, clampedPressureGoal) <= soldierCombatProfile.pressureWaitDistance)
        {
            TickPressureWaiting(
                enemySquad,
                cohesionOrigin,
                clampedPressureGoal,
                freeEngageDistance);

            return;
        }

        rhythmState = SoldierCombatRhythmState.Seeking;

        MoveTowardFallback(
            cohesionOrigin,
            clampedPressureGoal,
            pressureStoppingDistance,
            speedMultiplier);
    }

    /// Locks this soldier in place while a committed full-body action is playing.
    /// Actions complete through animation events routed by SoldierAnimator -> SoldierController.
    void TickActionLock(
        SquadController enemySquad,
        Vector3 pressureGoal)
    {
        soldier.Stop();

        switch (soldier.ActionState)
        {
            case SoldierActionState.Attack:
                if (currentTarget != null && currentTarget.IsAlive)
                    soldier.FaceToward(currentTarget.transform.position);
                else
                    FaceTowardEnemySquad(enemySquad, pressureGoal);
                break;

            case SoldierActionState.HitReact:
                if (lastHitAttacker != null && lastHitAttacker.IsAlive)
                    soldier.FaceToward(lastHitAttacker.transform.position);
                else if (currentTarget != null && currentTarget.IsAlive)
                    soldier.FaceToward(currentTarget.transform.position);
                else
                    FaceTowardEnemySquad(enemySquad, pressureGoal);
                break;
        }
    }
    
    public void HandleActionCompleted(SoldierActionState completedAction)
    {
        switch (completedAction)
        {
            case SoldierActionState.Attack:
                if (pendingAttackTarget != null && !pendingAttackImpactResolved)
                {
                    Debug.LogWarning(
                        $"{name}: Attack completed without resolving an impact. " +
                        "Check that the attack clip has an OnAttackImpact / OnAttackExecute / OnProjectileRelease event.",
                        this);
                }

                ClearPendingAttack();
                BeginCombatRecovery();
                break;

            case SoldierActionState.HitReact:
                if (!HasCombatProfile())
                {
                    rhythmState = SoldierCombatRhythmState.Seeking;
                    break;
                }

                combatRecoveryTimer = Mathf.Max(
                    combatRecoveryTimer,
                    soldierCombatProfile.hitReactionRecoveryExtension);

                if (combatRecoveryTimer > 0f)
                    rhythmState = SoldierCombatRhythmState.Recovering;
                else
                    rhythmState = SoldierCombatRhythmState.Seeking;

                break;
        }
    }

    public void HandleActionInterrupted(
        SoldierActionState interruptedAction,
        SoldierActionState newAction)
    {
        if (interruptedAction == SoldierActionState.Attack)
        {
            ClearPendingAttack();

            if (newAction == SoldierActionState.HitReact)
            {
                hasCombatRecoveryPoint = false;
                combatRecoveryTimer = 0f;
            }
        }

    }
    // Backward-compatible hook for older animation-event wiring.
    public void NotifyAttackAnimationEnded()
    {
        soldier?.CompleteAction(SoldierActionState.Attack);
    }

    /// Updates the attack cooldown.
    void TickAttackTimer()
    {
        if (attackTimer <= 0f)
            return;

        attackTimer -= Time.deltaTime;
    }

    /// Updates the longer recovery/reset timer.
    void TickCombatRecoveryTimer()
    {
        if (combatRecoveryTimer <= 0f)
            return;

        combatRecoveryTimer -= Time.deltaTime;

        if (combatRecoveryTimer <= 0f)
        {
            combatRecoveryTimer = 0f;
            hasCombatRecoveryPoint = false;
            rhythmState = SoldierCombatRhythmState.Seeking;
        }
    }

    /// Updates the near-pressure wait timer.
    void TickPressureWaitTimer()
    {
        if (pressureWaitTimer <= 0f)
            return;

        pressureWaitTimer -= Time.deltaTime;

        if (pressureWaitTimer <= 0f)
        {
            pressureWaitTimer = 0f;
            hasPressureShufflePoint = false;
        }
    }

    /// Updates the short anti-stunlock cooldown for hit reactions.
    void TickHitReactionTimers()
    {
        if (hitReactionCooldownTimer > 0f)
            hitReactionCooldownTimer -= Time.deltaTime;
    }

    /// Checks whether the current target is still usable.
    /// Soft cohesion allows the soldier to keep fighting a good local target,
    /// but prevents endless chase away from the squad body.
    bool IsValidCurrentTarget(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        float disengageDistance)
    {
        if (currentTarget == null || !currentTarget.IsAlive)
            return false;

        if (currentTarget.Squad != enemySquad)
            return false;

        if (IsWithinAttackRange(currentTarget))
            return true;

        float allowedDistance = disengageDistance + GetAttackRange();

        return Vector3.Distance(
            cohesionOrigin,
            currentTarget.transform.position) <= allowedDistance;
    }

    /// Finds the best nearby enemy soldier this soldier can reasonably engage.
    /// Melee uses local nearest/crowding scoring.
    /// Ranged uses random valid target selection so volleys spread across the enemy squad.
    SoldierController FindBestLocalTarget(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        float freeEngageDistance,
        float localTargetScanRange)
    {
        if (enemySquad == null || enemySquad.Roster == null)
            return null;

        if (IsUsingRangedWeapon)
        {
            return FindRandomRangedTarget(
                enemySquad,
                cohesionOrigin,
                freeEngageDistance,
                localTargetScanRange);
        }

        SoldierController bestTarget = null;
        float bestScore = float.PositiveInfinity;

        foreach (SoldierController candidate in enemySquad.Roster.Soldiers)
        {
            if (candidate == null || !candidate.IsAlive)
                continue;

            if (!IsCandidateReachable(
                    candidate,
                    cohesionOrigin,
                    freeEngageDistance,
                    localTargetScanRange))
            {
                continue;
            }

            float distance = Vector3.Distance(
                candidate.transform.position,
                transform.position);

            int currentAttackers = CountFriendlyAttackers(candidate);
            int extraAttackers = Mathf.Max(
                0,
                currentAttackers - Mathf.Max(0, soldierCombatProfile.preferredAttackersPerTarget - 1));

            float score = distance + extraAttackers * soldierCombatProfile.targetCrowdingPenalty;

            if (currentAttackers >= soldierCombatProfile.preferredAttackersPerTarget)
                score += soldierCombatProfile.crowdedTargetExtraPenaltyDistance;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    /// Picks a random ranged target.
    /// First tries targets currently inside this soldier's attack range.
    /// If none are in range, falls back to any reachable target so the soldier can step forward.
    SoldierController FindRandomRangedTarget(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        float freeEngageDistance,
        float localTargetScanRange)
    {
        if (TryFindRandomRangedTarget(
                enemySquad,
                cohesionOrigin,
                freeEngageDistance,
                localTargetScanRange,
                requireCurrentAttackRange: true,
                out SoldierController inRangeTarget))
        {
            return inRangeTarget;
        }

        if (TryFindRandomRangedTarget(
                enemySquad,
                cohesionOrigin,
                freeEngageDistance,
                localTargetScanRange,
                requireCurrentAttackRange: false,
                out SoldierController reachableTarget))
        {
            return reachableTarget;
        }

        return null;
    }

    /// Reservoir-samples one valid target without allocating a temporary list.
    bool TryFindRandomRangedTarget(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        float freeEngageDistance,
        float localTargetScanRange,
        bool requireCurrentAttackRange,
        out SoldierController selectedTarget)
    {
        selectedTarget = null;

        if (enemySquad == null || enemySquad.Roster == null)
            return false;

        int validCandidateCount = 0;

        foreach (SoldierController candidate in enemySquad.Roster.Soldiers)
        {
            if (candidate == null || !candidate.IsAlive)
                continue;

            if (!IsCandidateReachable(
                    candidate,
                    cohesionOrigin,
                    freeEngageDistance,
                    localTargetScanRange))
            {
                continue;
            }

            if (requireCurrentAttackRange &&
                !IsWithinAttackRange(candidate))
            {
                continue;
            }

            validCandidateCount++;

            if (Random.Range(0, validCandidateCount) == 0)
                selectedTarget = candidate;
        }

        return selectedTarget != null;
    }

    /// Returns true when the enemy is close enough to either this soldier or this
    /// soldier's local combat area that engaging makes sense.
    bool IsCandidateReachable(
        SoldierController target,
        Vector3 cohesionOrigin,
        float freeEngageDistance,
        float localTargetScanRange)
    {
        if (target == null)
            return false;

        float attackRange = GetAttackRange();

        float distanceFromSoldier = Vector3.Distance(
            transform.position,
            target.transform.position);

        if (distanceFromSoldier <= localTargetScanRange + attackRange)
            return true;

        float distanceFromCohesionOrigin = Vector3.Distance(
            cohesionOrigin,
            target.transform.position);

        return distanceFromCohesionOrigin <=
               freeEngageDistance + localTargetScanRange + attackRange;
    }

    /// Handles the longer post-attack reset/backoff window.
    /// This is intentionally not just a tiny twitch. It creates combat rhythm:
    /// attack, reset/hold/back off, then re-enter after a meaningful pause.
    void TickCombatRecovery(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        Vector3 pressureGoal,
        float freeEngageDistance,
        float speedMultiplier)
    {
        rhythmState = SoldierCombatRhythmState.Recovering;

        if (currentTarget != null && currentTarget.IsAlive)
            soldier.FaceToward(currentTarget.transform.position);
        else
            FaceTowardEnemySquad(enemySquad, pressureGoal);

        if (!hasCombatRecoveryPoint)
        {
            soldier.Stop();
            return;
        }

        Vector3 clampedPoint = ClampPointToRange(
            combatRecoveryPoint,
            cohesionOrigin,
            freeEngageDistance);

        if (Vector3.Distance(transform.position, clampedPoint) <= soldierCombatProfile.combatRecoveryStoppingDistance + 0.05f)
        {
            soldier.Stop();
            return;
        }

        soldier.MoveToCombatPoint(
            clampedPoint,
            soldierCombatProfile.combatRecoveryStoppingDistance,
            Mathf.Max(0.1f, speedMultiplier * soldierCombatProfile.combatRecoveryMoveSpeedMultiplier));
    }

    /// Handles soldiers who are close enough to the pressure line but cannot find
    /// a clean target yet. They wait, face the fight, and occasionally shuffle.
    void TickPressureWaiting(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        Vector3 pressureGoal,
        float freeEngageDistance)
    {
        rhythmState = SoldierCombatRhythmState.Waiting;

        FaceTowardEnemySquad(enemySquad, pressureGoal);

        if (pressureWaitTimer <= 0f)
            ChooseNextPressureWaitAction(enemySquad, cohesionOrigin, pressureGoal, freeEngageDistance);

        if (!hasPressureShufflePoint)
        {
            soldier.Stop();
            return;
        }

        Vector3 clampedPoint = ClampPointToRange(
            pressureShufflePoint,
            cohesionOrigin,
            freeEngageDistance);

        if (Vector3.Distance(transform.position, clampedPoint) <= 0.08f)
        {
            soldier.Stop();
            return;
        }

        soldier.MoveToCombatPoint(
            clampedPoint,
            0.05f,
            soldierCombatProfile.pressureShuffleMoveSpeedMultiplier);
    }

    void ChooseNextPressureWaitAction(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        Vector3 pressureGoal,
        float freeEngageDistance)
    {
        pressureWaitTimer = Random.Range(
            Mathf.Max(0.05f, soldierCombatProfile.pressureWaitMinDuration),
            Mathf.Max(soldierCombatProfile.pressureWaitMinDuration, soldierCombatProfile.pressureWaitMaxDuration));

        hasPressureShufflePoint = false;

        if (Random.value > soldierCombatProfile.pressureShuffleChance)
            return;

        Vector3 toEnemy = GetDirectionToEnemySquad(enemySquad, pressureGoal);
        Vector3 side = new Vector3(toEnemy.z, 0f, -toEnemy.x);

        if (Random.value < 0.5f)
            side = -side;

        Vector3 desiredPoint =
            pressureGoal +
            side * Random.Range(0.1f, soldierCombatProfile.pressureShuffleSideDistance) +
            toEnemy * Random.Range(-soldierCombatProfile.pressureShuffleForwardDistance, soldierCombatProfile.pressureShuffleForwardDistance);

        desiredPoint = Vector3.Lerp(
            desiredPoint,
            pressureGoal,
            Mathf.Clamp01(soldierCombatProfile.pressureShuffleHomeBias));

        pressureShufflePoint = ClampPointToRange(
            desiredPoint,
            cohesionOrigin,
            freeEngageDistance);

        hasPressureShufflePoint = true;
    }

    /// Moves toward the current target if not in range, or attacks if in range.
    /// Weapon kind decides the local movement rule:
    /// - Melee closes to contact.
    /// - Ranged holds fire line and only steps forward enough to get into missile range.
    void TickTargetMovementAndAttack(
        Vector3 cohesionOrigin,
        float forceRejoinDistance,
        float speedMultiplier,
        float preferredAttackDistance = -1f)
    {
        if (currentTarget == null || !currentTarget.IsAlive)
        {
            ClearCombatTargetOnly();
            return;
        }

        if (IsUsingRangedWeapon)
        {
            TickRangedTargetMovementAndAttack(
                cohesionOrigin,
                forceRejoinDistance,
                speedMultiplier,
                preferredAttackDistance);

            return;
        }

        TickMeleeTargetMovementAndAttack(
            cohesionOrigin,
            forceRejoinDistance,
            speedMultiplier);
    }

    void TickMeleeTargetMovementAndAttack(
        Vector3 cohesionOrigin,
        float forceRejoinDistance,
        float speedMultiplier)
    {
        if (IsWithinAttackRange(currentTarget))
        {
            soldier.Stop();
            soldier.FaceToward(currentTarget.transform.position);

            TryAttackCurrentTarget();
            return;
        }

        Vector3 desiredPoint = ClampPointToRange(
            currentTarget.transform.position,
            cohesionOrigin,
            forceRejoinDistance);

        soldier.FaceToward(currentTarget.transform.position);

        soldier.MoveToCombatPoint(
            desiredPoint,
            0.05f,
            speedMultiplier);
    }

    void TickRangedTargetMovementAndAttack(
        Vector3 cohesionOrigin,
        float forceRejoinDistance,
        float speedMultiplier,
        float preferredAttackDistance)
    {
        soldier.FaceToward(currentTarget.transform.position);

        if (IsWithinAttackRange(currentTarget))
        {
            soldier.Stop();
            TryAttackCurrentTarget();
            return;
        }

        Vector3 toTarget = currentTarget.transform.position - transform.position;
        toTarget.y = 0f;

        if (toTarget == Vector3.zero)
        {
            soldier.Stop();
            return;
        }

        float attackRange = GetAttackRange();

        float preferredDistance = preferredAttackDistance > 0f
            ? Mathf.Min(preferredAttackDistance, attackRange)
            : Mathf.Max(0.1f, attackRange - soldierCombatProfile.rangedPreferredRangeBuffer);

        Vector3 directionToTarget = toTarget.normalized;

        Vector3 desiredPoint =
            currentTarget.transform.position -
            directionToTarget * preferredDistance;

        desiredPoint = ClampPointToRange(
            desiredPoint,
            cohesionOrigin,
            forceRejoinDistance);

        soldier.MoveToCombatPoint(
            desiredPoint,
            Mathf.Max(0.05f, soldierCombatProfile.rangedPreferredRangeBuffer),
            Mathf.Max(0.1f, speedMultiplier * soldierCombatProfile.rangedMoveSpeedMultiplier));
    }

    /// Moves toward either the pressure goal or the cohesion/rejoin origin.
    void MoveTowardFallback(
        Vector3 cohesionOrigin,
        Vector3 fallbackPoint,
        float stoppingDistance,
        float speedMultiplier,
        float orginRandomRange = 1.0f)
    {
        Vector3 destination = fallbackPoint;
        
        // NEW CHECK
        // Randomize fallback destination to some degree ?
        if (orginRandomRange > 0f)
            destination = destination + new Vector3(Random.Range(-orginRandomRange, orginRandomRange), 0f, Random.Range(-orginRandomRange, orginRandomRange));

        if (Vector3.Distance(transform.position, destination) <= stoppingDistance + 0.05f)
        {
            soldier.Stop();
            return;
        }

        Vector3 faceTarget = destination;

        if (Vector3.Distance(transform.position, faceTarget) > 0.05f)
            soldier.FaceToward(faceTarget);

        soldier.MoveToCombatPoint(
            destination,
            stoppingDistance,
            speedMultiplier);
    }

    void ClearCombatTargetOnly()
    {
        currentTarget = null;

        if (soldier != null)
            soldier.ClearCombatTarget();
    }

    bool HasImmediateAttackOpportunity()
    {
        return currentTarget != null &&
               currentTarget.IsAlive &&
               IsWithinAttackRange(currentTarget);
    }

    bool IsWithinAttackRange(SoldierController target)
    {
        if (target == null || !target.IsAlive)
            return false;

        return Vector3.Distance(
            soldier.transform.position,
            target.transform.position) <= GetAttackRange();
    }

    bool IsFarOutsideCohesion(
        Vector3 cohesionOrigin,
        float forceRejoinDistance)
    {
        return Vector3.Distance(
            transform.position,
            cohesionOrigin) > forceRejoinDistance;
    }

    /// Clamps a desired point inside the soldier's current soft cohesion area.
    Vector3 ClampPointToRange(
        Vector3 point,
        Vector3 origin,
        float maxDistance)
    {
        Vector3 offset = point - origin;
        offset.y = 0f;

        if (offset == Vector3.zero)
            return origin;

        float distance = offset.magnitude;

        if (distance <= maxDistance)
            return point;

        return origin + offset.normalized * maxDistance;
    }

    /// Attempts to start an attack against the current target.
    /// The actual damage/projectile release happens from the animation event via ResolveAttackImpact().
    void TryAttackCurrentTarget()
    {
        if (currentTarget == null || !currentTarget.IsAlive)
            return;

        if (attackTimer > 0f)
            return;

        pendingAttackTarget = currentTarget;
        pendingAttackImpactResolved = false;

        if (!soldier.TryBeginAction(SoldierActionState.Attack))
        {
            ClearPendingAttack();
            return;
        }

        attackTimer = Mathf.Max(
            0.05f,
            GetAttackInterval(soldier));
    }

    /// Resolves the committed ranged attack at the animation's projectile-release frame.
    /// This keeps archer clips readable while still using the same pending attack guard.
    public void ResolveProjectileRelease()
    {
        ResolveAttackImpact();
    }

    /// Resolves the committed attack at the animation's impact/release frame.
    /// This is called by SoldierController.OnAttackImpact() or OnProjectileRelease(), which are forwarded from SoldierAnimator.
    public void ResolveAttackImpact()
    {
        if (soldier == null || !soldier.IsAlive)
            return;

        if (soldier.ActionState != SoldierActionState.Attack)
            return;

        if (pendingAttackImpactResolved)
            return;

        SoldierController target = pendingAttackTarget;

        if (target == null || !target.IsAlive)
        {
            pendingAttackImpactResolved = true;
            return;
        }

        if (!IsWithinImpactRange(target))
        {
            pendingAttackImpactResolved = true;
            return;
        }

        pendingAttackImpactResolved = true;

        WeaponProfile weaponProfile = GetWeaponProfile(soldier);
        WeaponKind weaponKind = weaponProfile != null
            ? weaponProfile.weaponKind
            : WeaponKind.Melee;

        if (weaponKind == WeaponKind.Ranged)
        {
            ResolveRangedAttackImpact(target, weaponProfile);
            return;
        }

        ResolveMeleeAttackImpact(target, weaponProfile);
    }


    void ResolveMeleeAttackImpact(
        SoldierController target,
        WeaponProfile weaponProfile)
    {
        if (weaponProfile == null)
            return;

        DamageResult result = CombatResolver.ResolveMeleeHit(
            weaponProfile.melee,
            GetDefenseStats(target));

        if (!result.didHit)
            return;

        ApplyDamageAndHitReaction(
            target,
            result.normalDamage,
            result.armorPiercingDamage,
            result.totalDamage);
    }

    void ResolveRangedAttackImpact(
        SoldierController target,
        WeaponProfile weaponProfile)
    {
        if (weaponProfile == null)
            return;

        RangedCombatStats rangedStats = weaponProfile.ranged;

        if (rangedStats.projectilePrefab != null)
        {
            Transform origin = soldier.AttackOrigin;

            GameObject projectileObject = Instantiate(
                rangedStats.projectilePrefab,
                origin.position,
                origin.rotation);

            if (!projectileObject.TryGetComponent(out ProjectileController projectile))
                projectile = projectileObject.AddComponent<ProjectileController>();

            projectile.Initialize(
                soldier,
                target,
                weaponProfile);

            return;
        }

        // Debug/fallback behavior: ranged weapons without a projectile prefab still resolve immediately.
        DamageResult result = CombatResolver.ResolveRangedHit(
            rangedStats,
            GetDefenseStats(target));

        if (!result.didHit)
            return;

        ApplyDamageAndHitReaction(
            target,
            result.normalDamage,
            result.armorPiercingDamage,
            result.totalDamage);
    }

    void ApplyDamageAndHitReaction(
        SoldierController target,
        int normalDamage,
        int armorPiercingDamage,
        int totalDamageForReaction)
    {
        if (target == null || !target.IsAlive || target.Health == null)
            return;

        target.Health.TakeDamage(
            normalDamage,
            armorPiercingDamage);

        if (target != null &&
            target.IsAlive &&
            target.Combat != null)
        {
            target.Combat.ReceiveHitReaction(
                soldier,
                totalDamageForReaction);
        }
    }

    bool IsWithinImpactRange(SoldierController target)
    {
        if (target == null || !target.IsAlive)
            return false;

        float allowedRange = GetAttackRange() + Mathf.Max(0f, attackImpactRangeGrace);

        return Vector3.Distance(
            soldier.transform.position,
            target.transform.position) <= allowedRange;
    }

    void ClearPendingAttack()
    {
        pendingAttackTarget = null;
        pendingAttackImpactResolved = false;
    }

    /// Called by an enemy soldier when this soldier is successfully hit.
    /// This is intentionally lightweight: it does not use physics yet.
    /// It creates a short stagger/reset window and may make this soldier step
    /// back or sideways before re-entering the melee rhythm.
    public void ReceiveHitReaction(
        SoldierController attacker,
        int damage)
    {
        if (!HasCombatProfile())
            return;

        if (!soldierCombatProfile.hitReactionEnabled)
            return;

        if (soldier == null || !soldier.IsAlive)
            return;

        if (attacker != null && attacker.IsAlive)
        {
            currentTarget = attacker;
            lastHitAttacker = attacker;
            soldier.SetCombatTarget(attacker);
        }

        if (hitReactionCooldownTimer > 0f)
            return;

        float maxHealth = soldier.Health != null
            ? Mathf.Max(1f, soldier.Health.MaxHealth)
            : 100f;

        float damagePressure = Mathf.Clamp01(
            Mathf.Max(0, damage) / maxHealth);

        float reactionChance = Mathf.Clamp01(
            soldierCombatProfile.hitReactionChance + damagePressure * soldierCombatProfile.hitReactionDamageChanceBonus);

        if (Random.value > reactionChance)
            return;

        BeginHitReaction(attacker);
    }
    
    void BeginHitReaction(SoldierController attacker)
    {
        if (!soldier.TryBeginAction(SoldierActionState.HitReact))
            return;

        rhythmState = SoldierCombatRhythmState.Recovering;

        hitReactionCooldownTimer = Mathf.Max(0f, soldierCombatProfile.hitReactionCooldown);

        hasPressureShufflePoint = false;
        hasCombatRecoveryPoint = false;

        pressureWaitTimer = 0f;
        combatRecoveryTimer = 0f;

        if (attacker != null && attacker.IsAlive)
        {
            currentTarget = attacker;
            lastHitAttacker = attacker;
            soldier.SetCombatTarget(attacker);
            soldier.FaceToward(attacker.transform.position);
        }
    }

    void BeginHitReactionRecovery()
    {
        if (!HasCombatProfile())
            return;

        if (soldierCombatProfile.hitReactionRecoveryExtension <= 0f)
        {
            rhythmState = SoldierCombatRhythmState.Seeking;
            return;
        }

        combatRecoveryTimer = Mathf.Max(
            combatRecoveryTimer,
            soldierCombatProfile.hitReactionRecoveryExtension);

        hasCombatRecoveryPoint = false;
        hasPressureShufflePoint = false;
        pressureWaitTimer = 0f;

        rhythmState = SoldierCombatRhythmState.Recovering;
    }


    void BeginRangedCombatRecovery()
    {
        rhythmState = SoldierCombatRhythmState.Recovering;

        combatRecoveryTimer = Random.Range(
            Mathf.Max(0.05f, soldierCombatProfile.rangedRecoveryMinDuration),
            Mathf.Max(soldierCombatProfile.rangedRecoveryMinDuration, soldierCombatProfile.rangedRecoveryMaxDuration));

        hasPressureShufflePoint = false;
        pressureWaitTimer = 0f;

        if (soldierCombatProfile.rangedHoldPositionDuringRecovery)
        {
            hasCombatRecoveryPoint = false;
            return;
        }

        hasCombatRecoveryPoint = false;
    }

    void BeginCombatRecovery()
    {
        if (!HasCombatProfile())
            return;

        if (IsUsingRangedWeapon)
        {
            BeginRangedCombatRecovery();
            return;
        }

        rhythmState = SoldierCombatRhythmState.Recovering;

        if (Random.value < soldierCombatProfile.combatLongRecoveryChance)
        {
            combatRecoveryTimer = Random.Range(
                Mathf.Max(0.05f, soldierCombatProfile.combatLongRecoveryMinDuration),
                Mathf.Max(soldierCombatProfile.combatLongRecoveryMinDuration, soldierCombatProfile.combatLongRecoveryMaxDuration));
        }
        else
        {
            combatRecoveryTimer = Random.Range(
                Mathf.Max(0.05f, soldierCombatProfile.combatRecoveryMinDuration),
                Mathf.Max(soldierCombatProfile.combatRecoveryMinDuration, soldierCombatProfile.combatRecoveryMaxDuration));
        }

        hasCombatRecoveryPoint = false;
        hasPressureShufflePoint = false;
        pressureWaitTimer = 0f;

        SoldierController recoveryTarget = currentTarget;

        if (currentTarget != null &&
            Random.value < soldierCombatProfile.combatRecoveryReleaseTargetChance)
        {
            ClearCombatTargetOnly();
        }

        if (recoveryTarget == null || !recoveryTarget.IsAlive)
            return;

        if (Random.value > soldierCombatProfile.combatRecoveryMoveChance)
            return;

        Vector3 away = transform.position - recoveryTarget.transform.position;
        away.y = 0f;

        if (away == Vector3.zero)
            away = -transform.forward;

        away.Normalize();

        Vector3 side = new Vector3(away.z, 0f, -away.x);

        if (Random.value < 0.5f)
            side = -side;

        combatRecoveryPoint =
            transform.position +
            away * Random.Range(soldierCombatProfile.combatRecoveryBackoffDistance * 0.65f, soldierCombatProfile.combatRecoveryBackoffDistance) +
            side * Random.Range(-soldierCombatProfile.combatRecoverySideStepDistance, soldierCombatProfile.combatRecoverySideStepDistance);

        combatRecoveryPoint = ApplyCombatHomeBias(
            combatRecoveryPoint,
            soldierCombatProfile.combatRecoveryHomeBias);

        hasCombatRecoveryPoint = true;
    }

    void RememberCombatContext(
        Vector3 cohesionOrigin,
        Vector3 pressureGoal)
    {
        hasLastCombatContext = true;
        lastCohesionOrigin = cohesionOrigin;
        lastPressureGoal = pressureGoal;
    }

    Vector3 ApplyCombatHomeBias(
        Vector3 desiredPoint,
        float bias)
    {
        if (!hasLastCombatContext)
            return desiredPoint;

        bias = Mathf.Clamp01(bias);

        if (bias <= 0f)
            return desiredPoint;

        Vector3 disciplinedAnchor = Vector3.Lerp(
            lastPressureGoal,
            lastCohesionOrigin,
            0.65f);

        return Vector3.Lerp(
            desiredPoint,
            disciplinedAnchor,
            bias);
    }

    int CountFriendlyAttackers(SoldierController target)
    {
        if (target == null || soldier == null || soldier.Squad == null || soldier.Squad.Roster == null)
            return 0;

        int count = 0;

        foreach (SoldierController friendly in soldier.Squad.Roster.Soldiers)
        {
            if (friendly == null || friendly == soldier || !friendly.IsAlive)
                continue;

            if (friendly.CombatTarget == target)
                count++;
        }

        return count;
    }

    void FaceTowardEnemySquad(
        SquadController enemySquad,
        Vector3 fallbackPoint)
    {
        if (enemySquad != null)
        {
            FaceTowardPoint(enemySquad.transform.position);
            return;
        }

        FaceTowardPoint(fallbackPoint);
    }

    void FaceTowardPoint(Vector3 point)
    {
        if (soldier == null)
            return;

        if (Vector3.Distance(transform.position, point) <= 0.05f)
            return;

        soldier.FaceToward(point);
    }

    Vector3 GetDirectionToEnemySquad(
        SquadController enemySquad,
        Vector3 fallbackPoint)
    {
        Vector3 targetPoint = enemySquad != null
            ? enemySquad.transform.position
            : fallbackPoint;

        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;

        if (direction == Vector3.zero)
            direction = transform.forward;

        direction.y = 0f;

        if (direction == Vector3.zero)
            return Vector3.forward;

        return direction.normalized;
    }

    bool IsRangedWeapon(WeaponProfile weaponProfile)
    {
        return weaponProfile != null && weaponProfile.weaponKind == WeaponKind.Ranged;
    }


    /// Gets this soldier's attack range.
    float GetAttackRange()
    {
        return GetAttackRange(GetWeaponProfile(soldier));
    }

    float GetAttackRange(WeaponProfile weaponProfile)
    {
        if (weaponProfile == null)
            return 0.1f;

        return weaponProfile.weaponKind == WeaponKind.Ranged
            ? Mathf.Max(0.1f, weaponProfile.ranged.attackRange)
            : Mathf.Max(0.1f, weaponProfile.melee.attackRange);
    }

    float GetAttackInterval(SoldierController source)
    {
        return GetAttackInterval(GetWeaponProfile(source));
    }

    float GetAttackInterval(WeaponProfile weaponProfile)
    {
        if (weaponProfile == null)
            return 0.05f;

        return weaponProfile.weaponKind == WeaponKind.Ranged
            ? Mathf.Max(0.05f, weaponProfile.ranged.attackInterval)
            : Mathf.Max(0.05f, weaponProfile.melee.attackInterval);
    }


    WeaponProfile GetWeaponProfile(SoldierController source)
    {
        if (source == null || source.Data == null)
            return null;

        return source.Data.weaponProfile;
    }

    CombatDefenseStats GetDefenseStats(SoldierController source)
    {
        if (source != null && source.Data != null)
            return source.Data.defense;

        return CombatDefenseStats.Default;
    }

    float GetDefaultFallbackAttackRange()
    {
        return 1.5f;
    }

    float GetDefaultFallbackAttackInterval()
    {
        return 1.5f;
    }

    public void RandomizeInitialAttackTimer(float maxDelay)
    {
        if (attackTimer > 0f)
            return;

        attackTimer = Random.Range(0f, Mathf.Max(0f, maxDelay));
    }
}




