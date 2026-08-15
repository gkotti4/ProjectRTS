using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleGameState
{
    Setup,
    Battle,
    Victory,
    Defeat
}

/// <summary>
/// First playable game-mode foundation.
///
/// Owns one self-contained battle:
/// - spawns player and enemy armies from BattleDefinitionData
/// - tracks only the squads spawned for this battle
/// - detects victory and defeat
/// - stops surviving squads when the battle ends
/// - exposes state/events for the upcoming HUD, result screen, AI, and upgrade flow
///
/// This intentionally does not own tactical AI or post-battle upgrades yet.
/// </summary>
public class BattleGameModeController : MonoBehaviour
{
    public static BattleGameModeController Instance { get; private set; }

    public event Action<BattleGameState> OnBattleStateChanged;
    public event Action<IReadOnlyList<SquadController>, IReadOnlyList<SquadController>>
        OnArmiesSpawned;
    public event Action<int, BattleDefinitionData> OnBattleRunAdvanced;
    public event Action OnBattleRunCompleted;
    public event Action<BattleResult> OnBattleResolved;

    #region Setup

    [Header("Battle")]
    [Tooltip("Fallback single battle when no Battle Run Sequence is authored.")]
    [SerializeField] private BattleDefinitionData battleDefinition;

    [Header("Battle Run")]
    [Tooltip("Ordered battle sequence for the current run. If empty, Battle Definition is used as a one-battle run.")]
    [SerializeField] private List<BattleDefinitionData> battleRunSequence =
        new List<BattleDefinitionData>();

    [Header("Startup")]
    [SerializeField] private bool startBattleAutomatically = true;

    [Min(0f)]
    [SerializeField] private float automaticStartDelay = 0.25f;

    [Header("Battle End")]
    [Min(0.05f)]
    [SerializeField] private float battleEndCheckInterval = 0.25f;
    
    [Header("Battle Map")]
    [SerializeField] private BattleMap battleMap;

    [Header("DEBUG")] 
    [SerializeField] private bool useHotkeyToTriggerBattleVictory = false;
    [SerializeField] private KeyCode triggerBattleVictoryKey = KeyCode.BackQuote;

    private bool destroyPreviousArmiesOnRestart = true;
    
    public Vector3 PlayerStartPosition =>
        battleMap != null
            ? battleMap.GetDeploymentCenter(true)
            : Vector3.zero;
    
    #endregion

    #region Runtime

    private readonly List<SquadController> playerSquads =
        new List<SquadController>();

    private readonly List<SquadController> enemySquads =
        new List<SquadController>();

    private readonly List<BattleSquadDeployment> playerArmyDeploymentOverride =
        new List<BattleSquadDeployment>();

    private sealed class BattleParticipation
    {
        public string externalSquadId;
        public SquadData squadData;
        public SquadController squad;
        public SquadRoster roster;
        public SquadMorale morale;
        public int startingSoldierCount;
        public int lastKnownLivingSoldierCount;
        public bool routedOffField;
        public bool isPlayerArmy;
    }

    private readonly List<BattleParticipation> battleParticipations =
        new List<BattleParticipation>();

    private readonly Dictionary<SquadRoster, BattleParticipation> participationByRoster =
        new Dictionary<SquadRoster, BattleParticipation>();

    private readonly Dictionary<SquadMorale, BattleParticipation> participationByMorale =
        new Dictionary<SquadMorale, BattleParticipation>();

    private readonly BattleRunState battleRunState = new BattleRunState();

    private BattleGameState state = BattleGameState.Setup;
    private float automaticStartTimer = 0f;
    private float battleEndCheckTimer = 0f;
    private float battleElapsedTime = 0f;

    public BattleDefinitionData BattleDefinition => battleDefinition;
    public BattleRunState RunState => battleRunState;
    public bool IsBattleRunComplete => battleRunState.IsComplete;
    public BattleGameState State => state;
    public IReadOnlyList<SquadController> PlayerSquads => playerSquads;
    public IReadOnlyList<SquadController> EnemySquads => enemySquads;
    public bool IsBattleActive => state == BattleGameState.Battle;
    public float BattleElapsedTime => battleElapsedTime;
    public BattleResult LastBattleResult { get; private set; }

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        automaticStartTimer = automaticStartDelay;

        InitializeBattleRun();
        
        if (battleMap == null)
            battleMap = BattleMap.Instance; // vs FindInstanceOfType ?
    }

    void Start()
    {
        ValidateSetup();

        if (!startBattleAutomatically)
            return;

        automaticStartTimer = automaticStartDelay;
    }

    void Update()
    {
        if (state == BattleGameState.Setup && startBattleAutomatically)
        {
            automaticStartTimer -= Time.deltaTime;

            if (automaticStartTimer <= 0f)
            {
                startBattleAutomatically = false;
                StartBattle();
            }

            return;
        }

        if (state != BattleGameState.Battle)
            return;
        
        // DEBUG 
        if (useHotkeyToTriggerBattleVictory && Input.GetKeyDown(triggerBattleVictoryKey))
        {
            EndBattle(BattleGameState.Victory);
        }

        battleElapsedTime += Time.deltaTime;
        battleEndCheckTimer -= Time.deltaTime;

        if (battleEndCheckTimer <= 0f)
        {
            battleEndCheckTimer = battleEndCheckInterval;
            EvaluateBattleEnd();
        }
    }

    void OnDestroy()
    {
        ClearBattleParticipationTracking();

        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Public Battle Control

    public void StartBattle()
    {
        if (!CanStartBattle())
            return;

        ClearPreviousArmies();

        battleElapsedTime = 0f;
        SetState(BattleGameState.Setup);

        LastBattleResult = null;
        ClearBattleParticipationTracking();

        if (playerArmyDeploymentOverride.Count > 0)
        {
            SpawnDeploymentArmy(
                playerArmyDeploymentOverride,
                GameManager.Instance.PlayerFaction,
                isPlayerArmy: true,
                playerSquads);
        }
        else
        {
            SpawnArmy(
                battleDefinition.playerArmy,
                GameManager.Instance.PlayerFaction,
                isPlayerArmy: true,
                playerSquads);
        }

        SpawnArmy(
            battleDefinition.enemyArmy,
            GameManager.Instance.EnemyFaction,
            isPlayerArmy: false,
            enemySquads);

        if (playerSquads.Count == 0 || enemySquads.Count == 0)
        {
            Debug.LogError(
                $"{name}: Battle could not start because one army spawned no squads.",
                this);

            StopAllSurvivingSquads();
            return;
        }

        battleEndCheckTimer = battleEndCheckInterval;
        SetState(BattleGameState.Battle);

        OnArmiesSpawned?.Invoke(playerSquads, enemySquads);
    }

    public void RestartBattle()
    {
        StartBattle();
    }

    /// <summary>
    /// Advances the run after a successful battle/reward. RestartBattle remains the
    /// explicit retry path for the current battle.
    /// </summary>
    public bool AdvanceBattleRun()
    {
        if (state == BattleGameState.Battle)
            return false;

        if (!battleRunState.TryAdvance())
        {
            if (battleRunState.IsComplete)
            {
                OnBattleRunCompleted?.Invoke();

                Debug.Log(
                    $"{name}: Battle run complete after {battleRunState.BattleCount} battle(s).",
                    this);
            }

            return false;
        }

        battleDefinition = battleRunState.CurrentBattle;

        OnBattleRunAdvanced?.Invoke(
            battleRunState.CurrentBattleIndex,
            battleDefinition);

        StartBattle();
        return true;
    }

    public void ResetBattleRun(bool startFirstBattle = true)
    {
        battleRunState.Reset();
        battleDefinition = battleRunState.CurrentBattle;

        if (startFirstBattle && battleDefinition != null)
            StartBattle();
    }

    public void ForcePlayerVictory()
    {
        if (state == BattleGameState.Battle)
            EndBattle(BattleGameState.Victory);
    }

    public void ForcePlayerDefeat()
    {
        if (state == BattleGameState.Battle)
            EndBattle(BattleGameState.Defeat);
    }

    public void SetBattleDefinition(BattleDefinitionData definition)
    {
        if (state == BattleGameState.Battle)
        {
            Debug.LogWarning(
                $"{name}: Cannot replace the battle definition during an active battle.",
                this);

            return;
        }

        battleDefinition = definition;
    }

    /// <summary>
    /// Overrides only the player army for subsequent StartBattle calls. This is an
    /// in-memory deployment handoff used by higher-level modes such as Contract
    /// Mercenary. The authored BattleDefinition player army remains the sandbox
    /// fallback when no override is present.
    /// </summary>
    public void SetPlayerArmyDeployments(
        IReadOnlyList<BattleSquadDeployment> deployments)
    {
        if (state == BattleGameState.Battle)
        {
            Debug.LogWarning(
                $"{name}: Cannot replace player army deployments during an active battle.",
                this);
            return;
        }

        playerArmyDeploymentOverride.Clear();

        if (deployments == null)
            return;

        for (int index = 0; index < deployments.Count; index++)
        {
            BattleSquadDeployment deployment = deployments[index];

            if (deployment == null ||
                deployment.squadData == null ||
                deployment.soldierCount <= 0)
            {
                continue;
            }

            playerArmyDeploymentOverride.Add(
                new BattleSquadDeployment
                {
                    externalSquadId = deployment.externalSquadId,
                    squadData = deployment.squadData,
                    soldierCount = Mathf.Max(1, deployment.soldierCount)
                });
        }
    }

    public void ClearPlayerArmyDeployments()
    {
        if (state == BattleGameState.Battle)
            return;

        playerArmyDeploymentOverride.Clear();
    }

    #endregion

    #region Spawning

    void SpawnArmy(
        IReadOnlyList<BattleSquadEntry> entries,
        FactionInstance faction,
        bool isPlayerArmy,
        List<SquadController> destination)
    {
        destination.Clear();

        if (entries == null ||
            faction == null ||
            battleMap == null)
        {
            return;
        }

        int totalSquadCount =
            CountRequestedSquads(entries);

        int spawnedSquadIndex = 0;

        Quaternion spawnRotation =
            battleMap.GetDeploymentRotation(
                isPlayerArmy);

        for (int entryIndex = 0;
             entryIndex < entries.Count;
             entryIndex++)
        {
            BattleSquadEntry entry =
                entries[entryIndex];

            if (entry.squadData == null)
                continue;

            int squadCount =
                Mathf.Max(1, entry.squadCount);

            for (int countIndex = 0;
                 countIndex < squadCount;
                 countIndex++)
            {
                Vector3 spawnPosition =
                    battleMap.GetDeploymentPosition(
                        isPlayerArmy,
                        spawnedSquadIndex,
                        totalSquadCount);

                SquadController squad =
                    SquadFactory.SpawnSquad(
                        entry.squadData,
                        spawnPosition,
                        spawnRotation,
                        faction);

                spawnedSquadIndex++;

                if (squad != null)
                {
                    destination.Add(squad);
                    RegisterBattleParticipation(
                        squad,
                        externalSquadId: null,
                        startingSoldierCount: squad.Roster != null
                            ? squad.Roster.LivingCount
                            : entry.squadData.ResolvedStartingSoldierCount,
                        isPlayerArmy);
                }
            }
        }
    }

    void SpawnDeploymentArmy(
        IReadOnlyList<BattleSquadDeployment> deployments,
        FactionInstance faction,
        bool isPlayerArmy,
        List<SquadController> destination)
    {
        destination.Clear();

        if (deployments == null ||
            faction == null ||
            battleMap == null)
        {
            return;
        }

        int totalSquadCount = 0;

        for (int index = 0; index < deployments.Count; index++)
        {
            BattleSquadDeployment deployment = deployments[index];

            if (deployment != null &&
                deployment.squadData != null &&
                deployment.soldierCount > 0)
            {
                totalSquadCount++;
            }
        }

        Quaternion spawnRotation =
            battleMap.GetDeploymentRotation(isPlayerArmy);

        int spawnedSquadIndex = 0;

        for (int index = 0; index < deployments.Count; index++)
        {
            BattleSquadDeployment deployment = deployments[index];

            if (deployment == null ||
                deployment.squadData == null ||
                deployment.soldierCount <= 0)
            {
                continue;
            }

            Vector3 spawnPosition =
                battleMap.GetDeploymentPosition(
                    isPlayerArmy,
                    spawnedSquadIndex,
                    totalSquadCount);

            SquadController squad =
                SquadFactory.SpawnSquad(
                    deployment.squadData,
                    spawnPosition,
                    spawnRotation,
                    faction,
                    deployment.soldierCount);

            spawnedSquadIndex++;

            if (squad == null)
                continue;

            destination.Add(squad);
            RegisterBattleParticipation(
                squad,
                deployment.externalSquadId,
                squad.Roster != null
                    ? squad.Roster.LivingCount
                    : deployment.soldierCount,
                isPlayerArmy);
        }
    }

    int CountRequestedSquads(IReadOnlyList<BattleSquadEntry> entries)
    {
        if (entries == null)
            return 0;

        int count = 0;

        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].squadData == null)
                continue;

            count += Mathf.Max(1, entries[index].squadCount);
        }

        return count;
    }

    #endregion

    #region Battle Resolution

    void EvaluateBattleEnd()
    {
        bool playerHasLivingSquads = HasLivingSquads(playerSquads);
        bool enemyHasLivingSquads = HasLivingSquads(enemySquads);

        if (playerHasLivingSquads && enemyHasLivingSquads)
            return;

        if (playerHasLivingSquads)
        {
            EndBattle(BattleGameState.Victory);
            return;
        }

        EndBattle(BattleGameState.Defeat);
    }

    bool HasLivingSquads(IReadOnlyList<SquadController> squads)
    {
        if (squads == null)
            return false;

        for (int index = 0; index < squads.Count; index++)
        {
            SquadController squad = squads[index];

            if (squad != null &&
                squad.Roster != null &&
                squad.Roster.HasLivingSoldiers &&
                (squad.Morale == null || !squad.Morale.HasRoutedOffField))
            {
                return true;
            }
        }

        return false;
    }

    void EndBattle(BattleGameState resultState)
    {
        LastBattleResult = BuildBattleResult(resultState);

        StopAllSurvivingSquads();
        SetState(resultState);

        OnBattleResolved?.Invoke(LastBattleResult);
    }

    void StopAllSurvivingSquads()
    {
        StopSquads(playerSquads);
        StopSquads(enemySquads);
    }

    void StopSquads(IReadOnlyList<SquadController> squads)
    {
        if (squads == null)
            return;

        for (int index = 0; index < squads.Count; index++)
        {
            SquadController squad = squads[index];

            if (squad == null ||
                squad.Roster == null ||
                !squad.Roster.HasLivingSoldiers)
            {
                continue;
            }

            squad.OrderStop();
        }
    }

    #endregion

    #region Battle Participation / Results

    void RegisterBattleParticipation(
        SquadController squad,
        string externalSquadId,
        int startingSoldierCount,
        bool isPlayerArmy)
    {
        if (squad == null)
            return;

        BattleParticipation participation =
            new BattleParticipation
            {
                externalSquadId = externalSquadId,
                squadData = squad.Data,
                squad = squad,
                roster = squad.Roster,
                morale = squad.Morale,
                startingSoldierCount = Mathf.Max(0, startingSoldierCount),
                lastKnownLivingSoldierCount = squad.Roster != null
                    ? Mathf.Max(0, squad.Roster.LivingCount)
                    : Mathf.Max(0, startingSoldierCount),
                routedOffField = false,
                isPlayerArmy = isPlayerArmy
            };

        battleParticipations.Add(participation);

        if (participation.roster != null)
        {
            participationByRoster[participation.roster] = participation;
            participation.roster.OnRosterChanged += HandleParticipantRosterChanged;
        }

        if (participation.morale != null)
        {
            participationByMorale[participation.morale] = participation;
            participation.morale.OnRoutedOffField += HandleParticipantRoutedOffField;
        }
    }

    void HandleParticipantRosterChanged(SquadRoster roster)
    {
        if (roster == null ||
            !participationByRoster.TryGetValue(
                roster,
                out BattleParticipation participation))
        {
            return;
        }

        participation.lastKnownLivingSoldierCount =
            Mathf.Max(0, roster.LivingCount);
    }

    void HandleParticipantRoutedOffField(
        SquadMorale morale,
        int survivingSoldierCount)
    {
        if (morale == null ||
            !participationByMorale.TryGetValue(
                morale,
                out BattleParticipation participation))
        {
            return;
        }

        participation.routedOffField = true;
        participation.lastKnownLivingSoldierCount =
            Mathf.Max(0, survivingSoldierCount);
    }

    BattleResult BuildBattleResult(BattleGameState resultState)
    {
        BattleResult result = new BattleResult
        {
            battleDefinition = battleDefinition,
            resultState = resultState,
            battleDuration = battleElapsedTime
        };

        for (int index = 0; index < battleParticipations.Count; index++)
        {
            BattleParticipation participation = battleParticipations[index];

            if (participation == null)
                continue;

            int survivingSoldierCount =
                participation.routedOffField
                    ? participation.lastKnownLivingSoldierCount
                    : participation.roster != null
                        ? Mathf.Max(0, participation.roster.LivingCount)
                        : Mathf.Max(0, participation.lastKnownLivingSoldierCount);

            survivingSoldierCount = Mathf.Clamp(
                survivingSoldierCount,
                0,
                Mathf.Max(0, participation.startingSoldierCount));

            BattleSquadResult squadResult =
                new BattleSquadResult
                {
                    externalSquadId = participation.externalSquadId,
                    squadData = participation.squadData,
                    startingSoldierCount = participation.startingSoldierCount,
                    survivingSoldierCount = survivingSoldierCount,
                    casualtyCount = Mathf.Max(
                        0,
                        participation.startingSoldierCount - survivingSoldierCount),
                    routedOffField = participation.routedOffField
                };

            if (participation.isPlayerArmy)
                result.playerSquads.Add(squadResult);
            else
                result.enemySquads.Add(squadResult);
        }

        return result;
    }

    void ClearBattleParticipationTracking()
    {
        foreach (KeyValuePair<SquadRoster, BattleParticipation> pair in participationByRoster)
        {
            if (pair.Key != null)
                pair.Key.OnRosterChanged -= HandleParticipantRosterChanged;
        }

        foreach (KeyValuePair<SquadMorale, BattleParticipation> pair in participationByMorale)
        {
            if (pair.Key != null)
                pair.Key.OnRoutedOffField -= HandleParticipantRoutedOffField;
        }

        participationByRoster.Clear();
        participationByMorale.Clear();
        battleParticipations.Clear();
    }

    #endregion

    #region Battle Run

    void InitializeBattleRun()
    {
        if (battleRunSequence != null && battleRunSequence.Count > 0)
        {
            battleRunState.Initialize(battleRunSequence);
        }
        else if (battleDefinition != null)
        {
            battleRunState.Initialize(
                new List<BattleDefinitionData> { battleDefinition });
        }
        else
        {
            battleRunState.Initialize(null);
        }

        if (battleRunState.CurrentBattle != null)
            battleDefinition = battleRunState.CurrentBattle;
    }

    #endregion

    #region Cleanup / Validation

    void ClearPreviousArmies()
    {
        ClearBattleParticipationTracking();

        if (!destroyPreviousArmiesOnRestart)
        {
            playerSquads.Clear();
            enemySquads.Clear();
            return;
        }

        DestroySquads(playerSquads);
        DestroySquads(enemySquads);

        playerSquads.Clear();
        enemySquads.Clear();
    }

    void DestroySquads(IReadOnlyList<SquadController> squads)
    {
        if (squads == null)
            return;

        for (int index = 0; index < squads.Count; index++)
        {
            SquadController squad = squads[index];

            if (squad != null)
                squad.DestroySquad(); // Destroy(squad.gameObject) works fine as well
        }
    }

    bool CanStartBattle()
    {
        if (!ValidateSetup())
            return false;

        return state != BattleGameState.Battle;
    }

    bool ValidateSetup()
    {
        bool isValid = true;

        if (battleDefinition == null)
        {
            Debug.LogError(
                $"{name}: BattleDefinitionData is not assigned.",
                this);

            isValid = false;
        }

        if (battleMap == null)
        {
            battleMap = BattleMap.Instance;
        }

        if (battleMap == null)
        {
            Debug.LogError(
                $"{name}: BattleMap is required.",
                this);

            isValid = false;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError(
                $"{name}: GameManager.Instance is required.",
                this);

            isValid = false;
        }

        return isValid;
    }

    void SetState(BattleGameState newState)
    {
        if (state == newState)
            return;

        state = newState;
        OnBattleStateChanged?.Invoke(state);

        Debug.Log(
            $"{name}: Battle state changed to {state}.",
            this);
    }

    #endregion
}
