using System.Collections.Generic;
using UnityEngine;

// ============================================================
// SELECTION / COMMANDABLE ROLES
// ============================================================

public enum SelectableKind
{
    None,
    Squad,
    Worker,
    Building,
    Resource
}

// public enum CommandContext
// {
//     Default,
//     SquadSelected,
//     WorkerSelected,
//     BuildingSelected,
//     ResourceSelected,
//     
// }

// ============================================================
// SQUADS / SOLDIERS
// ============================================================

public enum SquadCategory
{
    Infantry,
    Ranged,
    Cavalry,
    Siege
}

/// <summary>
/// Optional specialization beneath the broad combat category.
/// Leave this as None until a squad needs more specific classification.
/// </summary>
public enum SquadCombatSubcategory
{
    None,

    // Infantry
    LineInfantry,
    ShockInfantry,
    SpearInfantry,

    // Ranged
    MissileInfantry,
    Skirmisher,

    // Cavalry
    MeleeCavalry,
    ShockCavalry,
    MissileCavalry,

    // Siege / large units
    Artillery,
    WarMachine,
    Monster,
    Hero
}

[System.Flags]
public enum UnitTrait
{
    None = 0,
    Shielded = 1 << 0,
    Shock = 1 << 1,
    Heavy = 1 << 2,
    Light = 1 << 3,
    Skirmisher = 1 << 4,
    AntiLarge = 1 << 5,
    Armored = 1 << 6,
    Stalk = 1 << 7,
    Flying = 1 << 8,
    Magical = 1 << 9
}


public enum SquadCombatStyle
{
    FormationCombat = 0,
    RangedLine = 2,
}

public enum SquadEngagementReason
{
    None,
    ExplicitAttack,
    AttackMoveContact,
    PassiveContact,
    DefensiveHold,
    RangedDuel
}

public enum SquadState
{
    Idle,
    Moving,

    // Attack order has been issued, but the squad has not entered melee yet.
    // The squad is moving toward the enemy until it is close enough to engage.
    ApproachingCombat,

    InCombat,
    AttackMoving,
    Charging,
    Withdrawing,
    Reforming,
    Routing
}

public enum SquadMoveMode
{
    IdleFormed,
    FormedMove,
    LooseMove,
    Reforming
}

public enum SquadStance
{
    Engage, // TODO: Rename to Chase? Free? EngageFreely? Loose Combat?
    Hold,
    // StandGround,
    // NoAttack
}


public enum SquadFormation
{
    Line,
    Spread,
    Box,
    Circle,
    Wedge
}

public enum SoldierRole
{
    None,
    Frontline,
    Support,
    Reserve,
    Replacing,
    Ranged,
    Routing
}

public enum SoldierActionState
{
    None,
    Attack,
    HitReact,
    Death
}

// ============================================================
// WORKERS
// ============================================================

// public enum WorkerState
// {
//     Idle,
//     Moving,
//     Gathering,
//     Building,
//     Repairing,
//     Attacking
// }

// ============================================================
// BUILDINGS
// ============================================================

public enum BuildingCategory
{
    None,
    TownCenter,
    House,
    ResourceDropoff,
    MilitaryProduction,
    Research,
    Defense,
}

// ============================================================
// COMMANDS
// ============================================================

public enum CommandType
{
    None,

    // Core
    Stop=0,
    // Move,
    // Attack,
    AttackMove,
    // Patrol,

    // Worker
    Build=1,
    Repair,

    // Building
    //SetRallyPoint,
    //Produce,
    //Research,

    // Squad stances
    EngageStance=2,
    HoldStance=3,
    // StandGround,
    // NoAttack,

    // Squad formations
    FormationLine=4,
    FormationSpread=5,
    FormationBox=6,
    FormationCircle=7,
    FormationWedge=8,

    // Total War-style commands
    // Charge,
    // Withdraw,
    // Brace,
    // Merge,

    // Future / legacy
    // Garrison,
    // Ability
}

public enum HotkeySlot
{
    None,

    Q, W, E, R, T,
    A, S, D, F, G,
    Z, X, C, V, B
}

// ============================================================
// PRODUCTION
// ============================================================

public enum ProductionType
{
    None,
    Worker,
    Squad,
    Upgrade
}

// ============================================================
// RESOURCES
// ============================================================

public enum ResourceType
{
    Wood,
    Food,
    Gold,
    Stone
}

[System.Serializable]
public struct ResourceCost
{
    public int wood;
    public int food;
    public int gold;
    public int stone;

    public ResourceCost(
        int wood = 0,
        int food = 0,
        int gold = 0,
        int stone = 0)
    {
        this.wood = wood;
        this.food = food;
        this.gold = gold;
        this.stone = stone;
    }

    public bool IsZero =>
        wood == 0 &&
        food == 0 &&
        gold == 0 &&
        stone == 0;
}


// ============================================================
// INTERFACES
// ============================================================

public interface ISelectable
{
    GameObject GetGameObject();

    SelectableKind SelectionKind { get; }
    bool IsDragSelectable { get; }

    void OnSelect();
    void OnDeselect();
}

public interface IHoverable
{
    void OnHoverEnter();
    void OnHoverExit();
}

public interface ISelectionComparable
{
    float DoubleClickSelectRange { get; }
    bool IsSameSelectionType(ISelectable other);
}

public interface ICommandable
{
    SelectableKind CommandKind { get; }
    List<CommandData> GetCommands();
}

public interface IFactionOwned
{
    FactionInstance Faction { get; }
}

public interface IDamageable
{
    bool IsAlive { get; }
    void TakeDamage(int damage);
}

// ============================================================
// CONTROL GROUPS
// ============================================================

public class ControlGroup
{
    public List<ISelectable> members = new List<ISelectable>();
}


// ============================================================
// Faction Visuals
// ============================================================

[System.Serializable]
public struct TeamVisualSettings
{
    public Color teamColor;
    public Color selectionColor;
    public Color hoverColor;
    public Color bannerColor;

    public TeamVisualSettings(Color teamColor)
    {
        this.teamColor = teamColor;
        selectionColor = teamColor;
        hoverColor = teamColor;
        bannerColor = teamColor;
    }

    public TeamVisualSettings(
        Color teamColor,
        Color selectionColor,
        Color hoverColor,
        Color bannerColor)
    {
        this.teamColor = teamColor;
        this.selectionColor = selectionColor;
        this.hoverColor = hoverColor;
        this.bannerColor = bannerColor;
    }

    public static TeamVisualSettings Default => new TeamVisualSettings(Color.white);
}




#region StatBlocks

[System.Serializable]
public struct HealthStats
{
    [Min(1)] public int maxHealth;
    
    [Min(0f)] public float healthRegenerationPerSecond;
    [Min(0f)] public float healingReceivedMultiplier;

    public static HealthStats Default => new HealthStats
    {
        maxHealth = 100,
        healthRegenerationPerSecond = 0f,
        healingReceivedMultiplier = 1f
    };
}

[System.Serializable]
public struct MovementStats
{
    [Min(0f)] public float moveSpeed;

    [Tooltip("How quickly this soldier accelerates toward requested movement velocity. Old 99999-style values are treated as legacy instant values and remapped by SoldierMotor to a weighted default.")]
    [Min(0f)] public float acceleration;

    [Tooltip("How quickly this soldier bleeds speed when stopping or making sharp direction changes. If left at 0, SoldierMotor derives it from acceleration.")]
    [Min(0f)] public float deceleration;

    [Tooltip("Visual/body turn speed in degrees per second. Lower values make heavy units, especially cavalry, take wider-feeling turns.")]
    [Min(0f)] public float turnSpeed;

    [Min(0f)] public float backwardsSpeedMultiplier;
    [Min(0f)] public float combatMoveSpeedMultiplier;

    public static MovementStats Default => new MovementStats
    {
        moveSpeed = 4f,
        acceleration = 12f,
        deceleration = 18f,
        turnSpeed = 540f,
        backwardsSpeedMultiplier = 0.65f,
        combatMoveSpeedMultiplier = 1f
    };
}

[System.Serializable]
public struct BodyStats
{
    [Min(0.01f)] public float mass;
    [Min(0.01f)] public float radius;
    [Min(0.1f)] public float height;
    [Min(0f)] public float impulseResistance;
    [Min(0f)] public float staggerResistance;
    [Min(0f)] public float knockdownResistance;

    public static BodyStats Default => new BodyStats
    {
        mass = 1f,
        radius = 0.4f,
        height = 2f,
        impulseResistance = 0f,
        staggerResistance = 0f,
        knockdownResistance = 0f
    };
}

[System.Serializable]
public struct MeleeCombatStats
{
    [Min(0)] public int meleeAttack;
    [Min(0)] public int weaponDamage;
    [Min(0)] public int armorPiercingDamage;
    [Min(0.05f)] public float attackInterval;
    [Min(0.1f)] public float attackRange;
    [Range(0f, 1f)] public float criticalHitChance;
    [Min(1f)] public float criticalHitDamageMultiplier;
    [Min(0f)] public float impactForce;
    [Min(0f)] public float staggerStrength;
    [Range(0f, 1f)] public float knockdownChance;
    [Min(0f)] public float chargeDamageBonus;
    [Min(0f)] public float chargeImpactBonus;

    public static MeleeCombatStats Default => new MeleeCombatStats
    {
        meleeAttack = 20,
        weaponDamage = 20,
        armorPiercingDamage = 0,
        attackInterval = 2f,
        attackRange = 2.5f,
        criticalHitChance = 0f,
        criticalHitDamageMultiplier = 1.5f,
        impactForce = 0f,
        staggerStrength = 0f,
        knockdownChance = 0f,
        chargeDamageBonus = 0f,
        chargeImpactBonus = 0f
    };
}

[System.Serializable]
public struct RangedCombatStats
{
    [Min(0)] public int rangedAccuracy;
    [Min(0)] public int missileDamage;
    [Min(0)] public int armorPiercingDamage;
    [Min(0.05f)] public float attackInterval;
    [Min(0.1f)] public float attackRange;
    [Min(0f)] public float minimumRange;
    [Range(0f, 1f)] public float criticalHitChance;
    [Min(1f)] public float criticalHitDamageMultiplier;
    public GameObject projectilePrefab;
    [Min(0.1f)] public float projectileSpeed;
    [Min(0f)] public float projectileGravityMultiplier;
    [Min(0f)] public float spreadRadius;
    [Min(0f)] public float suppressionStrength;
    [Tooltip("-1 means unlimited ammunition.")] public int ammunition;

    public static RangedCombatStats Default => new RangedCombatStats
    {
        rangedAccuracy = 50,
        missileDamage = 12,
        armorPiercingDamage = 0,
        attackInterval = 2f,
        attackRange = 100f,
        minimumRange = 0f,
        criticalHitChance = 0f,
        criticalHitDamageMultiplier = 1.5f,
        projectilePrefab = null,
        projectileSpeed = 18f,
        projectileGravityMultiplier = 1f,
        spreadRadius = 0f,
        suppressionStrength = 0f,
        ammunition = -1
    };
}

[System.Serializable]
public struct CombatDefenseStats
{
    [Min(0)] public int armor;
    [Min(0)] public int meleeDefense;
    [Min(0)] public int missileDefense;
    [Range(0f, 1f)] public float meleeBlockChance;
    [Range(0f, 1f)] public float missileBlockChance;
    [Range(0f, 1f)] public float criticalHitResistance;
    [Range(0f, 1f)] public float armorPiercingResistance;
    [Range(0f, 1f)] public float hitReactResistance;

    public static CombatDefenseStats Default => new CombatDefenseStats
    {
        armor = 0,
        meleeDefense = 20,
        missileDefense = 0,
        meleeBlockChance = 0f,
        missileBlockChance = 0f,
        criticalHitResistance = 0f,
        armorPiercingResistance = 0f,
        hitReactResistance = 0f
    };
}

[System.Serializable]
public struct ArmorStats
{
    public int armor;
    public int meleeDefenseBonus;
    public int missileDefenseBonus;
    public float meleeBlockChanceBonus;
    public float missileBlockChanceBonus;
    public float movementSpeedMultiplierDelta;
    public float accelerationMultiplierDelta;
    public float massMultiplierDelta;
    public float hitReactResistanceBonus;

    public static ArmorStats Default => new ArmorStats();
}

[System.Serializable]
public struct MoraleStats
{
    [Min(0f)] public float maxMorale;
    [Min(0f)] public float leadership;
    [Min(0f)] public float moraleRecoveryRate;
    [Range(0f, 1f)] public float casualtyMoraleResistance;
    [Range(0f, 1f)] public float flankMoraleResistance;
    [Range(0f, 1f)] public float terrorResistance;
    [Min(0f)] public float routingThreshold;
    [Min(0f)] public float shatteredThreshold;

    public static MoraleStats Default => new MoraleStats
    {
        maxMorale = 100f,
        leadership = 50f,
        moraleRecoveryRate = 0f,
        casualtyMoraleResistance = 0f,
        flankMoraleResistance = 0f,
        terrorResistance = 0f,
        routingThreshold = 25f,
        shatteredThreshold = 10f
    };
}

[System.Serializable]
public struct FormationStats
{
    public SquadFormation defaultFormation;
    [Min(1)] public int defaultUnitsPerRow;
    [Min(0.1f)] public float spacing;
    [Min(0.1f)] public float minimumSpacing;
    [Min(0.1f)] public float maximumSpacing;
    [Min(0f)] public float reformSpeedMultiplier;
    [Min(0f)] public float cohesionDistanceMultiplier;

    public static FormationStats Default => new FormationStats
    {
        defaultFormation = SquadFormation.Line,
        defaultUnitsPerRow = 10,
        spacing = 2f,
        minimumSpacing = 0.5f,
        maximumSpacing = 5f,
        reformSpeedMultiplier = 1f,
        cohesionDistanceMultiplier = 1f
    };
}

[System.Serializable]
public struct SquadCapacityStats
{
    [Min(1)] public int startingSoldierCount;
    [Min(1)] public int maximumSoldierCount;
    [Min(0)] public int reinforcementAmount;
    [Min(0f)] public float reinforcementCostMultiplier;
    [Min(0)] public int officerSlots;
    [Min(0)] public int specialistSlots;

    public static SquadCapacityStats Default => new SquadCapacityStats
    {
        startingSoldierCount = 5,
        maximumSoldierCount = 50,
        reinforcementAmount = 1,
        reinforcementCostMultiplier = 1f,
        officerSlots = 0,
        specialistSlots = 0
    };
}

[System.Serializable]
public struct SoldierStatModifiers
{
    public int maxHealth;
    public float healthRegenerationPerSecond;
    public float healingReceivedMultiplierDelta;
    public float moveSpeed;
    public float acceleration;
    public float deceleration;
    public float turnSpeed;
    public float backwardsSpeedMultiplierDelta;
    public float combatMoveSpeedMultiplierDelta;
    public float bodyMass;
    public float bodyRadius;
    public float bodyHeight;
    public float impulseResistance;
    public float staggerResistance;
    public float knockdownResistance;
    public int armor;
    public int meleeDefense;
    public int missileDefense;
    public float meleeBlockChance;
    public float missileBlockChance;
    public float criticalHitResistance;
    public float armorPiercingResistance;
    public float hitReactResistance;
    public int meleeAttack;
    public int meleeWeaponDamage;
    public int meleeArmorPiercingDamage;
    public float meleeAttackInterval;
    public float meleeAttackRange;
    public float meleeCriticalHitChance;
    public float meleeCriticalHitDamageMultiplierDelta;
    public float meleeImpactForce;
    public float meleeStaggerStrength;
    public float meleeKnockdownChance;
    public float chargeDamageBonus;
    public float chargeImpactBonus;
    public int rangedAccuracy;
    public int missileDamage;
    public int rangedArmorPiercingDamage;
    public float rangedAttackInterval;
    public float rangedAttackRange;
    public float rangedMinimumRange;
    public float rangedCriticalHitChance;
    public float rangedCriticalHitDamageMultiplierDelta;
    public float projectileSpeed;
    public float projectileGravityMultiplierDelta;
    public float spreadRadius;
    public float suppressionStrength;
    public int ammunition;
}

[System.Serializable]
public struct SquadStatModifiers
{
    public int startingSoldierCount;
    public int maximumSoldierCount;
    public int reinforcementAmount;
    public float reinforcementCostMultiplierDelta;
    public int officerSlots;
    public int specialistSlots;
    public int defaultUnitsPerRow;
    public float spacing;
    public float minimumSpacing;
    public float maximumSpacing;
    public float reformSpeedMultiplierDelta;
    public float cohesionDistanceMultiplierDelta;
    public float maxMorale;
    public float leadership;
    public float moraleRecoveryRate;
    public float casualtyMoraleResistance;
    public float flankMoraleResistance;
    public float terrorResistance;
    public float routingThreshold;
    public float shatteredThreshold;
}

[System.Serializable]
public struct GatheringStats
{
    [Min(0)] public int gatherAmount;
    [Min(0f)] public float gatherRange;
    [Min(0.05f)] public float gatherInterval;
}

[System.Serializable]
public struct ProductionStats
{
    [Min(0.05f)] public float productionSpeed;
    [Min(0)] public int garrisonCapacity;

    public static ProductionStats Default => new ProductionStats
    {
        productionSpeed = 1f,
        garrisonCapacity = 0
    };
}
#endregion



#region UpgradeData Specifiers


// ============================================================
// UPGRADES / UNIT CLASSIFICATION
// ============================================================

/// <summary>
/// Content-driven nation/culture classification used by upgrade targeting.
/// This remains separate from runtime FactionInstance ownership.
/// </summary>
[CreateAssetMenu(
    fileName = "NationData_",
    menuName = "Scriptable Objects/Military/NationData")]
public class NationData : ScriptableObject
{
    [Header("Identity")]
    public string nationId;
    public string displayName = "Nation";
    public Sprite icon;
}

/// <summary>
/// Lore/content family shared by units that may occupy different combat roles.
/// Example: Marauder Infantry and Marauder Skirmishers can both use Marauder.
/// </summary>
[CreateAssetMenu(
    fileName = "UnitFamily_",
    menuName = "Scriptable Objects/Military/Unit Family")]
public class UnitFamilyData : ScriptableObject
{
    [Header("Identity")]
    public string familyId;
    public string displayName = "Unit Family";
}

/// <summary>
/// Base data asset for effects added to a resolved runtime weapon.
/// Actual effect behavior will be implemented by the weapon-effect system later.
/// </summary>
[CreateAssetMenu(
    fileName = "WeaponEffect_",
    menuName = "Scriptable Objects/Military/Weapon Effect")]
public class WeaponEffectData : ScriptableObject
{
    [Header("Identity")]
    public string effectId;
    public string displayName = "Weapon Effect";
    [TextArea] public string description;
    public Sprite icon;
}

/// <summary>
/// Presentation package used when an upgrade changes a squad's soldier model,
/// materials, attachments, colors, or other visual identity.
/// </summary>
[CreateAssetMenu(
    fileName = "SquadVisualProfile_",
    menuName = "Scriptable Objects/Military/Squad Visual Profile")]
public class SquadVisualProfile : ScriptableObject
{
    [Header("Model")]
    public GameObject modelPrefab;

    [Header("Presentation")]
    public Material[] materialOverrides;
    public GameObject[] attachmentPrefabs;
    public Color colorTint = Color.white;
}

public enum UpgradeScope
{
    Faction,
    Squad
}

public enum UpgradeGrantSource
{
    MatchStartingUpgrade,
    MatchResearch,
    ScenarioReward,
    CampaignProgression,
    Debug
}

public enum UpgradeEntityApplicationMode
{
    ExistingAndFuture,
    FutureOnly
}

public enum WeaponEffectOperation
{
    Add,
    Remove
}

/// <summary>
/// Determines which squads/soldiers are eligible for one upgrade or effect.
/// Empty normal-filter lists mean no restriction. Entries inside a populated list
/// use OR; separate populated filter groups use AND. Explicit exclusion always wins,
/// while explicit additional inclusion bypasses the normal classification filters.
/// </summary>
[System.Serializable]
public struct UpgradeTargetFilter
{
    [Header("Classification Filters")]
    public List<NationData> nations;
    public List<SquadCategory> combatCategories;
    public List<SquadCombatSubcategory> combatSubcategories;
    public List<UnitFamilyData> unitFamilies;

    [Header("Trait Filters")]
    [Tooltip("A target must contain every selected required trait.")]
    public UnitTrait requiredTraits;

    [Tooltip("A target is rejected if it contains any selected excluded trait.")]
    public UnitTrait excludedTraits;

    [Header("Exact Additional Targets")]
    [Tooltip("These SquadData assets are included even when normal classification filters do not match.")]
    public List<SquadData> additionallyIncludedSquads;

    [Tooltip("These SoldierData assets are included even when normal classification filters do not match.")]
    public List<SoldierData> additionallyIncludedSoldiers;

    [Header("Exact Exclusions")]
    [Tooltip("These SquadData assets never receive this effect.")]
    public List<SquadData> excludedSquads;

    [Tooltip("These SoldierData assets never receive this effect.")]
    public List<SoldierData> excludedSoldiers;
}

[System.Serializable]
public struct TargetedSoldierStatModifier
{
    [Tooltip("When false, this effect uses UpgradeData.defaultTarget.")]
    public bool overrideDefaultTarget;
    public UpgradeTargetFilter targetOverride;
    public UpgradeEntityApplicationMode applicationMode;
    public SoldierStatModifiers modifiers;
}

[System.Serializable]
public struct TargetedSquadStatModifier
{
    [Tooltip("When false, this effect uses UpgradeData.defaultTarget.")]
    public bool overrideDefaultTarget;
    public UpgradeTargetFilter targetOverride;
    public UpgradeEntityApplicationMode applicationMode;
    public SquadStatModifiers modifiers;
}

[System.Serializable]
public struct WeaponReplacementEffect
{
    [Tooltip("When false, this effect uses UpgradeData.defaultTarget.")]
    public bool overrideDefaultTarget;
    public UpgradeTargetFilter targetOverride;
    public UpgradeEntityApplicationMode applicationMode;

    [Tooltip("Optional. When assigned, only soldiers currently resolving this weapon are changed.")]
    public WeaponProfile requiredWeapon;

    public WeaponProfile replacementWeapon;
}

[System.Serializable]
public struct WeaponEffectUpgradeEffect
{
    [Tooltip("When false, this effect uses UpgradeData.defaultTarget.")]
    public bool overrideDefaultTarget;
    public UpgradeTargetFilter targetOverride;
    public UpgradeEntityApplicationMode applicationMode;

    [Tooltip("Optional. Restricts this effect to soldiers currently resolving this weapon.")]
    public WeaponProfile requiredWeapon;

    public WeaponEffectOperation operation;
    public WeaponEffectData weaponEffect;
}

[System.Serializable]
public struct ArmorReplacementEffect
{
    [Tooltip("When false, this effect uses UpgradeData.defaultTarget.")]
    public bool overrideDefaultTarget;
    public UpgradeTargetFilter targetOverride;
    public UpgradeEntityApplicationMode applicationMode;

    [Tooltip("Optional. When assigned, only soldiers currently resolving this armor are changed.")]
    public ArmorProfile requiredArmor;

    public ArmorProfile replacementArmor;
}

[System.Serializable]
public struct SquadVisualReplacementEffect
{
    [Tooltip("When false, this effect uses UpgradeData.defaultTarget.")]
    public bool overrideDefaultTarget;
    public UpgradeTargetFilter targetOverride;
    public UpgradeEntityApplicationMode applicationMode;

    [Tooltip("Optional. Restricts replacement to squads currently using this visual profile once that runtime system is wired.")]
    public SquadVisualProfile requiredVisualProfile;

    public SquadVisualProfile replacementVisualProfile;
}


#endregion


[System.Serializable]
public struct FormationBounds
{
    public int columnCount;
    public int rowCount;

    public float width;
    public float depth;

    public float HalfWidth => width * 0.5f;
    public float HalfDepth => depth * 0.5f;

    public static FormationBounds Empty => 
        new FormationBounds
        {
            columnCount = 0,
            rowCount = 0,
            width = 0f,
            depth = 0f
        };
}