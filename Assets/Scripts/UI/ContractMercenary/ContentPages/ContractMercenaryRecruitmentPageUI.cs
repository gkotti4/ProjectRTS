using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// -----------------------------------------------------------------------------
/// ContractMercenaryRecruitmentPageUI
/// -----------------------------------------------------------------------------
///
/// Contract Mercenary recruitment page.
///
/// Responsibilities:
/// - build the authored recruitment catalog dynamically
/// - present the currently selected recruit
/// - recruit new persistent squads
///
/// This page is intentionally focused only on acquiring new squads.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class ContractMercenaryRecruitmentPageUI : MonoBehaviour
{
    #region References

    private ContractMercenaryController contractController;

    [Header("Recruitment Catalog")]
    [SerializeField] private Transform recruitCatalogContainer;
    [SerializeField] private ContractMercenaryMenuButtonUI menuButtonPrefab;

    [Header("Selected Recruit")]
    [SerializeField] private TextMeshProUGUI recruitNameText;
    [SerializeField] private TextMeshProUGUI recruitTypeText;
    [SerializeField] private TextMeshProUGUI recruitStatsText;
    [SerializeField] private TextMeshProUGUI recruitCostText;
    [SerializeField] private TextMeshProUGUI recruitRequirementsText;
    [SerializeField] private Button recruitButton;

    #endregion

    #region Runtime

    private readonly List<ContractMercenaryMenuButtonUI> spawnedRecruitButtons =
        new List<ContractMercenaryMenuButtonUI>();

    private ContractMercenaryRecruitOption selectedRecruitOption;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        recruitButton?.onClick.AddListener(HandleRecruitClicked);
    }

    void OnDestroy()
    {
        recruitButton?.onClick.RemoveListener(HandleRecruitClicked);
    }

    #endregion

    #region Binding

    /// <summary>
    /// Bound by ContractMercenaryHubUI. This page intentionally does not resolve
    /// ContractMercenaryController independently.
    /// </summary>
    public void Initialize(ContractMercenaryController controller)
    {
        contractController = controller;
    }

    #endregion

    #region Page Refresh

    public void RefreshPage()
    {
        ResolveRecruitSelection();
        RebuildRecruitCatalog();
        RefreshSelectedRecruit();
    }

    #endregion

    #region Recruitment

    public void SelectRecruit(ContractMercenaryRecruitOption option)
    {
        selectedRecruitOption = option;
        RefreshSelectedRecruit();
    }

    void ResolveRecruitSelection()
    {
        IReadOnlyList<ContractMercenaryRecruitOption> options =
            contractController != null
                ? contractController.RecruitmentOptions
                : null;

        if (options == null || options.Count == 0)
        {
            selectedRecruitOption = null;
            return;
        }

        if (selectedRecruitOption != null &&
            ContainsRecruitOption(options, selectedRecruitOption))
        {
            return;
        }

        selectedRecruitOption = FirstValidRecruitOption(options);
    }

    void RebuildRecruitCatalog()
    {
        ClearButtons(spawnedRecruitButtons);

        if (contractController == null ||
            recruitCatalogContainer == null ||
            menuButtonPrefab == null)
        {
            return;
        }

        IReadOnlyList<ContractMercenaryRecruitOption> options =
            contractController.RecruitmentOptions;

        for (int index = 0; index < options.Count; index++)
        {
            ContractMercenaryRecruitOption option = options[index];

            if (option == null || option.squadData == null)
                continue;

            ContractMercenaryRecruitOption capturedOption = option;

            ContractMercenaryMenuButtonUI button = Instantiate(
                menuButtonPrefab,
                recruitCatalogContainer);

            // Locked/unaffordable recruits remain inspectable. The action button
            // communicates whether the actual purchase is currently allowed.
            button.Initialize(
                BuildRecruitCatalogLabel(option),
                () => SelectRecruit(capturedOption),
                true);

            spawnedRecruitButtons.Add(button);
        }
    }

    void RefreshSelectedRecruit()
    {
        if (selectedRecruitOption == null ||
            selectedRecruitOption.squadData == null)
        {
            SetText(recruitNameText, "No Recruit Selected");
            SetText(recruitTypeText, string.Empty);
            SetText(recruitStatsText, string.Empty);
            SetText(recruitCostText, string.Empty);
            SetText(recruitRequirementsText, string.Empty);

            if (recruitButton != null)
                recruitButton.interactable = false;

            return;
        }

        SquadData squadData = selectedRecruitOption.squadData;

        SetText(recruitNameText, squadData.squadName);
        SetText(recruitTypeText, squadData.category.ToString());
        SetText(recruitStatsText, BuildRecruitStatsText(squadData));
        SetText(
            recruitCostText,
            BuildCostText(
                selectedRecruitOption.goldCost,
                selectedRecruitOption.ironCost));
        SetText(
            recruitRequirementsText,
            BuildPrestigeRequirementText(
                selectedRecruitOption.minimumPrestige));

        if (recruitButton != null)
        {
            recruitButton.interactable =
                contractController != null &&
                contractController.CanRecruit(selectedRecruitOption);
        }
    }

    void HandleRecruitClicked()
    {
        if (contractController == null || selectedRecruitOption == null)
            return;

        if (contractController.RecruitSquad(selectedRecruitOption))
            RefreshPage();
    }

    #endregion

    #region Presentation Helpers

    static string BuildRecruitCatalogLabel(ContractMercenaryRecruitOption option)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(option.squadData.squadName);
        builder.Append($"  |  {option.goldCost} Gold");

        if (option.ironCost > 0)
            builder.Append($" + {option.ironCost} Iron");

        return builder.ToString();
    }

    static string BuildCostText(int goldCost, int ironCost)
    {
        string value = $"Cost: {Mathf.Max(0, goldCost)} Gold";

        if (ironCost > 0)
            value += $" + {ironCost} Iron";

        return value;
    }

    static string BuildPrestigeRequirementText(int minimumPrestige)
    {
        return minimumPrestige > 0
            ? $"Requires Prestige: {minimumPrestige}"
            : "Requirements: None";
    }

    static string BuildRecruitStatsText(SquadData squadData)
    {
        if (squadData == null || squadData.soldierData == null)
            return "Stats unavailable";

        SoldierData soldier = squadData.soldierData;

        return
            $"Starting Soldiers: {squadData.ResolvedStartingSoldierCount}\n" +
            $"Maximum Soldiers: {Mathf.Max(1, squadData.maxSoldierCount)}\n" +
            $"Health: {soldier.health.maxHealth}\n" +
            $"Armor: {soldier.defense.armor}\n" +
            $"Melee Defense: {soldier.defense.meleeDefense}";
    }

    static bool ContainsRecruitOption(
        IReadOnlyList<ContractMercenaryRecruitOption> options,
        ContractMercenaryRecruitOption target)
    {
        for (int index = 0; index < options.Count; index++)
        {
            if (options[index] == target)
                return true;
        }

        return false;
    }

    static ContractMercenaryRecruitOption FirstValidRecruitOption(
        IReadOnlyList<ContractMercenaryRecruitOption> options)
    {
        for (int index = 0; index < options.Count; index++)
        {
            ContractMercenaryRecruitOption option = options[index];

            if (option != null && option.squadData != null)
                return option;
        }

        return null;
    }

    static void ClearButtons(List<ContractMercenaryMenuButtonUI> buttons)
    {
        for (int index = 0; index < buttons.Count; index++)
        {
            if (buttons[index] != null)
                Destroy(buttons[index].gameObject);
        }

        buttons.Clear();
    }

    static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    #endregion
}
