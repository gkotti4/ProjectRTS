using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime faction state for one player/team in the match.
/// Owns faction-wide upgrade stacks and notifies matching runtime units when a new
/// upgrade is applied.
/// </summary>
public class FactionInstance
{
    public event Action<FactionInstance, UpgradeData, UpgradeGrantSource, int> OnUpgradeApplied;

    public FactionData baseData;
    public int factionId => baseData != null ? baseData.factionId : -1;

    public int teamId = 0;
    public bool isPlayerControlled = true;

    public readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
    public int currentPopulation = 0;
    public int populationCap = 10;

    private readonly Dictionary<UpgradeData, int> appliedUpgradeStacks =
        new Dictionary<UpgradeData, int>();

    public IReadOnlyDictionary<UpgradeData, int> AppliedUpgradeStacks =>
        appliedUpgradeStacks;

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

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
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

    public int GetUpgradeStackCount(UpgradeData upgradeData)
    {
        if (upgradeData == null)
            return 0;

        return appliedUpgradeStacks.TryGetValue(upgradeData, out int stackCount)
            ? Mathf.Max(0, stackCount)
            : 0;
    }

    public bool IsUpgradeApplied(UpgradeData upgradeData)
    {
        return GetUpgradeStackCount(upgradeData) > 0;
    }

    public bool CanApplyUpgrade(UpgradeData upgradeData)
    {
        if (upgradeData == null)
            return false;

        if (upgradeData.scope != UpgradeScope.Faction)
            return false;

        int currentStacks = GetUpgradeStackCount(upgradeData);
        int maximumStacks = upgradeData.repeatable
            ? Mathf.Max(1, upgradeData.maximumStacks)
            : 1;

        if (currentStacks >= maximumStacks)
            return false;

        if (upgradeData.requiredUpgrades != null)
        {
            for (int index = 0; index < upgradeData.requiredUpgrades.Count; index++)
            {
                UpgradeData requiredUpgrade = upgradeData.requiredUpgrades[index];

                if (requiredUpgrade != null && !IsUpgradeApplied(requiredUpgrade))
                    return false;
            }
        }

        if (upgradeData.blockedByUpgrades != null)
        {
            for (int index = 0; index < upgradeData.blockedByUpgrades.Count; index++)
            {
                UpgradeData blockedUpgrade = upgradeData.blockedByUpgrades[index];

                if (blockedUpgrade != null && IsUpgradeApplied(blockedUpgrade))
                    return false;
            }
        }

        return true;
    }

    public bool TryApplyUpgrade(
        UpgradeData upgradeData,
        UpgradeGrantSource grantSource)
    {
        if (!CanApplyUpgrade(upgradeData))
            return false;

        int newStackCount = GetUpgradeStackCount(upgradeData) + 1;
        appliedUpgradeStacks[upgradeData] = newStackCount;

        OnUpgradeApplied?.Invoke(
            this,
            upgradeData,
            grantSource,
            newStackCount);

        GameEvents.FactionUpgradeApplied(
            this,
            upgradeData,
            grantSource,
            newStackCount);

        return true;
    }

    // Compatibility wrapper for older callers.
    public void RegisterUpgrade(UpgradeData upgradeData)
    {
        if (!TryApplyUpgrade(upgradeData, UpgradeGrantSource.Debug))
        {
            string factionName = baseData != null ? baseData.name : "Unknown Faction";
            Debug.LogWarning(
                $"Could not apply upgrade {upgradeData?.name ?? "null"} to {factionName}.");
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
