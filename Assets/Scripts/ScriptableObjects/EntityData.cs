using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityData", menuName = "Scriptable Objects/Entity")]
public class EntityData : ScriptableObject
{
    // Identity
    public string entityName = "Entity";
    //public int entityID = 0; // use GetEntityID() built-in instead
    public EntityTag entityTag = EntityTag.None;
    public EntityType entityType; // Unit, Building
    public GameObject prefab;
    public GameObject ghostPrefab; // Mainly for buildings (placement)

    // Shared Stats
    [Space(10)]
    [Header("___Shared___")]
    public int maxHealth = 100;
    public int armor = 0;
    public int lineOfSight = 20;

    [Space(10)]
    [Header("___Units Only___")]
    // Movement (units only)
    public UnitType unitType;  
    public float moveSpeed = 0f;
    
    // Combat
    public int attackDamage = 0;
    public float attackRange = 0f;
    public float attackInterval = 1f;

    public float defensiveChaseRange = 20f;
    
    // Villager Only
    [Header("Villager Only")]
    public int gatherAmount = 0;
    public float gatherRange = 0f;
    public float gatherInterval = 0f;
    public List<BuildOptionData> buildOptions;
    
    //public float buildSpeed? = buildInterval = 0f; -> Instantly place buildings now

    // Unit Commands
    [Header("Unit Commands")] 
    public List<CommandData> baseCommands;
    
    // Building only
    [Space(10)]
    [Header("___Building Only___")]
    public BuildingType buildingType; 
    public int gridWidth = 1;
    public int gridHeight = 1;
    
    // Garrison
    [Header("Garrison")]
    public int garrisonCapacity = 0;
    
    // Production (buildings only)
    [Header("Production")]
    public List<ProductionOptionData> productionOptions;
    public float productionSpeed = 1f; // 1 = normal, 2 = double
    
}
