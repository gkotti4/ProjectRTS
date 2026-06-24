using UnityEngine;

[CreateAssetMenu(
    fileName = "SoldierCombatProfile_",
    menuName = "Scriptable Objects/Military/Profiles/SoldierCombatProfile")]
public class SoldierCombatProfile : ScriptableObject
{
    [Header("Combat Rhythm / Recovery")]
    [Min(0.05f)] public float combatRecoveryMinDuration = 2f;
    [Min(0.05f)] public float combatRecoveryMaxDuration = 4.75f;
    [Range(0f, 1f)] public float combatLongRecoveryChance = 0.24f;
    [Min(0.05f)] public float combatLongRecoveryMinDuration = 3f;
    [Min(0.05f)] public float combatLongRecoveryMaxDuration = 7f;
    [Range(0f, 1f)] public float combatRecoveryMoveChance = 0.65f;
    [Range(0f, 1f)] public float combatRecoveryReleaseTargetChance = 0.35f;
    [Min(0f)] public float combatRecoveryBackoffDistance = 1.15f;
    [Min(0f)] public float combatRecoverySideStepDistance = 0.7f;
    [Min(0.1f)] public float combatRecoveryMoveSpeedMultiplier = 0.55f;
    [Min(0f)] public float combatRecoveryStoppingDistance = 0.08f;

    [Header("Pressure Waiting")]
    [Min(0f)] public float pressureWaitDistance = 0.7f;
    [Min(0.05f)] public float pressureWaitMinDuration = 1f;
    [Min(0.05f)] public float pressureWaitMaxDuration = 2.5f;
    [Range(0f, 1f)] public float pressureShuffleChance = 0.35f;
    [Min(0f)] public float pressureShuffleSideDistance = 0.65f;
    [Min(0f)] public float pressureShuffleForwardDistance = 0.2f;
    [Min(0.1f)] public float pressureShuffleMoveSpeedMultiplier = 0.55f;

    [Header("Target Crowding")]
    [Min(1)] public int preferredAttackersPerTarget = 2;
    [Min(0f)] public float targetCrowdingPenalty = 2.25f;
    [Min(0f)] public float crowdedTargetExtraPenaltyDistance = 0.75f;

    [Header("Hit Reaction")]
    public bool hitReactionEnabled = true;
    [Range(0f, 1f)] public float hitReactionChance = 0.55f;
    [Range(0f, 1f)] public float hitReactionDamageChanceBonus = 0.25f;
    [Min(0f)] public float hitReactionCooldown = 0.95f;
    [Min(0f)] public float hitReactionRecoveryExtension = 0.45f;

    [Header("Combat Discipline / Home Bias")]
    [Range(0f, 1f)] public float combatRecoveryHomeBias = 0.35f;
    [Range(0f, 1f)] public float pressureShuffleHomeBias = 0.15f;

    void OnValidate()
    {
        combatRecoveryMinDuration = Mathf.Max(0.05f, combatRecoveryMinDuration);
        combatRecoveryMaxDuration = Mathf.Max(combatRecoveryMinDuration, combatRecoveryMaxDuration);
        combatLongRecoveryChance = Mathf.Clamp01(combatLongRecoveryChance);
        combatLongRecoveryMinDuration = Mathf.Max(0.05f, combatLongRecoveryMinDuration);
        combatLongRecoveryMaxDuration = Mathf.Max(combatLongRecoveryMinDuration, combatLongRecoveryMaxDuration);
        combatRecoveryMoveChance = Mathf.Clamp01(combatRecoveryMoveChance);
        combatRecoveryReleaseTargetChance = Mathf.Clamp01(combatRecoveryReleaseTargetChance);
        combatRecoveryBackoffDistance = Mathf.Max(0f, combatRecoveryBackoffDistance);
        combatRecoverySideStepDistance = Mathf.Max(0f, combatRecoverySideStepDistance);
        combatRecoveryMoveSpeedMultiplier = Mathf.Max(0.1f, combatRecoveryMoveSpeedMultiplier);
        combatRecoveryStoppingDistance = Mathf.Max(0f, combatRecoveryStoppingDistance);

        pressureWaitDistance = Mathf.Max(0f, pressureWaitDistance);
        pressureWaitMinDuration = Mathf.Max(0.05f, pressureWaitMinDuration);
        pressureWaitMaxDuration = Mathf.Max(pressureWaitMinDuration, pressureWaitMaxDuration);
        pressureShuffleChance = Mathf.Clamp01(pressureShuffleChance);
        pressureShuffleSideDistance = Mathf.Max(0f, pressureShuffleSideDistance);
        pressureShuffleForwardDistance = Mathf.Max(0f, pressureShuffleForwardDistance);
        pressureShuffleMoveSpeedMultiplier = Mathf.Max(0.1f, pressureShuffleMoveSpeedMultiplier);

        preferredAttackersPerTarget = Mathf.Max(1, preferredAttackersPerTarget);
        targetCrowdingPenalty = Mathf.Max(0f, targetCrowdingPenalty);
        crowdedTargetExtraPenaltyDistance = Mathf.Max(0f, crowdedTargetExtraPenaltyDistance);

        hitReactionChance = Mathf.Clamp01(hitReactionChance);
        hitReactionDamageChanceBonus = Mathf.Clamp01(hitReactionDamageChanceBonus);
        hitReactionCooldown = Mathf.Max(0f, hitReactionCooldown);
        hitReactionRecoveryExtension = Mathf.Max(0f, hitReactionRecoveryExtension);

        combatRecoveryHomeBias = Mathf.Clamp01(combatRecoveryHomeBias);
        pressureShuffleHomeBias = Mathf.Clamp01(pressureShuffleHomeBias);
    }
}
