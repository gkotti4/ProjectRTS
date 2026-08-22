using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ContractMercenaryHubPage
{
    Contracts,
    Army,
    Recruitment,
    Upgrades,
    Company
}

/// -----------------------------------------------------------------------------
/// ContractMercenaryHubUI
/// -----------------------------------------------------------------------------
///
/// Company Hub shell for Contract Mercenary.
///
/// Owns only hub-wide presentation/state:
/// - header resources/status
/// - bottom navigation
/// - active content page
/// - hub-local visibility when instructed by ContractMercenaryUI
///
/// Page-specific content is owned by the dedicated page components.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class ContractMercenaryHubUI : MonoBehaviour
{
    #region References

    [Header("Controller")]
    [SerializeField] private ContractMercenaryController contractController;

    [Header("Hub Root")]
    [SerializeField] private CanvasGroup hubCanvasGroup;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI companyNameText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI ironText;
    [SerializeField] private TextMeshProUGUI prestigeText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Bottom Navigation")]
    [SerializeField] private Button contractsPageButton;
    [SerializeField] private Button armyPageButton;
    [SerializeField] private Button recruitmentPageButton;
    [SerializeField] private Button upgradesPageButton;
    [SerializeField] private Button companyPageButton;

    [Header("Content Pages")]
    [SerializeField] private ContractMercenaryContractsPageUI contractsPage;
    [SerializeField] private ContractMercenaryArmyPageUI armyPage;
    [SerializeField] private ContractMercenaryRecruitmentPageUI recruitmentPage;
    [SerializeField] private ContractMercenaryUpgradesPageUI upgradesPage;
    [SerializeField] private ContractMercenaryCompanyPageUI companyPage;

    #endregion

    #region Tuning

    [Header("Hub Defaults")]
    [SerializeField] private string defaultCompanyName = "Mercenary Company";
    [SerializeField] private ContractMercenaryHubPage defaultPage =
        ContractMercenaryHubPage.Contracts;

    [Header("Testing")]
    [Tooltip("Starts a new Contract Mercenary run automatically when entering Play Mode if none exists yet.")]
    [SerializeField] private bool autoStartRunForTesting = true;

    #endregion

    #region Runtime

    private ContractMercenaryHubPage activePage;
    private bool hasActivePage = false;
    private bool subscribedToContractController = false;
    private string currentStatusMessage = string.Empty;

    public ContractMercenaryHubPage ActivePage => activePage;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (hubCanvasGroup == null)
            hubCanvasGroup = GetComponent<CanvasGroup>();

        if (hubCanvasGroup == null)
            hubCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        contractsPageButton?.onClick.AddListener(ShowContractsPage);
        armyPageButton?.onClick.AddListener(ShowArmyPage);
        recruitmentPageButton?.onClick.AddListener(ShowRecruitmentPage);
        upgradesPageButton?.onClick.AddListener(ShowUpgradesPage);
        companyPageButton?.onClick.AddListener(ShowCompanyPage);
    }

    void OnEnable()
    {
        ResolveController();
        Subscribe();
    }

    void Start()
    {
        ResolveController();
        Subscribe();

        if (autoStartRunForTesting &&
            contractController != null &&
            !contractController.HasRun)
        {
            contractController.StartNewRun();
        }

        ShowPage(defaultPage);
        RefreshAll();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();

        contractsPageButton?.onClick.RemoveListener(ShowContractsPage);
        armyPageButton?.onClick.RemoveListener(ShowArmyPage);
        recruitmentPageButton?.onClick.RemoveListener(ShowRecruitmentPage);
        upgradesPageButton?.onClick.RemoveListener(ShowUpgradesPage);
        companyPageButton?.onClick.RemoveListener(ShowCompanyPage);
    }

    #endregion

    #region Binding

    public void Initialize(ContractMercenaryController controller)
    {
        if (controller != null)
            contractController = controller;

        BindContentPages();
    }

    void ResolveController()
    {
        if (contractController == null)
            contractController = ContractMercenaryController.Instance;

        BindContentPages();
    }

    /// <summary>
    /// CompanyHub is the trusted binding root for all of its content pages.
    /// Child pages do not resolve ContractMercenaryController independently.
    /// </summary>
    void BindContentPages()
    {
        contractsPage?.Initialize(contractController);
        armyPage?.Initialize(contractController);
        recruitmentPage?.Initialize(contractController);
        upgradesPage?.Initialize(contractController);
        companyPage?.Initialize(contractController);
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
    }

    #endregion

    #region Controller Events

    void HandleRunStarted(ContractMercenaryRunState runState)
    {
        currentStatusMessage = "Choose a contract.";
        ShowPage(ContractMercenaryHubPage.Contracts);
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

        if (playerWon)
        {
            currentStatusMessage =
                $"Victory - {contractName}. Rewards received.";
        }
        else
        {
            ContractMercenaryRunState runState =
                contractController != null
                    ? contractController.RunState
                    : null;

            currentStatusMessage =
                runState != null && runState.HasActiveContract
                    ? $"Defeat - {contractName}. Resolve the contract result."
                    : $"Contract abandoned - {contractName}. Choose another contract.";
        }

        ShowPage(ContractMercenaryHubPage.Contracts);
        RefreshAll();
    }

    #endregion

    #region Navigation

    public void ShowContractsPage() => ShowPage(ContractMercenaryHubPage.Contracts);
    public void ShowArmyPage() => ShowPage(ContractMercenaryHubPage.Army);
    public void ShowRecruitmentPage() => ShowPage(ContractMercenaryHubPage.Recruitment);
    public void ShowUpgradesPage() => ShowPage(ContractMercenaryHubPage.Upgrades);
    public void ShowCompanyPage() => ShowPage(ContractMercenaryHubPage.Company);

    public void ShowPage(ContractMercenaryHubPage page)
    {
        activePage = page;
        hasActivePage = true;

        SetPageActive(contractsPage, page == ContractMercenaryHubPage.Contracts);
        SetPageActive(armyPage, page == ContractMercenaryHubPage.Army);
        SetPageActive(recruitmentPage, page == ContractMercenaryHubPage.Recruitment);
        SetPageActive(upgradesPage, page == ContractMercenaryHubPage.Upgrades);
        SetPageActive(companyPage, page == ContractMercenaryHubPage.Company);

        RefreshActivePage();
    }

    void RefreshActivePage()
    {
        if (!hasActivePage)
            return;

        switch (activePage)
        {
            case ContractMercenaryHubPage.Contracts:
                contractsPage?.RefreshPage();
                break;

            case ContractMercenaryHubPage.Army:
                armyPage?.RefreshPage();
                break;

            case ContractMercenaryHubPage.Recruitment:
                recruitmentPage?.RefreshPage();
                break;

            case ContractMercenaryHubPage.Upgrades:
                upgradesPage?.RefreshPage();
                break;

            case ContractMercenaryHubPage.Company:
                companyPage?.RefreshPage();
                break;
        }
    }

    static void SetPageActive(MonoBehaviour page, bool active)
    {
        if (page != null)
            page.gameObject.SetActive(active);
    }

    #endregion

    #region Refresh

    public void RefreshAll()
    {
        ResolveController();
        Subscribe();

        RefreshHeader();

        if (!hasActivePage)
            ShowPage(defaultPage);
        else
            RefreshActivePage();
    }

    void RefreshHeader()
    {
        ContractMercenaryRunState runState =
            contractController != null
                ? contractController.RunState
                : null;

        SetText(companyNameText, defaultCompanyName);

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

        SetText(prestigeText, $"Prestige: {runState.Prestige}");

        if (string.IsNullOrWhiteSpace(currentStatusMessage))
        {
            currentStatusMessage = runState.HasActiveContract
                ? $"Active Contract: {runState.CurrentContract.contractName}"
                : "Choose a contract.";
        }

        SetText(statusText, currentStatusMessage);
    }

    #endregion

    #region Presentation Helpers

    public void SetVisible(bool visible)
    {
        if (hubCanvasGroup == null)
            return;

        hubCanvasGroup.alpha = visible ? 1f : 0f;
        hubCanvasGroup.interactable = visible;
        hubCanvasGroup.blocksRaycasts = visible;
    }

    static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    #endregion
}
