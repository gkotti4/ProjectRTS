using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// -----------------------------------------------------------------------------
/// ContractMercenaryMenuUI
/// -----------------------------------------------------------------------------
///
/// Minimal company-phase UI for the first playable Contract Mercenary loop.
/// It intentionally favors testability over final presentation:
/// - starts a new run
/// - displays Gold / Iron / Prestige
/// - lists contracts and launches battles
/// - displays owned squads and replenishes them to full
/// - lists authored recruitment options
/// - exposes Retry / Abandon after a defeat
/// - hides while the battle is active and returns when battle resolution completes
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class ContractMercenaryMenuUI : MonoBehaviour
{
    #region References

    [Header("Controllers")]
    [SerializeField] private ContractMercenaryController contractController;
    [SerializeField] private BattleGameModeController battleController;

    [Header("Panel")]
    [SerializeField] private CanvasGroup menuCanvasGroup;

    [Header("Summary Text")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI ironText;
    [SerializeField] private TextMeshProUGUI prestigeText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Run Controls")]
    [SerializeField] private Button newRunButton;
    [SerializeField] private Button retryContractButton;
    [SerializeField] private Button abandonContractButton;

    [Header("Dynamic Lists")]
    [SerializeField] private Transform contractContainer;
    [SerializeField] private Transform armyContainer;
    [SerializeField] private Transform recruitmentContainer;
    [SerializeField] private ContractMercenaryMenuButtonUI menuButtonPrefab;

    #endregion

    #region Tuning

    [Header("Testing")]
    [Tooltip("Starts a new Contract Mercenary run automatically when entering Play Mode if none exists yet.")]
    [SerializeField] private bool autoStartRunForTesting = true;

    #endregion

    #region Runtime

    private readonly List<ContractMercenaryMenuButtonUI> spawnedButtons =
        new List<ContractMercenaryMenuButtonUI>();

    private bool subscribedToContractController = false;
    private bool subscribedToBattleController = false;
    private string currentStatusMessage = string.Empty;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (menuCanvasGroup == null)
            menuCanvasGroup = GetComponent<CanvasGroup>();

        if (menuCanvasGroup == null)
            menuCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (newRunButton != null)
            newRunButton.onClick.AddListener(HandleNewRunClicked);

        if (retryContractButton != null)
            retryContractButton.onClick.AddListener(HandleRetryClicked);

        if (abandonContractButton != null)
            abandonContractButton.onClick.AddListener(HandleAbandonClicked);
    }

    void OnEnable()
    {
        ResolveControllers();
        Subscribe();
    }

    void Start()
    {
        ResolveControllers();
        Subscribe();

        if (autoStartRunForTesting &&
            contractController != null &&
            !contractController.HasRun)
        {
            contractController.StartNewRun();
        }

        RefreshAll();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();

        if (newRunButton != null)
            newRunButton.onClick.RemoveListener(HandleNewRunClicked);

        if (retryContractButton != null)
            retryContractButton.onClick.RemoveListener(HandleRetryClicked);

        if (abandonContractButton != null)
            abandonContractButton.onClick.RemoveListener(HandleAbandonClicked);
    }

    #endregion

    #region Binding

    void ResolveControllers()
    {
        if (contractController == null)
            contractController = ContractMercenaryController.Instance;

        if (battleController == null)
            battleController = BattleGameModeController.Instance;
    }

    void Subscribe()
    {
        if (contractController != null && !subscribedToContractController)
        {
            contractController.OnRunStarted += HandleRunStarted;
            contractController.OnRunStateChanged += HandleRunStateChanged;
            contractController.OnContractStarted += HandleContractStarted;
            contractController.OnContractResolved += HandleContractResolved;
            subscribedToContractController = true;
        }

        if (battleController != null && !subscribedToBattleController)
        {
            battleController.OnBattleStateChanged += HandleBattleStateChanged;
            subscribedToBattleController = true;
        }
    }

    void Unsubscribe()
    {
        if (contractController != null && subscribedToContractController)
        {
            contractController.OnRunStarted -= HandleRunStarted;
            contractController.OnRunStateChanged -= HandleRunStateChanged;
            contractController.OnContractStarted -= HandleContractStarted;
            contractController.OnContractResolved -= HandleContractResolved;
            subscribedToContractController = false;
        }

        if (battleController != null && subscribedToBattleController)
        {
            battleController.OnBattleStateChanged -= HandleBattleStateChanged;
            subscribedToBattleController = false;
        }
    }

    #endregion

    #region Controller Events

    void HandleRunStarted(ContractMercenaryRunState runState)
    {
        currentStatusMessage = "Choose a contract.";
        RefreshAll();
    }

    void HandleRunStateChanged(ContractMercenaryRunState runState)
    {
        RefreshAll();
    }

    void HandleContractStarted(ContractData contract)
    {
        currentStatusMessage = contract != null
            ? $"Contract started: {contract.contractName}"
            : "Contract started.";

        RefreshAll();
    }

    void HandleContractResolved(ContractData contract, bool playerWon)
    {
        string contractName = contract != null
            ? contract.contractName
            : "Contract";

        currentStatusMessage = playerWon
            ? $"Victory - {contractName}. Rewards received."
            : $"Defeat - {contractName}. Retry or abandon the contract.";

        RefreshAll();
    }

    void HandleBattleStateChanged(BattleGameState state)
    {
        RefreshAll();
    }

    #endregion

    #region Run Controls

    void HandleNewRunClicked()
    {
        if (contractController == null)
            return;

        if (contractController.StartNewRun())
            currentStatusMessage = "New company created. Choose a contract.";

        RefreshAll();
    }

    void HandleRetryClicked()
    {
        if (contractController == null)
            return;

        if (contractController.RetryCurrentContract())
            currentStatusMessage = "Retrying contract.";

        RefreshAll();
    }

    void HandleAbandonClicked()
    {
        if (contractController == null)
            return;

        if (contractController.AbandonCurrentContract())
            currentStatusMessage = "Contract abandoned. Choose another contract.";

        RefreshAll();
    }

    #endregion

    #region Refresh

    public void RefreshAll()
    {
        ResolveControllers();
        Subscribe();

        bool battleActive =
            battleController != null &&
            battleController.IsBattleActive;

        SetMenuVisible(!battleActive);

        if (battleActive)
            return;

        RefreshSummary();
        RefreshRunControls();
        RebuildDynamicLists();
    }

    void RefreshSummary()
    {
        ContractMercenaryRunState runState =
            contractController != null
                ? contractController.RunState
                : null;

        if (runState == null)
        {
            SetText(goldText, "Gold: -");
            SetText(ironText, "Iron: -");
            SetText(prestigeText, "Prestige: -");
            SetText(statusText, "Start a new Contract Mercenary run.");
            return;
        }

        SetText(
            goldText,
            $"Gold: {runState.GetResource(ContractMercenaryResourceType.Gold)}");

        SetText(
            ironText,
            $"Iron: {runState.GetResource(ContractMercenaryResourceType.Iron)}");

        SetText(
            prestigeText,
            $"Prestige: {runState.Prestige}");

        if (string.IsNullOrWhiteSpace(currentStatusMessage))
        {
            currentStatusMessage = runState.HasActiveContract
                ? $"Active Contract: {runState.CurrentContract.contractName}"
                : "Choose a contract.";
        }

        SetText(statusText, currentStatusMessage);
    }

    void RefreshRunControls()
    {
        ContractMercenaryRunState runState =
            contractController != null
                ? contractController.RunState
                : null;

        if (newRunButton != null)
            newRunButton.gameObject.SetActive(runState == null);

        bool showDefeatControls =
            runState != null &&
            runState.HasActiveContract &&
            battleController != null &&
            battleController.State == BattleGameState.Defeat;

        if (retryContractButton != null)
            retryContractButton.gameObject.SetActive(showDefeatControls);

        if (abandonContractButton != null)
            abandonContractButton.gameObject.SetActive(showDefeatControls);
    }

    void RebuildDynamicLists()
    {
        ClearDynamicButtons();

        if (contractController == null || contractController.RunState == null)
            return;

        BuildContractButtons();
        BuildArmyButtons();
        BuildRecruitmentButtons();
    }

    #endregion

    #region Contract List

    void BuildContractButtons()
    {
        if (contractContainer == null || menuButtonPrefab == null)
            return;

        IReadOnlyList<ContractData> contracts =
            contractController.AvailableContracts;

        for (int index = 0; index < contracts.Count; index++)
        {
            ContractData contract = contracts[index];

            if (contract == null)
                continue;

            ContractData capturedContract = contract;
            bool canStart = contractController.CanStartContract(contract);

            SpawnButton(
                contractContainer,
                BuildContractLabel(contract),
                () =>
                {
                    if (contractController.StartContract(capturedContract))
                    {
                        currentStatusMessage =
                            $"Contract started: {capturedContract.contractName}";
                    }
                },
                canStart);
        }
    }

    string BuildContractLabel(ContractData contract)
    {
        StringBuilder builder = new StringBuilder();

        builder.Append(contract.contractName);
        builder.Append("  |  Threat ");
        builder.Append(Mathf.Clamp(contract.threatRating, 1, 5));
        builder.Append("/5");

        if (contract.prestigeReward > 0)
        {
            builder.Append("  |  +");
            builder.Append(contract.prestigeReward);
            builder.Append(" Prestige");
        }

        if (contract.rewards != null)
        {
            for (int index = 0; index < contract.rewards.Count; index++)
            {
                ContractMercenaryResourceAmount reward = contract.rewards[index];

                if (reward == null || reward.amount <= 0)
                    continue;

                builder.Append("  |  +");
                builder.Append(reward.amount);
                builder.Append(' ');
                builder.Append(reward.resourceType);
            }
        }

        return builder.ToString();
    }

    #endregion

    #region Army / Replenishment

    void BuildArmyButtons()
    {
        if (armyContainer == null || menuButtonPrefab == null)
            return;

        ContractMercenaryRunState runState = contractController.RunState;

        for (int index = 0; index < runState.Army.Count; index++)
        {
            ContractMercenarySquadState squadState = runState.Army[index];

            if (squadState == null || squadState.squadData == null)
                continue;

            ContractMercenarySquadState capturedSquad = squadState;
            int missingSoldiers = runState.GetMissingSoldierCount(squadState);
            int replenishCost = contractController.GetReplenishmentGoldCost(squadState);

            string label = missingSoldiers > 0
                ? $"{squadState.squadData.squadName}  {squadState.currentSoldierCount}/{squadState.MaximumSoldierCount}  |  Replenish +{missingSoldiers}: {replenishCost} Gold"
                : $"{squadState.squadData.squadName}  {squadState.currentSoldierCount}/{squadState.MaximumSoldierCount}  |  Full Strength";

            SpawnButton(
                armyContainer,
                label,
                () =>
                {
                    if (contractController.ReplenishSquadToFull(capturedSquad))
                        currentStatusMessage = $"Replenished {capturedSquad.squadData.squadName}.";
                },
                contractController.CanReplenishSquad(squadState));
        }
    }

    #endregion

    #region Recruitment

    void BuildRecruitmentButtons()
    {
        if (recruitmentContainer == null || menuButtonPrefab == null)
            return;

        IReadOnlyList<ContractMercenaryRecruitOption> recruitOptions =
            contractController.RecruitmentOptions;

        for (int index = 0; index < recruitOptions.Count; index++)
        {
            ContractMercenaryRecruitOption recruitOption = recruitOptions[index];

            if (recruitOption == null || recruitOption.squadData == null)
                continue;

            ContractMercenaryRecruitOption capturedOption = recruitOption;

            string label =
                $"Recruit {recruitOption.squadData.squadName}  |  {recruitOption.goldCost} Gold";

            if (recruitOption.ironCost > 0)
                label += $" + {recruitOption.ironCost} Iron";

            SpawnButton(
                recruitmentContainer,
                label,
                () =>
                {
                    if (contractController.RecruitSquad(capturedOption))
                        currentStatusMessage = $"Recruited {capturedOption.squadData.squadName}.";
                },
                contractController.CanRecruit(recruitOption));
        }
    }

    #endregion

    #region Dynamic Button Helpers

    void SpawnButton(
        Transform container,
        string label,
        System.Action onClick,
        bool interactable)
    {
        ContractMercenaryMenuButtonUI button = Instantiate(
            menuButtonPrefab,
            container);

        button.Initialize(label, onClick, interactable);
        spawnedButtons.Add(button);
    }

    void ClearDynamicButtons()
    {
        for (int index = 0; index < spawnedButtons.Count; index++)
        {
            ContractMercenaryMenuButtonUI button = spawnedButtons[index];

            if (button != null)
                Destroy(button.gameObject);
        }

        spawnedButtons.Clear();
    }

    #endregion

    #region Presentation Helpers

    void SetMenuVisible(bool visible)
    {
        if (menuCanvasGroup == null)
            return;

        menuCanvasGroup.alpha = visible ? 1f : 0f;
        menuCanvasGroup.interactable = visible;
        menuCanvasGroup.blocksRaycasts = visible;
    }

    void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value;
    }

    #endregion
}
