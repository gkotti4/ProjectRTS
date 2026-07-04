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

    /// Returns true if a living friendly soldier is occupying the space directly
    /// ahead of the owner along the desired movement direction.
    public bool IsForwardBlockedByFriendly(
        SoldierController owner,
        Vector3 desiredMoveDirection)
    {
        return TryGetForwardBlockingFriendly(
            owner,
            desiredMoveDirection,
            out _);
    }

    /// Finds the specific friendly body blocking the owner's forward movement.
    /// This is intentionally only a local body-space query.
    public bool TryGetForwardBlockingFriendly(
        SoldierController owner,
        Vector3 desiredMoveDirection,
        out SoldierController blockingFriendly)
    {
        blockingFriendly = null;

        float forwardCheckDistance = 0.65f;
        float bodyCheckRadius = 0.45f;
        float minimumForwardDot = 0.35f;

        if (owner == null || !owner.IsAlive)
            return false;

        desiredMoveDirection = NormalizeFlat(desiredMoveDirection);

        if (desiredMoveDirection == Vector3.zero)
            return false;

        Vector3 checkCenter =
            owner.transform.position +
            desiredMoveDirection * forwardCheckDistance;

        int hitCount = Physics.OverlapSphereNonAlloc(
            checkCenter,
            bodyCheckRadius,
            overlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        float bestForwardDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];

            if (hit == null)
                continue;

            SoldierController other =
                hit.GetComponentInParent<SoldierController>();

            if (!IsValidFriendlyBody(owner, other))
                continue;

            Vector3 toOther =
                other.transform.position - owner.transform.position;

            toOther.y = 0f;

            if (toOther.sqrMagnitude <= 0.0001f)
                continue;

            float forwardDot = Vector3.Dot(
                desiredMoveDirection,
                toOther.normalized);

            if (forwardDot < minimumForwardDot)
                continue;

            float forwardDistance = Vector3.Dot(
                desiredMoveDirection,
                toOther);

            if (forwardDistance < bestForwardDistance)
            {
                bestForwardDistance = forwardDistance;
                blockingFriendly = other;
            }
        }

        return blockingFriendly != null;
    }

    /// Returns true if any living soldier body occupies this point.
    /// This is kept for later tiny experiments, but PrototypeMelee fresh base does
    /// not use reserve slot or open-zone logic.
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
