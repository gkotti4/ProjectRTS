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

    [Header("Starting Upgrades")]
    [SerializeField] private List<UpgradeData> playerStartingUpgrades = new List<UpgradeData>();
    [SerializeField] private List<UpgradeData> enemyStartingUpgrades = new List<UpgradeData>();

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

        if (GameSession.Instance != null &&
            GameSession.Instance.TryConsumePendingRuntimeSetup(
                out GameRuntimeSetup pendingRuntimeSetup))
        {
            InitializeRuntime(pendingRuntimeSetup);
            return;
        }

        InitializeRuntime(BuildInspectorFallbackRuntimeSetup());
    }


    #region Runtime Initialization

    /// <summary>
    /// Creates the shared live-game faction runtime from an in-memory setup supplied
    /// by GameSession/game-mode code. GameManager owns the resulting FactionInstances;
    /// it does not own the progression/save state that produced this setup.
    /// </summary>
    public bool InitializeRuntime(GameRuntimeSetup runtimeSetup)
    {
        if (runtimeSetup == null || !runtimeSetup.IsValid)
        {
            Debug.LogError(
                "GameManager.InitializeRuntime failed: runtime setup is null or invalid.",
                this);
            return false;
        }

        PlayerFaction = CreateFactionInstance(runtimeSetup.playerFaction);
        EnemyFaction = CreateFactionInstance(runtimeSetup.enemyFaction);

        if (PlayerFaction == null || EnemyFaction == null)
        {
            Debug.LogError(
                "GameManager.InitializeRuntime failed to create required factions.",
                this);
            return false;
        }

        allFactions.Clear();
        allFactions.Add(PlayerFaction);
        allFactions.Add(EnemyFaction);

        ApplyStartingUpgrades(
            PlayerFaction,
            runtimeSetup.playerFaction.startingUpgrades);

        ApplyStartingUpgrades(
            EnemyFaction,
            runtimeSetup.enemyFaction.startingUpgrades);

        return true;
    }

    FactionInstance CreateFactionInstance(FactionRuntimeSetup factionSetup)
    {
        if (factionSetup == null || factionSetup.factionData == null)
            return null;

        return new FactionInstance(
            factionSetup.factionData,
            factionSetup.teamId,
            factionSetup.isPlayerControlled,
            Mathf.Max(0, factionSetup.startingPopulationCap),
            Mathf.Max(0, factionSetup.startingResources));
    }

    /// <summary>
    /// Keeps direct scene play and Battle Sandbox testing working without requiring
    /// a persistent GameSession to prepare a runtime setup first.
    /// </summary>
    GameRuntimeSetup BuildInspectorFallbackRuntimeSetup()
    {
        GameRuntimeSetup runtimeSetup = new GameRuntimeSetup();

        runtimeSetup.playerFaction.factionData = playerFactionData;
        runtimeSetup.playerFaction.teamId = 1;
        runtimeSetup.playerFaction.isPlayerControlled = true;
        runtimeSetup.playerFaction.startingResources = startingResources;
        runtimeSetup.playerFaction.startingPopulationCap = startingPopulationCap;
        runtimeSetup.playerFaction.startingUpgrades =
            new List<UpgradeData>(playerStartingUpgrades);

        runtimeSetup.enemyFaction.factionData = enemyFactionData;
        runtimeSetup.enemyFaction.teamId = 2;
        runtimeSetup.enemyFaction.isPlayerControlled = false;
        runtimeSetup.enemyFaction.startingResources = startingResources;
        runtimeSetup.enemyFaction.startingPopulationCap = startingPopulationCap;
        runtimeSetup.enemyFaction.startingUpgrades =
            new List<UpgradeData>(enemyStartingUpgrades);

        return runtimeSetup;
    }

    #endregion

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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

    public bool CanApplyFactionUpgrade(
        UpgradeData upgrade,
        FactionInstance factionInstance = null)
    {
        factionInstance ??= PlayerFaction;
        return factionInstance != null &&
               factionInstance.CanApplyUpgrade(upgrade);
    }

    public bool TryApplyFactionUpgrade(
        UpgradeData upgrade,
        FactionInstance factionInstance = null,
        UpgradeGrantSource grantSource = UpgradeGrantSource.Debug)
    {
        factionInstance ??= PlayerFaction;

        return factionInstance != null &&
               factionInstance.TryApplyUpgrade(upgrade, grantSource);
    }

    // Compatibility wrapper for older callers.
    public void RegisterUpgrade(
        UpgradeData upgrade,
        FactionInstance factionInstance = null)
    {
        TryApplyFactionUpgrade(
            upgrade,
            factionInstance,
            UpgradeGrantSource.Debug);
    }

    #region Runtime Snapshot API

    /// <summary>
    /// Captures the shared live-game state currently owned by this runtime.
    /// This is an in-memory handoff object for GameSession/game-mode systems;
    /// it is not itself a permanent save file.
    /// </summary>
    public GameRuntimeSnapshot CaptureRuntimeSnapshot()
    {
        GameRuntimeSnapshot snapshot = new GameRuntimeSnapshot();

        for (int index = 0; index < allFactions.Count; index++)
        {
            FactionInstance faction = allFactions[index];

            if (faction == null)
                continue;

            FactionRuntimeSnapshot factionSnapshot =
                CaptureFactionSnapshot(faction);

            snapshot.factions.Add(factionSnapshot);

            if (faction == PlayerFaction)
                snapshot.playerFaction = factionSnapshot;
        }

        snapshot.playerArmy.AddRange(
            CaptureFactionArmySnapshot(PlayerFaction));

        return snapshot;
    }

    /// <summary>
    /// Captures one faction's shared runtime state without exposing its mutable
    /// dictionaries to persistent/session systems.
    /// </summary>
    public FactionRuntimeSnapshot CaptureFactionSnapshot(
        FactionInstance factionInstance)
    {
        FactionRuntimeSnapshot snapshot = new FactionRuntimeSnapshot();

        if (factionInstance == null)
            return snapshot;

        snapshot.factionData = factionInstance.baseData;
        snapshot.factionId = factionInstance.factionId;
        snapshot.teamId = factionInstance.teamId;
        snapshot.isPlayerControlled = factionInstance.isPlayerControlled;
        snapshot.resources = factionInstance.GetResources();
        snapshot.currentPopulation = factionInstance.currentPopulation;
        snapshot.populationCap = factionInstance.populationCap;

        foreach (KeyValuePair<UpgradeData, int> pair in
                 factionInstance.AppliedUpgradeStacks)
        {
            UpgradeData upgrade = pair.Key;
            int stackCount = Mathf.Max(0, pair.Value);

            if (upgrade == null || stackCount <= 0)
                continue;

            snapshot.appliedUpgrades.Add(
                new RuntimeUpgradeStackSnapshot
                {
                    upgradeData = upgrade,
                    upgradeId = upgrade.upgradeId,
                    stackCount = stackCount
                });
        }

        return snapshot;
    }

    /// <summary>
    /// Captures every currently instantiated squad belonging to one faction.
    /// Squads that have already left/destroyed themselves (for example routed
    /// off-field squads) are intentionally not reconstructed here; a battle result
    /// must preserve those game-mode-specific outcomes before destruction.
    /// </summary>
    public List<SquadRuntimeSnapshot> CaptureFactionArmySnapshot(
        FactionInstance factionInstance)
    {
        List<SquadRuntimeSnapshot> snapshots =
            new List<SquadRuntimeSnapshot>();

        if (factionInstance == null || SquadManager.Instance == null)
            return snapshots;

        IReadOnlyList<SquadController> runtimeSquads =
            SquadManager.Instance.Squads;

        for (int index = 0; index < runtimeSquads.Count; index++)
        {
            SquadController squad = runtimeSquads[index];

            if (squad == null ||
                !squad.IsInitialized ||
                squad.Faction != factionInstance ||
                squad.Data == null ||
                squad.Roster == null)
            {
                continue;
            }

            SquadRuntimeSnapshot squadSnapshot =
                CaptureSquadSnapshot(squad);

            snapshots.Add(squadSnapshot);
        }

        return snapshots;
    }

    public List<SquadRuntimeSnapshot> CapturePlayerArmySnapshot()
    {
        return CaptureFactionArmySnapshot(PlayerFaction);
    }

    public SquadRuntimeSnapshot CaptureSquadSnapshot(
        SquadController squad)
    {
        SquadRuntimeSnapshot snapshot = new SquadRuntimeSnapshot();

        if (squad == null || squad.Data == null)
            return snapshot;

        snapshot.squadData = squad.Data;
        snapshot.squadId = squad.Data.squadId;
        snapshot.livingSoldierCount =
            squad.Roster != null ? squad.Roster.LivingCount : 0;
        snapshot.existingSoldierCount =
            squad.Roster != null ? squad.Roster.Count : 0;

        foreach (KeyValuePair<UpgradeData, int> pair in
                 squad.AppliedUpgradeStacks)
        {
            UpgradeData upgrade = pair.Key;
            int stackCount = Mathf.Max(0, pair.Value);

            if (upgrade == null || stackCount <= 0)
                continue;

            snapshot.appliedUpgrades.Add(
                new RuntimeUpgradeStackSnapshot
                {
                    upgradeData = upgrade,
                    upgradeId = upgrade.upgradeId,
                    stackCount = stackCount
                });
        }

        return snapshot;
    }

    #endregion

    void ApplyStartingUpgrades(
        FactionInstance factionInstance,
        IReadOnlyList<UpgradeData> startingUpgrades)
    {
        if (factionInstance == null || startingUpgrades == null)
            return;

        for (int index = 0; index < startingUpgrades.Count; index++)
        {
            UpgradeData upgrade = startingUpgrades[index];

            if (upgrade == null)
                continue;

            if (!factionInstance.TryApplyUpgrade(
                    upgrade,
                    UpgradeGrantSource.MatchStartingUpgrade))
            {
                Debug.LogWarning(
                    $"Could not apply starting upgrade {upgrade.name} to faction {factionInstance.factionId}.",
                    this);
            }
        }
    }

    public void CheckWinLose()
    {
        // TODO: check team elimination / objective state once buildings and squad death are finalized.
    }
}
