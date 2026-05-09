using UnityEngine;

public enum ResourceType { Wood, Food, Gold, Stone }
public enum UnitType { Villager, Soldier, Archer }
public enum BuildingType { TownCenter, Barracks, Farm }


public enum UnitState { Idle, Moving, Attacking, Gathering }

//public enum VillagerState { Idle, Moving, Gathering, Attacking } // Building, Attacking
//public enum SoldierState { Idle, Moving, Attacking } // Patrol, AttackMove, etc.


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

[System.Serializable]
public struct SpawnOption // Buildings will use to represent and spawn
{
    public string unitName;
    public UnitType unitType;
    public GameObject prefab;
    public Sprite icon;
    public float spawnTime;
    public ResourceCost cost;
}

