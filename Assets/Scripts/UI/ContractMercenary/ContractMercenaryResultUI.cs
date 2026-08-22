using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// -----------------------------------------------------------------------------
/// ContractMercenaryResultUI
/// -----------------------------------------------------------------------------
///
/// Contract Mercenary-specific result/resolution screen. It does not read live squad
/// GameObjects. The controller publishes a stored strategic result after the battle
/// engine has resolved, which means successful routers and already-destroyed squads
/// are represented correctly.
///
/// Result resolution ownership:
/// - Victory exposes Continue back to the Company Hub.
/// - Defeat exposes Retry Contract / Abandon Contract.
/// - Company lifecycle actions such as New Company do not belong here.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class ContractMercenaryResultUI : MonoBehaviour
{
    #region References

    [Header("Controller")]
    [SerializeField] private ContractMercenaryController contractController;

    [Header("Panel")]
    [SerializeField] private CanvasGroup resultCanvasGroup;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private TextMeshProUGUI contractNameText;
    [SerializeField] private TextMeshProUGUI battleSummaryText;
    [SerializeField] private TextMeshProUGUI rewardsText;
    [SerializeField] private TextMeshProUGUI squadResultsText;
    [SerializeField] private TextMeshProUGUI policyText;

    [Header("Resolution Controls")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button retryContractButton;
    [SerializeField] private Button abandonContractButton;

    #endregion

    #region Runtime

    private ContractMercenaryContractResult currentResult;

    public event Action OnContinueToHubRequested;
    public bool HasResult => currentResult != null;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (resultCanvasGroup == null)
            resultCanvasGroup = GetComponent<CanvasGroup>();

        if (resultCanvasGroup == null)
            resultCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        continueButton?.onClick.AddListener(HandleContinueClicked);
        retryContractButton?.onClick.AddListener(HandleRetryClicked);
        abandonContractButton?.onClick.AddListener(HandleAbandonClicked);

        Hide();
    }


    void OnDestroy()
    {
        continueButton?.onClick.RemoveListener(HandleContinueClicked);
        retryContractButton?.onClick.RemoveListener(HandleRetryClicked);
        abandonContractButton?.onClick.RemoveListener(HandleAbandonClicked);
    }

    #endregion

    #region Binding

    public void Initialize(ContractMercenaryController controller)
    {
        if (controller != null)
            contractController = controller;
    }

    void ResolveController()
    {
        if (contractController == null)
            contractController = ContractMercenaryController.Instance;
    }

    #endregion

    #region Presentation


    public void ShowResult(
        ContractMercenaryContractResult contractResult)
    {
        if (contractResult == null)
            return;

        currentResult = contractResult;

        SetText(
            resultTitleText,
            contractResult.playerWon ? "VICTORY" : "DEFEAT");

        SetText(
            contractNameText,
            contractResult.contract != null
                ? contractResult.contract.contractName
                : "Contract");

        SetText(
            battleSummaryText,
            BuildBattleSummary(contractResult));

        SetText(
            rewardsText,
            BuildRewardsSummary(contractResult));

        SetText(
            squadResultsText,
            BuildSquadSummary(contractResult));

        SetText(
            policyText,
            BuildPolicyText(contractResult));

        RefreshResolutionControls(contractResult);


        SetVisible(true);
    }

    public void Hide()
    {
        currentResult = null;
        SetVisible(false);
    }

    #endregion

    #region Resolution Controls

    void HandleContinueClicked()
    {
        if (currentResult == null || !currentResult.playerWon)
            return;

        Hide();
        OnContinueToHubRequested?.Invoke();
    }

    void HandleRetryClicked()
    {
        if (currentResult == null || currentResult.playerWon)
            return;

        ResolveController();

        if (contractController != null &&
            contractController.RetryCurrentContract())
        {
            Hide();
        }
    }

    void HandleAbandonClicked()
    {
        if (currentResult == null || currentResult.playerWon)
            return;

        ResolveController();

        if (contractController != null &&
            contractController.AbandonCurrentContract())
        {
            Hide();
        }
    }

    void RefreshResolutionControls(
        ContractMercenaryContractResult contractResult)
    {
        bool playerWon =
            contractResult != null &&
            contractResult.playerWon;

        SetButtonVisible(continueButton, playerWon);
        SetButtonVisible(retryContractButton, !playerWon);
        SetButtonVisible(abandonContractButton, !playerWon);
    }

    static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
            button.gameObject.SetActive(visible);
    }


    #endregion

    #region Result Summaries

    string BuildBattleSummary(
        ContractMercenaryContractResult contractResult)
    {
        int minutes = Mathf.FloorToInt(contractResult.battleDuration / 60f);
        int seconds = Mathf.FloorToInt(contractResult.battleDuration % 60f);

        return
            $"Time: {minutes:00}:{seconds:00}\n" +
            $"Manpower: {contractResult.TotalSurvivingSoldiers}/{contractResult.TotalStartingSoldiers}\n" +
            $"Casualties: {contractResult.TotalCasualties}\n" +
            $"Squads Lost: {contractResult.LostSquadCount}\n" +
            $"Squads Routed: {contractResult.RoutedSquadCount}";
    }

    string BuildRewardsSummary(
        ContractMercenaryContractResult contractResult)
    {
        if (!contractResult.playerWon)
            return "Rewards: None";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Rewards");

        for (int index = 0; index < contractResult.rewardsEarned.Count; index++)
        {
            ContractMercenaryResourceAmount reward =
                contractResult.rewardsEarned[index];

            if (reward == null || reward.amount <= 0)
                continue;

            builder.AppendLine($"+{reward.amount} {reward.resourceType}");
        }

        if (contractResult.prestigeEarned > 0)
            builder.AppendLine($"+{contractResult.prestigeEarned} Prestige");

        if (builder.Length == "Rewards\n".Length)
            builder.Append("None");

        return builder.ToString().TrimEnd();
    }

    string BuildSquadSummary(
        ContractMercenaryContractResult contractResult)
    {
        if (contractResult.playerSquads == null ||
            contractResult.playerSquads.Count == 0)
        {
            return "No player squad results.";
        }

        StringBuilder builder = new StringBuilder();

        for (int index = 0; index < contractResult.playerSquads.Count; index++)
        {
            ContractMercenarySquadBattleSummary squad =
                contractResult.playerSquads[index];

            if (squad == null)
                continue;

            string squadName = squad.squadData != null
                ? squad.squadData.squadName
                : "Squad";

            builder.Append(
                $"{squadName}: " +
                $"{squad.survivingSoldierCount}/{squad.startingSoldierCount}");

            if (squad.casualtyCount > 0)
                builder.Append($"  (-{squad.casualtyCount})");

            if (squad.routedOffField)
                builder.Append("  Routed");

            if (squad.startingSoldierCount > 0 &&
                squad.survivingSoldierCount <= 0)
            {
                builder.Append("  LOST");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    string BuildPolicyText(
        ContractMercenaryContractResult contractResult)
    {
        if (contractResult.companyChangesCommitted)
            return "Survivors and casualties have been committed to the company.";

        return "Failed-attempt casualties are not committed. Retry restores the pre-battle company army.";
    }

    #endregion

    #region Visibility

    void SetVisible(bool visible)
    {
        if (resultCanvasGroup == null)
            return;

        resultCanvasGroup.alpha = visible ? 1f : 0f;
        resultCanvasGroup.interactable = visible;
        resultCanvasGroup.blocksRaycasts = visible;
    }

    static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    #endregion
}

