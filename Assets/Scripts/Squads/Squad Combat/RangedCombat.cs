using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// RangedCombat
/// -----------------------------------------------------------------------------
///
/// Ranged behavior module for SquadCombat.
///
/// This is intentionally a partial of the single SquadCombat MonoBehaviour rather
/// than a second component. It owns ranged squad policy, avoidance, ammunition,
/// firing setup/facing, melee fallback, hold/release timing, and projectile release
/// while sharing one authoritative combat target/state with the coordinator.
///
public partial class SquadCombat
{
    #region Ranged Combat Module

    // -----------------------------------------------------------------------------
    // Ranged Runtime State
    // -----------------------------------------------------------------------------
    // Ranged squads switch as one unit between ranged and melee fallback.
    private bool formationRangedSquadUsingMeleeFallback = false;

    private bool rangedVolleyEnabled = false;
    private bool rangedAvoidanceEnabled = false;

    // The threat that caused the current ranged-avoidance withdrawal.
    // Kept separately because normal combat targets are cleared during retreat.
    private SquadController rangedAvoidanceThreatSquad;

    // Ranged ammunition belongs to the squad, not individual soldiers.
    // -1 means unlimited ammunition.
    private int currentRangedAmmunition = 0;
    private int maximumRangedAmmunition = 0;
    private int rangedAmmunitionStartingSoldierCount = 0;

    private float formationRangedInitialFireSettleTimer = 0f;
    private bool formationRangedSetupRequired = false;
    private bool formationRangedSetupInitialized = false;
    
    private readonly Dictionary<SoldierController, SoldierController> formationPendingProjectileTargets =
        new Dictionary<SoldierController, SoldierController>();

    private readonly Dictionary<SoldierController, WeaponProfile> formationPendingProjectileWeapons =
        new Dictionary<SoldierController, WeaponProfile>();

    // Ranged attacks enter a looping RangedAttackHold first. Gameplay owns this
    // delay and explicitly tells the Animator when to enter RangedAttackRelease.
    private readonly Dictionary<SoldierController, float> formationRangedReleaseTimers =
        new Dictionary<SoldierController, float>();
    
    // -----------------------------------------------------------------------------
    // Public Ranged Access
    // -----------------------------------------------------------------------------
    public bool RangedVolleyEnabled => rangedVolleyEnabled;
    public bool RangedAvoidanceEnabled => rangedAvoidanceEnabled;

    public int CurrentRangedAmmunition => currentRangedAmmunition;
    public int MaximumRangedAmmunition => maximumRangedAmmunition;
    public bool HasUnlimitedRangedAmmunition => maximumRangedAmmunition < 0;
    public bool HasRangedAmmunition =>
        IsAuthoredRangedSquad() &&
        (HasUnlimitedRangedAmmunition || currentRangedAmmunition > 0);

    public void SetRangedVolleyEnabled(bool enabled)
    {
        rangedVolleyEnabled = enabled;
    }
    public void ToggleRangedVolley()
    {
        rangedVolleyEnabled = !rangedVolleyEnabled;
    }
    public void SetRangedAvoidanceEnabled(bool enabled)
    {
        rangedAvoidanceEnabled = enabled;
    }
    public void ToggleRangedAvoidance()
    {
        rangedAvoidanceEnabled = !rangedAvoidanceEnabled;
    }
    bool TryBeginFormationRangedAvoidance()
    {
        if (!rangedAvoidanceEnabled ||
            !IsAuthoredRangedSquad() ||
            !HasRangedAmmunition ||
            targetSquad == null ||
            movement == null ||
            squad == null)
        {
            return false;
        }

        float avoidanceEnterDistance = Mathf.Max(
            0.1f,
            squadCombatProfile.formationRangedAvoidanceEnterDistance);

        // Use the same physical soldier-proximity basis as melee fallback. Squad
        // roots are virtual formation anchors and can be offset from the actual fight.
        if (!IsAnyLivingSquadMemberThreatenedWithin(avoidanceEnterDistance))
            return false;

        BeginFormationRangedAvoidanceRetreat(targetSquad);
        return true;
    }
    public bool TryChainFormationRangedAvoidanceWithdrawal()
    {
        if (!rangedAvoidanceEnabled ||
            !HasRangedAmmunition ||
            rangedAvoidanceThreatSquad == null ||
            movement == null ||
            squad == null)
        {
            rangedAvoidanceThreatSquad = null;
            return false;
        }

        if (!CanAttack(rangedAvoidanceThreatSquad))
        {
            rangedAvoidanceThreatSquad = null;
            return false;
        }

        float distanceToDestination = Vector3.Distance(
            Flatten(squad.transform.position),
            Flatten(movement.FinalDestination));

        float recheckDistance = Mathf.Max(
            0.1f,
            squadCombatProfile.formationRangedAvoidanceRecheckDistance);

        if (distanceToDestination > recheckDistance)
            return false;

        float avoidanceDistance = Mathf.Max(
            0.1f,
            squadCombatProfile.formationRangedAvoidanceEnterDistance);

        if (!IsAnyLivingSquadMemberThreatenedBySquad(
                rangedAvoidanceThreatSquad,
                avoidanceDistance * avoidanceDistance))
        {
            rangedAvoidanceThreatSquad = null;
            return false;
        }

        BeginFormationRangedAvoidanceRetreat(rangedAvoidanceThreatSquad);
        return true;
    }
    void BeginFormationRangedAvoidanceRetreat(SquadController avoidanceThreat)
    {
        if (avoidanceThreat == null || avoidanceThreat.Roster == null)
            return;

        Vector3 myCenter = TryGetLivingSoldierCenter(roster, out Vector3 resolvedMyCenter)
            ? resolvedMyCenter
            : squad.transform.position;

        Vector3 targetCenter = TryGetLivingSoldierCenter(avoidanceThreat.Roster, out Vector3 resolvedTargetCenter)
            ? resolvedTargetCenter
            : avoidanceThreat.transform.position;

        Vector3 awayFromTarget = myCenter - targetCenter;
        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude <= 0.0001f)
            awayFromTarget = -avoidanceThreat.transform.forward;

        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude <= 0.0001f)
            awayFromTarget = -squad.transform.forward;

        awayFromTarget.Normalize();

        // Start the next retreat from the real squad body center, not from a stale
        // virtual anchor that may have been left behind by combat movement.
        movement.SyncRootToLivingSoldierCenter();

        Vector3 retreatDestination =
            squad.transform.position +
            awayFromTarget * squadCombatProfile.formationRangedAvoidanceRetreatDistance;

        // Clear normal combat ownership, but preserve the avoidance threat separately
        // so withdrawal can recheck it before reaching the destination.
        ClearTargets();
        rangedAvoidanceThreatSquad = avoidanceThreat;

        Vector3 facing = -awayFromTarget;
        movement.OrderMove(retreatDestination, facing);
        squad.SetState(SquadState.Withdrawing);
    }
    public void CancelRangedAttacksForMoveOrder()
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null ||
                !soldier.IsAlive ||
                soldier.ActionState != SoldierActionState.Attack ||
                !soldier.IsUsingRangedWeapon)
            {
                continue;
            }

            soldier.CancelCurrentAction();
        }
    }
    void TickFormationRangedReleaseTimer(SoldierController soldier)
    {
        if (soldier == null ||
            !formationRangedReleaseTimers.TryGetValue(soldier, out float releaseTimer))
        {
            return;
        }

        releaseTimer -= Time.deltaTime;

        if (releaseTimer > 0f)
        {
            formationRangedReleaseTimers[soldier] = releaseTimer;
            return;
        }

        // Consume first so one attack can issue RangedRelease only once.
        formationRangedReleaseTimers.Remove(soldier);

        if (!soldier.IsAlive ||
            soldier.ActionState != SoldierActionState.Attack ||
            !soldier.IsUsingRangedWeapon)
        {
            return;
        }

        soldier.SoldierAnimator?.ReleaseRangedAttack();
    }
    bool TryCancelInvalidPendingRangedAttack(
        SoldierController attacker,
        RangedCombatStats rangedStats)
    {
        if (attacker == null ||
            attacker.ActionState != SoldierActionState.Attack ||
            !formationPendingProjectileTargets.TryGetValue(
                attacker,
                out SoldierController pendingTarget))
        {
            return false;
        }

        bool targetIsValid =
            pendingTarget != null &&
            pendingTarget.IsAlive &&
            pendingTarget.Squad != null &&
            CanAttack(pendingTarget.Squad);

        if (targetIsValid)
        {
            float attackRange = Mathf.Max(0.1f, rangedStats.attackRange);

            Vector3 toPendingTarget =
                pendingTarget.transform.position -
                attacker.transform.position;

            toPendingTarget.y = 0f;

            targetIsValid =
                toPendingTarget.sqrMagnitude <=
                attackRange * attackRange;
        }

        if (targetIsValid &&
            IsTargetSquadWithinRangedFiringArc(
                rangedStats.attackRangeArc))
        {
            return false;
        }

        // CancelCurrentAction routes through the existing interruption pipeline,
        // which clears the pending projectile, ranged release timer, and Animator
        // hold via CancelAttack.
        attacker.CancelCurrentAction();
        return true;
    }
    void BeginFormationRangedAttack(
        SoldierController attacker,
        SoldierController target,
        WeaponProfile weaponProfile,
        RangedCombatStats rangedStats)
    {
        if (attacker == null || target == null)
            return;

        float rangedHoldTime = weaponProfile != null
            ? Mathf.Max(0f, weaponProfile.animationRangedAttackHoldTime)
            : 0f;

        formationRangedReleaseTimers[attacker] = rangedHoldTime;

        if (weaponProfile != null && rangedStats.projectilePrefab != null)
        {
            formationPendingProjectileTargets[attacker] = target;
            formationPendingProjectileWeapons[attacker] = weaponProfile;
            return;
        }

        if (!TryConsumeRangedAmmunition())
            return;

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

        int appliedDamage = target.Health.TakeDamage(
            damageResult.normalDamage,
            damageResult.armorPiercingDamage);

        if (appliedDamage > 0)
            GameEvents.CombatDamageDealt(attacker, target, appliedDamage);

        if (target.IsAlive)
            target.TryBeginAction(SoldierActionState.HitReact);
    }
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

        if (!TryConsumeRangedAmmunition())
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
    void ClearPendingProjectile(SoldierController soldier)
    {
        if (soldier == null)
            return;

        formationPendingProjectileTargets.Remove(soldier);
        formationPendingProjectileWeapons.Remove(soldier);
        formationRangedReleaseTimers.Remove(soldier);
    }
    bool IsAuthoredRangedSquad()
    {
        return ResolveCombatStyle() == SquadCombatStyle.RangedLine;
    }
    bool IsRangedCombatStyle()
    {
        return currentCombatStyle == SquadCombatStyle.RangedLine;
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

        if (IsRangedCombatStyle() &&
            data.soldierData.rangedWeaponProfile != null)
        {
            return data.soldierData.rangedWeaponProfile;
        }

        return data.soldierData.meleeWeaponProfile != null
            ? data.soldierData.meleeWeaponProfile
            : data.soldierData.rangedWeaponProfile;
    }
    void InitializeRangedAmmunition()
    {
        RefreshRangedAmmunitionCapacity(refill: true);
    }
    public void RefreshRangedAmmunitionCapacity(bool refill = false)
    {
        int previousMaximum = maximumRangedAmmunition;
        int previousCurrent = currentRangedAmmunition;

        maximumRangedAmmunition = CalculateMaximumRangedAmmunition();

        if (maximumRangedAmmunition < 0)
        {
            currentRangedAmmunition = -1;
            return;
        }

        if (refill || previousMaximum < 0)
        {
            currentRangedAmmunition = maximumRangedAmmunition;
            return;
        }

        currentRangedAmmunition = Mathf.Clamp(
            previousCurrent,
            0,
            maximumRangedAmmunition);
    }
    int CalculateMaximumRangedAmmunition()
    {
        if (!IsAuthoredRangedSquad() || rangedAmmunitionStartingSoldierCount <= 0)
            return 0;

        WeaponProfile rangedWeapon = GetSquadRangedWeaponProfile();

        if (rangedWeapon == null)
            return 0;

        int ammunitionPerSoldier = rangedWeapon.ranged.ammunition;

        if (roster != null)
        {
            foreach (SoldierController soldier in roster.Soldiers)
            {
                if (soldier == null || !soldier.HasRangedWeapon || soldier.Stats == null)
                    continue;

                ammunitionPerSoldier = soldier.Stats.ranged.ammunition;
                break;
            }
        }

        if (ammunitionPerSoldier < 0)
            return -1;

        return rangedAmmunitionStartingSoldierCount *
               Mathf.Max(0, ammunitionPerSoldier);
    }
    int CountLivingRangedSoldiers()
    {
        if (!IsAuthoredRangedSquad() || roster == null)
            return 0;

        int count = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier != null && soldier.IsAlive && soldier.HasRangedWeapon)
                count++;
        }

        return count;
    }
    bool TryConsumeRangedAmmunition(int amount = 1)
    {
        amount = Mathf.Max(0, amount);

        if (!HasRangedAmmunition)
            return false;

        if (HasUnlimitedRangedAmmunition || amount == 0)
            return true;

        if (currentRangedAmmunition < amount)
            return false;

        currentRangedAmmunition -= amount;
        return true;
    }
    bool TickFormationRangedSetup()
    {
        if (!IsRangedCombatStyle())
            return true;

        if (formation == null || roster == null || targetSquad == null)
            return false;

        if (!TryGetLivingSoldierCenter(
                targetSquad.Roster,
                out Vector3 targetCenter))
        {
            return false;
        }

        Vector3 desiredFacing = targetCenter - transform.position;
        desiredFacing.y = 0f;

        if (desiredFacing.sqrMagnitude <= 0.0001f)
            desiredFacing = formation.Facing;

        if (desiredFacing.sqrMagnitude <= 0.0001f)
            desiredFacing = Vector3.forward;

        desiredFacing.Normalize();

        float facingError = Vector3.Angle(
            formation.Facing,
            desiredFacing);

        float halfFiringArc = GetSquadRangedAttackArc() * 0.5f;
        bool targetOutsideFiringArc = facingError > halfFiringArc;

        // Sustained ranged fire is intentionally stable. Small target-center shifts
        // and casualties do not force the squad back through formation setup. Only
        // a real firing-arc failure starts a deliberate reface/setup cycle.
        if (!formationRangedSetupRequired && !targetOutsideFiringArc)
            return true;

        if (!formationRangedSetupRequired && targetOutsideFiringArc)
        {
            // Major refacing is a meaningful formation event, so this is an
            // appropriate time to compact around the current survivors.
            formation.Rebuild();
            formationRangedSetupRequired = true;
            formationRangedSetupInitialized = false;
            formationRangedInitialFireSettleTimer =
                squadCombatProfile.formationRangedInitialFireSettleTime;
        }

        if (!formationRangedSetupInitialized)
        {
            // Pick the new facing and nearest slot assignment ONCE. CurrentSlots then
            // stay fixed for this setup cycle so root-following cannot move the
            // soldiers' goalposts while they are trying to settle.
            formation.ReassignLivingSoldiersToNearestSlots(
                transform.position,
                desiredFacing);

            formationRangedSetupInitialized = true;
        }

        if (!IsFormationRangedSetupReady())
        {
            MoveRangedSquadTowardFormationSlots(targetCenter);
            return false;
        }

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive || soldier.IsMovementLocked)
                continue;

            soldier.Stop();
        }

        if (formationRangedInitialFireSettleTimer > 0f)
        {
            formationRangedInitialFireSettleTimer -= Time.deltaTime;
            return false;
        }

        formationRangedSetupRequired = false;
        formationRangedSetupInitialized = false;
        return true;
    }
    bool IsFormationRangedSetupReady()
    {
        if (formation == null || roster == null)
            return false;

        IReadOnlyList<Vector3> slots = formation.CurrentSlots;

        if (slots == null || slots.Count == 0)
            return true;

        int livingCount = 0;
        int readyCount = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            int slotIndex = soldier.SlotIndex;

            if (slotIndex < 0 || slotIndex >= slots.Count)
                continue;

            livingCount++;

            float slotDistance = Vector3.Distance(
                Flatten(soldier.transform.position),
                Flatten(slots[slotIndex]));

            if (slotDistance <= squadCombatProfile.formationRangedSetupSlotDistance)
                readyCount++;
        }

        if (livingCount <= 0)
            return false;

        float readyRatio = (float)readyCount / livingCount;

        return readyRatio >=
               squadCombatProfile.formationRangedSetupRequiredRatio;
    }
    void MoveRangedSquadTowardFormationSlots(Vector3 targetCenter)
    {
        if (formation == null || roster == null)
            return;

        IReadOnlyList<Vector3> slots = formation.CurrentSlots;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null ||
                !soldier.IsAlive ||
                soldier.IsMovementLocked)
            {
                continue;
            }

            int slotIndex = soldier.SlotIndex;

            if (slotIndex < 0 || slotIndex >= slots.Count)
                continue;

            soldier.SetCombatRole(SoldierRole.Ranged);
            soldier.MoveToSlot(
                slots[slotIndex],
                0.05f,
                0.1f,
                squadCombatProfile.formationRangedSetupMoveSpeedMultiplier);

            soldier.FaceToward(
                targetCenter,
                soldier.Stats != null
                    ? soldier.Stats.movement.turnSpeed
                    : soldier.Data.movement.turnSpeed,
                false);
        }
    }
    bool IsTargetSquadWithinRangedFiringArc(float attackArc)
    {
        if (formation == null || targetSquad == null)
            return false;

        if (!TryGetLivingSoldierCenter(
                targetSquad.Roster,
                out Vector3 targetCenter))
        {
            return false;
        }

        Vector3 toTarget = targetCenter - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
            return true;

        Vector3 formationFacing = formation.Facing;
        formationFacing.y = 0f;

        if (formationFacing.sqrMagnitude <= 0.0001f)
            formationFacing = transform.forward;

        formationFacing.y = 0f;

        if (formationFacing.sqrMagnitude <= 0.0001f)
            formationFacing = Vector3.forward;

        float halfArc = Mathf.Clamp(attackArc, 1f, 360f) * 0.5f;
        float angleToTarget = Vector3.Angle(
            formationFacing.normalized,
            toTarget.normalized);

        return angleToTarget <= halfArc;
    }
    float GetSquadRangedAttackArc()
    {
        WeaponProfile rangedWeapon = GetSquadRangedWeaponProfile();

        return rangedWeapon != null
            ? Mathf.Clamp(rangedWeapon.ranged.attackRangeArc, 1f, 360f)
            : 360f;
    }
    WeaponProfile GetSquadRangedWeaponProfile()
    {
        if (roster != null)
        {
            foreach (SoldierController soldier in roster.Soldiers)
            {
                if (soldier != null &&
                    soldier.IsAlive &&
                    soldier.RangedWeaponProfile != null)
                {
                    return soldier.RangedWeaponProfile;
                }
            }
        }

        return data != null && data.soldierData != null
            ? data.soldierData.rangedWeaponProfile
            : null;
    }
    void DesynchronizeFormationAttackTimersForMeleeFallback()
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            WeaponProfile meleeWeapon =
                soldier.MeleeWeaponProfile;

            float meleeInterval =
                meleeWeapon != null
                    ? Mathf.Max(
                        0.05f,
                        soldier.Stats != null
                            ? soldier.Stats.melee.attackInterval
                            : meleeWeapon.melee.attackInterval)
                    : Mathf.Max(
                        0.05f,
                        squadCombatProfile.formationFallbackMeleeAttackInterval);

            float randomOffset = Random.Range(
                0f,
                Mathf.Max(
                    0.10f,
                    meleeInterval * 0.35f));

            formationAttackTimers[soldier] = randomOffset;
        }
    }
    void UpdateFormationSquadCombatMode()
    {
        bool isRangedSquad =
            IsAuthoredRangedSquad() &&
            HasLivingRangedWeapon();

        if (!isRangedSquad)
        {
            formationRangedSquadUsingMeleeFallback = false;
            currentCombatStyle = SquadCombatStyle.FormationCombat;
            SetFormationSquadWeaponMode(useRangedWeapon: false);
            return;
        }

        bool hasRangedAmmunition = HasLivingRangedAmmunition();
        bool hasMeleeFallback = HasLivingMeleeWeapon();

        if (!hasRangedAmmunition)
        {
            bool enteringMeleeFallback =
                hasMeleeFallback &&
                !formationRangedSquadUsingMeleeFallback;

            formationRangedSquadUsingMeleeFallback = hasMeleeFallback;
            currentCombatStyle = SquadCombatStyle.FormationCombat;
            SetFormationSquadWeaponMode(useRangedWeapon: false);

            if (enteringMeleeFallback)
                DesynchronizeFormationAttackTimersForMeleeFallback();

            return;
        }

        if (!squadCombatProfile.formationRangedMeleeFallbackEnabled ||
            !hasMeleeFallback)
        {
            formationRangedSquadUsingMeleeFallback = false;
            currentCombatStyle = SquadCombatStyle.RangedLine;
            SetFormationSquadWeaponMode(useRangedWeapon: true);
            return;
        }

        float meleeFallbackEnterDistance = Mathf.Max(
            GetSquadRangedMinimumRange(),
            squadCombatProfile.formationRangedMeleeFallbackEnterDistance);

        float meleeFallbackExitDistance = Mathf.Max(
            meleeFallbackEnterDistance,
            squadCombatProfile.formationRangedMeleeFallbackExitDistance);

        if (formationRangedSquadUsingMeleeFallback)
        {
            if (!IsAnyLivingSquadMemberThreatenedWithin(meleeFallbackExitDistance))
            {
                formationRangedSquadUsingMeleeFallback = false;
                currentCombatStyle = SquadCombatStyle.RangedLine;
                SetFormationSquadWeaponMode(useRangedWeapon: true);
            }

            return;
        }

        if (IsAnyLivingSquadMemberThreatenedWithin(meleeFallbackEnterDistance))
        {
            formationRangedSquadUsingMeleeFallback = true;
            currentCombatStyle = SquadCombatStyle.FormationCombat;
            SetFormationSquadWeaponMode(useRangedWeapon: false);
            DesynchronizeFormationAttackTimersForMeleeFallback();
            return;
        }

        currentCombatStyle = SquadCombatStyle.RangedLine;
        SetFormationSquadWeaponMode(useRangedWeapon: true);
    }
    void SetFormationSquadWeaponMode(bool useRangedWeapon)
    {
        if (roster == null)
            return;

        foreach (SoldierController squadMember in roster.Soldiers)
        {
            if (squadMember == null || !squadMember.IsAlive)
                continue;

            WeaponProfile desiredWeapon =
                ResolveFormationWeaponForMode(
                    squadMember,
                    useRangedWeapon);

            // SoldierController owns the atomic transition:
            // cancel any Attack authored for the old weapon, let the normal
            // interruption pipeline clear pending combat state, then switch the
            // active weapon/controller/visuals as one operation.
            squadMember.SetActiveWeaponProfile(desiredWeapon);
        }
    }
    WeaponProfile ResolveFormationWeaponForMode(
        SoldierController squadMember,
        bool useRangedWeapon)
    {
        if (squadMember == null)
            return null;

        if (useRangedWeapon)
        {
            if (squadMember.HasRangedWeapon)
                return squadMember.RangedWeaponProfile;

            if (squadMember.HasMeleeWeapon)
                return squadMember.MeleeWeaponProfile;

            return null;
        }

        return squadMember.HasMeleeWeapon
            ? squadMember.MeleeWeaponProfile
            : null;
    }
    bool HasLivingMeleeWeapon()
    {
        if (roster == null)
            return false;

        foreach (SoldierController squadMember in roster.Soldiers)
        {
            if (squadMember != null &&
                squadMember.IsAlive &&
                squadMember.HasMeleeWeapon)
            {
                return true;
            }
        }

        return false;
    }
    bool HasLivingRangedWeapon()
    {
        if (roster == null)
            return false;

        foreach (SoldierController squadMember in roster.Soldiers)
        {
            if (squadMember != null &&
                squadMember.IsAlive &&
                squadMember.HasRangedWeapon)
            {
                return true;
            }
        }

        return false;
    }
    bool HasLivingRangedAmmunition()
    {
        return HasRangedAmmunition;
    }
    float GetSquadRangedMinimumRange()
    {
        if (roster == null)
            return 0f;

        float minimumRange = 0f;

        foreach (SoldierController squadMember in roster.Soldiers)
        {
            if (squadMember == null ||
                !squadMember.IsAlive ||
                !squadMember.HasRangedWeapon)
            {
                continue;
            }

            float memberMinimumRange = squadMember.Stats != null
                ? squadMember.Stats.ranged.minimumRange
                : squadMember.RangedWeaponProfile.ranged.minimumRange;

            minimumRange = Mathf.Max(
                minimumRange,
                Mathf.Max(0f, memberMinimumRange));
        }

        return minimumRange;
    }
    bool IsAnyLivingSquadMemberThreatenedWithin(float distance)
    {
        if (roster == null)
            return false;

        float clampedDistance = Mathf.Max(0f, distance);
        float distanceSqr = clampedDistance * clampedDistance;

        if (squadCombatProfile.formationMultiSquadLocalTargetingEnabled &&
            SquadManager.Instance != null)
        {
            foreach (SquadController candidateSquad in SquadManager.Instance.Squads)
            {
                if (!CanAttack(candidateSquad))
                    continue;

                if (IsAnyLivingSquadMemberThreatenedBySquad(
                        candidateSquad,
                        distanceSqr))
                {
                    return true;
                }
            }

            return false;
        }

        return IsAnyLivingSquadMemberThreatenedBySquad(
            targetSquad,
            distanceSqr);
    }
    bool IsAnyLivingSquadMemberThreatenedBySquad(
        SquadController enemySquad,
        float distanceSqr)
    {
        if (roster == null ||
            enemySquad == null ||
            enemySquad.Roster == null)
        {
            return false;
        }

        foreach (SoldierController squadMember in roster.Soldiers)
        {
            if (squadMember == null || !squadMember.IsAlive)
                continue;

            Vector3 squadMemberPosition =
                Flatten(squadMember.transform.position);

            foreach (SoldierController enemy in enemySquad.Roster.Soldiers)
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;

                float enemyDistanceSqr = Vector3.SqrMagnitude(
                    squadMemberPosition -
                    Flatten(enemy.transform.position));

                if (enemyDistanceSqr <= distanceSqr)
                    return true;
            }
        }

        return false;
    }

    #endregion
}
