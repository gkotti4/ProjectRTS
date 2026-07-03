using UnityEngine;

[CreateAssetMenu(
    fileName = "SoldierCombatProfile_",
    menuName = "Scriptable Objects/Military/Profiles/SoldierCombatProfile")]
public class SoldierCombatProfile : ScriptableObject
{
    [Header("Combat Rhythm / Recovery")]
    [Header("Melee Post-Attack Recovery - FormationMelee")]

    [Tooltip("Minimum recovery duration after a melee soldier completes an attack.")]
    [Min(0.05f)] public float meleeCombatRecoveryMinDuration = 2f;

    [Tooltip("Maximum recovery duration after a melee soldier completes an attack.")]
    [Min(0.05f)] public float meleeCombatRecoveryMaxDuration = 4.0f;

    [Tooltip("Chance that a melee soldier uses the longer recovery duration range after attacking.")]
    [Range(0f, 1f)] public float meleeCombatLongRecoveryChance = 0.24f;

    [Tooltip("Minimum duration for a long melee recovery.")]
    [Min(0.05f)] public float meleeCombatLongRecoveryMinDuration = 3f;

    [Tooltip("Maximum duration for a long melee recovery.")]
    [Min(0.05f)] public float meleeCombatLongRecoveryMaxDuration = 7f;

    [Tooltip("Chance that a melee soldier moves during recovery instead of standing in place.")]
    [Range(0f, 1f)] public float meleeCombatRecoveryMoveChance = 0.65f;

    [Tooltip("Chance that a melee soldier releases its current target during recovery, allowing it to pick a new local target afterward.")]
    [Range(0f, 1f)] public float meleeCombatRecoveryReleaseTargetChance = 0.35f;

    [Tooltip("How far a melee soldier may step backward during post-attack recovery movement.")]
    [Min(0f)] public float meleeCombatRecoveryBackoffDistance = 1.15f;

    [Tooltip("How far a melee soldier may sidestep during post-attack recovery movement.")]
    [Min(0f)] public float meleeCombatRecoverySideStepDistance = 0.7f;

    [Tooltip("Movement speed multiplier used while a melee soldier performs recovery movement.")]
    [Min(0.1f)] public float meleeCombatRecoveryMoveSpeedMultiplier = 0.55f;

    [Tooltip("Stopping distance used when a melee soldier moves to its recovery point.")]
    [Min(0f)] public float meleeCombatRecoveryStoppingDistance = 0.08f;


    [Header("Target Crowding - FormationMelee")]

    [Tooltip("Preferred number of friendly attackers assigned to the same enemy target before crowding penalties are applied.")]
    [Min(1)] public int meleePreferredAttackersPerTarget = 2;

    [Tooltip("Distance-like score penalty added when evaluating enemy targets that already have enough friendly attackers.")]
    [Min(0f)] public float meleeTargetCrowdingPenalty = 2.25f;

    [Tooltip("Additional target score penalty applied when an enemy target is already crowded.")]
    [Min(0f)] public float meleeCrowdedTargetExtraPenaltyDistance = 0.75f;


    [Header("Context Anchor Bias - FormationMelee")]

    [Tooltip("How strongly recovery movement is biased back toward the soldier's current combat context anchor. Higher values keep recovery closer to the squad-provided combat position.")]
    [Range(0f, 1f)] public float combatRecoveryHomeBias = 0.35f;

    [Tooltip("How strongly pressure shuffle movement is biased back toward the soldier's current combat context anchor. Higher values keep waiting/shuffling soldiers closer to their assigned combat area.")]
    [Range(0f, 1f)] public float pressureShuffleHomeBias = 0.15f;


    [Header("Ranged Rhythm")]
    [Header("Ranged Post-Shot Recovery")]

    [Tooltip("Ranged soldiers should usually reload/ready in place instead of doing melee-style backoff shuffles.")]
    public bool rangedHoldPositionDuringRecovery = true;

    [Tooltip("Short post-shot visual/rhythm pause. AttackInterval still controls the real fire rate.")]
    [Min(0.05f)] public float rangedRecoveryMinDuration = 0.25f;

    [Tooltip("Short post-shot visual/rhythm pause. AttackInterval still controls the real fire rate.")]
    [Min(0.05f)] public float rangedRecoveryMaxDuration = 0.75f;

    [Tooltip("Movement speed multiplier used when a ranged soldier steps forward to get into missile range.")]
    [Min(0.1f)] public float rangedMoveSpeedMultiplier = 1.0f;

    [Tooltip("Small buffer used when a ranged soldier moves toward its preferred firing distance.")]
    [Min(0f)] public float rangedPreferredRangeBuffer = 0.35f;


    [Header("All Combat Styles - Pressure Waiting")]

    [Tooltip("Distance from the pressure goal at which a soldier may stop and wait instead of continuing to micro-move.")]
    [Min(0f)] public float pressureWaitDistance = 0.7f;

    [Tooltip("Minimum time a soldier waits near its pressure goal before trying another action.")]
    [Min(0.05f)] public float pressureWaitMinDuration = 1f;

    [Tooltip("Maximum time a soldier waits near its pressure goal before trying another action.")]
    [Min(0.05f)] public float pressureWaitMaxDuration = 2.5f;

    [Tooltip("Chance that a waiting soldier performs a small shuffle movement instead of standing still.")]
    [Range(0f, 1f)] public float pressureShuffleChance = 0.35f;

    [Tooltip("Maximum sideways distance used when generating a pressure shuffle point.")]
    [Min(0f)] public float pressureShuffleSideDistance = 0.65f;

    [Tooltip("Maximum forward distance used when generating a pressure shuffle point.")]
    [Min(0f)] public float pressureShuffleForwardDistance = 0.2f;

    [Tooltip("Movement speed multiplier used while a soldier performs pressure shuffle movement.")]
    [Min(0.1f)] public float pressureShuffleMoveSpeedMultiplier = 0.55f;


    [Header("Hit Reaction")]

    [Tooltip("If true, soldiers can play hit reaction animations after receiving damage.")]
    public bool hitReactionEnabled = true;

    [Tooltip("Base chance that a soldier reacts when hit.")]
    [Range(0f, 1f)] public float hitReactionChance = 0.55f;

    [Tooltip("Additional hit reaction chance added when the incoming hit deals meaningful damage.")]
    [Range(0f, 1f)] public float hitReactionDamageChanceBonus = 0.25f;

    [Tooltip("Cooldown after a hit reaction before this soldier can react again.")]
    [Min(0f)] public float hitReactionCooldown = 0.95f;

    [Tooltip("Extra recovery time added after a soldier plays a hit reaction.")]
    [Min(0f)] public float hitReactionRecoveryExtension = 0.45f;
    
    
    

    void OnValidate()
    {
        meleeCombatRecoveryMinDuration = Mathf.Max(0.05f, meleeCombatRecoveryMinDuration);
        meleeCombatRecoveryMaxDuration = Mathf.Max(meleeCombatRecoveryMinDuration, meleeCombatRecoveryMaxDuration);
        meleeCombatLongRecoveryChance = Mathf.Clamp01(meleeCombatLongRecoveryChance);
        meleeCombatLongRecoveryMinDuration = Mathf.Max(0.05f, meleeCombatLongRecoveryMinDuration);
        meleeCombatLongRecoveryMaxDuration = Mathf.Max(meleeCombatLongRecoveryMinDuration, meleeCombatLongRecoveryMaxDuration);
        meleeCombatRecoveryMoveChance = Mathf.Clamp01(meleeCombatRecoveryMoveChance);
        meleeCombatRecoveryReleaseTargetChance = Mathf.Clamp01(meleeCombatRecoveryReleaseTargetChance);
        meleeCombatRecoveryBackoffDistance = Mathf.Max(0f, meleeCombatRecoveryBackoffDistance);
        meleeCombatRecoverySideStepDistance = Mathf.Max(0f, meleeCombatRecoverySideStepDistance);
        meleeCombatRecoveryMoveSpeedMultiplier = Mathf.Max(0.1f, meleeCombatRecoveryMoveSpeedMultiplier);
        meleeCombatRecoveryStoppingDistance = Mathf.Max(0f, meleeCombatRecoveryStoppingDistance);

        rangedRecoveryMinDuration = Mathf.Max(0.05f, rangedRecoveryMinDuration);
        rangedRecoveryMaxDuration = Mathf.Max(rangedRecoveryMinDuration, rangedRecoveryMaxDuration);
        rangedMoveSpeedMultiplier = Mathf.Max(0.1f, rangedMoveSpeedMultiplier);
        rangedPreferredRangeBuffer = Mathf.Max(0f, rangedPreferredRangeBuffer);

        pressureWaitDistance = Mathf.Max(0f, pressureWaitDistance);
        pressureWaitMinDuration = Mathf.Max(0.05f, pressureWaitMinDuration);
        pressureWaitMaxDuration = Mathf.Max(pressureWaitMinDuration, pressureWaitMaxDuration);
        pressureShuffleChance = Mathf.Clamp01(pressureShuffleChance);
        pressureShuffleSideDistance = Mathf.Max(0f, pressureShuffleSideDistance);
        pressureShuffleForwardDistance = Mathf.Max(0f, pressureShuffleForwardDistance);
        pressureShuffleMoveSpeedMultiplier = Mathf.Max(0.1f, pressureShuffleMoveSpeedMultiplier);

        meleePreferredAttackersPerTarget = Mathf.Max(1, meleePreferredAttackersPerTarget);
        meleeTargetCrowdingPenalty = Mathf.Max(0f, meleeTargetCrowdingPenalty);
        meleeCrowdedTargetExtraPenaltyDistance = Mathf.Max(0f, meleeCrowdedTargetExtraPenaltyDistance);

        hitReactionChance = Mathf.Clamp01(hitReactionChance);
        hitReactionDamageChanceBonus = Mathf.Clamp01(hitReactionDamageChanceBonus);
        hitReactionCooldown = Mathf.Max(0f, hitReactionCooldown);
        hitReactionRecoveryExtension = Mathf.Max(0f, hitReactionRecoveryExtension);

        combatRecoveryHomeBias = Mathf.Clamp01(combatRecoveryHomeBias);
        pressureShuffleHomeBias = Mathf.Clamp01(pressureShuffleHomeBias);
    }
}
