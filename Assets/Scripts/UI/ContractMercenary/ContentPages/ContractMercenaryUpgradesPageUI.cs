using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// -----------------------------------------------------------------------------
/// ContractMercenaryUpgradesPageUI
/// -----------------------------------------------------------------------------
///
/// Contract Mercenary broad upgrade acquisition page.
///
/// Upgrade cards are company/army-wide progression choices. Their authored
/// UpgradeData targeting decides which squad categories are affected; the player
/// does not select one owned squad from this page.
///
/// Purchases are persisted as company-wide/faction-wide stacks in
/// ContractMercenaryRunState. UpgradeData targeting decides which matching squads
/// receive the effect once those stacks are mirrored into the live battle faction.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class ContractMercenaryUpgradesPageUI : MonoBehaviour
{
    #region References

    private ContractMercenaryController contractController;

    [Header("Upgrade Catalog")]
    [SerializeField] private Transform upgradeCatalogContainer;
    [SerializeField] private ContractMercenaryMenuButtonUI menuButtonPrefab;

    [Header("Selected Upgrade")]
    [SerializeField] private TextMeshProUGUI upgradeNameText;
    [SerializeField] private TextMeshProUGUI upgradeDescriptionText;
    [SerializeField] private TextMeshProUGUI upgradeCostText;
    [SerializeField] private TextMeshProUGUI upgradeRequirementsText;
    [SerializeField] private Button purchaseUpgradeButton;

    #endregion

    #region Runtime

    private readonly List<ContractMercenaryMenuButtonUI> spawnedUpgradeButtons =
        new List<ContractMercenaryMenuButtonUI>();

    private ContractMercenaryUpgradeShopOption selectedUpgradeOption;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        purchaseUpgradeButton?.onClick.AddListener(HandlePurchaseUpgradeClicked);
    }

    void OnDestroy()
    {
        purchaseUpgradeButton?.onClick.RemoveListener(HandlePurchaseUpgradeClicked);
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
        ResolveUpgradeSelection();
        RebuildUpgradeCatalog();
        RefreshSelectedUpgrade();
    }

    #endregion

    #region Upgrade Acquisition

    public void SelectUpgrade(ContractMercenaryUpgradeShopOption option)
    {
        selectedUpgradeOption = option;
        RefreshSelectedUpgrade();
    }

    void ResolveUpgradeSelection()
    {
        IReadOnlyList<ContractMercenaryUpgradeShopOption> options =
            contractController != null
                ? contractController.UpgradeShopOptions
                : null;

        if (options == null || options.Count == 0)
        {
            selectedUpgradeOption = null;
            return;
        }

        if (selectedUpgradeOption != null &&
            ContainsUpgradeOption(options, selectedUpgradeOption))
        {
            return;
        }

        selectedUpgradeOption = FirstValidUpgradeOption(options);
    }

    void RebuildUpgradeCatalog()
    {
        ClearButtons(spawnedUpgradeButtons);

        if (contractController == null ||
            upgradeCatalogContainer == null ||
            menuButtonPrefab == null)
        {
            return;
        }

        IReadOnlyList<ContractMercenaryUpgradeShopOption> options =
            contractController.UpgradeShopOptions;

        for (int index = 0; index < options.Count; index++)
        {
            ContractMercenaryUpgradeShopOption option = options[index];

            if (option == null || option.upgradeData == null)
                continue;

            ContractMercenaryUpgradeShopOption capturedOption = option;

            ContractMercenaryMenuButtonUI button = Instantiate(
                menuButtonPrefab,
                upgradeCatalogContainer);

            // Offers remain inspectable even when Prestige/currency requirements
            // would prevent purchasing them once broad purchasing is wired up.
            button.Initialize(
                BuildUpgradeCatalogLabel(option),
                () => SelectUpgrade(capturedOption),
                true);

            spawnedUpgradeButtons.Add(button);
        }
    }

    void RefreshSelectedUpgrade()
    {
        if (selectedUpgradeOption == null ||
            selectedUpgradeOption.upgradeData == null)
        {
            SetText(upgradeNameText, "No Upgrade Selected");
            SetText(upgradeDescriptionText, string.Empty);
            SetText(upgradeCostText, string.Empty);
            SetText(upgradeRequirementsText, string.Empty);

            if (purchaseUpgradeButton != null)
                purchaseUpgradeButton.interactable = false;

            return;
        }

        UpgradeData upgradeData = selectedUpgradeOption.upgradeData;

        SetText(upgradeNameText, upgradeData.upgradeName);
        SetText(
            upgradeDescriptionText,
            string.IsNullOrWhiteSpace(upgradeData.description)
                ? "No description authored."
                : upgradeData.description);
        SetText(
            upgradeCostText,
            BuildCostText(
                selectedUpgradeOption.goldCost,
                selectedUpgradeOption.ironCost));
        SetText(
            upgradeRequirementsText,
            BuildUpgradeRequirementsText(selectedUpgradeOption));

        if (purchaseUpgradeButton != null)
        {
            purchaseUpgradeButton.interactable =
                contractController != null &&
                contractController.CanPurchaseUpgrade(selectedUpgradeOption);
        }
    }

    void HandlePurchaseUpgradeClicked()
    {
        if (contractController == null || selectedUpgradeOption == null)
            return;

        if (contractController.PurchaseUpgrade(selectedUpgradeOption))
            RefreshPage();
    }

    #endregion

    #region Presentation Helpers

    static string BuildUpgradeCatalogLabel(ContractMercenaryUpgradeShopOption option)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(option.upgradeData.upgradeName);
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

    string BuildUpgradeRequirementsText(
        ContractMercenaryUpgradeShopOption option)
    {
        if (option == null || option.upgradeData == null)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        UpgradeData upgrade = option.upgradeData;

        int currentStacks =
            contractController != null && contractController.RunState != null
                ? contractController.RunState.GetCompanyUpgradeStackCount(upgrade)
                : 0;

        int maximumStacks = upgrade.repeatable
            ? Mathf.Max(1, upgrade.maximumStacks)
            : 1;

        builder.AppendLine($"Owned: {currentStacks}/{maximumStacks}");

        if (option.minimumPrestige > 0)
            builder.AppendLine($"Prestige: {option.minimumPrestige}");

        if (upgrade.requiredUpgrades != null &&
            upgrade.requiredUpgrades.Count > 0)
        {
            builder.Append("Requires: ");
            AppendUpgradeNames(builder, upgrade.requiredUpgrades);
            builder.AppendLine();
        }

        if (upgrade.blockedByUpgrades != null &&
            upgrade.blockedByUpgrades.Count > 0)
        {
            builder.Append("Blocked By: ");
            AppendUpgradeNames(builder, upgrade.blockedByUpgrades);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    static void AppendUpgradeNames(
        StringBuilder builder,
        IReadOnlyList<UpgradeData> upgrades)
    {
        bool addedAny = false;

        for (int index = 0; index < upgrades.Count; index++)
        {
            UpgradeData upgrade = upgrades[index];

            if (upgrade == null)
                continue;

            if (addedAny)
                builder.Append(", ");

            builder.Append(upgrade.upgradeName);
            addedAny = true;
        }
    }

    static bool ContainsUpgradeOption(
        IReadOnlyList<ContractMercenaryUpgradeShopOption> options,
        ContractMercenaryUpgradeShopOption target)
    {
        for (int index = 0; index < options.Count; index++)
        {
            if (options[index] == target)
                return true;
        }

        return false;
    }

    static ContractMercenaryUpgradeShopOption FirstValidUpgradeOption(
        IReadOnlyList<ContractMercenaryUpgradeShopOption> options)
    {
        for (int index = 0; index < options.Count; index++)
        {
            ContractMercenaryUpgradeShopOption option = options[index];

            if (option != null && option.upgradeData != null)
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
