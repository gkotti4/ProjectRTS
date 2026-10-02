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
    [Tooltip("Default runtime state for synchronized ranged volleys.")]
    public bool rangedVolleyEnabledByDefault = false;

    [Tooltip("Default runtime state for simple ranged avoidance. While enabled, a ranged squad with ammunition performs a short formation retreat when its target gets too close.")]
    public bool rangedAvoidanceEnabledByDefault = false;

    [Tooltip("If true, ranged squads derive scan/start/break distances from WeaponProfile.ranged.attackRange.")]
    public bool rangedUseWeaponRangeForTacticalRanges = true;

    [Tooltip("Extra scan range added to ranged weapon range.")]
    [Min(0f)] public float rangedScanRangePadding = 4f;

    [Tooltip("Combat start range as a multiplier of ranged weapon range.")]
    [Min(0.1f)] public float rangedCombatStartRangeMultiplier = 0.9f;


    [Tooltip("Combat break padding added beyond ranged weapon range.")]
    [Min(0f)] public float rangedCombatBreakRangePadding = 4f;

    [Tooltip("Ranged movement stopping distance as a multiplier of ranged attack range.")]
    [Range(0.1f, 1f)] [FormerlySerializedAs("prototypeRangedStoppingDistanceMultiplier")]
    public float formationRangedStoppingDistanceMultiplier = 0.82f;


    [Header("Formation Ranged Setup")]
    [Tooltip("Living-soldier ratio that must be reasonably close to formation slots before a ranged squad begins firing.")]
    [Range(0f, 1f)] public float formationRangedSetupRequiredRatio = 0.70f;

    [Tooltip("Maximum distance from an assigned formation slot for a soldier to count as ready to fire.")]
    [Min(0.05f)] public float formationRangedSetupSlotDistance = 1.25f;

    [Tooltip("Movement speed multiplier used while ranged soldiers settle into their firing formation.")]
    [Min(0.1f)] public float formationRangedSetupMoveSpeedMultiplier = 1.0f;

    [Tooltip("Short pause after the ranged formation becomes ready before soldiers may begin firing.")]
    [Min(0f)] public float formationRangedInitialFireSettleTime = 0.25f;


    [Header("Formation Ranged Avoidance")]
    [Tooltip("Enemy squad-center distance that triggers a simple retreat while ranged avoidance is enabled and ammunition remains.")]
    [Min(0.1f)] public float formationRangedAvoidanceEnterDistance = 10f;

    [Tooltip("How far the squad tries to move directly away from the enemy when avoidance triggers.")]
    [Min(0.1f)] public float formationRangedAvoidanceRetreatDistance = 10f;

    [Tooltip("When this close to the current avoidance destination, recheck the original threat and immediately chain another retreat if it is still inside avoidance range.")]
    [Min(0.1f)] public float formationRangedAvoidanceRecheckDistance = 2f;

    [Header("Formation Ranged Melee Fallback")]
    [Tooltip("Allows a ranged squad with melee sidearms to switch the entire squad into melee mode when enemies breach close range.")]
    public bool formationRangedMeleeFallbackEnabled = true;

    [Tooltip("If any living enemy reaches this distance from any living squad member, the entire ranged squad switches to melee mode.")]
    [Min(0.1f)] public float formationRangedMeleeFallbackEnterDistance = 5.0f;

    [Tooltip("The squad returns to ranged mode only after no living enemy remains within this distance and the squad still has ranged ammunition. Keep this above enter distance to prevent flicker.")]
    [Min(0.1f)] public float formationRangedMeleeFallbackExitDistance = 7.5f;

    
    

   [Header("Attack Timing")]

    [Tooltip("Minimum random value added to the attack interval.")]
    [Min(0f)]
    public float formationAttackIntervalRandomMin = 0f;

    [Tooltip("Maximum random value added to the attack interval.")]
    [Min(0f)]
    public float formationAttackIntervalRandomMax = 1.0f;


    [Header("Reserve Settle / Side-Step")]

    [Tooltip("How far ahead a reserve checks for a friendly-body gap before moving forward.")]
    [Min(0f)]
    public float formationReserveForwardGapDistance = 1.35f;

    [Tooltip("Width/radius of the forward gap check. Higher values require reserves to find a wider lane.")]
    [Min(0f)]
    public float formationReserveForwardGapRadius = 0.60f;

    [Tooltip("Shortest randomized time a newly blocked reserve must wait before repositioning.")]
    [Min(0f)]
    public float formationReserveMinimumBlockedSitTimeMin = 0.35f;

    [Tooltip("Longest randomized time a newly blocked reserve must wait before repositioning.")]
    [Min(0f)]
    public float formationReserveMinimumBlockedSitTimeMax = 1.40f;

    [Tooltip("Enables the small local side-step fallback for blocked reserve soldiers.")]
    public bool formationReserveSideStepEnabled = false;

    [Tooltip("Shortest randomized cooldown before a reserve can attempt another side-step.")]
    [Min(0f)]
    public float formationReserveSideStepIntervalMin = 5.0f;

    [Tooltip("Longest randomized cooldown before a reserve can attempt another side-step.")]
    [Min(0f)]
    public float formationReserveSideStepIntervalMax = 10.0f;

    [Tooltip("How far sideways a reserve tries to step when using the side-step fallback.")]
    [Min(0f)]
    public float formationReserveSideStepDistance = 0.65f;

    [Tooltip("Radius used to reject side-step points already occupied by living soldiers.")]
    [Min(0f)]
    public float formationReserveSideStepOccupancyRadius = 0.85f;

    [Tooltip("Movement speed multiplier used while performing a reserve side-step.")]
    [Min(0f)]
    public float formationReserveSideStepSpeedMultiplier = 0.50f;


    [Header("Local Enemy Targeting")]

    [Tooltip("Allows soldiers to locally target nearby enemies from non-primary hostile squads.")]
    public bool formationMultiSquadLocalTargetingEnabled = true;

    [Tooltip("Maximum distance for considering non-primary enemy soldiers as local reaction targets.")]
    [Min(0f)]
    public float formationLocalEnemyTargetSearchRadius = 7.5f;

    [Tooltip("Score penalty for non-primary enemies so soldiers still prefer the ordered target squad.")]
    [Min(0f)]
    public float formationNonPrimaryTargetPenalty = 1.25f;

    [Tooltip("Forces melee soldiers to prefer obvious nearby enemies over farther assigned targets.")]
    public bool formationImmediateContactOverrideEnabled = true;

    [Tooltip("Extra range added to attack range when checking for immediate-contact enemies.")]
    [Min(0f)]
    public float formationImmediateContactRangePadding = 0.55f;


    [Header("Approach Settle Gate")]

    [Tooltip("Enables the short initial delay before full melee release when only a few soldiers arrive.")]
    public bool formationApproachSettleGateEnabled = true;

    [Tooltip("How long the squad may wait at first contact for more soldiers to arrive.")]
    [Min(0f)]
    public float formationApproachSettleDuration = 0.75f;

    [Tooltip("Fraction of living soldiers that must be near the enemy to skip or finish the settle gate.")]
    [Min(0f)]
    public float formationApproachSettleReadyRatio = 0.45f;

    [Tooltip("Extra range added to combat start range when counting soldiers as approach-ready.")]
    [Min(0f)]
    public float formationApproachSettleReadyRangePadding = 0.95f;

    [Tooltip("Minimum ready-check radius so very small combat start ranges still count nearby soldiers.")]
    [Min(0f)]
    public float formationApproachSettleMinimumReadyRange = 2.75f;


    [Header("Charge")]

    [Tooltip("Master capability toggle for the ordered melee charge phase.")]
    public bool formationChargeEnabled = true;

    [Tooltip("Initial runtime charge toggle state for squads using this profile. Charge still requires an OrderedAttack.")]
    public bool formationChargeEnabledByDefault = true;

    [Tooltip("Closest living-soldier distance that allows a melee squad to begin charging.")]
    [Min(0f)]
    public float formationChargeStartDistance = 10.0f;

    [Tooltip("Formation-wide movement speed multiplier while charging.")]
    [Min(0f)]
    public float formationChargeSpeedMultiplier = 1.25f;

    [Tooltip("Safety time limit before the squad enters combat even if charge contact detection is imperfect.")]
    [Min(0f)]
    public float formationChargeMaximumDuration = 3.0f;

    [Tooltip("Fraction of living melee soldiers that must reach personal attack range before the charge enters follow-through.")]
    [Min(0f)]
    public float formationChargeContactReadyRatio = 0.15f;

    [Tooltip("Multiplier applied to each charging soldier's BodyStats mass to create its per-charge penetration budget. Enemy body mass consumes this budget once per unique charger/enemy contact.")]
    [Min(0f)]
    public float formationChargePenetrationMultiplier = 1.0f;

    [Tooltip("Fraction of living charging soldiers that must exhaust their penetration before the squad leaves the charge and enters normal melee.")]
    [Range(0f, 1f)]
    public float formationChargeEndSpentRatio = 0.65f;

    [FormerlySerializedAs("formationChargeFollowThroughDistance")]
    [Tooltip("Maximum forward distance the formation may travel after meaningful contact. The post-contact direction is locked, so this is a hard penetration ceiling rather than a target-relative destination.")]
    [Min(0f)]
    public float formationChargeFollowThroughMaximumDistance = 1.75f;

    [Tooltip("One-time morale loss applied to the ordered target squad when meaningful charge contact is first reached.")]
    [Min(0f)]
    public float formationChargeMoraleShock = 8.0f;

    [Tooltip("Enables the light directional contact impulse emitted by charging soldiers.")]
    public bool formationChargeImpulseEnabled = true;

    [Tooltip("Authored impulse magnitude applied to enemies touched by a charging soldier's forward capsule.")]
    [Min(0f)]
    public float formationChargeImpulseMagnitude = 4.0f;

    [Tooltip("Impulse decay duration. Short values make the effect feel like contact weight rather than sustained knockback.")]
    [Min(0f)]
    public float formationChargeImpulseDuration = 0.09f;

    [Tooltip("Length of the charge-contact capsule projected in front of each charging soldier.")]
    [Min(0f)]
    public float formationChargeImpulseForwardDistance = 0.95f;

    [Tooltip("Radius of each charging soldier's forward contact capsule.")]
    [Min(0f)]
    public float formationChargeImpulseRadius = 0.65f;

    [Tooltip("Blend between forward and radial impulse direction. Lower values keep the force mostly forward; higher values add more outward spread.")]
    [Min(0f)]
    public float formationChargeImpulseRadialBlend = 0.12f;

    [Tooltip("Enables an additional speed advantage for the soldiers currently closest to the enemy.")]
    public bool formationChargeLeadSpeedEnabled = true;

    [Tooltip("Fraction of living melee soldiers treated as the leading edge. Rounded up to at least one soldier.")]
    [Min(0f)]
    public float formationChargeLeadSoldierRatio = 0.15f;

    [Tooltip("Additional per-soldier movement multiplier applied to soldiers on the current leading edge while charging.")]
    [Min(0f)]
    public float formationChargeLeadSpeedMultiplier = 1.25f;


    [Header("Melee Impact")]

    [Tooltip("Enables physical movement feedback on successful melee damage.")]
    public bool formationMeleeHitImpulseEnabled = true;

    [Tooltip("Baseline successful-hit impulse magnitude before receiver body mass is applied.")]
    [Min(0f)]
    public float formationMeleeHitImpulseMagnitude = 3.0f;

    [Tooltip("Impulse decay duration for successful melee hits. Short values produce a visible strike response without sustained sliding.")]
    [Min(0f)]
    public float formationMeleeHitImpulseDuration = 0.15f;


    [Header("Attacker Combat Lock")]

    [Tooltip("Enables temporary movement lock for active melee attackers after a move or withdraw order.")]
    public bool formationAttackerCombatLockEnabled = true;

    [Tooltip("Shortest time an active melee attacker stays committed after the squad receives a move order.")]
    [Min(0f)]
    public float formationAttackerCombatLockTimeMin = 0.75f;

    [Tooltip("Longest time an active melee attacker stays committed after the squad receives a move order.")]
    [Min(0f)]
    public float formationAttackerCombatLockTimeMax = 1.75f;


    [Header("Reserve Behind-Friendly Reposition")]

    [Tooltip("Enables blocked reserves to move into an open pocket behind a better-positioned friendly.")]
    public bool formationReserveBehindFriendlyRepositionEnabled = true;

    [Tooltip("Cooldown between behind-friendly reposition searches for each reserve soldier.")]
    [Min(0f)]
    public float formationReserveBehindFriendlySearchInterval = 3.5f;

    [Tooltip("Maximum distance for finding friendly anchors that the reserve can queue behind.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyAnchorSearchRadius = 5.5f;

    [Tooltip("Distance behind the chosen friendly anchor where the reserve tries to move.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyBackOffset = 1.45f;

    [Tooltip("Optional left/right offset from the behind point when side probes are enabled.")]
    [Min(0f)]
    public float formationReserveBehindFriendlySideOffset = 0f;

    [Tooltip("Maximum distance allowed when projecting a candidate behind-friendly pocket onto the NavMesh.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyNavMeshProjectionRadius = 1.15f;

    [Tooltip("Radius used to reject candidate pockets already occupied by a living soldier.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyOccupancyRadius = 1.15f;

    [Tooltip("Radius used to count nearby bodies around a candidate pocket.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyCrowdRadius = 1.65f;

    [Tooltip("Maximum number of nearby living bodies allowed before a candidate pocket is considered crowded.")]
    [Min(0)]
    public int formationReserveBehindFriendlyMaxNearbyBodies = 1;

    [Tooltip("Distance from the target pocket at which the reserve considers the reposition complete.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyReachDistance = 0.18f;

    [Tooltip("Maximum distance a reserve is allowed to travel for a behind-friendly reposition.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyMaxMoveDistance = 10.0f;

    [Tooltip("Required amount the friendly anchor must be closer to the target than the reserve.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyMinAnchorForwardGain = 0.85f;

    [Tooltip("Required amount the candidate point must move the reserve closer to its target.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyMinTargetProgress = 0.05f;

    [Tooltip("Movement speed multiplier used while moving to a behind-friendly pocket.")]
    [Min(0f)]
    public float formationReserveBehindFriendlySpeedMultiplier = 0.60f;

    [Tooltip("Score penalty applied per nearby body when ranking behind-friendly candidate pockets.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyCrowdScoreWeight = 1.25f;

    [Tooltip("Score bonus for candidate pockets that make better progress toward the target.")]
    [Min(0f)]
    public float formationReserveBehindFriendlyProgressScoreWeight = 0.75f;

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
        rangedCombatBreakRangePadding = Mathf.Max(0f, rangedCombatBreakRangePadding);
        formationRangedStoppingDistanceMultiplier = Mathf.Clamp(formationRangedStoppingDistanceMultiplier, 0.1f, 1f);
        formationRangedSetupRequiredRatio = Mathf.Clamp01(formationRangedSetupRequiredRatio);
        formationRangedSetupSlotDistance = Mathf.Max(0.05f, formationRangedSetupSlotDistance);
        formationRangedSetupMoveSpeedMultiplier = Mathf.Max(0.1f, formationRangedSetupMoveSpeedMultiplier);
        formationRangedInitialFireSettleTime = Mathf.Max(0f, formationRangedInitialFireSettleTime);
        formationRangedAvoidanceEnterDistance = Mathf.Max(0.1f, formationRangedAvoidanceEnterDistance);
        formationRangedAvoidanceRetreatDistance = Mathf.Max(0.1f, formationRangedAvoidanceRetreatDistance);
        formationRangedAvoidanceRecheckDistance = Mathf.Max(0.1f, formationRangedAvoidanceRecheckDistance);
        formationRangedMeleeFallbackEnterDistance = Mathf.Max(0.1f, formationRangedMeleeFallbackEnterDistance);
        formationRangedMeleeFallbackExitDistance = Mathf.Max(
            formationRangedMeleeFallbackEnterDistance + 0.1f,
            formationRangedMeleeFallbackExitDistance);

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
        formationChargePenetrationMultiplier = Mathf.Max(0f, formationChargePenetrationMultiplier);
        formationChargeEndSpentRatio = Mathf.Clamp01(formationChargeEndSpentRatio);
        formationChargeFollowThroughMaximumDistance = Mathf.Max(0f, formationChargeFollowThroughMaximumDistance);
        formationChargeMoraleShock = Mathf.Max(0f, formationChargeMoraleShock);
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
