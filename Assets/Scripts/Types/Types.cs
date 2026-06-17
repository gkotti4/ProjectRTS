// SESSION: Squad Control Refactor

using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ENTITY TYPES
// ============================================================

public enum EntityType
{
    None,
    Unit,
    Building
}

public enum EntityTag
{
    None = 0,

    // Units — Economic
    Villager,

    // Units — Military
    Infantry,
    Ranged,
    Cavalry,
    Siege,

    // Buildings
    TownCenter,
    MilitaryBuilding,
    ResourceBuilding,
    ProductionBuilding,
    DefenseBuilding,

    // Broad selectors — used by upgrades
    AllUnits,
    AllBuildings,
    All
}

public enum UnitType
{
    None,
    Villager,

    // Legacy / member identity
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

// ============================================================
// UNIT STATE
// ============================================================

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

// Legacy low-level controller state.
// Used by UnitController/MilitaryController only.
// SquadController should not depend on this.
public enum UnitControlState
{
    PlayerControlled,
    AIControlled,
    Locked
}

// ============================================================
// SQUADS
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
    InCombat,
    AttackMoving,
    Routing
}

public enum SquadMoveMode
{
    IdleFormed,

    // Squad body/agent moves.
    // Members follow current moving slots.
    FormedMove,

    // Members path individually to final slots.
    // Used when formed movement fails around obstacles/chokes.
    LooseMove,

    // Squad is recovering shape after loose movement or merging.
    Reforming
}

public enum SquadStance
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

// ============================================================
// CONTROL GROUPS / SELECTION BOOKMARKS
// ============================================================

// This is now ONLY a hotkey bookmark.
// It should not own formation, stance, anchors, offsets, or movement behavior.
// Squads own their own formation/stance/movement.
public class ControlGroup
{
    public List<ISelectable> members = new List<ISelectable>();

    public ControlGroup()
    {
        members = new List<ISelectable>();
    }
}

// ============================================================
// COMMANDS
// ============================================================

// public enum CommandSubmenuType
// {
//     None,
//     Build
// }

public enum CommandType
{
    Stop,
    AttackMove,
    Patrol,
    Build,
    Garrison,

    // Stances
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

    // Special abilities / future ICommand components
    Ability
}

public enum CommandContext
{
    Default,
    EconomicUnitSelected,
    MilitarySquadSelected,
    BuildingSelected
}


public enum HotkeySlot
{
    None,

    Q, W, E, R, T,
    A, S, D, F, G,
    Z, X, C, V, B
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
    public int gold;
    public int food;
    public int stone;

    public ResourceCost(int wood = 0, int gold = 0, int food = 0, int stone = 0)
    {
        this.wood = wood;
        this.gold = gold;
        this.food = food;
        this.stone = stone;
    }
}

// ============================================================
// PRODUCTION & UPGRADES
// ============================================================

public enum ProductionType
{
    Unit,
    Squad,
    Upgrade
}

public enum UpgradeType
{
    Global,
    Unit
}

public enum StatType
{
    // Combat
    Attack,
    MeleeArmor,
    PierceArmor,
    AttackSpeed,
    AttackRange,

    // Movement
    MoveSpeed,

    // Vision
    LineOfSight,

    // Gathering
    GatherSpeed,
    GatherAmount,
    CarryCapacity,

    // Building
    BuildSpeed,

    // Health
    MaxHealth,
    HealRate,

    // Economy
    PopulationCost,
    ResourceGenerationRate,

    // Production
    ProductionSpeed,
    GarrisonCapacity
}

public enum ModifierType
{
    Flat,
    Percentage
}

[System.Serializable]
public struct StatModifier
{
    public StatType stat;
    public float value;
    public ModifierType modifierType;
}

