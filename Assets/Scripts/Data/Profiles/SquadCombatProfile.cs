using UnityEngine;

[CreateAssetMenu(
    fileName = "SquadCombatProfile_",
    menuName = "Scriptable Objects/Military/Profiles/SquadCombatProfile")]
public class SquadCombatProfile : ScriptableObject
{
    [Header("Scanning")]
    public bool autoScanEnabled = true;
    [Min(0.01f)] public float scanInterval = 0.35f;

    [Header("Stance Scan Ranges")]
    [Min(0f)] public float aggressiveAutoScanRange = 14f;
    [Min(0f)] public float defensiveAutoScanRange = 8f;
    [Min(0f)] public float standGroundScanPadding = 0.5f;

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
}
