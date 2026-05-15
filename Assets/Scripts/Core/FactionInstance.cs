using System.Collections.Generic;
using UnityEngine;

public class FactionInstance
{
    // Static config reference
    public FactionData baseData;
    public int factionId => baseData.factionId;

    // Runtime identity
    public int teamId = 0;
    public bool isPlayerControlled = true;
    
    // Economy
    public Dictionary<ResourceType, int> resources;
    public int currentPopulation = 0;
    public int populationCap = 10;
    
    // Upgrades
    public HashSet<UpgradeData> appliedUpgrades = new HashSet<UpgradeData>();
    public List<EntityStats> ownedEntities = new List<EntityStats>();
    
    // Win / lose
    public bool isEliminated = false;


    public FactionInstance(FactionData baseData, int teamId, bool isPlayerControlled, int populationCap, int startingResources)
    {
        this.baseData = baseData;
        this.teamId = teamId;
        this.isPlayerControlled = isPlayerControlled;
        this.populationCap = populationCap;
        
        // Initialize dictionary FIRST before adding anything
        resources = new Dictionary<ResourceType, int>();
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            resources[type] = 0;
        
        // Add starting resources
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            AddResources(type, startingResources);
        }
        
        //GameEvents.ResourcesChanged(this); // call too early? see GameManager Awake()
        //GameEvents.PopulationChanged(this); 
    }
    
    // Resources
    public int GetResources(ResourceType type) => resources[type];

    public ResourceCost GetResources()
    {
        ResourceCost res = new ResourceCost();
        res.wood = resources[ResourceType.Wood];
        res.food = resources[ResourceType.Food];
        res.gold = resources[ResourceType.Gold];
        res.stone = resources[ResourceType.Stone];
        return res;
    }
    public void AddResources(ResourceType type, int amount)
    {
        resources[type] += amount;
        GameEvents.ResourcesChanged(this);
    }

    public void AddResources(ResourceCost cost)
    {
        resources[ResourceType.Wood] += cost.wood;
        resources[ResourceType.Food] += cost.food;
        resources[ResourceType.Gold] += cost.gold;
        resources[ResourceType.Stone] += cost.stone;
        GameEvents.ResourcesChanged(this);
    }

    public void SpendResources(ResourceType type, int amount)
    {
        resources[type] -= amount;
        GameEvents.ResourcesChanged(this);
    }
    public void SpendResources(ResourceCost cost)
    {
        resources[ResourceType.Wood] -= cost.wood;
        resources[ResourceType.Food] -= cost.food;
        resources[ResourceType.Gold] -= cost.gold;
        resources[ResourceType.Stone] -= cost.stone;
        GameEvents.ResourcesChanged(this);
    }

    public bool CanAfford(ResourceCost cost)
    {
        return resources[ResourceType.Wood] >= cost.wood &&
               resources[ResourceType.Gold] >= cost.gold &&
               resources[ResourceType.Food] >= cost.food &&
               resources[ResourceType.Stone] >= cost.stone;
    }
    
    
    // Population
    public bool CanSpawn() => currentPopulation < populationCap;
    
    // Entities
    public void RegisterEntity(EntityStats stats)
    {
        ownedEntities.Add(stats);
        if (stats.baseData.entityType == EntityType.Unit) // Only change pop for units
        {
            currentPopulation++;
            GameEvents.PopulationChanged(stats.faction);
        }

        foreach(UpgradeData upgrade in appliedUpgrades)
            stats.ApplyUpgrade(upgrade); 
        
    }

    public void UnregisterEntity(EntityStats stats)
    {
        ownedEntities.Remove(stats);
        if (stats.baseData.entityType == EntityType.Unit) // Only change pop for units
        {
            currentPopulation--;
            GameEvents.PopulationChanged(stats.faction);
        }
    }
    
    
    // Upgrades

    public bool IsUpgradeApplied(UpgradeData upgradeData)
    {
        return appliedUpgrades.Contains(upgradeData);
    }

    public void RegisterUpgrade(UpgradeData upgradeData)
    {
        if (appliedUpgrades.Contains(upgradeData))
        {
            Debug.LogWarning("Upgrade already applied " + upgradeData.name + " in " + baseData.name);
        }
        appliedUpgrades.Add(upgradeData);
        foreach (EntityStats entityStats in ownedEntities)
            entityStats.ApplyUpgrade(upgradeData); 
    }
    
    
    
    
}
