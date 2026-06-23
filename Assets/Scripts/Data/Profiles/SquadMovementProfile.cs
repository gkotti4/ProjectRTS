using UnityEngine;

[CreateAssetMenu(
    fileName = "SquadMovementProfile_",
    menuName = "Scriptable Objects/Military/Profiles/SquadMovementProfile")]
public class SquadMovementProfile : ScriptableObject
{
    [Header("Slot Following")]
    [Min(0f)] public float slotUpdateThreshold = 0.25f;
    [Min(0f)] public float memberStoppingDistance = 0.1f;

    [Header("Catchup")]
    [Min(0f)] public float catchupStartDistance = 0.1f;
    [Min(0f)] public float catchupMaxDistance = 10f;
    [Min(0.1f)] public float catchupMaxMultiplier = 1.45f;

    [Header("Reform")]
    [Min(0.01f)] public float reformCheckInterval = 0.25f;
    [Min(0f)] public float reformMemberDistance = 1.25f;
    [Range(0f, 1f)] public float reformRatioRequired = 0.75f;

    [Header("Slot Reassignment")]
    public bool reassignSlotsOnLargeFacingChange = true;
    [Range(0f, 180f)] public float reassignFacingAngle = 100f;
}