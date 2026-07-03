using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "SquadCombatProfile_",
    menuName = "Scriptable Objects/Military/Profiles/SquadCombatProfile")]
public class SquadCombatProfile : ScriptableObject
{
    [Header("Auto Target Scanning - All Combat Styles")]
    [Tooltip("If true, this squad can automatically look for nearby enemy squads when not already fighting.")]
    public bool autoTargetScanEnabled = true;

    [Tooltip("How often this squad checks for nearby enemy squads while auto target scanning is enabled.")]
    [Min(0.01f)] public float autoTargetScanInterval = 0.35f;


    [Header("Auto Target Scan Range By Stance - All Combat Styles")]
    [Tooltip("How far this squad scans for enemy targets while in Engage stance.")]
    [Min(0f)] public float engageStanceAutoTargetScanRange = 14f;

    [Tooltip("How far this squad scans for enemy targets while in Hold stance. Usually smaller than Engage so the unit does not chase too aggressively.")]
    [Min(0f)] public float holdStanceAutoTargetScanRange = 8f;


    [Header("Approach And Combat Exit - Default / Fallback")]
    [Tooltip("Default distance at which this squad considers itself close enough to begin combat when no style-specific range overrides it.")]
    [Min(0f)] public float defaultCombatStartRange = 6f;

    [Tooltip("Default distance at which this squad breaks off from its current combat target if the target gets too far away.")]
    [Min(0f)] public float defaultCombatBreakRange = 10f;

    [Tooltip("How often this squad refreshes its approach destination while moving toward an enemy squad.")]
    [Min(0.01f)] public float combatApproachRefreshInterval = 0.25f;

    [Tooltip("Default stopping distance used when approaching an enemy squad before combat begins.")]
    [Min(0f)] public float defaultApproachStopDistance = 3f;


    [Header("Soft Engagement Budget")]
    [Tooltip("If true, FormationMelee limits how many soldiers can start new active engagements at once, helping prevent everyone from collapsing into the same target pile.")]
    public bool useSoftEngagementBudget = true;

    [Tooltip("Fraction of living soldiers that are allowed to actively start engagements at the same time when using the soft engagement budget.")]
    [Range(0f, 1f)] public float activeEngagementRatio = 0.58f;

    [Tooltip("Minimum number of soldiers allowed to actively engage, even if the ratio would produce a smaller number.")]
    [Min(0)] public int activeEngagementMinCount = 4;

    [Tooltip("Extra active engagement slots reserved for high-frontness soldiers so the front line can keep participating even when the budget is nearly full.")]
    [Min(0)] public int activeEngagementFrontlineOverflow = 2;

    [Tooltip("Frontness value required for a soldier to qualify for frontline overflow engagement slots.")]
    [Range(0f, 1f)] public float activeEngagementFrontlineThreshold = 0.7f;


    [Header("Soldier Combat Shared - All Combat Styles")]
    [Header("Engagement")]
    [Tooltip("Default local scan range used by individual soldiers when searching for nearby enemy soldiers.")]
    [Min(0f)] public float allStylesSoldierLocalTargetScanRange = 3f;

    [Tooltip("Movement speed multiplier used by soldiers during combat movement, pressure movement, and rejoining.")]
    [Min(0.1f)] public float allStylesSoldierCombatMoveSpeedMultiplier = 1.15f;

    [Tooltip("How often the squad refreshes its list of soldiers currently participating in combat.")]
    [Min(0.01f)] public float combatParticipantRefreshInterval = 0.35f;


    [Header("Formation Melee - Pressure / Cohesion")]
    [Tooltip("How far FormationMelee soldiers press forward from their assigned formation combat home.")]
    [Min(0f)] public float formationMeleePressureDistance = 2f;

    [Tooltip("How far rear/support FormationMelee soldiers are allowed to move from their combat home while engaging enemies.")]
    [Min(0f)] public float formationMeleeRearFreeEngageDistance = 1.25f;

    [Tooltip("How far frontline FormationMelee soldiers are allowed to move from their combat home while engaging enemies.")]
    [Min(0f)] public float formationMeleeFrontFreeEngageDistance = 3.25f;

    [Tooltip("Extra distance beyond free engage distance before a FormationMelee soldier starts disengaging from its local target.")]
    [Min(0f)] public float formationMeleeDisengageExtraDistance = 1.5f;

    [Tooltip("Extra distance beyond free engage distance before a FormationMelee soldier is forced to return toward its combat home.")]
    [Min(0f)] public float formationMeleeForceRejoinExtraDistance = 3.5f;

    [Tooltip("Stopping distance used when FormationMelee soldiers move toward their pressure goal.")]
    [Min(0f)] public float formationMeleePressureStoppingDistance = 0.15f;
    

    // [Header("Loose Melee - Pressure / Cohesion")]
    // [Tooltip("How far LooseMelee soldiers may operate from their personal dynamic combat anchor.")]
    // [Min(0f)] public float looseMeleeFreeEngageDistance = 8f;
    //
    // [Tooltip("Additional local target search range for LooseMelee. This is maxed with soldierLocalTargetScanRange.")]
    // [Min(0f)] public float looseMeleeTargetScanRange = 5f;
    //
    // [Tooltip("How far LooseMelee soldiers try to press forward from their dynamic combat anchor.")]
    // [Min(0f)] public float looseMeleePressureDistance = 4f;
    //
    // [Tooltip("Extra distance beyond loose free-engage distance before a soldier drops a stale target.")]
    // [Min(0f)] public float looseMeleeDisengageExtraDistance = 2.25f;
    //
    // [Tooltip("Maximum loose distance before a soldier without an immediate attack opportunity re-presses toward the fight.")]
    // [Min(0f)] public float looseMeleeForceRejoinDistance = 13f;
    //
    // [Tooltip("Stopping distance used when LooseMelee soldiers press toward the combat line.")]
    // [Min(0f)] public float looseMeleePressureStoppingDistance = 0.2f;
    //
    // [Tooltip("How far forward a soldier must progress before its dynamic loose anchor ratchets forward.")]
    // [Min(0f)] public float looseMeleeAnchorForwardUpdateDistance = 0.35f;
    //
    // [Tooltip("How much sideways drift is tolerated before the dynamic loose anchor starts following sideways pressure.")]
    // [Min(0f)] public float looseMeleeAnchorSidewaysUpdateDistance = 1.25f;
    //
    // [Tooltip("How strongly the dynamic loose anchor accepts sideways drift. 0 = never, 1 = immediate up to the update limit.")]
    // [Range(0f, 1f)] public float looseMeleeAnchorSidewaysFollow = 0.35f;
    
    
    [Header("Ranged - Tactical Ranges")]
    [Tooltip("When enabled, RangedLine squads derive scan/start/preferred/break distances from WeaponProfile.attackRange.")]
    public bool rangedUseWeaponRangeForTacticalRanges = true;

    [Tooltip("Ranged squads enter combat when the enemy squad is within weapon range * this value.")]
    [Range(0.1f, 1f)] public float rangedCombatStartRangeFactor = 0.92f;

    [Tooltip("Ranged squads approach to weapon range * this value before holding their fire line.")]
    [Range(0.1f, 1f)] public float rangedPreferredRangeFactor = 0.85f;

    [Tooltip("Extra distance beyond missile range before a ranged squad drops combat.")]
    [Min(0f)] public float rangedCombatBreakRangePadding = 4f;

    
    [Header("Ranged - Combat Home Movement")]
    [Tooltip("How far individual ranged soldiers may drift from their combat home to get a shot.")]
    [Min(0f)] public float rangedSoldierHomeFreeEngageDistance = 2.5f;

    [Tooltip("Small forward pressure from each archer's combat home. Keep low to preserve a firing line.")]
    [Min(0f)] public float rangedPressureDistance = 0.25f;

    [Tooltip("Stopping distance used when ranged soldiers return to their fire-line pressure/home point.")]
    [Min(0f)] public float rangedPressureStoppingDistance = 0.25f;



    [Header("PROTOTYPE COMBAT BASE")] public bool usePrototypeMeleeCombat = true;

    void OnValidate()
    {
        autoTargetScanInterval = Mathf.Max(0.01f, autoTargetScanInterval);
        engageStanceAutoTargetScanRange = Mathf.Max(0f, engageStanceAutoTargetScanRange);
        holdStanceAutoTargetScanRange = Mathf.Max(0f, holdStanceAutoTargetScanRange);

        defaultCombatStartRange = Mathf.Max(0f, defaultCombatStartRange);
        defaultCombatBreakRange = Mathf.Max(defaultCombatStartRange, defaultCombatBreakRange);
        combatApproachRefreshInterval = Mathf.Max(0.01f, combatApproachRefreshInterval);
        defaultApproachStopDistance = Mathf.Max(0f, defaultApproachStopDistance);

        rangedCombatStartRangeFactor = Mathf.Clamp(rangedCombatStartRangeFactor, 0.1f, 1f);
        rangedPreferredRangeFactor = Mathf.Clamp(rangedPreferredRangeFactor, 0.1f, 1f);
        rangedCombatBreakRangePadding = Mathf.Max(0f, rangedCombatBreakRangePadding);
        rangedSoldierHomeFreeEngageDistance = Mathf.Max(0f, rangedSoldierHomeFreeEngageDistance);
        rangedPressureDistance = Mathf.Max(0f, rangedPressureDistance);
        rangedPressureStoppingDistance = Mathf.Max(0f, rangedPressureStoppingDistance);

        allStylesSoldierLocalTargetScanRange = Mathf.Max(0f, allStylesSoldierLocalTargetScanRange);
        allStylesSoldierCombatMoveSpeedMultiplier = Mathf.Max(0.1f, allStylesSoldierCombatMoveSpeedMultiplier);

        formationMeleePressureDistance = Mathf.Max(0f, formationMeleePressureDistance);
        formationMeleeRearFreeEngageDistance = Mathf.Max(0f, formationMeleeRearFreeEngageDistance);
        formationMeleeFrontFreeEngageDistance = Mathf.Max(formationMeleeRearFreeEngageDistance, formationMeleeFrontFreeEngageDistance);
        formationMeleeDisengageExtraDistance = Mathf.Max(0f, formationMeleeDisengageExtraDistance);
        formationMeleeForceRejoinExtraDistance = Mathf.Max(formationMeleeDisengageExtraDistance, formationMeleeForceRejoinExtraDistance);
        formationMeleePressureStoppingDistance = Mathf.Max(0f, formationMeleePressureStoppingDistance);

        // looseMeleeFreeEngageDistance = Mathf.Max(0f, looseMeleeFreeEngageDistance);
        // looseMeleeTargetScanRange = Mathf.Max(0f, looseMeleeTargetScanRange);
        // looseMeleePressureDistance = Mathf.Max(0f, looseMeleePressureDistance);
        // looseMeleeDisengageExtraDistance = Mathf.Max(0f, looseMeleeDisengageExtraDistance);
        // looseMeleeForceRejoinDistance = Mathf.Max(looseMeleeFreeEngageDistance + looseMeleeDisengageExtraDistance, looseMeleeForceRejoinDistance);
        // looseMeleePressureStoppingDistance = Mathf.Max(0f, looseMeleePressureStoppingDistance);
        // looseMeleeAnchorForwardUpdateDistance = Mathf.Max(0f, looseMeleeAnchorForwardUpdateDistance);
        // looseMeleeAnchorSidewaysUpdateDistance = Mathf.Max(0f, looseMeleeAnchorSidewaysUpdateDistance);
        // looseMeleeAnchorSidewaysFollow = Mathf.Clamp01(looseMeleeAnchorSidewaysFollow);

        combatParticipantRefreshInterval = Mathf.Max(0.01f, combatParticipantRefreshInterval);

        activeEngagementRatio = Mathf.Clamp01(activeEngagementRatio);
        activeEngagementMinCount = Mathf.Max(0, activeEngagementMinCount);
        activeEngagementFrontlineOverflow = Mathf.Max(0, activeEngagementFrontlineOverflow);
        activeEngagementFrontlineThreshold = Mathf.Clamp01(activeEngagementFrontlineThreshold);
    }
}
