using UnityEngine;
using UnityEngine.Serialization;

/// -----------------------------------------------------------------------------
/// SquadCombatProfile
/// -----------------------------------------------------------------------------
///
/// Designer-facing tuning for squad-level combat and the current FormationCombat
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


    [Header("Formation Melee Targeting")]
    [Tooltip("How often each soldier refreshes its local enemy target while in FormationCombat.")]
    [Min(0.01f)] [FormerlySerializedAs("prototypeTargetRefreshInterval")]
    public float formationTargetRefreshInterval = 0.65f;

    [Tooltip("Distance-like score penalty for choosing an enemy already targeted by friendly soldiers.")]
    [Min(0f)] [FormerlySerializedAs("prototypeTargetCrowdingPenalty")]
    public float formationTargetCrowdingPenalty = 2.75f;

    [Tooltip("Score bonus for keeping the current target so soldiers do not flip targets too often.")]
    [Min(0f)] [FormerlySerializedAs("prototypeCurrentTargetStickinessBonus")]
    public float formationCurrentTargetStickinessBonus = 0.75f;


    [Header("Formation Melee Movement / Attacks")]
    [Tooltip("Fallback melee attack range used when a soldier has no WeaponProfile.")]
    [Min(0.1f)] [FormerlySerializedAs("prototypeFallbackMeleeAttackRange")]
    public float formationFallbackMeleeAttackRange = 1.85f;

    [Tooltip("Fallback melee attack interval used when a soldier has no WeaponProfile.")]
    [Min(0.05f)] [FormerlySerializedAs("prototypeFallbackMeleeAttackInterval")]
    public float formationFallbackMeleeAttackInterval = 2.5f;

    [Tooltip("Melee movement stopping distance as a multiplier of melee attack range.")]
    [Min(0.01f)] [FormerlySerializedAs("prototypeMeleeStoppingDistanceMultiplier")]
    public float formationMeleeStoppingDistanceMultiplier = 0.95f;

    [Tooltip("Combat movement speed multiplier used by FormationCombat soldiers.")]
    [Min(0.1f)] [FormerlySerializedAs("prototypeCombatMoveSpeedMultiplier")]
    public float formationCombatMoveSpeedMultiplier = 0.85f;


    [Header("Formation Ranged")]
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
    [Range(0.1f, 1f)] [FormerlySerializedAs("prototypeRangedStoppingDistanceMultiplier")]
    public float formationRangedStoppingDistanceMultiplier = 0.82f;

    [Tooltip("Preferred soldier firing distance as a multiplier of ranged weapon range.")]
    [Range(0.1f, 1f)] [FormerlySerializedAs("prototypeRangedPreferredDistanceMultiplier")]
    public float formationRangedPreferredDistanceMultiplier = 0.82f;

    
    
    
    


    [Header("Attack Timing")]
    [Min(0f)]
    public float formationAttackIntervalRandomMin = 0f; // Minimum Random Range value to add to attack interval

    [Min(0f)]
    public float formationAttackIntervalRandomMax = 1.0f; // Maximum Random Range value to add to attack interval

    [Header("Reserve Settle / Side-Step")]
    [Min(0f)]
    public float formationReserveForwardGapDistance = 1.35f; // Tune // How far ahead a reserve checks for a friendly-body gap before moving forward.

    [Min(0f)]
    public float formationReserveForwardGapRadius = 0.60f; // Tune // Width/radius of the forward gap check; higher means reserves need a wider lane.

    [Min(0f)]
    public float formationReserveMinimumBlockedSitTimeMin = 0.35f; // Shortest randomized time a newly blocked reserve must wait before repositioning.

    [Min(0f)]
    public float formationReserveMinimumBlockedSitTimeMax = 1.40f; // Longest randomized time a newly blocked reserve must wait before repositioning.

    public bool formationReserveSideStepEnabled = false; // Enables the small local side-step fallback for blocked reserve soldiers.

    [Min(0f)]
    public float formationReserveSideStepIntervalMin = 5.0f; // Shortest randomized cooldown before a reserve can attempt another side-step.

    [Min(0f)]
    public float formationReserveSideStepIntervalMax = 10.0f; // Longest randomized cooldown before a reserve can attempt another side-step.

    [Min(0f)]
    public float formationReserveSideStepDistance = 0.65f; // How far sideways a reserve tries to step when using the side-step fallback.

    [Min(0f)]
    public float formationReserveSideStepOccupancyRadius = 0.85f; // Radius used to reject side-step points already occupied by living soldiers.

    [Min(0f)]
    public float formationReserveSideStepSpeedMultiplier = 0.50f; // Movement speed multiplier used while performing a reserve side-step.

    [Header("Local Enemy Targeting")]
    public bool formationMultiSquadLocalTargetingEnabled = true; // Allows soldiers to locally target nearby enemies from non-primary hostile squads.

    [Min(0f)]
    public float formationLocalEnemyTargetSearchRadius = 7.5f; // Max distance for considering non-primary enemy soldiers as local reaction targets.

    [Min(0f)]
    public float formationNonPrimaryTargetPenalty = 1.25f; // Score penalty for non-primary enemies so soldiers still prefer the ordered target squad.

    public bool formationImmediateContactOverrideEnabled = true; // Forces melee soldiers to prefer obvious nearby enemies over farther assigned targets.

    [Min(0f)]
    public float formationImmediateContactRangePadding = 0.55f; // Extra range added to attack range when checking for immediate contact enemies.

    [Header("Approach Settle Gate")]
    public bool formationApproachSettleGateEnabled = true; // Enables the short initial delay before full melee release when only a few soldiers arrive.

    [Min(0f)]
    public float formationApproachSettleDuration = 0.75f; // How long the squad may wait at first contact for more soldiers to arrive.

    [Min(0f)]
    public float formationApproachSettleReadyRatio = 0.45f; // Fraction of living soldiers that must be near the enemy to skip/finish the settle gate.

    [Min(0f)]
    public float formationApproachSettleReadyRangePadding = 0.95f; // Extra range added to combat start range when counting soldiers as approach-ready.

    [Min(0f)]
    public float formationApproachSettleMinimumReadyRange = 2.75f; // Minimum ready-check radius so very small combat start ranges still count nearby soldiers.

    [Header("Charge")]
    public bool formationChargeEnabled = true; // Enables the shared melee charge phase between approach and combat.

    [Min(0f)]
    public float formationChargeStartDistance = 10.0f; // Closest living soldier distance that allows a melee squad to begin charging.

    [Min(0f)]
    public float formationChargeSpeedMultiplier = 1.25f; // Formation-wide movement multiplier while charging.

    [Min(0f)]
    public float formationChargeMaximumDuration = 3.0f; // Safety cap before the squad enters combat even if contact detection is imperfect.

    [Min(0f)]
    public float formationChargeContactReadyRatio = 0.15f; // Fraction of living melee soldiers that must reach personal attack range to finish the charge.

    public bool formationChargeImpulseEnabled = true; // Enables the very light directional contact impulse during the infantry charge MVP.

    [Min(0f)]
    public float formationChargeImpulseMagnitude = 4.0f; // Small authored impulse applied to enemies touched by a charging soldier's forward capsule.

    [Min(0f)]
    public float formationChargeImpulseDuration = 0.09f; // Short decay keeps this as contact weight rather than visible knockback.

    [Min(0f)]
    public float formationChargeImpulseForwardDistance = 0.95f; // Length of the contact capsule projected in front of each charging soldier.

    [Min(0f)]
    public float formationChargeImpulseRadius = 0.65f; // Width of each charging soldier's forward contact capsule.

    [Min(0f)]
    public float formationChargeImpulseRadialBlend = 0.12f; // Mostly forward force with a small outward spread.

    public bool formationChargeLeadSpeedEnabled = true; // Enables a small speed edge for the soldiers currently closest to the enemy.

    [Min(0f)]
    public float formationChargeLeadSoldierRatio = 0.15f; // 0.15 means 15% of living melee soldiers, rounded up to at least one soldier.

    [Min(0f)]
    public float formationChargeLeadSpeedMultiplier = 1.25f; // Additional per-soldier charge movement multiplier for the current leading edge.

    [Header("Melee Impact")]
    public bool formationMeleeHitImpulseEnabled = true; // Enables physical movement feedback on successful melee damage.

    [Min(0f)]
    public float formationMeleeHitImpulseMagnitude = 3.0f; // Baseline successful-hit impulse before receiver body mass is applied.

    [Min(0f)]
    public float formationMeleeHitImpulseDuration = 0.15f; // Short decay gives a visible strike response without sustained sliding.

    [Header("Attacker Combat Lock")]
    public bool formationAttackerCombatLockEnabled = true; // Enables temporary movement lock for active melee attackers after a move/withdraw order.

    [Min(0f)]
    public float formationAttackerCombatLockTimeMin = 0.75f; // Shortest time an active melee attacker stays committed after the squad receives a move order.

    [Min(0f)]
    public float formationAttackerCombatLockTimeMax = 1.75f; // Longest time an active melee attacker stays committed after the squad receives a move order.

    [Header("Reserve Behind-Friendly Reposition")]
    public bool formationReserveBehindFriendlyRepositionEnabled = true; // Enables blocked reserves to move into an open pocket behind a better-positioned friendly.

    [Min(0f)]
    public float formationReserveBehindFriendlySearchInterval = 3.5f; // Cooldown between behind-friendly reposition searches for each reserve soldier.

    [Min(0f)]
    public float formationReserveBehindFriendlyAnchorSearchRadius = 5.5f; // Max distance for finding friendly anchors that the reserve can queue behind.

    [Min(0f)]
    public float formationReserveBehindFriendlyBackOffset = 1.45f; // Distance behind the chosen friendly anchor where the reserve tries to move.

    [Min(0f)]
    public float formationReserveBehindFriendlySideOffset = 0f; // 0.65f; // Optional left/right offset from the behind point if side probes are enabled.

    [Min(0f)]
    public float formationReserveBehindFriendlyNavMeshProjectionRadius = 1.15f; // Max distance allowed when projecting the candidate pocket onto the NavMesh.

    [Min(0f)]
    public float formationReserveBehindFriendlyOccupancyRadius = 1.15f; // Radius used to reject candidate pockets already occupied by a living soldier.

    [Min(0f)]
    public float formationReserveBehindFriendlyCrowdRadius = 1.65f; // Radius used to count nearby bodies around a candidate pocket.

    [Min(0)]
    public int formationReserveBehindFriendlyMaxNearbyBodies = 1; // Maximum nearby living bodies allowed before a candidate pocket is considered crowded.

    [Min(0f)]
    public float formationReserveBehindFriendlyReachDistance = 0.18f; // Distance from the pocket at which the reserve considers the reposition complete.

    [Min(0f)]
    public float formationReserveBehindFriendlyMaxMoveDistance = 10.0f; // Maximum distance a reserve is allowed to travel for this behind-friendly reposition.

    [Min(0f)]
    public float formationReserveBehindFriendlyMinAnchorForwardGain = 0.85f; // Required amount the friendly anchor must be closer to the target than the reserve.

    [Min(0f)]
    public float formationReserveBehindFriendlyMinTargetProgress = 0.05f; // Required amount the candidate point must move the reserve closer to its target.

    [Min(0f)]
    public float formationReserveBehindFriendlySpeedMultiplier = 0.60f; // Movement speed multiplier used while moving to a behind-friendly pocket.

    [Min(0f)]
    public float formationReserveBehindFriendlyCrowdScoreWeight = 1.25f; // Score penalty per nearby body when ranking behind-friendly candidate pockets.

    [Min(0f)]
    public float formationReserveBehindFriendlyProgressScoreWeight = 0.75f; // Score bonus for candidate pockets that make better progress toward the target.

    void OnValidate()
    {
        autoTargetScanInterval = Mathf.Max(0.01f, autoTargetScanInterval);
        engageStanceAutoTargetScanRange = Mathf.Max(0f, engageStanceAutoTargetScanRange);
        holdStanceAutoTargetScanRange = Mathf.Max(0f, holdStanceAutoTargetScanRange);

        defaultCombatStartRange = Mathf.Max(0f, defaultCombatStartRange);
        defaultCombatBreakRange = Mathf.Max(defaultCombatStartRange, defaultCombatBreakRange);
        combatApproachRefreshInterval = Mathf.Max(0.01f, combatApproachRefreshInterval);
        defaultApproachStopDistance = Mathf.Max(0f, defaultApproachStopDistance);

        formationTargetRefreshInterval = Mathf.Max(0.01f, formationTargetRefreshInterval);
        formationTargetCrowdingPenalty = Mathf.Max(0f, formationTargetCrowdingPenalty);
        formationCurrentTargetStickinessBonus = Mathf.Max(0f, formationCurrentTargetStickinessBonus);

        formationFallbackMeleeAttackRange = Mathf.Max(0.1f, formationFallbackMeleeAttackRange);
        formationFallbackMeleeAttackInterval = Mathf.Max(0.05f, formationFallbackMeleeAttackInterval);
        formationMeleeStoppingDistanceMultiplier = Mathf.Max(0.01f, formationMeleeStoppingDistanceMultiplier);
        formationCombatMoveSpeedMultiplier = Mathf.Max(0.1f, formationCombatMoveSpeedMultiplier);

        rangedScanRangePadding = Mathf.Max(0f, rangedScanRangePadding);
        rangedCombatStartRangeMultiplier = Mathf.Max(0.1f, rangedCombatStartRangeMultiplier);
        rangedPreferredRangeMultiplier = Mathf.Clamp(rangedPreferredRangeMultiplier, 0.1f, 1f);
        rangedCombatBreakRangePadding = Mathf.Max(0f, rangedCombatBreakRangePadding);
        formationRangedStoppingDistanceMultiplier = Mathf.Clamp(formationRangedStoppingDistanceMultiplier, 0.1f, 1f);
        formationRangedPreferredDistanceMultiplier = Mathf.Clamp(formationRangedPreferredDistanceMultiplier, 0.1f, 1f);

        formationAttackIntervalRandomMin = Mathf.Max(0f, formationAttackIntervalRandomMin);
        formationAttackIntervalRandomMax = Mathf.Max(0f, formationAttackIntervalRandomMax);
        formationReserveForwardGapDistance = Mathf.Max(0f, formationReserveForwardGapDistance);
        formationReserveForwardGapRadius = Mathf.Max(0f, formationReserveForwardGapRadius);
        formationReserveMinimumBlockedSitTimeMin = Mathf.Max(0f, formationReserveMinimumBlockedSitTimeMin);
        formationReserveMinimumBlockedSitTimeMax = Mathf.Max(0f, formationReserveMinimumBlockedSitTimeMax);
        formationReserveSideStepIntervalMin = Mathf.Max(0f, formationReserveSideStepIntervalMin);
        formationReserveSideStepIntervalMax = Mathf.Max(0f, formationReserveSideStepIntervalMax);
        formationReserveSideStepDistance = Mathf.Max(0f, formationReserveSideStepDistance);
        formationReserveSideStepOccupancyRadius = Mathf.Max(0f, formationReserveSideStepOccupancyRadius);
        formationReserveSideStepSpeedMultiplier = Mathf.Max(0f, formationReserveSideStepSpeedMultiplier);
        formationLocalEnemyTargetSearchRadius = Mathf.Max(0f, formationLocalEnemyTargetSearchRadius);
        formationNonPrimaryTargetPenalty = Mathf.Max(0f, formationNonPrimaryTargetPenalty);
        formationImmediateContactRangePadding = Mathf.Max(0f, formationImmediateContactRangePadding);
        formationApproachSettleDuration = Mathf.Max(0f, formationApproachSettleDuration);
        formationApproachSettleReadyRatio = Mathf.Clamp01(formationApproachSettleReadyRatio);
        formationApproachSettleReadyRangePadding = Mathf.Max(0f, formationApproachSettleReadyRangePadding);
        formationApproachSettleMinimumReadyRange = Mathf.Max(0f, formationApproachSettleMinimumReadyRange);
        formationChargeStartDistance = Mathf.Max(0f, formationChargeStartDistance);
        formationChargeSpeedMultiplier = Mathf.Max(0f, formationChargeSpeedMultiplier);
        formationChargeMaximumDuration = Mathf.Max(0f, formationChargeMaximumDuration);
        formationChargeContactReadyRatio = Mathf.Clamp01(formationChargeContactReadyRatio);
        formationChargeImpulseMagnitude = Mathf.Max(0f, formationChargeImpulseMagnitude);
        formationChargeImpulseDuration = Mathf.Max(0f, formationChargeImpulseDuration);
        formationChargeImpulseForwardDistance = Mathf.Max(0f, formationChargeImpulseForwardDistance);
        formationChargeImpulseRadius = Mathf.Max(0f, formationChargeImpulseRadius);
        formationChargeImpulseRadialBlend = Mathf.Clamp01(formationChargeImpulseRadialBlend);
        formationChargeLeadSoldierRatio = Mathf.Clamp01(formationChargeLeadSoldierRatio);
        formationChargeLeadSpeedMultiplier = Mathf.Max(0f, formationChargeLeadSpeedMultiplier);
        formationMeleeHitImpulseMagnitude = Mathf.Max(0f, formationMeleeHitImpulseMagnitude);
        formationMeleeHitImpulseDuration = Mathf.Max(0f, formationMeleeHitImpulseDuration);
        formationAttackerCombatLockTimeMin = Mathf.Max(0f, formationAttackerCombatLockTimeMin);
        formationAttackerCombatLockTimeMax = Mathf.Max(0f, formationAttackerCombatLockTimeMax);
        formationReserveBehindFriendlySearchInterval = Mathf.Max(0f, formationReserveBehindFriendlySearchInterval);
        formationReserveBehindFriendlyAnchorSearchRadius = Mathf.Max(0f, formationReserveBehindFriendlyAnchorSearchRadius);
        formationReserveBehindFriendlyBackOffset = Mathf.Max(0f, formationReserveBehindFriendlyBackOffset);
        formationReserveBehindFriendlySideOffset = Mathf.Max(0f, formationReserveBehindFriendlySideOffset);
        formationReserveBehindFriendlyNavMeshProjectionRadius = Mathf.Max(0f, formationReserveBehindFriendlyNavMeshProjectionRadius);
        formationReserveBehindFriendlyOccupancyRadius = Mathf.Max(0f, formationReserveBehindFriendlyOccupancyRadius);
        formationReserveBehindFriendlyCrowdRadius = Mathf.Max(0f, formationReserveBehindFriendlyCrowdRadius);
        formationReserveBehindFriendlyMaxNearbyBodies = Mathf.Max(0, formationReserveBehindFriendlyMaxNearbyBodies);
        formationReserveBehindFriendlyReachDistance = Mathf.Max(0f, formationReserveBehindFriendlyReachDistance);
        formationReserveBehindFriendlyMaxMoveDistance = Mathf.Max(0f, formationReserveBehindFriendlyMaxMoveDistance);
        formationReserveBehindFriendlyMinAnchorForwardGain = Mathf.Max(0f, formationReserveBehindFriendlyMinAnchorForwardGain);
        formationReserveBehindFriendlyMinTargetProgress = Mathf.Max(0f, formationReserveBehindFriendlyMinTargetProgress);
        formationReserveBehindFriendlySpeedMultiplier = Mathf.Max(0f, formationReserveBehindFriendlySpeedMultiplier);
        formationReserveBehindFriendlyCrowdScoreWeight = Mathf.Max(0f, formationReserveBehindFriendlyCrowdScoreWeight);
        formationReserveBehindFriendlyProgressScoreWeight = Mathf.Max(0f, formationReserveBehindFriendlyProgressScoreWeight);
        formationAttackIntervalRandomMax = Mathf.Max(formationAttackIntervalRandomMin, formationAttackIntervalRandomMax);
        formationReserveMinimumBlockedSitTimeMax = Mathf.Max(formationReserveMinimumBlockedSitTimeMin, formationReserveMinimumBlockedSitTimeMax);
        formationReserveSideStepIntervalMax = Mathf.Max(formationReserveSideStepIntervalMin, formationReserveSideStepIntervalMax);
        formationAttackerCombatLockTimeMax = Mathf.Max(formationAttackerCombatLockTimeMin, formationAttackerCombatLockTimeMax);
    }
}
