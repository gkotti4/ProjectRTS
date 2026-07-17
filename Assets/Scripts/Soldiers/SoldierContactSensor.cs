using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierContactSensor
/// -----------------------------------------------------------------------------
///
/// Temporary prototype body-space sensor.
/// It does not decide combat.
/// It does not move the soldier.
/// It only answers simple local body-space questions for PrototypeMelee.
///
[DisallowMultipleComponent]
public class SoldierContactSensor : MonoBehaviour
{
    [Header("Forward Gap Debug")]
    [Tooltip("Draws the most recent reserve forward-gap capsule. Green means open, red means blocked, and a yellow line points to the friendly soldier that caused the block.")]
    [SerializeField] private bool debugDrawForwardGapCheck = true;

    [Tooltip("How long each debug shape remains visible. Zero redraws it for one frame, which is ideal while the combat check runs every Update.")]
    [Min(0f)]
    [SerializeField] private float debugForwardGapDrawDuration = 0f;

    [Range(6, 32)]
    [SerializeField] private int debugForwardGapCircleSegments = 16;

    private readonly Collider[] overlapBuffer = new Collider[32];

    /// Returns true when no living friendly body occupies the requested forward
    /// gap. SquadCombat uses this as permission for reserve soldiers to move up.
    public bool IsForwardFriendlyGapOpen(
        SoldierController owner,
        Vector3 desiredMoveDirection,
        float gapDistance,
        float gapRadius)
    {
        if (owner == null || !owner.IsAlive)
            return false;

        desiredMoveDirection = NormalizeFlat(desiredMoveDirection);

        if (desiredMoveDirection == Vector3.zero)
            return false;

        gapDistance = Mathf.Max(0.05f, gapDistance);
        gapRadius = Mathf.Max(0.01f, gapRadius);

        Vector3 startPoint =
            owner.transform.position +
            desiredMoveDirection * 0.25f;

        Vector3 endPoint =
            owner.transform.position +
            desiredMoveDirection * gapDistance;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            startPoint,
            endPoint,
            gapRadius,
            overlapBuffer,
            GetBodyQueryLayerMask(),
            QueryTriggerInteraction.Collide);

        SoldierController blockingFriendly = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];

            if (hit == null)
                continue;

            SoldierController other =
                hit.GetComponentInParent<SoldierController>();

            if (!IsValidFriendlyBody(owner, other))
                continue;

            blockingFriendly = other;
            break;
        }

        bool isGapOpen = blockingFriendly == null;

        DrawForwardGapDebug(
            owner,
            startPoint,
            endPoint,
            gapRadius,
            isGapOpen,
            blockingFriendly);

        return isGapOpen;
    }



    void DrawForwardGapDebug(
        SoldierController owner,
        Vector3 startPoint,
        Vector3 endPoint,
        float radius,
        bool isGapOpen,
        SoldierController blockingFriendly)
    {
        if (!debugDrawForwardGapCheck)
            return;

        Color capsuleColor = isGapOpen ? Color.green : Color.red;
        int segmentCount = Mathf.Clamp(debugForwardGapCircleSegments, 6, 32);
        float duration = Mathf.Max(0f, debugForwardGapDrawDuration);

        DrawWireCircle(startPoint, radius, capsuleColor, segmentCount, duration);
        DrawWireCircle(endPoint, radius, capsuleColor, segmentCount, duration);

        Vector3 forward = endPoint - startPoint;
        forward.y = 0f;

        Vector3 side = forward.sqrMagnitude > 0.0001f
            ? Vector3.Cross(Vector3.up, forward.normalized) * radius
            : Vector3.right * radius;

        Debug.DrawLine(startPoint + side, endPoint + side, capsuleColor, duration);
        Debug.DrawLine(startPoint - side, endPoint - side, capsuleColor, duration);

        Debug.DrawLine(startPoint, endPoint, capsuleColor, duration);

        if (owner != null)
        {
            Debug.DrawLine(
                owner.transform.position,
                startPoint,
                Color.cyan,
                duration);
        }

        if (blockingFriendly != null)
        {
            Debug.DrawLine(
                (startPoint + endPoint) * 0.5f,
                blockingFriendly.transform.position,
                Color.yellow,
                duration);
        }
    }

    void DrawWireCircle(
        Vector3 center,
        float radius,
        Color color,
        int segmentCount,
        float duration)
    {
        float step = Mathf.PI * 2f / segmentCount;
        Vector3 previousPoint = center + Vector3.right * radius;

        for (int segmentIndex = 1; segmentIndex <= segmentCount; segmentIndex++)
        {
            float angle = step * segmentIndex;
            Vector3 nextPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);

            Debug.DrawLine(previousPoint, nextPoint, color, duration);
            previousPoint = nextPoint;
        }
    }

        /// Returns true if a living friendly soldier is occupying one side of the owner.
    /// This is intentionally only a side-boundary query; SquadCombat decides what
    /// to do with the result.
    public bool IsSideBlockedByFriendly(
        SoldierController owner,
        Vector3 sideDirection)
    {
        float sideCheckDistance = 0.75f;
        float bodyCheckRadius = 0.45f;

        if (owner == null || !owner.IsAlive)
            return false;

        sideDirection = NormalizeFlat(sideDirection);

        if (sideDirection == Vector3.zero)
            return false;

        Vector3 checkCenter =
            owner.transform.position +
            sideDirection * sideCheckDistance;

        int hitCount = Physics.OverlapSphereNonAlloc(
            checkCenter,
            bodyCheckRadius,
            overlapBuffer,
            GetBodyQueryLayerMask(),
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];

            if (hit == null)
                continue;

            SoldierController other =
                hit.GetComponentInParent<SoldierController>();

            if (IsValidFriendlyBody(owner, other))
                return true;
        }

        return false;
    }

    /// Returns true if any living soldier body occupies this point.
    /// Used by small local combat movement checks. It still does not decide behavior.
    public bool IsPointOccupiedByLivingSoldier(
        SoldierController owner,
        Vector3 point,
        float radius)
    {
        radius = Mathf.Max(0.01f, radius);

        int hitCount = Physics.OverlapSphereNonAlloc(
            point,
            radius,
            overlapBuffer,
            GetBodyQueryLayerMask(),
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];

            if (hit == null)
                continue;

            SoldierController other =
                hit.GetComponentInParent<SoldierController>();

            if (other == null || other == owner || !other.IsAlive)
                continue;

            return true;
        }

        return false;
    }

    bool IsValidFriendlyBody(
        SoldierController owner,
        SoldierController other)
    {
        if (owner == null || other == null)
            return false;

        if (other == owner)
            return false;

        if (!other.IsAlive)
            return false;

        return other.Squad == owner.Squad;
    }


    /// Returns every physics layer except the large selection-only collider layer.
    /// Selection colliders are intentionally oversized for input and must never
    /// participate in body-space occupancy or forward-gap decisions.
    static int GetBodyQueryLayerMask()
    {
        return GameLayers.Instance != null
            ? GameLayers.Instance.UnitLayer.value
            : ~0;
    }

    Vector3 NormalizeFlat(Vector3 value)
    {
        value.y = 0f;

        if (value.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        return value.normalized;
    }
}
