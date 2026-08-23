
using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// ImpulseEmitter
/// -----------------------------------------------------------------------------
///
/// Shared overlap-based impulse distributor for custom SoldierMotor bodies.
///
/// SoldierMotor remains responsible for receiving and resolving one impulse:
/// mass conversion, accumulated push velocity, decay, body blocking, and final
/// NavMeshAgent movement. This utility only finds valid soldiers and decides the
/// direction/strength delivered to each target.
///
/// Intended uses:
/// - explosions / artillery impacts / shockwaves through EmitSphere
/// - cavalry frontage / monster charges through EmitDirectionalCapsule
/// - temporary gameplay tests without Rigidbody forces
///
/// Design role:
/// Area query, target filtering, distance falloff, and impulse distribution.
///
public static class ImpulseEmitter
{
    // Large enough for current squad-scale effects without allocating every use.
    // NonAlloc overlap calls silently stop at this limit, so unusually large future
    // effects may need a larger buffer or a dedicated central query manager.
    private const int impulseMaximumColliderHits = 128;

    private static readonly Collider[] impulseOverlapBuffer =
        new Collider[impulseMaximumColliderHits];

    private static readonly HashSet<SoldierController> impulseUniqueTargets =
        new HashSet<SoldierController>();

    /// Emits an outward radial impulse from one world-space point.
    ///
    /// Strength uses linear distance falloff:
    /// - origin = full strength
    /// - radius edge = minimumFalloff multiplier
    ///
    /// Returns the number of unique living soldiers that received an impulse.
    public static int EmitSphere(
        Vector3 origin,
        float radius,
        float impulseMagnitude,
        float duration,
        SoldierController sourceSoldier = null,
        bool affectFriendlies = false,
        float minimumFalloff = 0.15f,
        LayerMask layerMask = default,
        ISet<SoldierController> excludedTargets = null,
        ISet<SoldierController> affectedTargets = null)
    {
        radius = Mathf.Max(0.01f, radius);
        impulseMagnitude = Mathf.Max(0f, impulseMagnitude);
        duration = Mathf.Max(0.01f, duration);
        minimumFalloff = Mathf.Clamp01(minimumFalloff);

        if (impulseMagnitude <= 0f)
            return 0;

        int resolvedLayerMask = ResolveLayerMask(layerMask);

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            radius,
            impulseOverlapBuffer,
            resolvedLayerMask,
            QueryTriggerInteraction.Collide);

        impulseUniqueTargets.Clear();

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = impulseOverlapBuffer[hitIndex];

            if (!TryResolveValidTarget(
                    hit,
                    sourceSoldier,
                    affectFriendlies,
                    out SoldierController targetSoldier))
            {
                continue;
            }

            if (excludedTargets != null &&
                excludedTargets.Contains(targetSoldier))
            {
                continue;
            }

            if (!impulseUniqueTargets.Add(targetSoldier))
                continue;

            Vector3 radialDirection =
                targetSoldier.transform.position - origin;

            radialDirection.y = 0f;

            if (radialDirection.sqrMagnitude <= 0.0001f)
                radialDirection = ResolveFallbackDirection(sourceSoldier);

            float distance = Vector3.Distance(
                Flatten(origin),
                Flatten(targetSoldier.transform.position));

            float normalizedDistance = Mathf.Clamp01(distance / radius);
            float distanceStrength = Mathf.Lerp(
                1f,
                minimumFalloff,
                normalizedDistance);

            targetSoldier.Motor.ApplyExternalImpulse(
                radialDirection,
                impulseMagnitude * distanceStrength,
                duration);

            affectedTargets?.Add(targetSoldier);
        }

        return impulseUniqueTargets.Count;
    }

    /// Emits a forward-biased impulse through a capsule-shaped contact volume.
    ///
    /// Useful for cavalry, monsters, shield rushes, or any effect whose force should
    /// follow a movement direction rather than behave like a radial explosion.
    /// radialBlend controls how much targets are pushed away from the capsule center:
    /// - 0 = completely forward
    /// - 1 = completely radial
    ///
    /// Returns the number of unique living soldiers that received an impulse.
    public static int EmitDirectionalCapsule(
        Vector3 capsuleStart,
        Vector3 capsuleEnd,
        float capsuleRadius,
        Vector3 forwardDirection,
        float impulseMagnitude,
        float duration,
        SoldierController sourceSoldier = null,
        bool affectFriendlies = false,
        float radialBlend = 0.15f,
        float minimumFalloff = 0.35f,
        LayerMask layerMask = default,
        ISet<SoldierController> excludedTargets = null,
        ISet<SoldierController> affectedTargets = null)
    {
        capsuleRadius = Mathf.Max(0.01f, capsuleRadius);
        impulseMagnitude = Mathf.Max(0f, impulseMagnitude);
        duration = Mathf.Max(0.01f, duration);
        radialBlend = Mathf.Clamp01(radialBlend);
        minimumFalloff = Mathf.Clamp01(minimumFalloff);

        forwardDirection = NormalizeFlat(forwardDirection);

        if (impulseMagnitude <= 0f || forwardDirection == Vector3.zero)
            return 0;

        int resolvedLayerMask = ResolveLayerMask(layerMask);

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            capsuleStart,
            capsuleEnd,
            capsuleRadius,
            impulseOverlapBuffer,
            resolvedLayerMask,
            QueryTriggerInteraction.Collide);

        impulseUniqueTargets.Clear();

        Vector3 capsuleCenter = (capsuleStart + capsuleEnd) * 0.5f;
        float capsuleLength = Vector3.Distance(
            Flatten(capsuleStart),
            Flatten(capsuleEnd));

        float maximumDistanceFromCenter =
            Mathf.Max(capsuleRadius, capsuleLength * 0.5f + capsuleRadius);

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = impulseOverlapBuffer[hitIndex];

            if (!TryResolveValidTarget(
                    hit,
                    sourceSoldier,
                    affectFriendlies,
                    out SoldierController targetSoldier))
            {
                continue;
            }

            if (excludedTargets != null &&
                excludedTargets.Contains(targetSoldier))
            {
                continue;
            }

            if (!impulseUniqueTargets.Add(targetSoldier))
                continue;

            Vector3 radialDirection =
                targetSoldier.transform.position - capsuleCenter;

            radialDirection.y = 0f;

            if (radialDirection.sqrMagnitude <= 0.0001f)
                radialDirection = forwardDirection;
            else
                radialDirection.Normalize();

            Vector3 resolvedDirection = Vector3.Slerp(
                forwardDirection,
                radialDirection,
                radialBlend);

            resolvedDirection = NormalizeFlat(resolvedDirection);

            float distanceFromCenter = Vector3.Distance(
                Flatten(capsuleCenter),
                Flatten(targetSoldier.transform.position));

            float normalizedDistance = Mathf.Clamp01(
                distanceFromCenter / maximumDistanceFromCenter);

            float distanceStrength = Mathf.Lerp(
                1f,
                minimumFalloff,
                normalizedDistance);

            targetSoldier.Motor.ApplyExternalImpulse(
                resolvedDirection,
                impulseMagnitude * distanceStrength,
                duration);

            affectedTargets?.Add(targetSoldier);
        }

        return impulseUniqueTargets.Count;
    }

    static bool TryResolveValidTarget(
        Collider hit,
        SoldierController sourceSoldier,
        bool affectFriendlies,
        out SoldierController targetSoldier)
    {
        targetSoldier = null;

        if (hit == null)
            return false;

        targetSoldier = hit.GetComponentInParent<SoldierController>();

        if (targetSoldier == null || !targetSoldier.IsAlive)
            return false;

        if (targetSoldier == sourceSoldier)
            return false;

        if (targetSoldier.Motor == null)
            return false;

        if (!affectFriendlies &&
            sourceSoldier != null &&
            AreFriendly(sourceSoldier, targetSoldier))
        {
            return false;
        }

        return true;
    }

    static bool AreFriendly(
        SoldierController sourceSoldier,
        SoldierController targetSoldier)
    {
        if (sourceSoldier == null || targetSoldier == null)
            return false;

        if (sourceSoldier.Faction != null && targetSoldier.Faction != null)
        {
            return sourceSoldier.Faction.teamId ==
                   targetSoldier.Faction.teamId;
        }

        return sourceSoldier.Squad != null &&
               sourceSoldier.Squad == targetSoldier.Squad;
    }

    static int ResolveLayerMask(LayerMask layerMask)
    {
        int unitMask = GameLayers.Instance != null
            ? GameLayers.Instance.UnitLayer.value
            : ~0;

        if (layerMask.value == 0)
            return unitMask;

        return layerMask.value & unitMask;
    }

    static Vector3 ResolveFallbackDirection(
        SoldierController sourceSoldier)
    {
        if (sourceSoldier != null)
        {
            Vector3 sourceForward = NormalizeFlat(
                sourceSoldier.transform.forward);

            if (sourceForward != Vector3.zero)
                return sourceForward;
        }

        return Vector3.forward;
    }

    static Vector3 NormalizeFlat(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        return direction.normalized;
    }

    static Vector3 Flatten(Vector3 position)
    {
        position.y = 0f;
        return position;
    }
}


