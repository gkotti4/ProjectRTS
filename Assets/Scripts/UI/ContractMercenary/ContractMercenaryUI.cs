using UnityEngine;

public enum ContractMercenaryUIState
{
    CompanyHub,
    Battle,
    Results
}

/// -----------------------------------------------------------------------------
/// ContractMercenaryUI
/// -----------------------------------------------------------------------------
///
/// Top-level presentation flow for Contract Mercenary.
/// Attach this to the CM_UI root.
///
/// This is the single authority for mutually-exclusive CM presentation states:
/// - CompanyHub
/// - Battle
/// - Results
///
/// It coordinates major UI visibility and tactical input, but it does not own the
/// gameplay rules that cause those transitions. ContractMercenaryController and
/// BattleGameModeController remain the gameplay authorities.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class ContractMercenaryUI : MonoBehaviour
{
    public static ContractMercenaryUI Instance { get; private set; }

    #region References

    [Header("Controllers")]
    [SerializeField] private ContractMercenaryController contractController;
    [SerializeField] private BattleGameModeController battleController;

    [Header("Major UI")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private ContractMercenaryHubUI companyHubUI;
    [SerializeField] private ContractMercenaryResultUI resultUI;

    [Header("Tactical Input")]
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private SelectionManager selectionManager;

    #endregion

    #region Runtime

    private ContractMercenaryUIState state = ContractMercenaryUIState.CompanyHub;
    private bool subscribedToContractController = false;
    private bool subscribedToBattleController = false;

    public ContractMercenaryUIState State => state;

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
        ResolveReferences();
        InitializeChildren();

        if (resultUI != null)
            resultUI.OnContinueToHubRequested += HandleContinueToHubRequested;
    }

    void OnEnable()
    {
        ResolveReferences();
        InitializeChildren();
        Subscribe();
    }

    void Start()
    {
        ResolveReferences();
        InitializeChildren();
        Subscribe();
        RefreshInitialState();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();

        if (resultUI != null)
            resultUI.OnContinueToHubRequested -= HandleContinueToHubRequested;

        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Binding

    void ResolveReferences()
    {
        if (contractController == null)
            contractController = ContractMercenaryController.Instance;

        if (battleController == null)
            battleController = BattleGameModeController.Instance;

        if (uiManager == null)
            uiManager = UIManager.Instance;

        if (companyHubUI == null)
            companyHubUI = GetComponentInChildren<ContractMercenaryHubUI>(true);

        if (resultUI == null)
            resultUI = GetComponentInChildren<ContractMercenaryResultUI>(true);

        if (playerInputHandler == null)
            playerInputHandler = PlayerInputHandler.Instance;

        if (selectionManager == null)
            selectionManager = SelectionManager.Instance;
    }

    void InitializeChildren()
    {
        companyHubUI?.Initialize(contractController);
        resultUI?.Initialize(contractController);
    }

    void Subscribe()
    {
        if (contractController != null && !subscribedToContractController)
        {
            contractController.OnRunStarted += HandleRunStarted;
            contractController.OnContractStarted += HandleContractStarted;
            contractController.OnContractResolved += HandleContractResolved;
            contractController.OnContractResultReady += HandleContractResultReady;
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
            contractController.OnContractStarted -= HandleContractStarted;
            contractController.OnContractResolved -= HandleContractResolved;
            contractController.OnContractResultReady -= HandleContractResultReady;
            subscribedToContractController = false;
        }

        if (battleController != null && subscribedToBattleController)
        {
            battleController.OnBattleStateChanged -= HandleBattleStateChanged;
            subscribedToBattleController = false;
        }
    }

    #endregion

    #region State Resolution

    void RefreshInitialState()
    {
        if (battleController != null && battleController.IsBattleActive)
        {
            SetUIState(ContractMercenaryUIState.Battle);
            return;
        }

        SetUIState(ContractMercenaryUIState.CompanyHub);
    }

    void HandleRunStarted(ContractMercenaryRunState runState)
    {
        SetUIState(ContractMercenaryUIState.CompanyHub);
    }

    void HandleContractStarted(ContractData contract)
    {
        SetUIState(ContractMercenaryUIState.Battle);
    }

    void HandleBattleStateChanged(BattleGameState battleState)
    {
        switch (battleState)
        {
            case BattleGameState.Battle:
                SetUIState(ContractMercenaryUIState.Battle);
                break;

            case BattleGameState.Victory:
            case BattleGameState.Defeat:
                // Hide the tactical HUD immediately. Contract result data is published
                // immediately afterward by ContractMercenaryController.
                SetUIState(ContractMercenaryUIState.Results);
                break;

            case BattleGameState.Setup:
                if (contractController != null &&
                    contractController.RunState != null &&
                    contractController.RunState.HasActiveContract)
                {
                    SetUIState(ContractMercenaryUIState.Battle);
                }
                break;
        }
    }

    void HandleContractResultReady(
        ContractMercenaryContractResult contractResult)
    {
        resultUI?.ShowResult(contractResult);
        SetUIState(ContractMercenaryUIState.Results);
    }

    void HandleContractResolved(ContractData contract, bool playerWon)
    {
        if (playerWon)
            return;

        ContractMercenaryRunState runState =
            contractController != null
                ? contractController.RunState
                : null;

        // A failed attempt keeps the contract active while Results offers Retry or
        // Abandon. Only an actual abandon clears the active contract and returns Hub.
        if (runState == null || !runState.HasActiveContract)
            SetUIState(ContractMercenaryUIState.CompanyHub);
    }

    void HandleContinueToHubRequested()
    {
        SetUIState(ContractMercenaryUIState.CompanyHub);
    }

    #endregion

    #region Public State API

    public void SetUIState(ContractMercenaryUIState newState)
    {
        ResolveReferences();
        state = newState;

        switch (state)
        {
            case ContractMercenaryUIState.CompanyHub:
                ApplyCompanyHubState();
                break;

            case ContractMercenaryUIState.Battle:
                ApplyBattleState();
                break;

            case ContractMercenaryUIState.Results:
                ApplyResultsState();
                break;
        }
    }

    void ApplyCompanyHubState()
    {
        companyHubUI?.SetVisible(true);
        resultUI?.Hide();
        SetBattleHUDVisible(false);
        SetTacticalInputEnabled(false);
        companyHubUI?.RefreshAll();
    }

    void ApplyBattleState()
    {
        companyHubUI?.SetVisible(false);
        resultUI?.Hide();
        SetBattleHUDVisible(true);
        SetTacticalInputEnabled(true);
    }

    void ApplyResultsState()
    {
        companyHubUI?.SetVisible(false);
        SetBattleHUDVisible(false);
        SetTacticalInputEnabled(false);
    }

    #endregion

    #region Major System Boundaries

    void SetBattleHUDVisible(bool visible)
    {
        if (uiManager == null)
            uiManager = UIManager.Instance;

        if (uiManager != null)
        {
            uiManager.SetBattleHUDVisible(visible);
            return;
        }

        BattleHUDUI.Instance?.SetVisible(visible);
    }

    void SetTacticalInputEnabled(bool enabled)
    {
        if (playerInputHandler == null)
            playerInputHandler = PlayerInputHandler.Instance;

        if (selectionManager == null)
            selectionManager = SelectionManager.Instance;

        playerInputHandler?.SetTacticalInputEnabled(enabled);
        selectionManager?.SetTacticalInputEnabled(enabled);
    }

    #endregion
}
