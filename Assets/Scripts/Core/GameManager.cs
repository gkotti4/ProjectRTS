using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Faction Setup")]
    [SerializeField] private FactionData playerFactionData;
    [SerializeField] private FactionData enemyFactionData;
    
    [Header("Starting Values")]
    [SerializeField] private int startingResources = 1000;
    [SerializeField] private int startingPopulationCap = 1000;

    public FactionInstance PlayerFaction { get; private set; }
    public FactionInstance EnemyFaction { get; private set; }

    private readonly List<FactionInstance> allFactions = new List<FactionInstance>();

    public IReadOnlyList<FactionInstance> AllFactions => allFactions;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        PlayerFaction = new FactionInstance(
            playerFactionData,
            teamId: 1,
            isPlayerControlled: true,
            populationCap: startingPopulationCap,
            startingResources: startingResources);

        EnemyFaction = new FactionInstance(
            enemyFactionData,
            teamId: 2,
            isPlayerControlled: false,
            populationCap: startingPopulationCap,
            startingResources: startingResources);

        allFactions.Clear();
        allFactions.Add(PlayerFaction);
        allFactions.Add(EnemyFaction);
    }

    void Start()
    {
        GameEvents.ResourcesChanged(PlayerFaction);
        GameEvents.PopulationChanged(PlayerFaction);

        GameEvents.ResourcesChanged(EnemyFaction);
        GameEvents.PopulationChanged(EnemyFaction);
    }

    public FactionInstance GetFaction(int factionId)
    {
        return allFactions.Find(faction =>
            faction != null &&
            faction.factionId == factionId);
    }

    public void AddResources(
        ResourceType type,
        int amount,
        FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;

        if (factionInstance == null)
            return;

        factionInstance.AddResources(type, amount);
    }

    public void AddResources(
        ResourceCost cost,
        FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;

        if (factionInstance == null)
            return;

        factionInstance.AddResources(cost);
    }

    public void SpendResources(
        ResourceCost cost,
        FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;

        if (factionInstance == null)
            return;

        factionInstance.SpendResources(cost);
    }

    public bool CanAfford(
        ResourceCost cost,
        FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;
        return factionInstance != null && factionInstance.CanAfford(cost);
    }

    public int GetCurrentResources(
        ResourceType type,
        FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;
        return factionInstance != null ? factionInstance.GetResources(type) : 0;
    }

    public bool CanSpawn(
        FactionInstance factionInstance = null,
        int count = 1)
    {
        factionInstance ??= PlayerFaction;
        return factionInstance != null && factionInstance.CanSpawn(count);
    }

    public int GetCurrentPopulation(FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;
        return factionInstance != null ? factionInstance.currentPopulation : 0;
    }

    public int GetPopulationCap(FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;
        return factionInstance != null ? factionInstance.populationCap : 0;
    }

    public bool IsUpgradeApplied(
        UpgradeData upgrade,
        FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;
        return factionInstance != null && factionInstance.IsUpgradeApplied(upgrade);
    }

    public void RegisterUpgrade(
        UpgradeData upgrade,
        FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;

        if (factionInstance == null)
            return;

        factionInstance.RegisterUpgrade(upgrade);
    }

    public void CheckWinLose()
    {
        // TODO: check team elimination / objective state once buildings and squad death are finalized.
    }
}
