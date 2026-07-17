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
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];

            if (hit == null)
                continue;

            SoldierController other =
                hit.GetComponentInParent<SoldierController>();

            if (IsValidFriendlyBody(owner, other))
                return false;
        }

        return true;
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
            ~0,
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
            ~0,
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

    Vector3 NormalizeFlat(Vector3 value)
    {
        value.y = 0f;

        if (value.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        return value.normalized;
    }
}
