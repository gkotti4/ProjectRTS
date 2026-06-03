using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

// TODO: pass stats.faction directly, remove GameManager wrappers

public class EntityStats : MonoBehaviour
{
    [SerializeField] public EntityData baseData;
    
    // Runtime faction instance — set at spawn by spawner
    [HideInInspector] public FactionInstance faction;

    
    [Header("SET FROM BASE DATA (DO NOT CHANGE IN EDITOR)")]
    // Identity
    public EntityTag entityTag;

    // Health
    private int currentHealth;

    // Shared
    public int maxHealth;
    public int armor;
    public int lineOfSight;

    // Movement
    public float moveSpeed;

    // Combat
    public int attackDamage;
    public float attackRange;
    public float attackInterval;

    public float defensiveChaseRange;

    // Gathering
    public int gatherAmount;
    public float gatherRange;
    public float gatherInterval;

    // Production
    public float productionSpeed;
    public int garrisonCapacity;
    
    // private UI
    private HealthBarUI healthBar;
    public HealthBarUI HealthBar => healthBar;
    
    void Awake()
    {
        InitializeFromBaseData();
        healthBar = GetComponentInChildren<HealthBarUI>();
    }
    
    void Start()
    {
        if (faction == null) // check
        {
            //Debug.LogWarning(gameObject.name + " spawned with no faction — check spawner"); // (ghost buildings call this)
            return;
        }
        GameManager.Instance.RegisterEntity(this);
    }

    void OnDestroy()
    {
        if (faction == null)
        {
            //Debug.LogWarning(gameObject.name + " destroyed with no faction"); // Check (ghost buildings call this)
            return;
        }
        GameManager.Instance.UnregisterEntity(this);
    }

    // Copies base values from EntityData into runtime fields
    public void InitializeFromBaseData()
    {
        if (baseData == null)
        {
            Debug.LogError("baseData is null - set in editor");
            return;
        }
        
        entityTag = baseData.entityTag;

        currentHealth = baseData.maxHealth;
        
        maxHealth = baseData.maxHealth;
        armor = baseData.armor;
        lineOfSight = baseData.lineOfSight;

        moveSpeed = baseData.moveSpeed;

        attackDamage = baseData.attackDamage;
        attackRange = baseData.attackRange;
        attackInterval = baseData.attackInterval;
        
        defensiveChaseRange = baseData.defensiveChaseRange;

        gatherAmount = baseData.gatherAmount;
        gatherRange = baseData.gatherRange;
        gatherInterval = baseData.gatherInterval;

        productionSpeed = baseData.productionSpeed;
        garrisonCapacity = baseData.garrisonCapacity;
    }
    
    // Health x IDamageable
    public bool IsAlive => currentHealth > 0;
    public int CurrentHealth => currentHealth;
    public void TakeDamage(int rawDamage)
    {
        int reducedDamage = Mathf.Max(1, rawDamage - armor); // armor reduces damage
        currentHealth -= reducedDamage;
        
        if (currentHealth <= 0)
            Die();
        
        if (healthBar != null)
            healthBar.OnDamaged(currentHealth, maxHealth);
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth; // Clamp
    }
    
    void Die()
    {
        // Disable controller immediately so Update stops
        if (!TryGetComponent(out EntityController ec)) Debug.Log("No entity controller found on Die()");
        if (TryGetComponent(out UnitController uc)) uc.enabled = false;
        if (TryGetComponent(out MilitaryController mc)) mc.enabled = false;

        if (TryGetComponent(out NavMeshAgent agent))
            agent.enabled = false;

        // if (TryGetComponent(out Rigidbody rb)) // later
        //     rb.isKinematic = false;

        SelectionManager.Instance.UnregisterSelectable(ec); // changed to ec to account for buildings
        
        faction?.UnregisterEntity(this);
        GameEvents.EntityDied(gameObject);
        
        // Death animation and destroy
        if (TryGetComponent(out UnitAnimator unitAnimator))
            unitAnimator.TriggerDeath();
        Destroy(gameObject, 2f);
    }



    // Called by GameManager (FACTION INSTANCE) when an upgrade is registered
    public void ApplyUpgrade(UpgradeData upgrade) // Check when upgrades are added
    {
        if (upgrade.upgradeType == UpgradeType.Global)
        {
            if (!IsAffectedByUpgrade(upgrade)) return;

            foreach (StatModifier modifier in upgrade.statModifiers)
                ApplyModifier(modifier);
        }

        if (upgrade.upgradeType == UpgradeType.Unit)
        {
            if (baseData != upgrade.fromUnit) return;
            baseData = upgrade.toUnit;
            InitializeFromBaseData();
            // Mesh swap handled by controller
        }
    }

    // Checks if this entity is affected by a global upgrade via tag matching
    bool IsAffectedByUpgrade(UpgradeData upgrade)
    {
        foreach (EntityTag tag in upgrade.affectedTags)
        {
            if (tag == EntityTag.All) return true;
            if (tag == EntityTag.AllUnits && baseData.entityType == EntityType.Unit) return true;
            if (tag == EntityTag.AllBuildings && baseData.entityType == EntityType.Building) return true;
            if (tag == entityTag) return true;
        }
        return false;
    }

    // Routes modifier to the correct runtime field
    void ApplyModifier(StatModifier modifier)
    {
        switch (modifier.stat)
        {
            case StatType.Attack:
                attackDamage = Calculate(attackDamage, modifier); break;
            case StatType.AttackSpeed:
                attackInterval = Calculate(attackInterval, modifier); break;
            case StatType.AttackRange:
                attackRange = Calculate(attackRange, modifier); break;
            case StatType.MoveSpeed:
                moveSpeed = Calculate(moveSpeed, modifier); break;
            case StatType.MaxHealth:
                maxHealth = Calculate(maxHealth, modifier); break;
            case StatType.GatherAmount:
                gatherAmount = Calculate(gatherAmount, modifier); break;
            case StatType.GatherSpeed:
                gatherInterval = Calculate(gatherInterval, modifier); break;
            case StatType.LineOfSight:
                lineOfSight = Calculate(lineOfSight, modifier); break;
            case StatType.MeleeArmor:
            case StatType.PierceArmor:
                armor = Calculate(armor, modifier); break;
            case StatType.ProductionSpeed:
                productionSpeed = Calculate(productionSpeed, modifier); break;
            case StatType.GarrisonCapacity:
                garrisonCapacity = Calculate(garrisonCapacity, modifier); break;
        }
    }

    // Float modifier calculation
    float Calculate(float baseValue, StatModifier modifier)
    {
        return modifier.modifierType == ModifierType.Flat
            ? baseValue + modifier.value
            : baseValue * (1f + modifier.value / 100f);
    }

    // Int modifier calculation
    int Calculate(int baseValue, StatModifier modifier)
    {
        return modifier.modifierType == ModifierType.Flat
            ? baseValue + (int)modifier.value
            : Mathf.RoundToInt(baseValue * (1f + modifier.value / 100f));
    }
    
    // Faction Section
    public bool IsEnemy(EntityStats other)
    {
        if (other.faction == null || faction == null) return false;
        if (other.faction.teamId == 0 || faction.teamId == 0) return false;
        return other.faction.teamId != faction.teamId;
    }
    
    
    
    
}