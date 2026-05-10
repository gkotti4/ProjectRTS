using UnityEngine;
using UnityEngine.Serialization;


// Entities (ALL things)
public enum EntityType { None, Unit, Building }

// In Types.cs — replaces UnitType for upgrade targeting
public enum EntityTag
{
    None = 0,
    
    // Unit categories
    // Economic
    Villager,
    
    // Military
    Infantry,
    Archer,
    Cavalry,
    Siege,
    Ship,
    
    // Building categories
    MilitaryBuilding,
    EconomicBuilding,
    DefenseBuilding,
    
    // Broad
    AllUnits,
    AllBuildings,
    All
    
}


// Units
public enum UnitState { Idle, Moving, Attacking, Gathering, Building, Patrolling }
public enum UnitType { Villager, Soldier, Archer }

// Buildings
public enum BuildingType { TownCenter, Barracks, Farm }


// Resources
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


// Production
public enum ProductionType { Unit, Upgrade }

// Upgrade Production
public enum UpgradeType { Global, Unit } // AgeUp
public enum StatType
{
    Attack,
    MeleeArmor,
    PierceArmor,
    AttackSpeed,
    AttackRange,
    LineOfSight,
    MoveSpeed,
    GatherSpeed,
    GatherAmount,
    CarryCapacity,
    BuildSpeed,
    MaxHealth,
    HealRate,
    PopulationCost,
    ResourceGenerationRate,
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
    public ModifierType modifierType; // Flat, Percentage
}



// Command Context - Hotkeys - What is selected
public enum CommandContext
{
    Default,
    EconomicUnitSelected,
    MilitaryUnitSelected,
    BuildingSelected,
    // Multiple Units?
}

public enum HotkeySlot
{
    Q, W, E, R, T,
    A, S, D, F, G,
    Z, X, C, V, B
}