using System.Collections.Generic;
using UnityEngine;

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
    // Prototype Runtime State
    // -----------------------------------------------------------------------------
    private readonly Dictionary<SoldierController, SoldierController> prototypeTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, float> prototypeTargetRefreshTimers =
        new Dictionary<SoldierController, float>();

    private readonly Dictionary<SoldierController, float> prototypeAttackTimers =
        new Dictionary<SoldierController, float>();

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
                out _))
        {
            soldier.Stop();
            return;
        }

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
    }

    void TickPrototypeTimers(SoldierController soldier)
    {
        prototypeTargetRefreshTimers[soldier] -= Time.deltaTime;
        prototypeAttackTimers[soldier] -= Time.deltaTime;
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
