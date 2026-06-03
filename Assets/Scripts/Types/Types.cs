using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ENTITY TYPES
// ============================================================

public enum EntityType { None, Unit, Building }

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

    // Broad selectors (used in upgrades)
    AllUnits,
    AllBuildings,
    All
}

public enum UnitType { None, Villager, Infantry, Ranged }
public enum BuildingType { None, TownCenter, Barracks, Farm }


// ============================================================
// UNIT STATE & STANCE & FORMATION
// ============================================================

public enum UnitState { Idle, Moving, Attacking, Returning, Gathering, Building, Patrolling, AttackMoving }
public enum UnitStance { Aggressive, Defensive, StandGround, NoAttack } // Stance saved per unit

public enum UnitFormation { Line, Spread, Box, Circle, Wedge } // used in Control groups or for selected units (not saved per unit)


// ============================================================
// Control Groups
// ============================================================
public class ControlGroup
{
    public List<ISelectable> members = new List<ISelectable>();
    public bool formationMode = false;
    public UnitStance formationStance = UnitStance.Aggressive;
    public UnitFormation formation = UnitFormation.Line;
    public List<Vector2> formationOffsets = new List<Vector2>();
    public float formationWidth = -1f; // -1 = default spacing
    public FormationAnchor anchor;
    public float formationChaseRange = 3f;

    // Maps each member to their offset index
    public Dictionary<int, int> unitToOffsetIndex = new Dictionary<int, int>(); // instanceID → offset index
    
    public ControlGroup()
    {
        members = new  List<ISelectable>();
        formationMode = false;
        formationStance = UnitStance.Aggressive;
        formation = UnitFormation.Line;
        formationOffsets = new List<Vector2>();
        formationWidth = -1f;
        formationChaseRange = 3f;
        //formationAnchor = new FormationAnchor(...);
    }
}


// ============================================================
// COMMANDS
// ============================================================

public enum CommandSubmenuType { None, Build } //, Stance, Formation } 

public enum CommandType
{
    Stop,
    AttackMove,
    Patrol,
    Build,
    Garrison,
    
    // Stances
    Aggressive, // Chases units forever
    Defensive, // Chases units until x dist
    StandGround, // Doesn't chase, but attacks if in attack range
    NoAttack, // No attack
    
    // Formations
    ToggleFormationMode, // enter formation mode, only active in control groups.
    
    FormationLine,
    FormationSpread,
    FormationBox,
    FormationCircle,
    FormationWedge,

    // Special abilities (future ICommand components)
    Ability
}

public enum CommandContext
{
    Default,
    EconomicUnitSelected,
    MilitaryUnitSelected,
    BuildingSelected,
}

public enum CommandScope
{
    PerUnit,
    Group
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

public enum ResourceType { Wood, Food, Gold, Stone }

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

public enum ProductionType { Unit, Upgrade }
public enum UpgradeType { Global, Unit }

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
    GarrisonCapacity,
}

public enum ModifierType
{
    Flat,       // +5 attack
    Percentage  // +10% attack
}

[System.Serializable]
public struct StatModifier
{
    public StatType stat;
    public float value;
    public ModifierType modifierType;
}