using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Faction Setup")]
    [SerializeField] private FactionData playerFactionData; // Temporary - CurrentPlayer 
    [SerializeField] private FactionData enemyFactionData;
    [SerializeField] private int startingResources = 1000;
    [SerializeField] private int startingPopulationCap = 1000;

    // Faction instances
    public FactionInstance PlayerFaction { get; private set; }
    public FactionInstance EnemyFaction { get; private set; }
    private List<FactionInstance> allFactions = new List<FactionInstance>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create faction instances
        PlayerFaction = new FactionInstance(playerFactionData, teamId: 1, true, startingPopulationCap, startingResources);
        EnemyFaction = new FactionInstance(enemyFactionData, teamId: 2, false, startingPopulationCap, 10000);

        allFactions.Add(PlayerFaction);
        allFactions.Add(EnemyFaction);
        
        // TEMPORARY — replace with proper spawn system later - bug fix: moved to awake cause entity start was running before this ran
        foreach (EntityStats entity in FindObjectsOfType<EntityStats>())
        {
            if (entity.CompareTag("Player"))
                entity.faction = PlayerFaction;
            else if (entity.CompareTag("Enemy"))
                entity.faction = EnemyFaction;
        }
    }

    void Start()
    {
        // Start up ui event calls - needed? could move to Awake?
        GameEvents.ResourcesChanged(PlayerFaction); // TEMPORARY - see line 32
        GameEvents.PopulationChanged(PlayerFaction);
        GameEvents.ResourcesChanged(EnemyFaction); // TEMPORARY - see line 32
        GameEvents.PopulationChanged(EnemyFaction);
    }

    // Returns faction instance by factionId
    public FactionInstance GetFaction(int factionId)
    {
        return allFactions.Find(f => f.factionId == factionId);
    }

    // Routes entity registration to correct faction (Possibly temp, might skip routing to faction only when better game logic better situated)
    public void RegisterEntity(EntityStats entity)
    {
        FactionInstance faction = GetFactionForEntity(entity);
        faction?.RegisterEntity(entity);
    }
    
    public void UnregisterEntity(EntityStats entity)
    {
        FactionInstance faction = GetFactionForEntity(entity);
        faction?.UnregisterEntity(entity);
    }

    // Resources — routed through faction
    public void AddResources(ResourceType type, int amount, FactionInstance faction = null)
    {
        faction ??= PlayerFaction;
        faction.AddResources(type, amount);
        //GameEvents.ResourcesChanged(faction);
    }

    public void AddResources(ResourceCost cost, FactionInstance faction = null)
    {
        faction ??= PlayerFaction;
        faction.AddResources(cost);
        //GameEvents.ResourcesChanged(faction);
    }

    public void SpendResources(ResourceCost cost, FactionInstance faction = null)
    {
        faction ??= PlayerFaction;
        faction.SpendResources(cost);
        //GameEvents.ResourcesChanged(faction);
    }

    public bool CanAfford(ResourceCost cost, FactionInstance faction = null)
    {
        faction ??= PlayerFaction;
        return faction.CanAfford(cost);
    }

    public int GetCurrentResources(ResourceType type, FactionInstance faction = null)
    {
        faction ??= PlayerFaction;
        return faction.GetResources(type);
    }

    // Population — routed through faction
    public bool CanSpawn(FactionInstance faction = null, int count = 1)
    {
        faction ??= PlayerFaction;
        return faction.CanSpawn();
    }
    

    public int GetCurrentPopulation(FactionInstance faction = null)
    {
        faction ??= PlayerFaction;
        return faction.currentPopulation;
    }

    public int GetPopulationCap(FactionInstance faction = null)
    {
        faction ??= PlayerFaction;
        return faction.populationCap;
    }

    // Upgrades — routed through faction
    public bool IsUpgradeApplied(UpgradeData upgrade, FactionInstance faction = null)
    {
        faction ??= PlayerFaction;
        return faction.IsUpgradeApplied(upgrade);
    }

    public void RegisterUpgrade(UpgradeData upgrade, FactionInstance faction = null)
    {
        faction ??= PlayerFaction;
        faction.RegisterUpgrade(upgrade);
    }

    // Win/lose
    public void CheckWinLose()
    {
        // TODO — check if player or enemy town center is destroyed
    }

    // Helper — gets faction instance from entity's factionData
    private FactionInstance GetFactionForEntity(EntityStats entityStats)
    {
        if (entityStats.faction == null) return null;
        return GetFaction(entityStats.faction.baseData.factionId);
    }
}











// public void RegisterSpawn(FactionInstance faction = null)
// {
//     faction ??= PlayerFaction;
//     faction.RegisterSpawn();
//     GameEvents.PopulationChanged();
// }
//
// public void RegisterDespawn(FactionInstance faction = null)
// {
//     faction ??= PlayerFaction;
//     faction.RegisterDespawn();
//     GameEvents.PopulationChanged();
// }