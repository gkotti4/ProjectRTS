using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime faction state for one player/team in the match.
/// This intentionally does not store EntityStats or apply upgrades directly to old entity components.
/// New systems should ask this faction whether an upgrade is applied, then apply that upgrade through
/// role-specific systems later: Squad/Soldier/Worker/Building.
/// </summary>
public class FactionInstance
{
    public FactionData baseData;
    public int factionId => baseData != null ? baseData.factionId : -1;

    public int teamId = 0;
    public bool isPlayerControlled = true;

    public readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
    public int currentPopulation = 0;
    public int populationCap = 10;

    public readonly HashSet<UpgradeData> appliedUpgrades = new HashSet<UpgradeData>();

    public bool isEliminated = false;
    
    public TeamVisualSettings Visuals =>
        baseData != null ? baseData.visuals : TeamVisualSettings.Default;

    public FactionInstance(
        FactionData baseData,
        int teamId,
        bool isPlayerControlled,
        int populationCap,
        int startingResources)
    {
        this.baseData = baseData;
        this.teamId = teamId;
        this.isPlayerControlled = isPlayerControlled;
        this.populationCap = Mathf.Max(0, populationCap);

        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            resources[type] = Mathf.Max(0, startingResources);
    }

    public int GetResources(ResourceType type)
    {
        EnsureResourceKey(type);
        return resources[type];
    }

    public ResourceCost GetResources()
    {
        return new ResourceCost(
            wood: GetResources(ResourceType.Wood),
            food: GetResources(ResourceType.Food),
            gold: GetResources(ResourceType.Gold),
            stone: GetResources(ResourceType.Stone));
    }

    public void AddResources(ResourceType type, int amount)
    {
        EnsureResourceKey(type);
        resources[type] += Mathf.Max(0, amount);
        GameEvents.ResourcesChanged(this);
    }

    public void AddResources(ResourceCost cost)
    {
        AddResourceSilently(ResourceType.Wood, cost.wood);
        AddResourceSilently(ResourceType.Food, cost.food);
        AddResourceSilently(ResourceType.Gold, cost.gold);
        AddResourceSilently(ResourceType.Stone, cost.stone);

        GameEvents.ResourcesChanged(this);
    }

    public void SpendResources(ResourceType type, int amount)
    {
        EnsureResourceKey(type);
        resources[type] -= Mathf.Max(0, amount);
        GameEvents.ResourcesChanged(this);
    }

    public void SpendResources(ResourceCost cost)
    {
        SpendResourceSilently(ResourceType.Wood, cost.wood);
        SpendResourceSilently(ResourceType.Food, cost.food);
        SpendResourceSilently(ResourceType.Gold, cost.gold);
        SpendResourceSilently(ResourceType.Stone, cost.stone);

        GameEvents.ResourcesChanged(this);
    }

    public bool CanAfford(ResourceCost cost)
    {
        return GetResources(ResourceType.Wood) >= cost.wood &&
               GetResources(ResourceType.Food) >= cost.food &&
               GetResources(ResourceType.Gold) >= cost.gold &&
               GetResources(ResourceType.Stone) >= cost.stone;
    }

    public bool CanSpawn(int count = 1)
    {
        count = Mathf.Max(0, count);
        return currentPopulation + count <= populationCap;
    }

    public void RegisterPopulation(int count = 1)
    {
        currentPopulation += Mathf.Max(0, count);
        GameEvents.PopulationChanged(this);
    }

    public void UnregisterPopulation(int count = 1)
    {
        currentPopulation = Mathf.Max(0, currentPopulation - Mathf.Max(0, count));
        GameEvents.PopulationChanged(this);
    }

    public bool IsUpgradeApplied(UpgradeData upgradeData)
    {
        return upgradeData != null && appliedUpgrades.Contains(upgradeData);
    }

    public void RegisterUpgrade(UpgradeData upgradeData)
    {
        if (upgradeData == null)
            return;

        if (!appliedUpgrades.Add(upgradeData))
        {
            string factionName = baseData != null ? baseData.name : "Unknown Faction";
            Debug.LogWarning($"Upgrade already applied: {upgradeData.name} in {factionName}.");
        }
    }

    void EnsureResourceKey(ResourceType type)
    {
        if (!resources.ContainsKey(type))
            resources[type] = 0;
    }

    void AddResourceSilently(ResourceType type, int amount)
    {
        EnsureResourceKey(type);
        resources[type] += Mathf.Max(0, amount);
    }

    void SpendResourceSilently(ResourceType type, int amount)
    {
        EnsureResourceKey(type);
        resources[type] -= Mathf.Max(0, amount);
    }
}
