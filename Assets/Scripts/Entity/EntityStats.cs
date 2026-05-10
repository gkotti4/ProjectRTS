using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

public class EntityStats : MonoBehaviour
{
    [SerializeField] public EntityData baseData;
    [SerializeField] public DecalProjector selectionDecal;

    
    [Header("SET FROM BASE DATA (DO NOT CHANGE IN EDITOR)")]
    // Identity
    public EntityTag entityTag;
    
    // Anything that can be changed - define outside of baseData
    // Shared
    public int maxHealth;
    public int armor;
    public int lineOfSight;

    // Combat
    public int attackDamage;
    public float attackRange;
    public float attackInterval;

    // Movement
    public float moveSpeed;

    // Gathering
    public int gatherAmount;
    public float gatherRange;
    public float gatherInterval;

    // Production
    public float productionSpeed;
    public int garrisonCapacity;

    
    void Awake()
    {
        InitializeFromBaseData();
    }
    
    void Start()
    {
        GameManager.Instance.RegisterEntity(this);
    }

    void OnDestroy()
    {
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

        if (selectionDecal == null)
        {
            Debug.Log("SelectionDecal is null - set in editor");
        }
        
        entityTag = baseData.entityTag;

        maxHealth = baseData.maxHealth;
        armor = baseData.armor;
        lineOfSight = baseData.lineOfSight;

        attackDamage = baseData.attackDamage;
        attackRange = baseData.attackRange;
        attackInterval = baseData.attackInterval;

        moveSpeed = baseData.moveSpeed;

        gatherAmount = baseData.gatherAmount;
        gatherRange = baseData.gatherRange;
        gatherInterval = baseData.gatherInterval;

        productionSpeed = baseData.productionSpeed;
        garrisonCapacity = baseData.garrisonCapacity;
    }

    // Called by GameManager when an upgrade is registered
    public void ApplyUpgrade(UpgradeData upgrade)
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
}