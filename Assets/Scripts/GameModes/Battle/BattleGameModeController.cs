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

    #region Setup

    [Header("Battle")]
    [SerializeField] private BattleDefinitionData battleDefinition;

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

    private BattleGameState state = BattleGameState.Setup;
    private float automaticStartTimer = 0f;
    private float battleEndCheckTimer = 0f;
    private float battleElapsedTime = 0f;

    public BattleDefinitionData BattleDefinition => battleDefinition;
    public BattleGameState State => state;
    public IReadOnlyList<SquadController> PlayerSquads => playerSquads;
    public IReadOnlyList<SquadController> EnemySquads => enemySquads;
    public bool IsBattleActive => state == BattleGameState.Battle;
    public float BattleElapsedTime => battleElapsedTime;

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

        SpawnArmy(
            battleDefinition.playerArmy,
            GameManager.Instance.PlayerFaction,
            isPlayerArmy: true,
            playerSquads);

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
                    destination.Add(squad);
            }
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
                squad.Roster.HasLivingSoldiers)
            {
                return true;
            }
        }

        return false;
    }

    void EndBattle(BattleGameState resultState)
    {
        StopAllSurvivingSquads();
        SetState(resultState);
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

    #region Cleanup / Validation

    void ClearPreviousArmies()
    {
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
