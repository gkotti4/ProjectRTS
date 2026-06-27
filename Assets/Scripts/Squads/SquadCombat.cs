using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// SquadCombat
/// -----------------------------------------------------------------------------
///
/// Squad-level combat coordinator.
/// Handles attack orders, auto-scan, approaching enemy squads, beginning/ending
/// engagements, assigning combat homes, calculating pressure goals, managing
/// frontline/support participation, and ticking each SoldierCombat with a local
/// combat context.
///
/// This class should decide squad engagement context, not individual attack
/// results or animation playback.
///
/// Design role:
/// Converts "this squad is fighting that squad" into per-soldier combat context.
///
public class SquadCombat : MonoBehaviour
{
    #region Fields

    // -----------------------------------------------------------------------------
    // Profile Reference
    // -----------------------------------------------------------------------------
    // SquadCombatProfile is the single source of truth for designer-tunable
    // combat values. Runtime fields below are only for actual combat state.
    private bool hasLoggedMissingCombatProfile = false;

    // -----------------------------------------------------------------------------
    // Runtime Collections
    // -----------------------------------------------------------------------------
    private readonly Dictionary<SoldierController, Vector3> combatHomePositions =
        new Dictionary<SoldierController, Vector3>();

    private readonly HashSet<SoldierController> activeCombatSoldiers =
        new HashSet<SoldierController>();

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
    private SquadCombatStyle currentCombatStyle = SquadCombatStyle.MeleeLine;
    private SquadEngagementType currentEngagementType = SquadEngagementType.None;

    // -----------------------------------------------------------------------------
    // Runtime Timers
    // -----------------------------------------------------------------------------
    private float scanTimer = 0f;
    private float approachRefreshTimer = 0f;
    private float targetRefreshTimer = 0f;

    // -----------------------------------------------------------------------------
    // Public Read-Only Access
    // -----------------------------------------------------------------------------
    public SquadController TargetSquad => targetSquad;
    public SquadCombatStyle CurrentCombatStyle => currentCombatStyle;
    public SquadEngagementType CurrentEngagementType => currentEngagementType;

    #endregion

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
        currentEngagementType = SquadEngagementType.None;

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

    /// Receives an explicit attack order.
    /// MeleeLine squads approach to contact. RangedLine squads approach to missile range.
    public void OrderAttack(SquadController target)
    {
        OrderAttack(target, SquadEngagementType.ExplicitAttack);
    }

    /// Starts an attack-like engagement from either an explicit command or auto-scan.
    void OrderAttack(
        SquadController target,
        SquadEngagementType engagementType)
    {
        if (!HasCombatProfile())
            return;

        if (!CanAttack(target))
            return;

        targetSquad = target;
        currentCombatStyle = ResolveCombatStyle();
        currentEngagementType = engagementType;
        approachRefreshTimer = 0f;
        targetRefreshTimer = 0f;

        if (IsCloseEnoughToStartEngagement(targetSquad))
        {
            BeginEngagement(notifyTarget: true);
            return;
        }

        BeginApproachingCombat();
    }

    /// Called by the enemy squad when this squad has entered melee range.
    /// This lets the defender fight back without requiring auto-scan.
    public void ReceiveEngagementRequest(SquadController attacker)
    {
        if (!HasCombatProfile())
            return;

        if (!CanRespondToEngagement(attacker))
            return;

        targetSquad = attacker;
        currentCombatStyle = ResolveCombatStyle();
        currentEngagementType = squad != null && squad.Stance == SquadStance.Hold
            ? SquadEngagementType.DefensiveHold
            : SquadEngagementType.PassiveContact;
        targetRefreshTimer = 0f;

        if (!IsCloseEnoughToStartEngagement(targetSquad))
            return;

        BeginEngagement(notifyTarget: false);
    }

    /// Clears squad-level and soldier-level combat state.
    public void ClearTargets()
    {
        targetSquad = null;
        currentEngagementType = SquadEngagementType.None;
        activeCombatSoldiers.Clear();
        combatHomePositions.Clear();

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

    /// Moves toward the current attack target until close enough to enter melee.
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

        approachRefreshTimer = squadCombatProfile.approachRefreshInterval;

        MoveTowardCombatTarget();
    }

    /// Ticks active squad melee engagement.
    /// In combat, normal formation slot following is paused.
    /// Soldiers receive a combat context and then run local priority behavior.
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

        combatContactDirection = GetContactDirection();

        targetRefreshTimer -= Time.deltaTime;

        if (targetRefreshTimer <= 0f)
        {
            targetRefreshTimer = squadCombatProfile.targetRefreshInterval;
            RefreshCombatEligibleSoldiers();
        }

        TickCombatHomeSoldiers();
    }

    /// Ticks auto-scan and starts an attack order if a valid target is found.
    void TickScan()
    {
        scanTimer -= Time.deltaTime;

        if (scanTimer > 0f)
            return;

        scanTimer = squadCombatProfile.scanInterval;

        if (TryFindTarget(out SquadController target))
        {
            SquadEngagementType scanEngagementType = squad != null && squad.State == SquadState.AttackMoving
                ? SquadEngagementType.AttackMoveContact
                : SquadEngagementType.PassiveContact;

            OrderAttack(target, scanEngagementType);
        }
    }

    /// Enters the intermediate attack-approach state.
    void BeginApproachingCombat()
    {
        ClearSoldierCombatStates();

        squad.SetState(SquadState.ApproachingCombat);

        MoveTowardCombatTarget();
    }

    /// Starts melee engagement for this squad.
    /// The target is optionally notified so it also enters combat.
    /// Combat homes are remembered as cohesion references, not strict reserve slots.
    void BeginEngagement(bool notifyTarget)
    {
        if (targetSquad == null)
            return;

        movement.OrderStop();

        currentCombatStyle = ResolveCombatStyle();
        combatContactDirection = GetContactDirection();

        BuildCombatHomePositions();
        RefreshCombatEligibleSoldiers();

        targetRefreshTimer = 0f;

        squad.SetState(SquadState.InCombat);

        if (notifyTarget && targetSquad.Combat != null)
            targetSquad.Combat.ReceiveEngagementRequest(squad);
    }

    /// Moves the squad formation toward a point near the target squad.
    /// This is approach movement only, not melee pressure movement.
    void MoveTowardCombatTarget()
    {
        if (targetSquad == null)
            return;

        Vector3 fromTargetToMe =
            transform.position - targetSquad.transform.position;

        fromTargetToMe.y = 0f;

        if (fromTargetToMe == Vector3.zero)
            fromTargetToMe = -targetSquad.transform.forward;

        fromTargetToMe.Normalize();

        Vector3 approachPoint =
            targetSquad.transform.position +
            fromTargetToMe * GetEffectiveApproachStopDistance();

        Vector3 facing = -fromTargetToMe;

        movement.OrderMove(
            approachPoint,
            facing);
    }

    /// Marks every living soldier as a combat participant.
    /// No hard reserves: backline soldiers may fail to find a target and will
    /// instead press/hold through their local priority behavior.
    void RefreshCombatEligibleSoldiers()
    {
        activeCombatSoldiers.Clear();

        if (targetSquad == null || roster == null)
            return;

        if (combatHomePositions.Count == 0)
            BuildCombatHomePositions();

        GetCombatHomeFrontScoreRange(
            out float rearScore,
            out float frontScore);

        bool isRangedLine = IsRangedCombatStyle();

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            bool wasAlreadyInCombatRole =
                soldier.Role == SoldierRole.Frontline ||
                soldier.Role == SoldierRole.Support ||
                soldier.Role == SoldierRole.Replacing ||
                soldier.Role == SoldierRole.Ranged;

            Vector3 home = GetCombatHomeForSoldier(soldier);
            float frontness = GetCombatHomeFrontness(home, rearScore, frontScore);

            if (isRangedLine)
            {
                soldier.SetCombatRole(SoldierRole.Ranged);
            }
            else
            {
                soldier.SetCombatRole(
                    frontness >= 0.55f
                        ? SoldierRole.Frontline
                        : SoldierRole.Support);
            }

            if (!wasAlreadyInCombatRole)
                soldier.Combat?.RandomizeInitialAttackTimer(0.75f);

            activeCombatSoldiers.Add(soldier);
        }
    }

    /// Ticks every living soldier during active combat.
    /// MeleeLine soldiers press toward contact. RangedLine soldiers hold a fire line
    /// and only step forward enough to get a legal shot.
    void TickCombatHomeSoldiers()
    {
        if (targetSquad == null || roster == null)
            return;

        currentCombatStyle = ResolveCombatStyle();
        bool isRangedLine = IsRangedCombatStyle();

        GetCombatHomeFrontScoreRange(
            out float rearScore,
            out float frontScore);

        int activeEngagementBudget = isRangedLine ? int.MaxValue : GetActiveEngagementBudget();
        int activeEngagementCount = isRangedLine ? 0 : CountActiveAttackers();

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            if (soldier.Combat == null)
                continue;

            Vector3 home = GetCombatHomeForSoldier(soldier);
            float frontness = GetCombatHomeFrontness(home, rearScore, frontScore);

            float freeEngageDistance = isRangedLine
                ? squadCombatProfile.rangedSoldierFreeEngageDistance
                : GetFreeEngageDistance(frontness);

            float disengageDistance = freeEngageDistance + squadCombatProfile.combatDisengageExtraDistance;
            float forceRejoinDistance = freeEngageDistance + squadCombatProfile.combatForceRejoinExtraDistance;

            Vector3 pressureGoal = GetPressureGoalForSoldier(
                home,
                freeEngageDistance,
                isRangedLine);

            bool canStartNewEngagement = isRangedLine ||
                CanSoldierStartNewEngagement(
                    soldier,
                    frontness,
                    activeEngagementCount,
                    activeEngagementBudget);

            if (!isRangedLine && canStartNewEngagement && !soldier.Combat.HasTarget)
                activeEngagementCount++;

            soldier.Combat.TickCombat(
                targetSquad,
                home,
                pressureGoal,
                freeEngageDistance,
                disengageDistance,
                forceRejoinDistance,
                squadCombatProfile.soldierLocalTargetScanRange,
                squadCombatProfile.combatMoveSpeedMultiplier,
                isRangedLine
                    ? squadCombatProfile.rangedPressureStoppingDistance
                    : squadCombatProfile.combatPressureStoppingDistance,
                canStartNewEngagement,
                isRangedLine ? GetEffectivePreferredEngagementRange() : -1f);
        }
    }

    int GetActiveEngagementBudget()
    {
        if (!squadCombatProfile.useSoftEngagementBudget)
            return int.MaxValue;

        int livingCount = roster != null ? roster.LivingCount : 0;

        if (livingCount <= 0)
            return 0;

        int ratioCount = Mathf.CeilToInt(
            livingCount * Mathf.Clamp01(squadCombatProfile.activeEngagementRatio));

        return Mathf.Clamp(
            Mathf.Max(squadCombatProfile.activeEngagementMinCount, ratioCount),
            1,
            livingCount);
    }

    int CountActiveAttackers()
    {
        if (roster == null)
            return 0;

        int count = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive || soldier.Combat == null)
                continue;

            if (soldier.Combat.IsActiveAttacker)
                count++;
        }

        return count;
    }

    bool CanSoldierStartNewEngagement(
        SoldierController soldier,
        float frontness,
        int activeEngagementCount,
        int activeEngagementBudget)
    {
        if (!squadCombatProfile.useSoftEngagementBudget)
            return true;

        if (soldier == null || soldier.Combat == null)
            return false;

        // Existing combat locks are allowed to finish their rhythm.
        // The budget only gates fresh target acquisition.
        if (soldier.Combat.HasTarget)
            return true;

        if (activeEngagementCount < activeEngagementBudget)
            return true;

        bool isFrontlineHome = frontness >= squadCombatProfile.activeEngagementFrontlineThreshold;

        if (!isFrontlineHome)
            return false;

        return activeEngagementCount <
               activeEngagementBudget + Mathf.Max(0, squadCombatProfile.activeEngagementFrontlineOverflow);
    }

    /// Freezes each living soldier's combat home at engagement start.
    /// These homes are cohesion references. They are not strict slots during combat.
    void BuildCombatHomePositions()
    {
        combatHomePositions.Clear();

        if (roster == null)
            return;

        formation.UpdateSlots(
            transform.position,
            movement.DesiredFacing);

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            Vector3 home;

            if (formation.TryGetSlotForSoldier(soldier, out home))
                combatHomePositions[soldier] = home;
            else
                combatHomePositions[soldier] = soldier.transform.position;
        }
    }

    /// Gets a soldier's combat home / cohesion origin.
    Vector3 GetCombatHomeForSoldier(SoldierController soldier)
    {
        if (soldier == null)
            return transform.position;

        if (combatHomePositions.TryGetValue(soldier, out Vector3 home))
            return home;

        return soldier.transform.position;
    }

    /// Gets the current direction from this squad toward the target squad.
    Vector3 GetContactDirection()
    {
        if (targetSquad == null)
            return movement != null ? movement.DesiredFacing : transform.forward;

        Vector3 direction = targetSquad.transform.position - transform.position;
        direction.y = 0f;

        if (direction == Vector3.zero)
            direction = movement != null ? movement.DesiredFacing : transform.forward;

        direction.y = 0f;

        if (direction == Vector3.zero)
            return Vector3.forward;

        return direction.normalized;
    }

    /// Finds rear/front scores across combat homes along the contact direction.
    void GetCombatHomeFrontScoreRange(
        out float rearScore,
        out float frontScore)
    {
        rearScore = float.PositiveInfinity;
        frontScore = float.NegativeInfinity;

        if (combatHomePositions.Count == 0)
        {
            rearScore = 0f;
            frontScore = 0f;
            return;
        }

        foreach (KeyValuePair<SoldierController, Vector3> pair in combatHomePositions)
        {
            SoldierController soldier = pair.Key;

            if (soldier == null || !soldier.IsAlive)
                continue;

            float score = Vector3.Dot(
                pair.Value - transform.position,
                combatContactDirection);

            if (score < rearScore)
                rearScore = score;

            if (score > frontScore)
                frontScore = score;
        }

        if (float.IsPositiveInfinity(rearScore) || float.IsPositiveInfinity(frontScore))
        {
            rearScore = 0f;
            frontScore = 0f;
        }
    }

    /// Returns 0 for rearmost combat home and 1 for frontmost combat home.
    float GetCombatHomeFrontness(
        Vector3 home,
        float rearScore,
        float frontScore)
    {
        float range = frontScore - rearScore;

        if (range <= 0.01f)
            return 1f;

        float score = Vector3.Dot(
            home - transform.position,
            combatContactDirection);

        return Mathf.Clamp01(
            Mathf.InverseLerp(rearScore, frontScore, score));
    }

    /// ENGAGE allows more freedom. HOLD keeps the body tighter.
    float GetFreeEngageDistance(float frontness)
    {
        float distance = Mathf.Lerp(
            squadCombatProfile.combatRearFreeEngageDistance,
            squadCombatProfile.combatFrontFreeEngageDistance,
            frontness);

        switch (squad.Stance)
        {
            case SquadStance.Hold:
                return distance * 0.65f;

            default:
                return distance;
        }
    }

    /// Builds a pressure goal that keeps the soldier's original lateral spread
    /// because it starts from the individual combat home, not the squad center.
    Vector3 GetPressureGoalForSoldier(
        Vector3 home,
        float freeEngageDistance,
        bool isRangedLine = false)
    {
        float basePressureDistance = isRangedLine
            ? squadCombatProfile.rangedPressureDistance
            : squadCombatProfile.combatPressureDistance * GetPressureMultiplier();

        float pressureDistance = Mathf.Min(
            basePressureDistance,
            Mathf.Max(0f, freeEngageDistance));

        return home + combatContactDirection * pressureDistance;
    }

    float GetPressureMultiplier()
    {
        switch (squad.Stance)
        {
            case SquadStance.Hold:
                return 0.65f;

            default:
                return 1f;
        }
    }

    /// Clears local combat state on every soldier in this squad.
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

            if (soldier.Combat != null)
                soldier.Combat.ClearCombat();
        }
    }

    /// Checks whether this squad can start an attack against a target.
    bool CanAttack(SquadController target)
    {
        if (target == null)
            return false;

        if (target == squad)
            return false;

        if (!target.IsInitialized)
            return false;

        if (target.Roster == null || !target.Roster.HasLivingSoldiers)
            return false;

        if (squad == null || !squad.IsInitialized)
            return false;

        if (roster == null || !roster.HasLivingSoldiers)
            return false;

        if (squad.Faction == null || target.Faction == null)
            return false;

        if (squad.Faction.teamId == target.Faction.teamId)
            return false;

        // if (squad.Stance == SquadStance.NoAttack)
        //     return false;

        return true;
    }

    /// Checks whether this squad can respond to a nearby melee engagement.
    /// This intentionally ignores NoAttack, because NoAttack means "do not initiate"
    /// for now, not "stand still while being killed."
    bool CanRespondToEngagement(SquadController attacker)
    {
        if (attacker == null)
            return false;

        if (attacker == squad)
            return false;

        if (!attacker.IsInitialized)
            return false;

        if (attacker.Roster == null || !attacker.Roster.HasLivingSoldiers)
            return false;

        if (squad == null || !squad.IsInitialized)
            return false;

        if (roster == null || !roster.HasLivingSoldiers)
            return false;

        if (squad.Faction == null || attacker.Faction == null)
            return false;

        if (squad.Faction.teamId == attacker.Faction.teamId)
            return false;

        return true;
    }

    /// Checks whether squads are close enough to begin combat.
    /// MeleeLine uses authored tactical contact ranges. RangedLine derives this
    /// from WeaponProfile.ranged.attackRange so archers stop at missile range.
    bool IsCloseEnoughToStartEngagement(SquadController target)
    {
        if (target == null)
            return false;

        return Vector3.Distance(
            transform.position,
            target.transform.position) <= GetEffectiveCombatStartRange();
    }

    /// Checks whether squads have drifted too far apart to remain in combat.
    bool IsWithinCombatBreakRange(SquadController target)
    {
        if (target == null)
            return false;

        return Vector3.Distance(
            transform.position,
            target.transform.position) <= GetEffectiveCombatBreakRange();
    }

    /// Returns whether this squad should auto-scan for enemies.
    bool ShouldScan()
    {
        if (!squadCombatProfile.autoScanEnabled)
            return false;

        if (squad == null)
            return false;

        // if (squad.Stance == SquadStance.NoAttack)
        //     return false;

        if (roster == null || !roster.HasLivingSoldiers)
            return false;

        return true;
    }

    /// Finds the closest valid enemy squad inside this squad's scan range.
    bool TryFindTarget(out SquadController bestTarget)
    {
        bestTarget = null;

        if (SquadManager.Instance == null)
            return false;

        float range = GetScanRange();

        if (range <= 0f)
            return false;

        float bestDistance = range * range;

        foreach (SquadController candidate in SquadManager.Instance.Squads)
        {
            if (!CanAttack(candidate))
                continue;

            float distance = Vector3.SqrMagnitude(
                candidate.transform.position - transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = candidate;
            }
        }

        return bestTarget != null;
    }

    /// Gets stance-based auto-scan range.
    float GetScanRange()
    {
        return GetEffectiveScanRange();
    }

    SquadCombatStyle ResolveCombatStyle()
    {
        if (data == null)
            return SquadCombatStyle.MeleeLine;

        if (data.combatStyle != SquadCombatStyle.MeleeLine)
            return data.combatStyle;

        WeaponProfile weaponProfile = GetSquadWeaponProfile();

        if (weaponProfile != null && weaponProfile.weaponKind == WeaponKind.Ranged)
            return SquadCombatStyle.RangedLine;

        if (data.category == SquadCategory.Ranged)
            return SquadCombatStyle.RangedLine;

        return SquadCombatStyle.MeleeLine;
    }

    bool IsRangedCombatStyle()
    {
        return ResolveCombatStyle() == SquadCombatStyle.RangedLine;
    }

    float GetEffectiveScanRange()
    {
        if (squad == null || squadCombatProfile == null)
            return 0f;

        float baseRange = 0f;

        switch (squad.Stance)
        {
            case SquadStance.Engage:
                baseRange = squadCombatProfile.engageStanceScanRange;
                break;

            case SquadStance.Hold:
                baseRange = squadCombatProfile.holdStanceScanRange;
                break;
        }

        if (!IsRangedCombatStyle() || !squadCombatProfile.rangedUseWeaponRangeForTacticalRanges)
            return baseRange;

        float padding = squad.Stance == SquadStance.Hold
            ? squadCombatProfile.rangedHoldScanRangePadding
            : squadCombatProfile.rangedEngageScanRangePadding;

        return Mathf.Max(
            baseRange,
            GetSquadWeaponAttackRange() + padding);
    }

    float GetEffectiveCombatStartRange()
    {
        if (!IsRangedCombatStyle() || !squadCombatProfile.rangedUseWeaponRangeForTacticalRanges)
            return Mathf.Max(0f, squadCombatProfile.combatStartRange);

        return Mathf.Max(
            0.1f,
            GetSquadWeaponAttackRange() * squadCombatProfile.rangedCombatStartRangeFactor);
    }

    float GetEffectivePreferredEngagementRange()
    {
        if (!IsRangedCombatStyle() || !squadCombatProfile.rangedUseWeaponRangeForTacticalRanges)
            return Mathf.Max(0f, squadCombatProfile.approachStopDistance);

        return Mathf.Max(
            0.1f,
            GetSquadWeaponAttackRange() * squadCombatProfile.rangedPreferredRangeFactor);
    }

    float GetEffectiveApproachStopDistance()
    {
        return GetEffectivePreferredEngagementRange();
    }

    float GetEffectiveCombatBreakRange()
    {
        if (!IsRangedCombatStyle() || !squadCombatProfile.rangedUseWeaponRangeForTacticalRanges)
            return Mathf.Max(
                squadCombatProfile.combatStartRange,
                squadCombatProfile.combatBreakRange);

        return Mathf.Max(
            GetEffectiveCombatStartRange(),
            GetSquadWeaponAttackRange() + squadCombatProfile.rangedCombatBreakRangePadding);
    }

    float GetSquadWeaponAttackRange()
    {
        WeaponProfile weaponProfile = GetSquadWeaponProfile();

        if (weaponProfile == null)
            return 1.5f;

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

    /// Ends combat and tells the squad to reform.
    /// Survivors return to the normal Line formation after combat.
    void EndCombatAndReform()
    {
        ClearTargets();

        approachRefreshTimer = 0f;
        targetRefreshTimer = 0f;
        scanTimer = 0f;

        if (squad == null || roster == null || !roster.HasLivingSoldiers)
            return;

        if (formation != null)
            formation.Rebuild();

        if (movement != null)
            movement.BeginReform(recenterFromSoldiers: true);

        squad.SetState(SquadState.Reforming);
    }
}
