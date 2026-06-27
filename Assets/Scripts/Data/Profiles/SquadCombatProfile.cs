using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "SquadCombatProfile_",
    menuName = "Scriptable Objects/Military/Profiles/SquadCombatProfile")]
public class SquadCombatProfile : ScriptableObject
{
    [Header("Scanning")]
    public bool autoScanEnabled = true;
    [Min(0.01f)] public float scanInterval = 0.35f;

    [FormerlySerializedAs("aggressiveAutoScanRange")]
    [Header("Stance Scan Ranges")]
    [Min(0f)] public float engageStanceScanRange = 14f;
    [FormerlySerializedAs("defensiveAutoScanRange")] [Min(0f)] public float holdStanceScanRange = 8f;

    [Header("Approach")]
    [Min(0f)] public float combatStartRange = 6f;
    [Min(0f)] public float combatBreakRange = 10f;
    [Min(0.01f)] public float approachRefreshInterval = 0.25f;
    [Min(0f)] public float approachStopDistance = 3f;

    [Header("Ranged Tactical Ranges")]
    [Tooltip("When enabled, RangedLine squads derive scan/start/preferred/break distances from WeaponProfile.attackRange.")]
    public bool rangedUseWeaponRangeForTacticalRanges = true;

    [Tooltip("Ranged squads enter combat when the enemy squad is within weapon range * this value.")]
    [Range(0.1f, 1f)] public float rangedCombatStartRangeFactor = 0.92f;

    [Tooltip("Ranged squads approach to weapon range * this value before holding their fire line.")]
    [Range(0.1f, 1f)] public float rangedPreferredRangeFactor = 0.82f;

    [Tooltip("Extra scan distance beyond missile range for Engage stance fire-at-will behavior.")]
    [Min(0f)] public float rangedEngageScanRangePadding = 2f;

    [Tooltip("Extra scan distance beyond missile range for Hold stance. Keep low if Hold should not chase.")]
    [Min(0f)] public float rangedHoldScanRangePadding = 0f;

    [Tooltip("Extra distance beyond missile range before a ranged squad drops combat.")]
    [Min(0f)] public float rangedCombatBreakRangePadding = 4f;

    [Tooltip("How far individual ranged soldiers may drift from their combat home to get a shot.")]
    [Min(0f)] public float rangedSoldierFreeEngageDistance = 2.5f;

    [Tooltip("Small forward pressure from each archer's combat home. Keep low to preserve a firing line.")]
    [Min(0f)] public float rangedPressureDistance = 0.25f;

    [Tooltip("Stopping distance used when ranged soldiers return to their fire-line pressure/home point.")]
    [Min(0f)] public float rangedPressureStoppingDistance = 0.25f;

    [Header("Engagement")]
    [Min(0f)] public float soldierLocalTargetScanRange = 3f;
    [Min(0.1f)] public float combatMoveSpeedMultiplier = 1.15f;

    [Header("Combat Pressure / Cohesion")]
    [Min(0f)] public float combatPressureDistance = 2f;
    [Min(0f)] public float combatRearFreeEngageDistance = 1.25f;
    [Min(0f)] public float combatFrontFreeEngageDistance = 3.25f;
    [Min(0f)] public float combatDisengageExtraDistance = 1.5f;
    [Min(0f)] public float combatForceRejoinExtraDistance = 3.5f;
    [Min(0f)] public float combatPressureStoppingDistance = 0.15f;

    [Header("Combat Ticks")]
    [Min(0.01f)] public float targetRefreshInterval = 0.35f;

    [Header("Soft Engagement Budget")]
    public bool useSoftEngagementBudget = true;
    [Range(0f, 1f)] public float activeEngagementRatio = 0.58f;
    [Min(0)] public int activeEngagementMinCount = 4;
    [Min(0)] public int activeEngagementFrontlineOverflow = 2;
    [Range(0f, 1f)] public float activeEngagementFrontlineThreshold = 0.7f;

    void OnValidate()
    {
        scanInterval = Mathf.Max(0.01f, scanInterval);
        engageStanceScanRange = Mathf.Max(0f, engageStanceScanRange);
        holdStanceScanRange = Mathf.Max(0f, holdStanceScanRange);

        combatStartRange = Mathf.Max(0f, combatStartRange);
        combatBreakRange = Mathf.Max(combatStartRange, combatBreakRange);
        approachRefreshInterval = Mathf.Max(0.01f, approachRefreshInterval);
        approachStopDistance = Mathf.Max(0f, approachStopDistance);

        rangedCombatStartRangeFactor = Mathf.Clamp(rangedCombatStartRangeFactor, 0.1f, 1f);
        rangedPreferredRangeFactor = Mathf.Clamp(rangedPreferredRangeFactor, 0.1f, 1f);
        rangedEngageScanRangePadding = Mathf.Max(0f, rangedEngageScanRangePadding);
        rangedHoldScanRangePadding = Mathf.Max(0f, rangedHoldScanRangePadding);
        rangedCombatBreakRangePadding = Mathf.Max(0f, rangedCombatBreakRangePadding);
        rangedSoldierFreeEngageDistance = Mathf.Max(0f, rangedSoldierFreeEngageDistance);
        rangedPressureDistance = Mathf.Max(0f, rangedPressureDistance);
        rangedPressureStoppingDistance = Mathf.Max(0f, rangedPressureStoppingDistance);

        soldierLocalTargetScanRange = Mathf.Max(0f, soldierLocalTargetScanRange);
        combatMoveSpeedMultiplier = Mathf.Max(0.1f, combatMoveSpeedMultiplier);

        combatPressureDistance = Mathf.Max(0f, combatPressureDistance);
        combatRearFreeEngageDistance = Mathf.Max(0f, combatRearFreeEngageDistance);
        combatFrontFreeEngageDistance = Mathf.Max(combatRearFreeEngageDistance, combatFrontFreeEngageDistance);
        combatDisengageExtraDistance = Mathf.Max(0f, combatDisengageExtraDistance);
        combatForceRejoinExtraDistance = Mathf.Max(combatDisengageExtraDistance, combatForceRejoinExtraDistance);
        combatPressureStoppingDistance = Mathf.Max(0f, combatPressureStoppingDistance);

        targetRefreshInterval = Mathf.Max(0.01f, targetRefreshInterval);

        activeEngagementRatio = Mathf.Clamp01(activeEngagementRatio);
        activeEngagementMinCount = Mathf.Max(0, activeEngagementMinCount);
        activeEngagementFrontlineOverflow = Mathf.Max(0, activeEngagementFrontlineOverflow);
        activeEngagementFrontlineThreshold = Mathf.Clamp01(activeEngagementFrontlineThreshold);
    }
}
