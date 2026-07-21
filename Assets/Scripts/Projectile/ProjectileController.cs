using UnityEngine;
using UnityEngine.Serialization;

/// -----------------------------------------------------------------------------
/// ProjectileController
/// -----------------------------------------------------------------------------
///
/// Simple ranged projectile for the ranged MVP.
/// It does not use physics, obstruction, line of sight, friendly fire, or real
/// arrow collision yet. It visually travels from the attack origin to a planned
/// impact point and applies a pre-resolved ranged hit on arrival.
///
/// Design role:
/// Visual/damage carrier spawned from a ranged weapon animation event.
///
public class ProjectileController : MonoBehaviour
{
    [Header("Flight")]
    [Tooltip("How close the projectile must get to its planned impact point before it counts as arrived. Higher values make impacts more forgiving.")]
    [SerializeField] private float hitDistance = 0.25f;

    [Tooltip("Safety lifetime in seconds. If the projectile somehow never reaches its impact point, it destroys itself after this time.")]
    [SerializeField] private float maxLifetime = 8f;

    [Tooltip("World-space offset added to the target soldier position. Use this to aim at chest/head height instead of the soldier's feet.")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.5f, 0f);


    [Header("Arc")]
    [SerializeField] private bool useArc = true;

    [Tooltip("Base arc height before distance scaling is added.")]
    [SerializeField] private float arcHeight = 1.0f;

    [Tooltip("Additional arc height added per meter of horizontal shot distance.")]
    [SerializeField] private float arcHeightPerMeter = 0.22f;

    [Tooltip("Minimum visual arc height for short shots.")]
    [SerializeField] private float minArcHeight = 1.0f;

    [Tooltip("Maximum visual arc height so long shots do not become cartoon lobs.")]
    [SerializeField] private float maxArcHeight = 15f;

    [Tooltip("Minimum flight duration so very short shots do not instantly snap to the impact point.")]
    [SerializeField] private float minimumTravelTime = 0.08f;
    

    [FormerlySerializedAs("useSnapshotImpactPoint")]
    [Tooltip("Keeps arrows on a fixed launch-to-impact path instead of bending/homing toward a moving target.")]
    [SerializeField] private bool useNonHomingArrows = true;


    [Header("Miss Presentation")]
    [Tooltip("If true, the projectile rolls hit/miss when launched. This lets missed shots visibly fly wide instead of reaching the target and then disappearing.")]
    [SerializeField] private bool resolveHitAtLaunch = true;

    [Tooltip("If true, missed shots aim at a random point near the target instead of the target body.")]
    [SerializeField] private bool useVisualMissOffset = true;

    [Tooltip("Maximum distance from the target point where missed shots may land. Higher values make misses more visibly inaccurate.")]
    [SerializeField] private float visualMissRadius = 1.25f;


    [Header("Impact FX")]
    [Tooltip("Optional effect spawned when a projectile successfully hits the target.")]
    [SerializeField] private GameObject hitImpactPrefab;

    [Tooltip("Optional effect spawned when a projectile misses and lands near the target.")]
    [SerializeField] private GameObject missImpactPrefab;

    private SoldierController attacker;
    private SoldierController target;

    private RangedCombatStats rangedStats;
    private DamageResult plannedDamageResult;

    private Vector3 launchPoint;
    private Vector3 impactPoint;
    private Vector3 previousPosition;

    private float projectileSpeed = 18f;
    private float lifetimeTimer = 0f;
    private float travelTimer = 0f;
    private float travelDuration = 0.1f;
    private bool hasInitialized = false;
    
    void OnValidate()
    {
        hitDistance = Mathf.Max(0.01f, hitDistance);
        maxLifetime = Mathf.Max(0.1f, maxLifetime);

        arcHeight = Mathf.Max(0f, arcHeight);
        arcHeightPerMeter = Mathf.Max(0f, arcHeightPerMeter);
        minArcHeight = Mathf.Max(0f, minArcHeight);
        maxArcHeight = Mathf.Max(minArcHeight, maxArcHeight);

        minimumTravelTime = Mathf.Max(0.01f, minimumTravelTime);
        visualMissRadius = Mathf.Max(0f, visualMissRadius);
    }

    public void Initialize(
        SoldierController source,
        SoldierController targetSoldier,
        WeaponProfile weaponProfile)
    {
        attacker = source;
        target = targetSoldier;

        rangedStats = source != null && source.Stats != null
            ? source.Stats.ranged
            : weaponProfile != null
                ? weaponProfile.ranged
                : RangedCombatStats.Default;

        projectileSpeed = Mathf.Max(0.1f, rangedStats.projectileSpeed);

        InitializeFlight();
    }
    

    void InitializeFlight()
    {
        launchPoint = transform.position;
        previousPosition = launchPoint;
        travelTimer = 0f;
        lifetimeTimer = Mathf.Max(0.1f, maxLifetime);

        plannedDamageResult = ResolvePlannedDamageResult();
        impactPoint = ResolveInitialImpactPoint();

        float distance = Vector3.Distance(launchPoint, impactPoint);
        travelDuration = Mathf.Max(
            minimumTravelTime,
            distance / Mathf.Max(0.1f, projectileSpeed));

        hasInitialized = true;
    }

    void Update()
    {
        if (!hasInitialized)
            return;

        lifetimeTimer -= Time.deltaTime;

        if (lifetimeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null || !target.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        if (!useNonHomingArrows && plannedDamageResult.didHit)
            impactPoint = GetTargetPoint(target);

        TickFlight();
    }

    void TickFlight()
    {
        travelTimer += Time.deltaTime;

        float progress = travelDuration > 0f
            ? Mathf.Clamp01(travelTimer / travelDuration)
            : 1f;

        Vector3 newPosition = EvaluateArcPosition(progress);
        Vector3 movementDirection = newPosition - previousPosition;

        transform.position = newPosition;

        if (movementDirection.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(movementDirection.normalized, Vector3.up);

        previousPosition = newPosition;

        if (progress >= 1f || Vector3.Distance(transform.position, impactPoint) <= hitDistance)
            ApplyImpact();
    }

    Vector3 EvaluateArcPosition(float progress)
    {
        Vector3 linearPosition = Vector3.Lerp(
            launchPoint,
            impactPoint,
            progress);

        if (!useArc)
            return linearPosition;

        float resolvedArcHeight = GetResolvedArcHeight();
        float arcOffset = Mathf.Sin(progress * Mathf.PI) * resolvedArcHeight;

        return linearPosition + Vector3.up * arcOffset;
    }

    float GetResolvedArcHeight()
    {
        Vector3 flatLaunchPoint = launchPoint;
        Vector3 flatImpactPoint = impactPoint;

        flatLaunchPoint.y = 0f;
        flatImpactPoint.y = 0f;

        float horizontalDistance = Vector3.Distance(
            flatLaunchPoint,
            flatImpactPoint);
        
        float distanceBasedArcHeight =
            arcHeight +
            horizontalDistance * arcHeightPerMeter;

        return Mathf.Clamp(
            distanceBasedArcHeight,
            minArcHeight,
            maxArcHeight);
    }

    DamageResult ResolvePlannedDamageResult()
    {
        if (!resolveHitAtLaunch)
        {
            return new DamageResult
            {
                didHit = true,
                normalDamage = Mathf.Max(0, rangedStats.missileDamage),
                armorPiercingDamage = Mathf.Max(0, rangedStats.armorPiercingDamage),
                totalDamage = Mathf.Max(0, rangedStats.missileDamage) + Mathf.Max(0, rangedStats.armorPiercingDamage)
            };
        }

        return CombatResolver.ResolveRangedHit(
            rangedStats,
            GetTargetDefenseStats(target));
    }

    Vector3 ResolveInitialImpactPoint()
    {
        Vector3 targetPoint = GetTargetPoint(target);

        if (plannedDamageResult.didHit || !useVisualMissOffset)
            return targetPoint;

        Vector2 randomCircle = Random.insideUnitCircle;

        if (randomCircle.sqrMagnitude <= 0.0001f)
            randomCircle = Vector2.right;

        randomCircle.Normalize();

        float missDistance = Random.Range(
            Mathf.Max(0f, visualMissRadius * 0.35f),
            Mathf.Max(0f, visualMissRadius));

        Vector3 missOffset = new Vector3(
            randomCircle.x,
            0f,
            randomCircle.y) * missDistance;

        return targetPoint + missOffset;
    }

    Vector3 GetTargetPoint(SoldierController targetSoldier)
    {
        if (targetSoldier == null)
            return transform.position + transform.forward;

        return targetSoldier.transform.position + targetOffset;
    }

    void ApplyImpact()
    {
        SpawnImpactEffect();

        if (!plannedDamageResult.didHit)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null || !target.IsAlive || target.Health == null)
        {
            Destroy(gameObject);
            return;
        }

        int totalDamageForReaction = EstimateDamageAfterArmor(
            target,
            plannedDamageResult.normalDamage,
            plannedDamageResult.armorPiercingDamage);

        target.Health.TakeDamage(
            plannedDamageResult.normalDamage,
            plannedDamageResult.armorPiercingDamage);

        // if (target != null &&
        //     target.IsAlive &&
        //     target.Combat != null)
        // {
        //     target.Combat.ReceiveHitReaction( // DEPRECIATED with new base PrototypeMeleeCleanup pass
        //         attacker,
        //         totalDamageForReaction);
        // }

        Destroy(gameObject);
    }

    void SpawnImpactEffect()
    {
        GameObject prefab = plannedDamageResult.didHit
            ? hitImpactPrefab
            : missImpactPrefab;

        if (prefab == null)
            return;

        Instantiate(
            prefab,
            impactPoint,
            Quaternion.identity);
    }

    CombatDefenseStats GetTargetDefenseStats(SoldierController targetSoldier)
    {
        if (targetSoldier != null && targetSoldier.Data != null)
            return targetSoldier.Data.defense;

        return CombatDefenseStats.Default;
    }

    int EstimateDamageAfterArmor(
        SoldierController targetSoldier,
        int damage,
        int armorPiercing)
    {
        int armor = targetSoldier != null && targetSoldier.Health != null
            ? targetSoldier.Health.Armor
            : 0;

        int reducedNormalDamage = Mathf.Max(0, damage - armor);
        return Mathf.Max(1, reducedNormalDamage + armorPiercing);
    }
}
