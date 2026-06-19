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
    Aggressive,
    Defensive,
    StandGround,
    NoAttack
}

public enum CombatStance
{
    Aggressive,
    Defensive,
    StandGround,
    NoAttack
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
    Stop,
    Move,
    Attack,
    AttackMove,
    Patrol,
    HoldPosition,

    // Worker
    Gather,
    Build,
    Repair,

    // Building
    SetRallyPoint,
    Produce,
    Research,

    // Squad stances
    Aggressive,
    Defensive,
    StandGround,
    NoAttack,

    // Squad formations
    FormationLine,
    FormationSpread,
    FormationBox,
    FormationCircle,
    FormationWedge,

    // Total War-style commands
    Charge,
    Withdraw,
    Brace,
    Merge,

    // Future / legacy
    Garrison,
    Ability
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
// UPGRADES / MODIFIERS
// ============================================================

public enum UpgradeType
{
    Global,
    Unit,       // legacy
    Squad,
    Soldier,
    Worker,
    Building
}

public enum ModifierType
{
    Flat,
    Percentage
}

public enum StatType
{
    None,

    // Health
    MaxHealth,
    HealRate,
    Armor,

    // Armor legacy splits
    MeleeArmor,
    PierceArmor,

    // Movement
    MoveSpeed,
    Acceleration,
    TurnSpeed,

    // Vision
    LineOfSight,

    // Combat
    Attack,
    MeleeAttack,
    MeleeDefense,
    WeaponDamage,
    ArmorPiercingDamage,
    AttackDamage,
    AttackSpeed,
    AttackInterval,
    AttackRange,

    // Morale
    Morale,
    Leadership,

    // Economy
    GatherAmount,
    GatherSpeed,
    GatherInterval,
    GatherRange,
    CarryCapacity,
    BuildSpeed,

    // Production / population
    ProductionSpeed,
    PopulationCost,
    PopulationCap,
    GarrisonCapacity,
    ResourceGenerationRate
}

[System.Serializable]
public struct StatModifier
{
    public StatType stat;
    public float value;
    public ModifierType modifierType;
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
// LEGACY COMPATIBILITY
// ============================================================
// Keep this until old EntityStats / UnitController / VillagerController /
// MilitaryController / EntityDetails are fully deleted or rewritten.

public enum EntityType
{
    None,
    Unit,
    Building
}

public enum EntityTag
{
    None = 0,

    Villager,

    Infantry,
    Ranged,
    Cavalry,
    Siege,

    TownCenter,
    MilitaryBuilding,
    ResourceBuilding,
    ProductionBuilding,
    DefenseBuilding,

    AllUnits,
    AllBuildings,
    All
}

public enum UnitType
{
    None,
    Villager,

    Infantry,
    Ranged,
    Cavalry,
    Siege,
    Support,
    Hero
}

public enum BuildingType
{
    None,
    TownCenter,
    Barracks,
    Farm
}

public enum UnitState
{
    Idle,
    Moving,
    Attacking,
    Returning,
    Gathering,
    Building,
    Patrolling,
    AttackMoving
}

public enum UnitControlState
{
    PlayerControlled,
    AIControlled,
    Locked
}