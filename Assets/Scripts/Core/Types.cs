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

public enum CommandContext
{
    Default,
    SquadSelected,
    WorkerSelected,
    BuildingSelected,
    ResourceSelected,

    // Legacy aliases while old files are removed.
    EconomicUnitSelected,
    MilitarySquadSelected
}

// ============================================================
// SQUADS / SOLDIERS
// ============================================================

public enum SquadCategory
{
    Infantry,
    Ranged,
    Cavalry,
    Siege,
    Support,
    Hero
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
    Engage = 0,
    Hold = 1,
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

public enum WorkerState
{
    Idle,
    Moving,
    Gathering,
    Building,
    Repairing,
    Attacking
}

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
    Farm
}

// ============================================================
// COMMANDS
// ============================================================

public enum CommandType
{
    None,

    // Core
    Stop = 0,
    // Move,
    // Attack,
    AttackMove,
    // Patrol,
    // HoldPosition,

    // Worker
    Build = 1,
    // Repair,

    // Building
    //SetRallyPoint,
    //Produce,
    //Research,

    // Squad stances
    EngageStance = 2, // was aggressive
    HoldStance = 3, // was defensive
    // StandGround, // depreciated 
    // NoAttack, // depreciated

    // Squad formations
    FormationLine = 4,
    FormationSpread = 5,
    FormationBox = 6,
    FormationCircle = 7,
    FormationWedge = 8,

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
    Unit,   // legacy
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
    [Min(0)] public int armor;

    public static HealthStats Default => new HealthStats
    {
        maxHealth = 100,
        armor = 0
    };
}

[System.Serializable]
public struct MovementStats
{
    [Min(0f)] public float moveSpeed;
    [Min(0f)] public float acceleration;
    [Min(0f)] public float turnSpeed;

    public static MovementStats Default => new MovementStats
    {
        moveSpeed = 4f,
        acceleration = 99999f,
        turnSpeed = 900f
    };
}

[System.Serializable]
public struct MeleeCombatStats
{
    [Min(0)] public int meleeAttack;
    [Min(0)] public int meleeDefense;

    [Min(0)] public int weaponDamage;
    [Min(0)] public int armorPiercingDamage;

    [Min(0.05f)] public float attackInterval;
    [Min(0f)] public float attackRange;

    public static MeleeCombatStats Default => new MeleeCombatStats
    {
        meleeAttack = 20,
        meleeDefense = 20,
        weaponDamage = 20,
        armorPiercingDamage = 0,
        attackInterval = 1.5f,
        attackRange = 1.5f
    };
}

[System.Serializable]
public struct RangedCombatStats
{
    [Min(0)] public int missileDamage;
    [Min(0)] public int armorPiercingDamage;

    [Min(0.05f)] public float attackInterval;
    [Min(0f)] public float attackRange;
    [Min(0f)] public float projectileSpeed;
}

[System.Serializable]
public struct MoraleStats
{
    [Min(0f)] public float maxMorale;
    [Min(0f)] public float leadership;

    public static MoraleStats Default => new MoraleStats
    {
        maxMorale = 100f,
        leadership = 50f
    };
}

[System.Serializable]
public struct FormationStats
{
    public SquadFormation defaultFormation;

    [Min(1)] public int defaultUnitsPerRow;
    [Min(0.1f)] public float spacing;

    public static FormationStats Default => new FormationStats
    {
        defaultFormation = SquadFormation.Line,
        defaultUnitsPerRow = 10,
        spacing = 2f
    };
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











// ============================================================
// LEGACY COMPATIBILITY
// ============================================================
// Keep this until old EntityStats / UnitController / VillagerController /
// MilitaryController / EntityDetails are fully deleted or rewritten.

// public enum EntityType
// {
//     None,
//     Unit,
//     Building
// }
//
// public enum EntityTag
// {
//     None = 0,
//
//     Villager,
//
//     Infantry,
//     Ranged,
//     Cavalry,
//     Siege,
//
//     TownCenter,
//     MilitaryBuilding,
//     ResourceBuilding,
//     ProductionBuilding,
//     DefenseBuilding,
//
//     AllUnits,
//     AllBuildings,
//     All
// }
//
// public enum UnitType
// {
//     None,
//     Villager,
//
//     Infantry,
//     Ranged,
//     Cavalry,
//     Siege,
//     Support,
//     Hero
// }
//
// public enum BuildingType
// {
//     None,
//     TownCenter,
//     Barracks,
//     Farm
// }
//
// public enum UnitState
// {
//     Idle,
//     Moving,
//     Attacking,
//     Returning,
//     Gathering,
//     Building,
//     Patrolling,
//     AttackMoving
// }
//
// public enum UnitControlState
// {
//     PlayerControlled,
//     AIControlled,
//     Locked
// }