using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "SquadMovementProfile_",
    menuName = "Scriptable Objects/Military/Profiles/SquadMovementProfile")]
public class SquadMovementProfile : ScriptableObject
{
    [Header("Slot Following")]

    [Tooltip("Minimum distance a slot target must move before a soldier receives a new slot-follow destination. Higher values reduce path spam; lower values make slot following more responsive.")]
    [Min(0f)] public float slotUpdateThreshold = 0.25f;

    [Tooltip("Stopping distance used when soldiers move toward assigned slots during idle, loose fallback, and reforming.")]
    [FormerlySerializedAs("looseMoveMemberStoppingDistance")]
    [Min(0f)] public float memberStoppingDistance = 0.1f;


    [Header("Formed Movement")]

    [Tooltip("When enabled, the squad uses the virtual formation anchor. When disabled, the squad intentionally uses loose individual slot movement for debugging/fallback testing. Normal use should leave this enabled.")]
    public bool useVirtualFormationMovement = true;

    [Tooltip("Caps the virtual formation anchor speed as a ratio of member movement speed. 1.0 means the anchor cannot move faster than the soldiers' base speed.")]
    [Range(0.1f, 1f)] public float maxAnchorToMemberSpeedRatio = 1.0f;

    [Tooltip("Maximum sideways/slot-error correction speed soldiers use to drift back toward assigned slots during formed movement. Catchup speed is controlled by the shared Catchup values below.")]
    [Min(0f)] public float formedSlotCorrectionSpeed = 1.75f;

    [Tooltip("Extreme slot error distance where the virtual anchor pauses so a badly scattered formation can recover. Normal slot error does not slow the anchor; soldiers catch up instead.")]
    [FormerlySerializedAs("formedEmergencyPauseDistance")]
    [FormerlySerializedAs("formedBreakDistance")]
    [Min(0f)] public float formedAnchorPauseDistance = 7.0f;

    [Tooltip("Fraction of living soldiers that must exceed Formed Anchor Pause Distance before the anchor pauses.")]
    [FormerlySerializedAs("formedEmergencyPauseRatio")]
    [FormerlySerializedAs("formedBreakRatio")]
    [Range(0f, 1f)] public float formedAnchorPauseRatio = 0.65f;

    [Tooltip("Distance from the final destination where the virtual anchor counts as having arrived.")]
    [Min(0.01f)] public float formedAnchorArrivalDistance = 0.25f;

    [Tooltip("When the anchor is within this distance of the destination, the formation starts rotating toward the final ordered facing instead of path direction.")]
    [Min(0f)] public float formedFinalFacingDistance = 2.5f;

    [Tooltip("Allowed angle difference from the final ordered facing before movement is considered visually complete.")]
    [Range(0f, 45f)] public float formedFinalFacingAngle = 3f;

    [Tooltip("Maximum degrees per second the formation facing can rotate during formed movement. Lower values create slower wheeling turns.")]
    [Min(1f)] public float formedTurnSpeedDegrees = 300f;


    [Header("Formation Footprint")]

    [Tooltip("When enabled, the movement system probes the formation footprint ahead of the anchor and falls back to loose movement if the shape cannot fit.")]
    public bool useFootprintValidation = true;

    [Tooltip("How far ahead of the virtual anchor to test the formation footprint before accepting the next formed movement step.")]
    [Min(0f)] public float footprintLookAheadDistance = 0.75f;

    [Tooltip("Radius used when checking whether each footprint probe point is near the NavMesh.")]
    [Min(0.01f)] public float footprintProbeRadius = 0.45f;

    [Tooltip("Maximum allowed distance between a footprint probe point and the nearest NavMesh point. Lower values make footprint validation stricter.")]
    [Min(0f)] public float footprintMaxNavMeshProjection = 0.9f;

    [Tooltip("Optional physics obstacle layers checked by footprint probes. Leave empty/zero to use NavMesh-only footprint checks.")]
    public LayerMask footprintObstacleLayers;

    [Tooltip("Radius used for optional physics obstacle checks at each footprint probe point.")]
    [Min(0.01f)] public float footprintObstacleProbeRadius = 0.35f;


    [Header("Catchup")]

    [Tooltip("Distance from assigned slot where catchup speed begins. Used by formed movement, idle slot following, loose fallback, and reforming.")]
    [FormerlySerializedAs("formedCatchupStartDistance")]
    [Min(0f)] public float catchupStartDistance = 0.75f;

    [Tooltip("Distance from assigned slot where catchup reaches its maximum multiplier.")]
    [FormerlySerializedAs("formedCatchupMaxDistance")]
    [Min(0f)] public float catchupMaxDistance = 10f;

    [Tooltip("Maximum speed multiplier used for all soldier catchup movement.")]
    [FormerlySerializedAs("formedCatchupMaxSpeedMultiplier")]
    [Min(0.1f)] public float catchupMaxMultiplier = 1.25f;


    [Header("Reform")]

    [Tooltip("How often the reform state checks whether enough soldiers are close enough to their assigned slots.")]
    [Min(0.01f)] public float reformCheckInterval = 0.25f;

    [Tooltip("Distance from assigned slot where a soldier counts as successfully reformed. Also used as the loose fallback completion distance.")]
    [FormerlySerializedAs("looseMoveReformDistance")]
    [Min(0f)] public float reformMemberDistance = 1.25f;

    [Tooltip("Fraction of living soldiers that must be close enough to assigned slots before reforming completes. Also used for loose fallback completion.")]
    [FormerlySerializedAs("looseMoveReformRatioRequired")]
    [Range(0f, 1f)] public float reformRatioRequired = 0.75f;


    [Header("Slot Reassignment")]

    [Tooltip("When enabled, sharp move orders may reassign soldiers to nearest slots instead of rotating the whole grid through itself.")]
    public bool reassignSlotsOnLargeFacingChange = true;

    [Tooltip("Facing angle difference required before large-facing slot reassignment is considered.")]
    [Range(0f, 180f)] public float reassignFacingAngle = 100f;

    void OnValidate()
    {
        slotUpdateThreshold = Mathf.Max(0f, slotUpdateThreshold);
        memberStoppingDistance = Mathf.Max(0f, memberStoppingDistance);

        maxAnchorToMemberSpeedRatio = Mathf.Clamp(maxAnchorToMemberSpeedRatio, 0.1f, 1f);
        formedSlotCorrectionSpeed = Mathf.Max(0f, formedSlotCorrectionSpeed);
        formedAnchorPauseDistance = Mathf.Max(0f, formedAnchorPauseDistance);
        formedAnchorPauseRatio = Mathf.Clamp01(formedAnchorPauseRatio);
        formedAnchorArrivalDistance = Mathf.Max(0.01f, formedAnchorArrivalDistance);
        formedFinalFacingDistance = Mathf.Max(0f, formedFinalFacingDistance);
        formedFinalFacingAngle = Mathf.Clamp(formedFinalFacingAngle, 0f, 45f);
        formedTurnSpeedDegrees = Mathf.Max(1f, formedTurnSpeedDegrees);

        footprintLookAheadDistance = Mathf.Max(0f, footprintLookAheadDistance);
        footprintProbeRadius = Mathf.Max(0.01f, footprintProbeRadius);
        footprintMaxNavMeshProjection = Mathf.Max(0f, footprintMaxNavMeshProjection);
        footprintObstacleProbeRadius = Mathf.Max(0.01f, footprintObstacleProbeRadius);

        catchupStartDistance = Mathf.Max(0f, catchupStartDistance);
        catchupMaxDistance = Mathf.Max(catchupStartDistance, catchupMaxDistance);
        catchupMaxMultiplier = Mathf.Max(0.1f, catchupMaxMultiplier);

        reformCheckInterval = Mathf.Max(0.01f, reformCheckInterval);
        reformMemberDistance = Mathf.Max(0f, reformMemberDistance);
        reformRatioRequired = Mathf.Clamp01(reformRatioRequired);

        reassignFacingAngle = Mathf.Clamp(reassignFacingAngle, 0f, 180f);
    }

}
