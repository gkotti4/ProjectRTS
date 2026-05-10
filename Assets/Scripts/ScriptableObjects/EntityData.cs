using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityData", menuName = "Scriptable Objects/Entity")]
public class EntityData : ScriptableObject
{
    // Identity
    public string entityName = "Entity";
    public int entityID = 0;
    public EntityTag entityTag = EntityTag.None;
    public EntityType entityType; // Unit, Building
    public UnitType unitType;     // Infantry, Villager, None (if building)
    public BuildingType buildingType; // None (if unit)
    public GameObject prefab;

    // Shared Stats
    [Header("Shared")]
    public int maxHealth = 100;
    public int armor = 0;
    public int lineOfSight = 5;

    // Combat
    [Header("Combat")]
    public int attackDamage = 0;
    public float attackRange = 0f;
    public float attackInterval = 1f;

    // Movement (units only)
    [Header("Units Only")]
    public float moveSpeed = 0f;

    // Gathering (villager only)
    [Header("Villager Only")]
    public int gatherAmount = 0;
    public float gatherRange = 0f;
    public float gatherInterval = 0f;

    // Building only
    [Header("Building Only")]
    public ResourceCost buildingCost;
    public int gridWidth = 1;
    public int gridHeight = 1;
    
    // Garrison
    public int garrisonCapacity = 0;
    
    // Production (buildings only)
    [Header("Production Only")]
    public List<ProductionOptionData> productionOptions;
    public float productionSpeed = 1f; // 1 = normal, 2 = double
    

}
