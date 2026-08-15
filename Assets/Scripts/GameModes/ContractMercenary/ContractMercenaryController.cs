using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// -----------------------------------------------------------------------------
/// ContractMercenaryController
/// -----------------------------------------------------------------------------
///
/// Game-mode rules/controller for the Contract Mercenary company phase.
///
/// This component is intentionally NOT persistent. GameSession owns the run state;
/// this controller may be destroyed/recreated as menu/battle scenes change.
///
/// First playable responsibilities:
/// - create a new mercenary run
/// - expose authored contracts
/// - accept/start a contract through the existing BattleGameModeController
/// - deploy the persistent company army at its current manpower
/// - consume BattleResult and commit victory casualties/survivors
/// - award contract resources/prestige on victory exactly once
/// - preserve defeat as a retry/abandon decision
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class ContractMercenaryController : MonoBehaviour
{
    public static ContractMercenaryController Instance { get; private set; }

    public event Action<ContractMercenaryRunState> OnRunStarted;
    public event Action<ContractData> OnContractStarted;
    public event Action<ContractData, bool> OnContractResolved;
    public event Action<ContractMercenaryRunState> OnRunStateChanged;

    #region New Run Setup

    [Header("New Run - Resources")]
    [SerializeField] private List<ContractMercenaryResourceAmount> startingResources =
        new List<ContractMercenaryResourceAmount>();

    [Header("New Run - Army")]
    [SerializeField] private List<ContractMercenaryStartingSquad> startingArmy =
        new List<ContractMercenaryStartingSquad>();

    [Header("New Run - Progression")]
    [Min(0)]
    [SerializeField] private int startingPrestige = 0;

    [Header("Contracts")]
    [SerializeField] private List<ContractData> availableContracts =
        new List<ContractData>();

    [Header("Company Phase - Replenishment")]
    [Tooltip("Base Gold cost for replacing one missing soldier. SquadData.reinforcementCostMultiplier scales this value per squad type.")]
    [Min(0)]
    [SerializeField] private int companyReplenishmentGoldCostPerSoldier = 20;

    [Header("Company Phase - Recruitment")]
    [SerializeField] private List<ContractMercenaryRecruitOption> recruitmentOptions =
        new List<ContractMercenaryRecruitOption>();

    #endregion

    #region Battle Integration

    [FormerlySerializedAs("battleGameModeController")]
    [Header("Battle Integration")]
    [Tooltip("Optional for the current same-scene prototype. If empty, BattleGameModeController.Instance is resolved at runtime.")]
    [SerializeField] private BattleGameModeController battleController;

    #endregion

    #region Runtime

    private bool isSubscribedToBattleController = false;
    private bool currentContractVictoryCommitted = false;

    public ContractMercenaryRunState RunState =>
        GameSession.Instance != null
            ? GameSession.Instance.ContractMercenaryRunState
            : null;

    public IReadOnlyList<ContractData> AvailableContracts => availableContracts;
    public IReadOnlyList<ContractMercenaryRecruitOption> RecruitmentOptions => recruitmentOptions;
    public bool HasRun => RunState != null;

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
        ResolveBattleController();
    }

    void OnEnable()
    {
        ResolveBattleController();
        SubscribeToBattleController();
    }

    void Start()
    {
        ResolveBattleController();
        SubscribeToBattleController();
    }

    void OnDisable()
    {
        UnsubscribeFromBattleController();
    }

    void OnDestroy()
    {
        UnsubscribeFromBattleController();

        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Run Lifecycle

    public bool StartNewRun()
    {
        if (GameSession.Instance == null)
        {
            Debug.LogError(
                $"{name}: Contract Mercenary requires GameSession.Instance.",
                this);
            return false;
        }

        ContractMercenaryRunState runState =
            new ContractMercenaryRunState();

        runState.Initialize(
            startingResources,
            startingArmy,
            startingPrestige);

        GameSession.Instance.SetContractMercenaryRunState(runState);

        currentContractVictoryCommitted = false;

        OnRunStarted?.Invoke(runState);
        OnRunStateChanged?.Invoke(runState);

        Debug.Log(
            $"{name}: Started Contract Mercenary run. " +
            $"Army={runState.Army.Count}, " +
            $"Gold={runState.GetResource(ContractMercenaryResourceType.Gold)}, " +
            $"Iron={runState.GetResource(ContractMercenaryResourceType.Iron)}, " +
            $"Prestige={runState.Prestige}.",
            this);

        return true;
    }

    public void EndCurrentRun()
    {
        if (GameSession.Instance == null)
            return;

        GameSession.Instance.ClearContractMercenaryRunState();
        currentContractVictoryCommitted = false;
    }

    #endregion

    #region Company Phase Economy

    public int GetReplenishmentGoldCost(
        ContractMercenarySquadState squadState)
    {
        if (squadState == null || squadState.squadData == null)
            return 0;

        int missingSoldiers = RunState != null
            ? RunState.GetMissingSoldierCount(squadState)
            : 0;

        if (missingSoldiers <= 0)
            return 0;

        float squadCostMultiplier = Mathf.Max(
            0f,
            squadState.squadData.reinforcementCostMultiplier);

        return Mathf.CeilToInt(
            missingSoldiers *
            Mathf.Max(0, companyReplenishmentGoldCostPerSoldier) *
            squadCostMultiplier);
    }

    public bool CanReplenishSquad(
        ContractMercenarySquadState squadState)
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null ||
            runState.HasActiveContract ||
            squadState == null ||
            squadState.squadData == null)
        {
            return false;
        }

        int missingSoldiers = runState.GetMissingSoldierCount(squadState);

        if (missingSoldiers <= 0)
            return false;

        return runState.CanAfford(
            ContractMercenaryResourceType.Gold,
            GetReplenishmentGoldCost(squadState));
    }

    public bool ReplenishSquadToFull(
        ContractMercenarySquadState squadState)
    {
        if (!CanReplenishSquad(squadState))
            return false;

        ContractMercenaryRunState runState = RunState;
        int goldCost = GetReplenishmentGoldCost(squadState);

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Gold,
                goldCost))
        {
            return false;
        }

        int replenishedCount = runState.ReplenishSquadToFull(squadState);

        if (replenishedCount <= 0)
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                goldCost);
            return false;
        }

        OnRunStateChanged?.Invoke(runState);
        return true;
    }

    public bool CanRecruit(ContractMercenaryRecruitOption recruitOption)
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null ||
            runState.HasActiveContract ||
            recruitOption == null ||
            recruitOption.squadData == null)
        {
            return false;
        }

        return runState.CanAfford(
                   ContractMercenaryResourceType.Gold,
                   recruitOption.goldCost) &&
               runState.CanAfford(
                   ContractMercenaryResourceType.Iron,
                   recruitOption.ironCost);
    }

    public bool RecruitSquad(ContractMercenaryRecruitOption recruitOption)
    {
        if (!CanRecruit(recruitOption))
            return false;

        ContractMercenaryRunState runState = RunState;

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Gold,
                recruitOption.goldCost))
        {
            return false;
        }

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Iron,
                recruitOption.ironCost))
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                recruitOption.goldCost);
            return false;
        }

        ContractMercenarySquadState recruitedSquad =
            runState.AddSquad(
                recruitOption.squadData,
                recruitOption.squadData.ResolvedStartingSoldierCount);

        if (recruitedSquad == null)
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                recruitOption.goldCost);
            runState.AddResource(
                ContractMercenaryResourceType.Iron,
                recruitOption.ironCost);
            return false;
        }

        OnRunStateChanged?.Invoke(runState);
        return true;
    }

    #endregion

    #region Contract Selection / Battle

    public bool CanStartContract(ContractData contract)
    {
        if (contract == null || contract.battleDefinition == null)
            return false;

        ContractMercenaryRunState runState = RunState;

        if (runState == null || runState.HasActiveContract)
            return false;

        if (!HasDeployableArmy(runState))
            return false;

        if (!contract.repeatable && runState.IsContractCompleted(contract))
            return false;

        ResolveBattleController();

        return battleController != null &&
               !battleController.IsBattleActive;
    }

    public bool StartContract(ContractData contract)
    {
        if (!CanStartContract(contract))
            return false;

        ContractMercenaryRunState runState = RunState;

        if (runState == null || !runState.BeginContract(contract))
            return false;

        currentContractVictoryCommitted = false;

        battleController.SetBattleDefinition(
            contract.battleDefinition);

        battleController.SetPlayerArmyDeployments(
            BuildPlayerArmyDeployments(runState));

        battleController.StartBattle();

        OnContractStarted?.Invoke(contract);
        OnRunStateChanged?.Invoke(runState);

        Debug.Log(
            $"{name}: Started contract '{contract.contractName}'.",
            this);

        return true;
    }

    public bool RetryCurrentContract()
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null || runState.CurrentContract == null)
            return false;

        ResolveBattleController();

        if (battleController == null ||
            battleController.IsBattleActive)
        {
            return false;
        }

        currentContractVictoryCommitted = false;

        battleController.SetBattleDefinition(
            runState.CurrentContract.battleDefinition);

        battleController.SetPlayerArmyDeployments(
            BuildPlayerArmyDeployments(runState));

        battleController.StartBattle();
        return true;
    }

    public bool AbandonCurrentContract()
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null || !runState.HasActiveContract)
            return false;

        if (battleController != null &&
            battleController.IsBattleActive)
        {
            return false;
        }

        ContractData abandonedContract = runState.CurrentContract;
        runState.AbandonCurrentContract();
        currentContractVictoryCommitted = false;
        battleController?.ClearPlayerArmyDeployments();

        OnContractResolved?.Invoke(abandonedContract, false);
        OnRunStateChanged?.Invoke(runState);
        return true;
    }

    #endregion

    #region Army Deployment

    bool HasDeployableArmy(ContractMercenaryRunState runState)
    {
        if (runState == null)
            return false;

        for (int index = 0; index < runState.Army.Count; index++)
        {
            ContractMercenarySquadState squadState = runState.Army[index];

            if (squadState != null &&
                squadState.squadData != null &&
                squadState.currentSoldierCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    List<BattleSquadDeployment> BuildPlayerArmyDeployments(
        ContractMercenaryRunState runState)
    {
        List<BattleSquadDeployment> deployments =
            new List<BattleSquadDeployment>();

        if (runState == null)
            return deployments;

        for (int index = 0; index < runState.Army.Count; index++)
        {
            ContractMercenarySquadState squadState = runState.Army[index];

            if (squadState == null ||
                squadState.squadData == null ||
                squadState.currentSoldierCount <= 0)
            {
                continue;
            }

            deployments.Add(
                new BattleSquadDeployment
                {
                    externalSquadId = squadState.companySquadId,
                    squadData = squadState.squadData,
                    soldierCount = squadState.currentSoldierCount
                });
        }

        return deployments;
    }

    #endregion

    #region Battle Binding

    void ResolveBattleController()
    {
        if (battleController == null)
            battleController = BattleGameModeController.Instance;
    }

    void SubscribeToBattleController()
    {
        if (battleController == null ||
            isSubscribedToBattleController)
        {
            return;
        }

        battleController.OnBattleResolved +=
            HandleBattleResolved;

        isSubscribedToBattleController = true;
    }

    void UnsubscribeFromBattleController()
    {
        if (battleController == null ||
            !isSubscribedToBattleController)
        {
            return;
        }

        battleController.OnBattleResolved -=
            HandleBattleResolved;

        isSubscribedToBattleController = false;
    }

    void HandleBattleResolved(BattleResult battleResult)
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null ||
            runState.CurrentContract == null ||
            battleResult == null)
        {
            return;
        }

        if (battleResult.PlayerWon)
        {
            CommitContractVictory(runState, battleResult);
            return;
        }

        // MVP defeat policy: do not commit casualties. Retrying starts again from
        // the pre-battle company manpower.
        currentContractVictoryCommitted = false;
        OnContractResolved?.Invoke(runState.CurrentContract, false);
        OnRunStateChanged?.Invoke(runState);
    }

    void CommitContractVictory(
        ContractMercenaryRunState runState,
        BattleResult battleResult)
    {
        if (runState == null ||
            runState.CurrentContract == null ||
            currentContractVictoryCommitted)
        {
            return;
        }

        ContractData completedContract = runState.CurrentContract;

        runState.ApplyBattleResult(battleResult);

        if (!runState.CompleteCurrentContractVictory())
            return;

        currentContractVictoryCommitted = true;
        battleController?.ClearPlayerArmyDeployments();

        OnContractResolved?.Invoke(completedContract, true);
        OnRunStateChanged?.Invoke(runState);

        Debug.Log(
            $"{name}: Completed contract '{completedContract.contractName}'. " +
            $"Gold={runState.GetResource(ContractMercenaryResourceType.Gold)}, " +
            $"Iron={runState.GetResource(ContractMercenaryResourceType.Iron)}, " +
            $"Prestige={runState.Prestige}.",
            this);
    }

    #endregion
}
