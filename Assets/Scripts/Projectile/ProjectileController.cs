using UnityEngine;

/// -----------------------------------------------------------------------------
/// ProjectileController
/// -----------------------------------------------------------------------------
///
/// Simple ranged projectile for the first ranged MVP.
/// It does not use physics, ammo, obstruction, arcs, or friendly fire yet.
/// It flies toward the target soldier and applies damage on arrival.
///
/// Design role:
/// Visual/damage carrier spawned from a ranged weapon animation event.
///
public class ProjectileController : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private float hitDistance = 0.25f;
    [SerializeField] private float maxLifetime = 8f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    private SoldierController attacker;
    private SoldierController target;

    private int normalDamage;
    private int armorPiercingDamage;
    private float projectileSpeed = 18f;
    private float lifetimeTimer = 0f;
    private bool hasInitialized = false;

    public void Initialize(
        SoldierController source,
        SoldierController targetSoldier,
        int damage,
        int armorPiercing,
        float speed)
    {
        attacker = source;
        target = targetSoldier;
        normalDamage = Mathf.Max(0, damage);
        armorPiercingDamage = Mathf.Max(0, armorPiercing);
        projectileSpeed = Mathf.Max(0.1f, speed);
        lifetimeTimer = Mathf.Max(0.1f, maxLifetime);
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

        Vector3 targetPoint = target.transform.position + targetOffset;
        Vector3 toTarget = targetPoint - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= hitDistance)
        {
            ApplyImpact();
            return;
        }

        Vector3 direction = toTarget.normalized;

        transform.position += direction * projectileSpeed * Time.deltaTime;

        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    void ApplyImpact()
    {
        if (target == null || !target.IsAlive || target.Health == null)
        {
            Destroy(gameObject);
            return;
        }

        int totalDamageForReaction = EstimateDamageAfterArmor(target);

        target.Health.TakeDamage(
            normalDamage,
            armorPiercingDamage);

        if (target != null &&
            target.IsAlive &&
            target.Combat != null)
        {
            target.Combat.ReceiveHitReaction(
                attacker,
                totalDamageForReaction);
        }

        Destroy(gameObject);
    }

    int EstimateDamageAfterArmor(SoldierController targetSoldier)
    {
        int armor = targetSoldier != null && targetSoldier.Health != null
            ? targetSoldier.Health.Armor
            : 0;

        int reducedNormalDamage = Mathf.Max(0, normalDamage - armor);
        return Mathf.Max(1, reducedNormalDamage + armorPiercingDamage);
    }
}
