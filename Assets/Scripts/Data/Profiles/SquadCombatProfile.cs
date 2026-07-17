using UnityEngine;

/// -----------------------------------------------------------------------------
/// SquadCombatProfile
/// -----------------------------------------------------------------------------
///
/// Designer-facing tuning for squad-level combat and the current PrototypeMelee
/// base. Old old melee pressure, combat-home, old row-scoring, and
/// soft-engagement-budget values have been removed.
///
[CreateAssetMenu(
    fileName = "SquadCombatProfile_",
    menuName = "Scriptable Objects/Military/Profiles/SquadCombatProfile")]
public class SquadCombatProfile : ScriptableObject
{
    [Header("Auto Target Scanning")]
    [Tooltip("If true, this squad can automatically look for nearby enemy squads when not already fighting.")]
    public bool autoTargetScanEnabled = true;

    [Tooltip("How often this squad checks for nearby enemy squads while auto target scanning is enabled.")]
    [Min(0.01f)] public float autoTargetScanInterval = 0.5f;

    [Tooltip("How far this squad scans for enemy targets while in Engage stance.")]
    [Min(0f)] public float engageStanceAutoTargetScanRange = 9f;

    [Tooltip("How far this squad scans for enemy targets while in Hold stance.")]
    [Min(0f)] public float holdStanceAutoTargetScanRange = 1f;


    [Header("Approach And Combat Exit")]
    [Tooltip("Default distance at which this squad considers itself close enough to begin combat.")]
    [Min(0f)] public float defaultCombatStartRange = 6f;

    [Tooltip("Default distance at which this squad breaks off from its current combat target if the target gets too far away.")]
    [Min(0f)] public float defaultCombatBreakRange = 10f;

    [Tooltip("How often this squad refreshes its approach destination while moving toward an enemy squad.")]
    [Min(0.01f)] public float combatApproachRefreshInterval = 0.25f;

    [Tooltip("Default stopping distance used when approaching an enemy squad before combat begins.")]
    [Min(0f)] public float defaultApproachStopDistance = 3f;


    [Header("Prototype Melee Targeting")]
    [Tooltip("How often each soldier refreshes its local enemy target while in PrototypeMelee.")]
    [Min(0.01f)] public float prototypeTargetRefreshInterval = 0.85f;

    [Tooltip("Distance-like score penalty for choosing an enemy already targeted by friendly soldiers.")]
    [Min(0f)] public float prototypeTargetCrowdingPenalty = 2.75f;

    [Tooltip("Score bonus for keeping the current target so soldiers do not flip targets too often.")]
    [Min(0f)] public float prototypeCurrentTargetStickinessBonus = 0.85f;


    [Header("Prototype Melee Movement / Attacks")]
    [Tooltip("Fallback melee attack range used when a soldier has no WeaponProfile.")]
    [Min(0.1f)] public float prototypeFallbackMeleeAttackRange = 1.85f;

    [Tooltip("Fallback melee attack interval used when a soldier has no WeaponProfile.")]
    [Min(0.05f)] public float prototypeFallbackMeleeAttackInterval = 2.5f;

    [Tooltip("Melee movement stopping distance as a multiplier of melee attack range.")]
    [Min(0.01f)] public float prototypeMeleeStoppingDistanceMultiplier = 0.95f;

    [Tooltip("Combat movement speed multiplier used by PrototypeMelee soldiers.")]
    [Min(0.1f)] public float prototypeCombatMoveSpeedMultiplier = 0.80f;


    [Header("Prototype Ranged")]
    [Tooltip("If true, ranged squads derive scan/start/preferred/break distances from WeaponProfile.ranged.attackRange.")]
    public bool rangedUseWeaponRangeForTacticalRanges = true;

    [Tooltip("Extra scan range added to ranged weapon range.")]
    [Min(0f)] public float rangedScanRangePadding = 4f;

    [Tooltip("Combat start range as a multiplier of ranged weapon range.")]
    [Min(0.1f)] public float rangedCombatStartRangeMultiplier = 0.9f;

    [Tooltip("Preferred approach/firing range as a multiplier of ranged weapon range.")]
    [Range(0.1f, 1f)] public float rangedPreferredRangeMultiplier = 0.82f;

    [Tooltip("Combat break padding added beyond ranged weapon range.")]
    [Min(0f)] public float rangedCombatBreakRangePadding = 4f;

    [Tooltip("Ranged movement stopping distance as a multiplier of ranged attack range.")]
    [Range(0.1f, 1f)] public float prototypeRangedStoppingDistanceMultiplier = 0.82f;

    [Tooltip("Preferred soldier firing distance as a multiplier of ranged weapon range.")]
    [Range(0.1f, 1f)] public float prototypeRangedPreferredDistanceMultiplier = 0.82f;

    
    
    
    
    void OnValidate()
    {
        autoTargetScanInterval = Mathf.Max(0.01f, autoTargetScanInterval);
        engageStanceAutoTargetScanRange = Mathf.Max(0f, engageStanceAutoTargetScanRange);
        holdStanceAutoTargetScanRange = Mathf.Max(0f, holdStanceAutoTargetScanRange);

        defaultCombatStartRange = Mathf.Max(0f, defaultCombatStartRange);
        defaultCombatBreakRange = Mathf.Max(defaultCombatStartRange, defaultCombatBreakRange);
        combatApproachRefreshInterval = Mathf.Max(0.01f, combatApproachRefreshInterval);
        defaultApproachStopDistance = Mathf.Max(0f, defaultApproachStopDistance);

        prototypeTargetRefreshInterval = Mathf.Max(0.01f, prototypeTargetRefreshInterval);
        prototypeTargetCrowdingPenalty = Mathf.Max(0f, prototypeTargetCrowdingPenalty);
        prototypeCurrentTargetStickinessBonus = Mathf.Max(0f, prototypeCurrentTargetStickinessBonus);

        prototypeFallbackMeleeAttackRange = Mathf.Max(0.1f, prototypeFallbackMeleeAttackRange);
        prototypeFallbackMeleeAttackInterval = Mathf.Max(0.05f, prototypeFallbackMeleeAttackInterval);
        prototypeMeleeStoppingDistanceMultiplier = Mathf.Max(0.01f, prototypeMeleeStoppingDistanceMultiplier);
        prototypeCombatMoveSpeedMultiplier = Mathf.Max(0.1f, prototypeCombatMoveSpeedMultiplier);

        rangedScanRangePadding = Mathf.Max(0f, rangedScanRangePadding);
        rangedCombatStartRangeMultiplier = Mathf.Max(0.1f, rangedCombatStartRangeMultiplier);
        rangedPreferredRangeMultiplier = Mathf.Clamp(rangedPreferredRangeMultiplier, 0.1f, 1f);
        rangedCombatBreakRangePadding = Mathf.Max(0f, rangedCombatBreakRangePadding);
        prototypeRangedStoppingDistanceMultiplier = Mathf.Clamp(prototypeRangedStoppingDistanceMultiplier, 0.1f, 1f);
        prototypeRangedPreferredDistanceMultiplier = Mathf.Clamp(prototypeRangedPreferredDistanceMultiplier, 0.1f, 1f);
    }
}
