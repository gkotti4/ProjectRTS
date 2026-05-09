using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // * Resources
    public event Action OnResourcesChanged;
    [SerializeField] private int startingResources = 0;
    private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    // * Population
    public event Action OnPopulationChanged;
    [SerializeField] private int currentPopulation = 0;
    [SerializeField] private int populationCap = 10;

    // * Upgrades
    private HashSet<UpgradeData> appliedUpgrades = new HashSet<UpgradeData>();
    private List<EntityStats> allEntities = new List<EntityStats>();

    void Awake()
    {
        if (Instance is not null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            resources[type] = startingResources;
    }

    // ** Resources
    public int GetCurrentResources(ResourceType type) => resources[type];

    public void AddResources(ResourceType type, int amount)
    {
        resources[type] += amount;
        OnResourcesChanged?.Invoke();
    }

    public void AddResources(ResourceCost amount)
    {
        resources[ResourceType.Wood] -= amount.wood;
        resources[ResourceType.Food] -= amount.food;
        resources[ResourceType.Gold] -= amount.gold;
        resources[ResourceType.Stone] -= amount.stone;
        OnResourcesChanged?.Invoke();
    }

    public void SpendResources(ResourceCost cost)
    {
        resources[ResourceType.Wood] -= cost.wood;
        resources[ResourceType.Food] -= cost.food;
        resources[ResourceType.Gold] -= cost.gold;
        resources[ResourceType.Stone] -= cost.stone;
        OnResourcesChanged?.Invoke();
    }

    public bool CanAfford(ResourceCost cost)
    {
        return resources[ResourceType.Wood] >= cost.wood &&
               resources[ResourceType.Gold] >= cost.gold &&
               resources[ResourceType.Food] >= cost.food &&
               resources[ResourceType.Stone] >= cost.stone;
    }

    // ** Population
    public bool CanSpawn() => currentPopulation < populationCap;
    public int GetCurrentPopulation() => currentPopulation;
    public int GetPopulationCap() => populationCap;

    public void RegisterSpawn()
    {
        currentPopulation++;
        OnPopulationChanged?.Invoke();
    }

    public void RegisterDespawn()
    {
        currentPopulation--;
        OnPopulationChanged?.Invoke();
    }

    // ** Entities
    // Called by EntityAttributes on Start to register with GameManager
    public void RegisterEntity(EntityStats entity)
    {
        allEntities.Add(entity);

        // Catch up new spawns on any upgrades already applied
        foreach (UpgradeData upgrade in appliedUpgrades)
            entity.ApplyUpgrade(upgrade);
    }

    // Called by EntityAttributes on OnDestroy
    public void UnregisterEntity(EntityStats entity)
    {
        allEntities.Remove(entity);
    }

    
    // ** Upgrades
    public bool IsUpgradeApplied(UpgradeData upgrade) => appliedUpgrades.Contains(upgrade);
    public IEnumerable<UpgradeData> GetAppliedUpgrades() => appliedUpgrades;
    
    // Called by ProductionBuildingController when upgrade completes
    public void RegisterUpgrade(UpgradeData upgrade)
    {
        if (appliedUpgrades.Contains(upgrade)) return;
        appliedUpgrades.Add(upgrade);

        // Apply directly to all live entities — no event broadcasting
        foreach (EntityStats entity in allEntities)
            entity.ApplyUpgrade(upgrade);
    }

}