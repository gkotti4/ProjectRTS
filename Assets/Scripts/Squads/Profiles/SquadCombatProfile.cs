using UnityEngine;
using UnityEngine.Serialization;

/// -----------------------------------------------------------------------------
/// SquadCombatProfile
/// -----------------------------------------------------------------------------
///
/// Designer-facing tuning for squad-level combat. Formation-prefixed values in
/// this profile now specifically describe the current Formed execution baseline.
/// Old melee pressure, combat-home, old row-scoring, and
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


    [FormerlySerializedAs("formationTargetRefreshInterval")]
    [Header("Melee - Formed Targeting")]
    [Tooltip("How often each soldier refreshes its local enemy target while using Formed combat execution.")]
    [Min(0.01f)] 
    public float meleeTargetRefreshInterval = 0.65f;

    [FormerlySerializedAs("formationTargetCrowdingPenalty")]
    [Tooltip("Distance-like score penalty for choosing an enemy already targeted by friendly soldiers.")]
    [Min(0f)] 
    public float meleeTargetCrowdingPenalty = 2.75f;

    [FormerlySerializedAs("formationCurrentTargetStickinessBonus")]
    [Tooltip("Score bonus for keeping the current target so soldiers do not flip targets too often.")]
    [Min(0f)]
    public float meleeCurrentTargetStickinessBonus = 0.75f;


    [FormerlySerializedAs("meleeFallbackMeleeAttackRange")]
    [FormerlySerializedAs("formationFallbackMeleeAttackRange")]
    [Header("Melee - Formed Movement / Attacks")]
    [Tooltip("Fallback melee attack range used when a soldier has no WeaponProfile.")]
    [Min(0.1f)] 
    public float fallbackMeleeAttackRange = 1.85f;

    [FormerlySerializedAs("meleeFallbackMeleeAttackInterval")]
    [FormerlySerializedAs("formationFallbackMeleeAttackInterval")]
    [Tooltip("Fallback melee attack interval used when a soldier has no WeaponProfile.")]
    [Min(0.05f)] 
    public float fallbackMeleeAttackInterval = 2.5f;

    [FormerlySerializedAs("formationMeleeStoppingDistanceMultiplier")]
    [Tooltip("Melee movement stopping distance as a multiplier of melee attack range.")]
    [Min(0.01f)] 
    public float meleeStoppingDistanceMultiplier = 0.95f;

    [FormerlySerializedAs("formationCombatMoveSpeedMultiplier")]
    [Tooltip("Combat movement speed multiplier used by soldiers in Formed combat execution.")]
    [Min(0.1f)] 
    public float meleeCombatMoveSpeedMultiplier = 0.85f;


    [Header("Ranged")]
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

    [FormerlySerializedAs("formationRangedStoppingDistanceMultiplier")]
    [Tooltip("Ranged movement stopping distance as a multiplier of ranged attack range.")]
    [Range(0.1f, 1f)] [FormerlySerializedAs("prototypeRangedStoppingDistanceMultiplier")]
    public float rangedStoppingDistanceMultiplier = 0.82f;


    [FormerlySerializedAs("meleeRangedSetupRequiredRatio")]
    [FormerlySerializedAs("formationRangedSetupRequiredRatio")]
    [Header("Ranged - Formed Setup")]
    [Tooltip("Living-soldier ratio that must be reasonably close to formation slots before a ranged squad begins firing.")]
    [Range(0f, 1f)] public float rangedSetupRequiredRatio = 0.70f;

    [FormerlySerializedAs("formationRangedSetupSlotDistance")]
    [Tooltip("Maximum distance from an assigned formation slot for a soldier to count as ready to fire.")]
    [Min(0.05f)] public float rangedSetupSlotDistance = 1.25f;

    [FormerlySerializedAs("formationRangedSetupMoveSpeedMultiplier")]
    [Tooltip("Movement speed multiplier used while ranged soldiers settle into their firing formation.")]
    [Min(0.1f)] public float rangedSetupMoveSpeedMultiplier = 1.0f;

    [FormerlySerializedAs("formationRangedInitialFireSettleTime")]
    [Tooltip("Short pause after the ranged formation becomes ready before soldiers may begin firing.")]
    [Min(0f)] public float rangedInitialFireSettleTime = 0.25f;


    [FormerlySerializedAs("formationRangedAvoidanceEnterDistance")]
    [Header("Ranged - Avoidance")]
    [Tooltip("Enemy squad-center distance that triggers a simple retreat while ranged avoidance is enabled and ammunition remains.")]
    [Min(0.1f)] public float rangedAvoidanceEnterDistance = 10f;

    [FormerlySerializedAs("formationRangedAvoidanceRetreatDistance")]
    [Tooltip("How far the squad tries to move directly away from the enemy when avoidance triggers.")]
    [Min(0.1f)] public float rangedAvoidanceRetreatDistance = 10f;

    [FormerlySerializedAs("formationRangedAvoidanceRecheckDistance")]
    [Tooltip("When this close to the current avoidance destination, recheck the original threat and immediately chain another retreat if it is still inside avoidance range.")]
    [Min(0.1f)] public float rangedAvoidanceRecheckDistance = 2f;

    [FormerlySerializedAs("formationRangedMeleeFallbackEnabled")]
    [Header("Ranged - Melee Fallback")]
    [Tooltip("Allows a ranged squad with melee sidearms to switch the entire squad into melee mode when enemies breach close range.")]
    public bool rangedMeleeFallbackEnabled = true;

    [FormerlySerializedAs("formationRangedMeleeFallbackEnterDistance")]
    [Tooltip("If any living enemy reaches this distance from any living squad member, the entire ranged squad switches to melee mode.")]
    [Min(0.1f)] public float rangedMeleeFallbackEnterDistance = 5.0f;

    [FormerlySerializedAs("formationRangedMeleeFallbackExitDistance")]
    [Tooltip("The squad returns to ranged mode only after no living enemy remains within this distance and the squad still has ranged ammunition. Keep this above enter distance to prevent flicker.")]
    [Min(0.1f)] public float rangedMeleeFallbackExitDistance = 7.5f;

    
    

   [FormerlySerializedAs("formationAttackIntervalRandomMin")]
   [Header("Attack Timing")]

    [Tooltip("Minimum random value added to the attack interval.")]
    [Min(0f)]
    public float attackIntervalRandomMin = 0f;

    [FormerlySerializedAs("formationAttackIntervalRandomMax")]
    [Tooltip("Maximum random value added to the attack interval.")]
    [Min(0f)]
    public float attackIntervalRandomMax = 1.0f;


    [FormerlySerializedAs("formationReserveForwardGapDistance")]
    [Header("Reserve Settle / Side-Step")]

    [Tooltip("How far ahead a reserve checks for a friendly-body gap before moving forward.")]
    [Min(0f)]
    public float reserveForwardGapDistance = 1.35f;

    [FormerlySerializedAs("formationReserveForwardGapRadius")]
    [Tooltip("Width/radius of the forward gap check. Higher values require reserves to find a wider lane.")]
    [Min(0f)]
    public float reserveForwardGapRadius = 0.60f;

    [FormerlySerializedAs("formationReserveMinimumBlockedSitTimeMin")]
    [Tooltip("Shortest randomized time a newly blocked reserve must wait before repositioning.")]
    [Min(0f)]
    public float reserveMinimumBlockedSitTimeMin = 0.35f;

    [FormerlySerializedAs("formationReserveMinimumBlockedSitTimeMax")]
    [Tooltip("Longest randomized time a newly blocked reserve must wait before repositioning.")]
    [Min(0f)]
    public float reserveMinimumBlockedSitTimeMax = 1.40f;

    [FormerlySerializedAs("formationReserveSideStepEnabled")] [Tooltip("Enables the small local side-step fallback for blocked reserve soldiers.")]
    public bool reserveSideStepEnabled = false;

    [FormerlySerializedAs("formationReserveSideStepIntervalMin")]
    [Tooltip("Shortest randomized cooldown before a reserve can attempt another side-step.")]
    [Min(0f)]
    public float reserveSideStepIntervalMin = 5.0f;

    [FormerlySerializedAs("formationReserveSideStepIntervalMax")]
    [Tooltip("Longest randomized cooldown before a reserve can attempt another side-step.")]
    [Min(0f)]
    public float reserveSideStepIntervalMax = 10.0f;

    [FormerlySerializedAs("formationReserveSideStepDistance")]
    [Tooltip("How far sideways a reserve tries to step when using the side-step fallback.")]
    [Min(0f)]
    public float reserveSideStepDistance = 0.65f;

    [FormerlySerializedAs("formationReserveSideStepOccupancyRadius")]
    [Tooltip("Radius used to reject side-step points already occupied by living soldiers.")]
    [Min(0f)]
    public float reserveSideStepOccupancyRadius = 0.85f;

    [FormerlySerializedAs("formationReserveSideStepSpeedMultiplier")]
    [Tooltip("Movement speed multiplier used while performing a reserve side-step.")]
    [Min(0f)]
    public float reserveSideStepSpeedMultiplier = 0.50f;


    [FormerlySerializedAs("formationMultiSquadLocalTargetingEnabled")]
    [Header("Local Enemy Targeting")]

    [Tooltip("Allows soldiers to locally target nearby enemies from non-primary hostile squads.")]
    public bool multiSquadLocalTargetingEnabled = true;

    [FormerlySerializedAs("formationLocalEnemyTargetSearchRadius")]
    [Tooltip("Maximum distance for considering non-primary enemy soldiers as local reaction targets.")]
    [Min(0f)]
    public float localEnemyTargetSearchRadius = 7.5f;

    [FormerlySerializedAs("formationNonPrimaryTargetPenalty")]
    [Tooltip("Score penalty for non-primary enemies so soldiers still prefer the ordered target squad.")]
    [Min(0f)]
    public float nonPrimaryTargetPenalty = 1.25f;

    [FormerlySerializedAs("formationImmediateContactOverrideEnabled")] [Tooltip("Forces melee soldiers to prefer obvious nearby enemies over farther assigned targets.")]
    public bool immediateContactOverrideEnabled = true;

    [FormerlySerializedAs("formationImmediateContactRangePadding")]
    [Tooltip("Extra range added to attack range when checking for immediate-contact enemies.")]
    [Min(0f)]
    public float immediateContactRangePadding = 0.55f;


    [FormerlySerializedAs("formationApproachSettleGateEnabled")]
    [Header("Approach Settle Gate")]

    [Tooltip("Enables the short initial delay before full melee release when only a few soldiers arrive.")]
    public bool approachSettleGateEnabled = true;

    [FormerlySerializedAs("formationApproachSettleDuration")]
    [Tooltip("How long the squad may wait at first contact for more soldiers to arrive.")]
    [Min(0f)]
    public float approachSettleDuration = 0.75f;

    [FormerlySerializedAs("formationApproachSettleReadyRatio")]
    [Tooltip("Fraction of living soldiers that must be near the enemy to skip or finish the settle gate.")]
    [Min(0f)]
    public float approachSettleReadyRatio = 0.45f;

    [FormerlySerializedAs("formationApproachSettleReadyRangePadding")]
    [Tooltip("Extra range added to combat start range when counting soldiers as approach-ready.")]
    [Min(0f)]
    public float approachSettleReadyRangePadding = 0.95f;

    [FormerlySerializedAs("formationApproachSettleMinimumReadyRange")]
    [Tooltip("Minimum ready-check radius so very small combat start ranges still count nearby soldiers.")]
    [Min(0f)]
    public float approachSettleMinimumReadyRange = 2.75f;


    [FormerlySerializedAs("formationChargeEnabled")]
    [Header("Charge")]

    [Tooltip("Master capability toggle for the ordered melee charge phase.")]
    public bool chargeEnabled = true;

    [FormerlySerializedAs("formationChargeEnabledByDefault")] [Tooltip("Initial runtime charge toggle state for squads using this profile. Charge still requires an OrderedAttack.")]
    public bool chargeEnabledByDefault = true;

    [FormerlySerializedAs("formationChargeMode")] [Tooltip("Selects the behavior used inside SquadState.Charging. RunUp performs a lightweight infantry-style final rush and immediately settles on contact. FullCharge enables momentum, shock damage, penetration, and follow-through.")]
    public ChargeMode chargeMode = ChargeMode.RunUp;

    [FormerlySerializedAs("formationChargeStartDistance")]
    [Tooltip("Farthest closest-soldier distance that allows this squad to enter Charging from ApproachingCombat.")]
    [Min(0f)]
    public float chargeStartDistance = 12.0f; // much higher for cav (~25)

    [FormerlySerializedAs("formationChargeMinimumStartDistance")]
    [Tooltip("Minimum run-up space required to begin a NEW charge. Attack orders issued inside this distance enter normal engagement instead of manufacturing a point-blank charge. Tune this per squad: ordinary infantry can use a shorter window, while cavalry should require substantial run-up space.")]
    [Min(0f)]
    public float chargeMinimumStartDistance = 10.0f; // much higher for cav (~20)

    [FormerlySerializedAs("formationChargeSpeedMultiplier")]
    [Tooltip("Formation-wide movement speed multiplier while charging.")]
    [Min(0f)]
    public float chargeSpeedMultiplier = 1.20f;

    [FormerlySerializedAs("formationChargeMaximumDuration")]
    [Tooltip("Safety time limit before the squad enters combat even if charge contact detection is imperfect.")]
    [Min(0f)]
    public float chargeMaximumDuration = 3.0f;

    [FormerlySerializedAs("formationChargeContactReadyRatio")]
    [Tooltip("Fraction of living melee soldiers that must reach personal attack range before the charge resolves contact. RunUp settles directly into melee; FullCharge begins follow-through.")]
    [Min(0f)]
    public float chargeContactReadyRatio = 0.15f;


    [FormerlySerializedAs("formationChargeMoraleShock")]
    [Tooltip("One-time morale loss applied to the ordered target squad when meaningful charge contact is first reached. Applies to both RunUp and FullCharge; tune ordinary infantry much lower than shock units if desired.")]
    [Min(0f)]
    public float chargeMoraleShock = 8.0f;

    [FormerlySerializedAs("formationChargeLeadSpeedEnabled")] [Tooltip("Enables an additional speed advantage for the soldiers currently closest to the enemy during either RunUp or FullCharge.")]
    public bool chargeLeadSpeedEnabled = true;

    [FormerlySerializedAs("formationChargeLeadSoldierRatio")]
    [Tooltip("Fraction of living melee soldiers treated as the leading edge. Rounded up to at least one soldier.")]
    [Min(0f)]
    public float chargeLeadSoldierRatio = 0.15f;

    [FormerlySerializedAs("formationChargeLeadSpeedMultiplier")]
    [Tooltip("Additional per-soldier movement multiplier applied to soldiers on the current leading edge while charging.")]
    [Min(0f)]
    public float chargeLeadSpeedMultiplier = 1.25f;


    [FormerlySerializedAs("formationFullChargeMinimumImpactSpeedRatio")]
    [Header("Charge - Full Charge")]

    [Tooltip("Minimum fraction of authored charge speed required before FullCharge contact produces shock damage or the momentum-scaled charge impulse. Below this threshold the body may still make contact, but it has not built meaningful shock power.")]
    [Range(0f, 1f)]
    public float fullChargeMinimumImpactSpeedRatio = 0.45f;

    [FormerlySerializedAs("formationFullChargeImpactDamage")]
    [Tooltip("Maximum normal shock damage applied once per enemy soldier per FullCharge. Actual damage scales from current forward charge speed and the charger/receiver body-mass relationship. This is separate from the rider's normal targeted charge attack.")]
    [Min(0)]
    public int fullChargeImpactDamage = 6;

    [FormerlySerializedAs("formationFullChargeImpactArmorPiercingDamage")]
    [Tooltip("Maximum armor-piercing portion of FullCharge shock damage. Scales with the same speed/mass impact factor as normal shock damage.")]
    [Min(0)]
    public int fullChargeImpactArmorPiercingDamage = 1;

    [FormerlySerializedAs("formationChargePenetrationMultiplier")]
    [Tooltip("Multiplier applied to each charging soldier's BodyStats mass to create its per-charge penetration budget. Enemy body mass consumes this budget once per unique charger/enemy contact. FullCharge only.")]
    [Min(0f)]
    public float chargePenetrationMultiplier = 1.0f;

    [FormerlySerializedAs("formationChargeEndSpentRatio")]
    [Tooltip("Fraction of living charging soldiers that must exhaust their penetration before the squad leaves the charge and enters normal melee.")]
    [Range(0f, 1f)]
    public float chargeEndSpentRatio = 0.65f;

    [FormerlySerializedAs("formationChargeFollowThroughMaximumDistance")]
    [FormerlySerializedAs("formationChargeFollowThroughDistance")]
    [Tooltip("Maximum forward distance the formation may travel after meaningful contact. The post-contact direction is locked, so this is a hard penetration ceiling rather than a target-relative destination.")]
    [Min(0f)]
    public float chargeFollowThroughMaximumDistance = 1.75f;

    [FormerlySerializedAs("formationChargeImpulseEnabled")] [Tooltip("Enables the momentum-scaled directional contact impulse emitted by FullCharge soldiers. RunUp relies on its normal weapon-hit impulse instead.")]
    public bool chargeImpulseEnabled = true;

    [FormerlySerializedAs("formationChargeImpulseMagnitude")]
    [Tooltip("FullCharge impulse scale. Final impulse is derived from this value, the charger's body mass, and its current forward charge-speed ratio.")]
    [Min(0f)]
    public float chargeImpulseMagnitude = 4.0f;

    [FormerlySerializedAs("formationChargeImpulseDuration")]
    [Tooltip("Impulse decay duration. Short values make the effect feel like contact weight rather than sustained knockback.")]
    [Min(0f)]
    public float chargeImpulseDuration = 0.09f;

    [FormerlySerializedAs("formationChargeImpulseForwardDistance")]
    [Tooltip("Length of the charge-contact capsule projected in front of each charging soldier.")]
    [Min(0f)]
    public float chargeImpulseForwardDistance = 0.95f;

    [FormerlySerializedAs("formationChargeImpulseRadius")]
    [Tooltip("Radius of each charging soldier's forward contact capsule.")]
    [Min(0f)]
    public float chargeImpulseRadius = 0.65f;

    [FormerlySerializedAs("formationChargeImpulseRadialBlend")]
    [Tooltip("Blend between forward and radial impulse direction. Lower values keep the force mostly forward; higher values add more outward spread.")]
    [Min(0f)]
    public float chargeImpulseRadialBlend = 0.12f;


    [FormerlySerializedAs("formationMeleeHitImpulseEnabled")]
    [Header("Melee Impact")]

    [Tooltip("Enables physical movement feedback on successful melee damage.")]
    public bool meleeHitImpulseEnabled = true;

    [FormerlySerializedAs("formationMeleeHitImpulseMagnitude")]
    [Tooltip("Baseline successful-hit impulse magnitude before receiver body mass is applied.")]
    [Min(0f)]
    public float meleeHitImpulseMagnitude = 3.0f;

    [FormerlySerializedAs("formationMeleeHitImpulseDuration")]
    [Tooltip("Impulse decay duration for successful melee hits. Short values produce a visible strike response without sustained sliding.")]
    [Min(0f)]
    public float meleeHitImpulseDuration = 0.15f;


    [FormerlySerializedAs("formationAttackerCombatLockEnabled")]
    [Header("Attacker Combat Lock")]

    [Tooltip("Enables temporary movement lock for active melee attackers after a move or withdraw order.")]
    public bool attackerCombatLockEnabled = true;

    [FormerlySerializedAs("formationAttackerCombatLockTimeMin")]
    [Tooltip("Shortest time an active melee attacker stays committed after the squad receives a move order.")]
    [Min(0f)]
    public float attackerCombatLockTimeMin = 0.75f;

    [FormerlySerializedAs("formationAttackerCombatLockTimeMax")]
    [Tooltip("Longest time an active melee attacker stays committed after the squad receives a move order.")]
    [Min(0f)]
    public float attackerCombatLockTimeMax = 1.75f;


    [FormerlySerializedAs("formationReserveBehindFriendlyRepositionEnabled")]
    [Header("Reserve Behind-Friendly Reposition")]

    [Tooltip("Enables blocked reserves to move into an open pocket behind a better-positioned friendly.")]
    public bool reserveBehindFriendlyRepositionEnabled = true;

    [FormerlySerializedAs("formationReserveBehindFriendlySearchInterval")]
    [Tooltip("Cooldown between behind-friendly reposition searches for each reserve soldier.")]
    [Min(0f)]
    public float reserveBehindFriendlySearchInterval = 3.5f;

    [FormerlySerializedAs("formationReserveBehindFriendlyAnchorSearchRadius")]
    [Tooltip("Maximum distance for finding friendly anchors that the reserve can queue behind.")]
    [Min(0f)]
    public float reserveBehindFriendlyAnchorSearchRadius = 5.5f;

    [FormerlySerializedAs("formationReserveBehindFriendlyBackOffset")]
    [Tooltip("Distance behind the chosen friendly anchor where the reserve tries to move.")]
    [Min(0f)]
    public float reserveBehindFriendlyBackOffset = 1.45f;

    [FormerlySerializedAs("formationReserveBehindFriendlySideOffset")]
    [Tooltip("Optional left/right offset from the behind point when side probes are enabled.")]
    [Min(0f)]
    public float reserveBehindFriendlySideOffset = 0f;

    [FormerlySerializedAs("formationReserveBehindFriendlyNavMeshProjectionRadius")]
    [Tooltip("Maximum distance allowed when projecting a candidate behind-friendly pocket onto the NavMesh.")]
    [Min(0f)]
    public float reserveBehindFriendlyNavMeshProjectionRadius = 1.15f;

    [FormerlySerializedAs("formationReserveBehindFriendlyOccupancyRadius")]
    [Tooltip("Radius used to reject candidate pockets already occupied by a living soldier.")]
    [Min(0f)]
    public float reserveBehindFriendlyOccupancyRadius = 1.15f;

    [FormerlySerializedAs("formationReserveBehindFriendlyCrowdRadius")]
    [Tooltip("Radius used to count nearby bodies around a candidate pocket.")]
    [Min(0f)]
    public float reserveBehindFriendlyCrowdRadius = 1.65f;

    [FormerlySerializedAs("formationReserveBehindFriendlyMaxNearbyBodies")]
    [Tooltip("Maximum number of nearby living bodies allowed before a candidate pocket is considered crowded.")]
    [Min(0)]
    public int reserveBehindFriendlyMaxNearbyBodies = 1;

    [FormerlySerializedAs("formationReserveBehindFriendlyReachDistance")]
    [Tooltip("Distance from the target pocket at which the reserve considers the reposition complete.")]
    [Min(0f)]
    public float reserveBehindFriendlyReachDistance = 0.18f;

    [FormerlySerializedAs("formationReserveBehindFriendlyMaxMoveDistance")]
    [Tooltip("Maximum distance a reserve is allowed to travel for a behind-friendly reposition.")]
    [Min(0f)]
    public float reserveBehindFriendlyMaxMoveDistance = 10.0f;

    [FormerlySerializedAs("formationReserveBehindFriendlyMinAnchorForwardGain")]
    [Tooltip("Required amount the friendly anchor must be closer to the target than the reserve.")]
    [Min(0f)]
    public float reserveBehindFriendlyMinAnchorForwardGain = 0.85f;

    [FormerlySerializedAs("formationReserveBehindFriendlyMinTargetProgress")]
    [Tooltip("Required amount the candidate point must move the reserve closer to its target.")]
    [Min(0f)]
    public float reserveBehindFriendlyMinTargetProgress = 0.05f;

    [FormerlySerializedAs("formationReserveBehindFriendlySpeedMultiplier")]
    [Tooltip("Movement speed multiplier used while moving to a behind-friendly pocket.")]
    [Min(0f)]
    public float reserveBehindFriendlySpeedMultiplier = 0.60f;

    [FormerlySerializedAs("formationReserveBehindFriendlyCrowdScoreWeight")]
    [Tooltip("Score penalty applied per nearby body when ranking behind-friendly candidate pockets.")]
    [Min(0f)]
    public float reserveBehindFriendlyCrowdScoreWeight = 1.25f;

    [FormerlySerializedAs("formationReserveBehindFriendlyProgressScoreWeight")]
    [Tooltip("Score bonus for candidate pockets that make better progress toward the target.")]
    [Min(0f)]
    public float reserveBehindFriendlyProgressScoreWeight = 0.75f;

    void OnValidate()
    {
        autoTargetScanInterval = Mathf.Max(0.01f, autoTargetScanInterval);
        engageStanceAutoTargetScanRange = Mathf.Max(0f, engageStanceAutoTargetScanRange);
        holdStanceAutoTargetScanRange = Mathf.Max(0f, holdStanceAutoTargetScanRange);

        defaultCombatStartRange = Mathf.Max(0f, defaultCombatStartRange);
        defaultCombatBreakRange = Mathf.Max(defaultCombatStartRange, defaultCombatBreakRange);
        combatApproachRefreshInterval = Mathf.Max(0.01f, combatApproachRefreshInterval);
        defaultApproachStopDistance = Mathf.Max(0f, defaultApproachStopDistance);

        meleeTargetRefreshInterval = Mathf.Max(0.01f, meleeTargetRefreshInterval);
        meleeTargetCrowdingPenalty = Mathf.Max(0f, meleeTargetCrowdingPenalty);
        meleeCurrentTargetStickinessBonus = Mathf.Max(0f, meleeCurrentTargetStickinessBonus);

        fallbackMeleeAttackRange = Mathf.Max(0.1f, fallbackMeleeAttackRange);
        fallbackMeleeAttackInterval = Mathf.Max(0.05f, fallbackMeleeAttackInterval);
        meleeStoppingDistanceMultiplier = Mathf.Max(0.01f, meleeStoppingDistanceMultiplier);
        meleeCombatMoveSpeedMultiplier = Mathf.Max(0.1f, meleeCombatMoveSpeedMultiplier);

        rangedScanRangePadding = Mathf.Max(0f, rangedScanRangePadding);
        rangedCombatStartRangeMultiplier = Mathf.Max(0.1f, rangedCombatStartRangeMultiplier);
        rangedCombatBreakRangePadding = Mathf.Max(0f, rangedCombatBreakRangePadding);
        rangedStoppingDistanceMultiplier = Mathf.Clamp(rangedStoppingDistanceMultiplier, 0.1f, 1f);
        rangedSetupRequiredRatio = Mathf.Clamp01(rangedSetupRequiredRatio);
        rangedSetupSlotDistance = Mathf.Max(0.05f, rangedSetupSlotDistance);
        rangedSetupMoveSpeedMultiplier = Mathf.Max(0.1f, rangedSetupMoveSpeedMultiplier);
        rangedInitialFireSettleTime = Mathf.Max(0f, rangedInitialFireSettleTime);
        rangedAvoidanceEnterDistance = Mathf.Max(0.1f, rangedAvoidanceEnterDistance);
        rangedAvoidanceRetreatDistance = Mathf.Max(0.1f, rangedAvoidanceRetreatDistance);
        rangedAvoidanceRecheckDistance = Mathf.Max(0.1f, rangedAvoidanceRecheckDistance);
        rangedMeleeFallbackEnterDistance = Mathf.Max(0.1f, rangedMeleeFallbackEnterDistance);
        rangedMeleeFallbackExitDistance = Mathf.Max(
            rangedMeleeFallbackEnterDistance + 0.1f,
            rangedMeleeFallbackExitDistance);

        attackIntervalRandomMin = Mathf.Max(0f, attackIntervalRandomMin);
        attackIntervalRandomMax = Mathf.Max(0f, attackIntervalRandomMax);
        reserveForwardGapDistance = Mathf.Max(0f, reserveForwardGapDistance);
        reserveForwardGapRadius = Mathf.Max(0f, reserveForwardGapRadius);
        reserveMinimumBlockedSitTimeMin = Mathf.Max(0f, reserveMinimumBlockedSitTimeMin);
        reserveMinimumBlockedSitTimeMax = Mathf.Max(0f, reserveMinimumBlockedSitTimeMax);
        reserveSideStepIntervalMin = Mathf.Max(0f, reserveSideStepIntervalMin);
        reserveSideStepIntervalMax = Mathf.Max(0f, reserveSideStepIntervalMax);
        reserveSideStepDistance = Mathf.Max(0f, reserveSideStepDistance);
        reserveSideStepOccupancyRadius = Mathf.Max(0f, reserveSideStepOccupancyRadius);
        reserveSideStepSpeedMultiplier = Mathf.Max(0f, reserveSideStepSpeedMultiplier);
        localEnemyTargetSearchRadius = Mathf.Max(0f, localEnemyTargetSearchRadius);
        nonPrimaryTargetPenalty = Mathf.Max(0f, nonPrimaryTargetPenalty);
        immediateContactRangePadding = Mathf.Max(0f, immediateContactRangePadding);
        approachSettleDuration = Mathf.Max(0f, approachSettleDuration);
        approachSettleReadyRatio = Mathf.Clamp01(approachSettleReadyRatio);
        approachSettleReadyRangePadding = Mathf.Max(0f, approachSettleReadyRangePadding);
        approachSettleMinimumReadyRange = Mathf.Max(0f, approachSettleMinimumReadyRange);
        chargeStartDistance = Mathf.Max(0f, chargeStartDistance);
        chargeMinimumStartDistance = Mathf.Clamp(chargeMinimumStartDistance, 0f, chargeStartDistance);
        chargeSpeedMultiplier = Mathf.Max(0f, chargeSpeedMultiplier);
        chargeMaximumDuration = Mathf.Max(0f, chargeMaximumDuration);
        chargeContactReadyRatio = Mathf.Clamp01(chargeContactReadyRatio);
        fullChargeMinimumImpactSpeedRatio = Mathf.Clamp01(fullChargeMinimumImpactSpeedRatio);
        fullChargeImpactDamage = Mathf.Max(0, fullChargeImpactDamage);
        fullChargeImpactArmorPiercingDamage = Mathf.Max(0, fullChargeImpactArmorPiercingDamage);
        chargePenetrationMultiplier = Mathf.Max(0f, chargePenetrationMultiplier);
        chargeEndSpentRatio = Mathf.Clamp01(chargeEndSpentRatio);
        chargeFollowThroughMaximumDistance = Mathf.Max(0f, chargeFollowThroughMaximumDistance);
        chargeMoraleShock = Mathf.Max(0f, chargeMoraleShock);
        chargeImpulseMagnitude = Mathf.Max(0f, chargeImpulseMagnitude);
        chargeImpulseDuration = Mathf.Max(0f, chargeImpulseDuration);
        chargeImpulseForwardDistance = Mathf.Max(0f, chargeImpulseForwardDistance);
        chargeImpulseRadius = Mathf.Max(0f, chargeImpulseRadius);
        chargeImpulseRadialBlend = Mathf.Clamp01(chargeImpulseRadialBlend);
        chargeLeadSoldierRatio = Mathf.Clamp01(chargeLeadSoldierRatio);
        chargeLeadSpeedMultiplier = Mathf.Max(0f, chargeLeadSpeedMultiplier);
        meleeHitImpulseMagnitude = Mathf.Max(0f, meleeHitImpulseMagnitude);
        meleeHitImpulseDuration = Mathf.Max(0f, meleeHitImpulseDuration);
        attackerCombatLockTimeMin = Mathf.Max(0f, attackerCombatLockTimeMin);
        attackerCombatLockTimeMax = Mathf.Max(0f, attackerCombatLockTimeMax);
        reserveBehindFriendlySearchInterval = Mathf.Max(0f, reserveBehindFriendlySearchInterval);
        reserveBehindFriendlyAnchorSearchRadius = Mathf.Max(0f, reserveBehindFriendlyAnchorSearchRadius);
        reserveBehindFriendlyBackOffset = Mathf.Max(0f, reserveBehindFriendlyBackOffset);
        reserveBehindFriendlySideOffset = Mathf.Max(0f, reserveBehindFriendlySideOffset);
        reserveBehindFriendlyNavMeshProjectionRadius = Mathf.Max(0f, reserveBehindFriendlyNavMeshProjectionRadius);
        reserveBehindFriendlyOccupancyRadius = Mathf.Max(0f, reserveBehindFriendlyOccupancyRadius);
        reserveBehindFriendlyCrowdRadius = Mathf.Max(0f, reserveBehindFriendlyCrowdRadius);
        reserveBehindFriendlyMaxNearbyBodies = Mathf.Max(0, reserveBehindFriendlyMaxNearbyBodies);
        reserveBehindFriendlyReachDistance = Mathf.Max(0f, reserveBehindFriendlyReachDistance);
        reserveBehindFriendlyMaxMoveDistance = Mathf.Max(0f, reserveBehindFriendlyMaxMoveDistance);
        reserveBehindFriendlyMinAnchorForwardGain = Mathf.Max(0f, reserveBehindFriendlyMinAnchorForwardGain);
        reserveBehindFriendlyMinTargetProgress = Mathf.Max(0f, reserveBehindFriendlyMinTargetProgress);
        reserveBehindFriendlySpeedMultiplier = Mathf.Max(0f, reserveBehindFriendlySpeedMultiplier);
        reserveBehindFriendlyCrowdScoreWeight = Mathf.Max(0f, reserveBehindFriendlyCrowdScoreWeight);
        reserveBehindFriendlyProgressScoreWeight = Mathf.Max(0f, reserveBehindFriendlyProgressScoreWeight);
        attackIntervalRandomMax = Mathf.Max(attackIntervalRandomMin, attackIntervalRandomMax);
        reserveMinimumBlockedSitTimeMax = Mathf.Max(reserveMinimumBlockedSitTimeMin, reserveMinimumBlockedSitTimeMax);
        reserveSideStepIntervalMax = Mathf.Max(reserveSideStepIntervalMin, reserveSideStepIntervalMax);
        attackerCombatLockTimeMax = Mathf.Max(attackerCombatLockTimeMin, attackerCombatLockTimeMax);
    }
}



