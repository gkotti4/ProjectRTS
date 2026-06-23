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
    private SoldierController soldier;

    private SoldierController currentTarget;
    private float attackTimer = 0f;

    [Header("Combat Rhythm / Recovery")]
    [SerializeField] private float combatRecoveryMinDuration = 2f;
    [SerializeField] private float combatRecoveryMaxDuration = 4.75f;
    [SerializeField] private float combatLongRecoveryChance = 0.24f;
    [SerializeField] private float combatLongRecoveryMinDuration = 3f;
    [SerializeField] private float combatLongRecoveryMaxDuration = 7f;
    [SerializeField] private float combatRecoveryMoveChance = 0.65f;
    [SerializeField] private float combatRecoveryReleaseTargetChance = 0.35f;
    [SerializeField] private float combatRecoveryBackoffDistance = 1.15f;
    [SerializeField] private float combatRecoverySideStepDistance = 0.7f;
    [SerializeField] private float combatRecoveryMoveSpeedMultiplier = 0.55f;
    [SerializeField] private float combatRecoveryStoppingDistance = 0.08f;

    [Header("Pressure Waiting")]
    [SerializeField] private float pressureWaitDistance = 0.7f;
    [SerializeField] private float pressureWaitMinDuration = 1f;
    [SerializeField] private float pressureWaitMaxDuration = 2.5f;
    [SerializeField] private float pressureShuffleChance = 0.35f;
    [SerializeField] private float pressureShuffleSideDistance = 0.65f;
    [SerializeField] private float pressureShuffleForwardDistance = 0.2f;
    [SerializeField] private float pressureShuffleMoveSpeedMultiplier = 0.55f;

    [Header("Target Crowding")]
    [SerializeField] private int preferredAttackersPerTarget = 2;
    [SerializeField] private float targetCrowdingPenalty = 2.25f;
    [SerializeField] private float crowdedTargetExtraPenaltyDistance = 0.75f;

    [Header("Hit Reaction")]
    [SerializeField] private bool hitReactionEnabled = true;
    [SerializeField] private float hitReactionChance = 0.55f;
    [SerializeField] private float hitReactionDamageChanceBonus = 0.25f;
    [SerializeField] private float hitReactionMinDuration = 0.35f;
    [SerializeField] private float hitReactionMaxDuration = 1.05f;
    [SerializeField] private float hitReactionCooldown = 0.95f;
    [SerializeField] private float hitReactionMoveChance = 0.25f;
    [SerializeField] private float hitReactionBackoffDistance = 0.55f;
    [SerializeField] private float hitReactionSideStepDistance = 0.35f;
    [SerializeField] private float hitReactionMoveSpeedMultiplier = 0.45f;
    [SerializeField] private float hitReactionStoppingDistance = 0.06f;
    [SerializeField] private float hitReactionRecoveryExtension = 0.45f;

    [Header("Combat Discipline / Home Bias")]
    [SerializeField] private float combatRecoveryHomeBias = 0.35f;
    [SerializeField] private float hitReactionHomeBias = 0.5f;
    [SerializeField] private float pressureShuffleHomeBias = 0.15f;

    private SoldierCombatRhythmState rhythmState = SoldierCombatRhythmState.Seeking;

    private float combatRecoveryTimer = 0f;
    private bool hasCombatRecoveryPoint = false;
    private Vector3 combatRecoveryPoint = Vector3.zero;

    private float pressureWaitTimer = 0f;
    private bool hasPressureShufflePoint = false;
    private Vector3 pressureShufflePoint = Vector3.zero;

    private float hitReactionTimer = 0f;
    private float hitReactionCooldownTimer = 0f;
    private bool hasHitReactionPoint = false;
    private Vector3 hitReactionPoint = Vector3.zero;
    private SoldierController lastHitAttacker;

    private bool hasLastCombatContext = false;
    private Vector3 lastCohesionOrigin = Vector3.zero;
    private Vector3 lastPressureGoal = Vector3.zero;

    public SoldierController CurrentTarget => currentTarget;
    public bool HasTarget => currentTarget != null && currentTarget.IsAlive;

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
        combatRecoveryTimer = 0f;
        hasCombatRecoveryPoint = false;
        pressureWaitTimer = 0f;
        hasPressureShufflePoint = false;
        hitReactionTimer = 0f;
        hitReactionCooldownTimer = 0f;
        hasHitReactionPoint = false;
        lastHitAttacker = null;
        hasLastCombatContext = false;
        lastCohesionOrigin = Vector3.zero;
        lastPressureGoal = Vector3.zero;
        rhythmState = SoldierCombatRhythmState.Seeking;
    }

    public void ApplyProfileFromSquad()
    {
        if (soldier == null ||
            soldier.Squad == null ||
            soldier.Squad.Data == null)
        {
            return;
        }

        ApplyProfile(soldier.Squad.Data.soldierCombatProfile);
    }

    public void ApplyProfile(SoldierCombatProfile profile)
    {
        if (profile == null)
            return;

        combatRecoveryMinDuration = Mathf.Max(0.05f, profile.combatRecoveryMinDuration);
        combatRecoveryMaxDuration = Mathf.Max(combatRecoveryMinDuration, profile.combatRecoveryMaxDuration);
        combatLongRecoveryChance = Mathf.Clamp01(profile.combatLongRecoveryChance);
        combatLongRecoveryMinDuration = Mathf.Max(0.05f, profile.combatLongRecoveryMinDuration);
        combatLongRecoveryMaxDuration = Mathf.Max(combatLongRecoveryMinDuration, profile.combatLongRecoveryMaxDuration);
        combatRecoveryMoveChance = Mathf.Clamp01(profile.combatRecoveryMoveChance);
        combatRecoveryReleaseTargetChance = Mathf.Clamp01(profile.combatRecoveryReleaseTargetChance);
        combatRecoveryBackoffDistance = Mathf.Max(0f, profile.combatRecoveryBackoffDistance);
        combatRecoverySideStepDistance = Mathf.Max(0f, profile.combatRecoverySideStepDistance);
        combatRecoveryMoveSpeedMultiplier = Mathf.Max(0.1f, profile.combatRecoveryMoveSpeedMultiplier);
        combatRecoveryStoppingDistance = Mathf.Max(0f, profile.combatRecoveryStoppingDistance);

        pressureWaitDistance = Mathf.Max(0f, profile.pressureWaitDistance);
        pressureWaitMinDuration = Mathf.Max(0.05f, profile.pressureWaitMinDuration);
        pressureWaitMaxDuration = Mathf.Max(pressureWaitMinDuration, profile.pressureWaitMaxDuration);
        pressureShuffleChance = Mathf.Clamp01(profile.pressureShuffleChance);
        pressureShuffleSideDistance = Mathf.Max(0f, profile.pressureShuffleSideDistance);
        pressureShuffleForwardDistance = Mathf.Max(0f, profile.pressureShuffleForwardDistance);
        pressureShuffleMoveSpeedMultiplier = Mathf.Max(0.1f, profile.pressureShuffleMoveSpeedMultiplier);

        preferredAttackersPerTarget = Mathf.Max(1, profile.preferredAttackersPerTarget);
        targetCrowdingPenalty = Mathf.Max(0f, profile.targetCrowdingPenalty);
        crowdedTargetExtraPenaltyDistance = Mathf.Max(0f, profile.crowdedTargetExtraPenaltyDistance);

        hitReactionEnabled = profile.hitReactionEnabled;
        hitReactionChance = Mathf.Clamp01(profile.hitReactionChance);
        hitReactionDamageChanceBonus = Mathf.Clamp01(profile.hitReactionDamageChanceBonus);
        hitReactionMinDuration = Mathf.Max(0.05f, profile.hitReactionMinDuration);
        hitReactionMaxDuration = Mathf.Max(hitReactionMinDuration, profile.hitReactionMaxDuration);
        hitReactionCooldown = Mathf.Max(0f, profile.hitReactionCooldown);
        hitReactionMoveChance = Mathf.Clamp01(profile.hitReactionMoveChance);
        hitReactionBackoffDistance = Mathf.Max(0f, profile.hitReactionBackoffDistance);
        hitReactionSideStepDistance = Mathf.Max(0f, profile.hitReactionSideStepDistance);
        hitReactionMoveSpeedMultiplier = Mathf.Max(0.1f, profile.hitReactionMoveSpeedMultiplier);
        hitReactionStoppingDistance = Mathf.Max(0f, profile.hitReactionStoppingDistance);
        hitReactionRecoveryExtension = Mathf.Max(0f, profile.hitReactionRecoveryExtension);

        combatRecoveryHomeBias = Mathf.Clamp01(profile.combatRecoveryHomeBias);
        hitReactionHomeBias = Mathf.Clamp01(profile.hitReactionHomeBias);
        pressureShuffleHomeBias = Mathf.Clamp01(profile.pressureShuffleHomeBias);
    }

    /// Clears this soldier's local combat target and temporary rhythm state.
    /// The attack timer is intentionally not reset so clearing/reacquiring
    /// targets cannot create free instant attacks.
    public void ClearCombat()
    {
        currentTarget = null;
        combatRecoveryTimer = 0f;
        hasCombatRecoveryPoint = false;
        pressureWaitTimer = 0f;
        hasPressureShufflePoint = false;
        hitReactionTimer = 0f;
        hitReactionCooldownTimer = 0f;
        hasHitReactionPoint = false;
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
        bool canStartNewEngagement = true)
    {
        if (soldier == null || !soldier.IsAlive)
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
                speedMultiplier);

            return;
        }

        if (Vector3.Distance(transform.position, clampedPressureGoal) <= pressureWaitDistance)
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
                BeginCombatRecovery();
                break;

            case SoldierActionState.HitReact:
                combatRecoveryTimer = Mathf.Max(
                    combatRecoveryTimer,
                    hitReactionRecoveryExtension);

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
        if (interruptedAction == SoldierActionState.Attack &&
            newAction == SoldierActionState.HitReact)
        {
            hasCombatRecoveryPoint = false;
            combatRecoveryTimer = 0f;
        }

        if (interruptedAction == SoldierActionState.HitReact)
        {
            hasHitReactionPoint = false;
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
    /// This is intentionally local: the soldier prefers nearby reachable targets
    /// instead of blindly chasing the enemy squad center.
    SoldierController FindBestLocalTarget(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        float freeEngageDistance,
        float localTargetScanRange)
    {
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
                currentAttackers - Mathf.Max(0, preferredAttackersPerTarget - 1));

            float score = distance + extraAttackers * targetCrowdingPenalty;

            if (currentAttackers >= preferredAttackersPerTarget)
                score += crowdedTargetExtraPenaltyDistance;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
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

        if (Vector3.Distance(transform.position, clampedPoint) <= combatRecoveryStoppingDistance + 0.05f)
        {
            soldier.Stop();
            return;
        }

        soldier.MoveToCombatPoint(
            clampedPoint,
            combatRecoveryStoppingDistance,
            Mathf.Max(0.1f, speedMultiplier * combatRecoveryMoveSpeedMultiplier));
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
            pressureShuffleMoveSpeedMultiplier);
    }

    void ChooseNextPressureWaitAction(
        SquadController enemySquad,
        Vector3 cohesionOrigin,
        Vector3 pressureGoal,
        float freeEngageDistance)
    {
        pressureWaitTimer = Random.Range(
            Mathf.Max(0.05f, pressureWaitMinDuration),
            Mathf.Max(pressureWaitMinDuration, pressureWaitMaxDuration));

        hasPressureShufflePoint = false;

        if (Random.value > pressureShuffleChance)
            return;

        Vector3 toEnemy = GetDirectionToEnemySquad(enemySquad, pressureGoal);
        Vector3 side = new Vector3(toEnemy.z, 0f, -toEnemy.x);

        if (Random.value < 0.5f)
            side = -side;

        Vector3 desiredPoint =
            pressureGoal +
            side * Random.Range(0.1f, pressureShuffleSideDistance) +
            toEnemy * Random.Range(-pressureShuffleForwardDistance, pressureShuffleForwardDistance);

        desiredPoint = Vector3.Lerp(
            desiredPoint,
            pressureGoal,
            Mathf.Clamp01(pressureShuffleHomeBias));

        pressureShufflePoint = ClampPointToRange(
            desiredPoint,
            cohesionOrigin,
            freeEngageDistance);

        hasPressureShufflePoint = true;
    }

    /// Moves toward the current target if not in range, or attacks if in range.
    void TickTargetMovementAndAttack(
        Vector3 cohesionOrigin,
        float forceRejoinDistance,
        float speedMultiplier)
    {
        if (currentTarget == null || !currentTarget.IsAlive)
        {
            ClearCombatTargetOnly();
            return;
        }

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

    /// Moves toward either the pressure goal or the cohesion/rejoin origin.
    void MoveTowardFallback(
        Vector3 cohesionOrigin,
        Vector3 fallbackPoint,
        float stoppingDistance,
        float speedMultiplier)
    {
        Vector3 destination = fallbackPoint;

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

    /// Attempts a melee attack against the current target.
    /// Damage is still immediate for now; animation-timed damage can come later.
    void TryAttackCurrentTarget()
    {
        if (currentTarget == null || !currentTarget.IsAlive)
            return;

        if (attackTimer > 0f)
            return;

        if (!soldier.TryBeginAction(SoldierActionState.Attack))
            return;

        MeleeCombatStats attackerStats = GetMeleeStats(soldier);
        MeleeCombatStats defenderStats = GetMeleeStats(currentTarget);

        DamageResult result = CombatResolver.ResolveMeleeHit(
            attackerStats,
            defenderStats);

        if (result.didHit)
        {
            currentTarget.Health.TakeDamage(
                result.normalDamage,
                result.armorPiercingDamage);

            if (currentTarget != null &&
                currentTarget.IsAlive &&
                currentTarget.Combat != null)
            {
                currentTarget.Combat.ReceiveHitReaction(
                    soldier,
                    result.totalDamage);
            }
        }

        attackTimer = Mathf.Max(
            0.05f,
            attackerStats.attackInterval);
    }

    /// Called by an enemy soldier when this soldier is successfully hit.
    /// This is intentionally lightweight: it does not use physics yet.
    /// It creates a short stagger/reset window and may make this soldier step
    /// back or sideways before re-entering the melee rhythm.
    public void ReceiveHitReaction(
        SoldierController attacker,
        int damage)
    {
        if (!hitReactionEnabled)
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
            hitReactionChance + damagePressure * hitReactionDamageChanceBonus);

        if (Random.value > reactionChance)
            return;

        BeginHitReaction(attacker);
    }
    
    void BeginHitReaction(SoldierController attacker)
    {
        if (!soldier.TryBeginAction(SoldierActionState.HitReact))
            return;

        rhythmState = SoldierCombatRhythmState.Recovering;

        hitReactionCooldownTimer = Mathf.Max(0f, hitReactionCooldown);

        hasHitReactionPoint = false;
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
        if (hitReactionRecoveryExtension <= 0f)
        {
            rhythmState = SoldierCombatRhythmState.Seeking;
            return;
        }

        combatRecoveryTimer = Mathf.Max(
            combatRecoveryTimer,
            hitReactionRecoveryExtension);

        hasCombatRecoveryPoint = false;
        hasPressureShufflePoint = false;
        pressureWaitTimer = 0f;

        rhythmState = SoldierCombatRhythmState.Recovering;
    }

    void BeginCombatRecovery()
    {
        rhythmState = SoldierCombatRhythmState.Recovering;

        if (Random.value < combatLongRecoveryChance)
        {
            combatRecoveryTimer = Random.Range(
                Mathf.Max(0.05f, combatLongRecoveryMinDuration),
                Mathf.Max(combatLongRecoveryMinDuration, combatLongRecoveryMaxDuration));
        }
        else
        {
            combatRecoveryTimer = Random.Range(
                Mathf.Max(0.05f, combatRecoveryMinDuration),
                Mathf.Max(combatRecoveryMinDuration, combatRecoveryMaxDuration));
        }

        hasCombatRecoveryPoint = false;
        hasPressureShufflePoint = false;
        pressureWaitTimer = 0f;

        SoldierController recoveryTarget = currentTarget;

        if (currentTarget != null &&
            Random.value < combatRecoveryReleaseTargetChance)
        {
            ClearCombatTargetOnly();
        }

        if (recoveryTarget == null || !recoveryTarget.IsAlive)
            return;

        if (Random.value > combatRecoveryMoveChance)
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
            away * Random.Range(combatRecoveryBackoffDistance * 0.65f, combatRecoveryBackoffDistance) +
            side * Random.Range(-combatRecoverySideStepDistance, combatRecoverySideStepDistance);

        combatRecoveryPoint = ApplyCombatHomeBias(
            combatRecoveryPoint,
            combatRecoveryHomeBias);

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

    /// Gets this soldier's attack range.
    float GetAttackRange()
    {
        if (soldier != null && soldier.Data != null)
            return Mathf.Max(0.1f, soldier.Data.melee.attackRange);

        return MeleeCombatStats.Default.attackRange;
    }

    /// Gets melee stats from a soldier.
    MeleeCombatStats GetMeleeStats(SoldierController source)
    {
        if (source != null && source.Data != null)
            return source.Data.melee;

        return MeleeCombatStats.Default;
    }

    public void RandomizeInitialAttackTimer(float maxDelay)
    {
        if (attackTimer > 0f)
            return;

        attackTimer = Random.Range(0f, Mathf.Max(0f, maxDelay));
    }
}




