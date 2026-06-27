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
    // [Min(0f)] public float standGroundScanPadding = 0.5f;

    [Header("Approach")]
    [Min(0f)] public float combatStartRange = 6f;
    [Min(0f)] public float combatBreakRange = 10f;
    [Min(0.01f)] public float approachRefreshInterval = 0.25f;
    [Min(0f)] public float approachStopDistance = 3f;

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
        // standGroundScanPadding = Mathf.Max(0f, standGroundScanPadding);

        combatStartRange = Mathf.Max(0f, combatStartRange);
        combatBreakRange = Mathf.Max(combatStartRange, combatBreakRange);
        approachRefreshInterval = Mathf.Max(0.01f, approachRefreshInterval);
        approachStopDistance = Mathf.Max(0f, approachStopDistance);

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
