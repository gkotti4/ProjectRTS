using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Army content page. Owns persistent squad inspection and squad-specific actions:
/// - squad roster/manpower inspection
/// - replenishment
/// - Equipment V1 loadout inspection and purchase/equip flow
///
/// Equipment is selected in the context of the already-selected persistent squad.
/// There is no inventory and no target-squad dropdown: purchasing an item immediately
/// replaces the item currently equipped in that item's Weapon / Armor / Kit slot.
/// </summary>
[DisallowMultipleComponent]
public class ContractMercenaryArmyPageUI : MonoBehaviour
{
    private ContractMercenaryController contractController;

    [Header("Squad List")]
    [SerializeField] private Transform squadListContainer;
    [SerializeField] private ContractMercenaryMenuButtonUI menuButtonPrefab;

    [Header("Selected Squad - Details")]
    [SerializeField] private TextMeshProUGUI squadNameText;
    [SerializeField] private TextMeshProUGUI squadTypeText;
    [SerializeField] private TextMeshProUGUI manpowerText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI upgradesText;

    [Header("Selected Squad - Equipment")]
    [SerializeField] private TextMeshProUGUI weaponEquipmentText;
    [SerializeField] private TextMeshProUGUI armorEquipmentText;
    [SerializeField] private TextMeshProUGUI kitEquipmentText;
    [SerializeField] private Button weaponEquipmentButton;
    [SerializeField] private Button armorEquipmentButton;
    [SerializeField] private Button kitEquipmentButton;

    [Header("Equipment Catalog")]
    [SerializeField] private GameObject equipmentCatalogRoot;
    [SerializeField] private TextMeshProUGUI equipmentCatalogTitleText;
    [SerializeField] private Transform equipmentCatalogContainer;
    [SerializeField] private TextMeshProUGUI equipmentNameText;
    [SerializeField] private TextMeshProUGUI equipmentDescriptionText;
    [SerializeField] private TextMeshProUGUI equipmentEffectsText;
    [SerializeField] private TextMeshProUGUI equipmentCostText;
    [SerializeField] private TextMeshProUGUI equipmentRequirementsText;
    [SerializeField] private Button purchaseEquipmentButton;
    [SerializeField] private Button closeEquipmentButton;

    [Header("Selected Squad - Replenishment")]
    [SerializeField] private TextMeshProUGUI replenishCostText;
    [SerializeField] private Button replenishButton;

    private readonly List<ContractMercenaryMenuButtonUI> spawnedSquadButtons =
        new List<ContractMercenaryMenuButtonUI>();

    private readonly List<ContractMercenaryMenuButtonUI> spawnedEquipmentButtons =
        new List<ContractMercenaryMenuButtonUI>();

    private ContractMercenarySquadState selectedSquad;
    private ContractMercenaryEquipmentOption selectedEquipmentOption;
    private EquipmentSlot activeEquipmentSlot = EquipmentSlot.Weapon;

    void Awake()
    {
        replenishButton?.onClick.AddListener(HandleReplenishClicked);
        weaponEquipmentButton?.onClick.AddListener(HandleWeaponEquipmentClicked);
        armorEquipmentButton?.onClick.AddListener(HandleArmorEquipmentClicked);
        kitEquipmentButton?.onClick.AddListener(HandleKitEquipmentClicked);
        purchaseEquipmentButton?.onClick.AddListener(HandlePurchaseEquipmentClicked);
        closeEquipmentButton?.onClick.AddListener(CloseEquipmentCatalog);

        SetEquipmentCatalogVisible(false);
    }

    void OnDestroy()
    {
        replenishButton?.onClick.RemoveListener(HandleReplenishClicked);
        weaponEquipmentButton?.onClick.RemoveListener(HandleWeaponEquipmentClicked);
        armorEquipmentButton?.onClick.RemoveListener(HandleArmorEquipmentClicked);
        kitEquipmentButton?.onClick.RemoveListener(HandleKitEquipmentClicked);
        purchaseEquipmentButton?.onClick.RemoveListener(HandlePurchaseEquipmentClicked);
        closeEquipmentButton?.onClick.RemoveListener(CloseEquipmentCatalog);
    }

    public void RefreshPage()
    {
        ResolveSelection();
        RebuildSquadList();
        RefreshSelectedSquad();

        if (IsEquipmentCatalogVisible())
            RebuildEquipmentCatalog();
    }

    public void SelectSquad(ContractMercenarySquadState squadState)
    {
        selectedSquad = squadState;
        selectedEquipmentOption = null;
        RefreshSelectedSquad();

        if (IsEquipmentCatalogVisible())
            RebuildEquipmentCatalog();
    }

    /// <summary>
    /// Bound by ContractMercenaryHubUI, the Company Hub root.
    /// This page intentionally does not resolve the CM controller on its own.
    /// </summary>
    public void Initialize(ContractMercenaryController controller)
    {
        contractController = controller;
    }

    #region Squad Selection / Details

    void ResolveSelection()
    {
        ContractMercenaryRunState runState =
            contractController != null
                ? contractController.RunState
                : null;

        if (runState == null || runState.Army.Count == 0)
        {
            selectedSquad = null;
            selectedEquipmentOption = null;
            SetEquipmentCatalogVisible(false);
            return;
        }

        if (selectedSquad != null && ContainsSquad(runState.Army, selectedSquad))
            return;

        selectedSquad = FirstValidSquad(runState.Army);
        selectedEquipmentOption = null;
    }

    void RebuildSquadList()
    {
        ClearButtons(spawnedSquadButtons);

        if (contractController == null ||
            contractController.RunState == null ||
            squadListContainer == null ||
            menuButtonPrefab == null)
        {
            return;
        }

        IReadOnlyList<ContractMercenarySquadState> army =
            contractController.RunState.Army;

        for (int index = 0; index < army.Count; index++)
        {
            ContractMercenarySquadState squad = army[index];
            if (squad == null || squad.squadData == null)
                continue;

            ContractMercenarySquadState capturedSquad = squad;
            string label =
                $"{squad.squadData.squadName}  {squad.currentSoldierCount}/{squad.MaximumSoldierCount}";

            ContractMercenaryMenuButtonUI button = Instantiate(
                menuButtonPrefab,
                squadListContainer);

            button.Initialize(label, () => SelectSquad(capturedSquad), true);
            spawnedSquadButtons.Add(button);
        }
    }

    void RefreshSelectedSquad()
    {
        if (selectedSquad == null || selectedSquad.squadData == null)
        {
            SetText(squadNameText, "No Squad Selected");
            SetText(squadTypeText, string.Empty);
            SetText(manpowerText, string.Empty);
            SetText(statsText, string.Empty);
            SetText(upgradesText, string.Empty);
            SetText(replenishCostText, string.Empty);
            SetText(weaponEquipmentText, "Weapon: Standard Issue");
            SetText(armorEquipmentText, "Armor: Standard Issue");
            SetText(kitEquipmentText, "Kit: None");

            SetButtonInteractable(replenishButton, false);
            SetButtonInteractable(weaponEquipmentButton, false);
            SetButtonInteractable(armorEquipmentButton, false);
            SetButtonInteractable(kitEquipmentButton, false);
            SetEquipmentCatalogVisible(false);
            return;
        }

        SquadData squadData = selectedSquad.squadData;
        SetText(squadNameText, squadData.squadName);
        SetText(squadTypeText, BuildSquadTypeText(squadData));
        SetText(
            manpowerText,
            $"Manpower: {selectedSquad.currentSoldierCount}/{selectedSquad.MaximumSoldierCount}");

        SetText(statsText, BuildStatsText(selectedSquad));
        SetText(upgradesText, BuildUpgradeSummary(selectedSquad));
        RefreshEquipmentLoadoutSummary();

        int missing = contractController.RunState.GetMissingSoldierCount(selectedSquad);
        int cost = contractController.GetReplenishmentGoldCost(selectedSquad);

        SetText(
            replenishCostText,
            missing > 0
                ? $"Replace {missing} soldiers: {cost} Gold"
                : "Full Strength");

        SetButtonInteractable(
            replenishButton,
            contractController.CanReplenishSquad(selectedSquad));

        bool canManageEquipment =
            contractController.RunState != null &&
            !contractController.RunState.HasActiveContract;

        SetButtonInteractable(
            weaponEquipmentButton,
            canManageEquipment && HasEquipmentOptionsForSlot(EquipmentSlot.Weapon));
        SetButtonInteractable(
            armorEquipmentButton,
            canManageEquipment && HasEquipmentOptionsForSlot(EquipmentSlot.Armor));
        SetButtonInteractable(
            kitEquipmentButton,
            canManageEquipment && HasEquipmentOptionsForSlot(EquipmentSlot.Kit));
    }

    void RefreshEquipmentLoadoutSummary()
    {
        SetText(
            weaponEquipmentText,
            $"Weapon: {GetEquipmentSlotDisplayName(EquipmentSlot.Weapon, "Standard Issue")}");
        SetText(
            armorEquipmentText,
            $"Armor: {GetEquipmentSlotDisplayName(EquipmentSlot.Armor, "Standard Issue")}");
        SetText(
            kitEquipmentText,
            $"Kit: {GetEquipmentSlotDisplayName(EquipmentSlot.Kit, "None")}");
    }

    string GetEquipmentSlotDisplayName(
        EquipmentSlot slot,
        string emptyLabel)
    {
        EquipmentData equipmentData = selectedSquad?.GetEquippedEquipment(slot);
        return equipmentData != null
            ? equipmentData.DisplayName
            : emptyLabel;
    }

    void HandleReplenishClicked()
    {
        if (contractController == null || selectedSquad == null)
            return;

        if (contractController.ReplenishSquadToFull(selectedSquad))
            RefreshPage();
    }

    #endregion

    #region Equipment Catalog

    void HandleWeaponEquipmentClicked() => OpenEquipmentCatalog(EquipmentSlot.Weapon);
    void HandleArmorEquipmentClicked() => OpenEquipmentCatalog(EquipmentSlot.Armor);
    void HandleKitEquipmentClicked() => OpenEquipmentCatalog(EquipmentSlot.Kit);

    void OpenEquipmentCatalog(EquipmentSlot slot)
    {
        if (contractController == null || selectedSquad == null)
            return;

        activeEquipmentSlot = slot;
        selectedEquipmentOption = null;
        SetEquipmentCatalogVisible(true);
        RebuildEquipmentCatalog();
    }

    public void CloseEquipmentCatalog()
    {
        selectedEquipmentOption = null;
        ClearButtons(spawnedEquipmentButtons);
        ClearSelectedEquipmentDetails();
        SetEquipmentCatalogVisible(false);
    }

    void RebuildEquipmentCatalog()
    {
        ClearButtons(spawnedEquipmentButtons);
        SetText(equipmentCatalogTitleText, $"{activeEquipmentSlot} Equipment");

        if (contractController == null ||
            selectedSquad == null ||
            selectedSquad.squadData == null ||
            equipmentCatalogContainer == null ||
            menuButtonPrefab == null)
        {
            selectedEquipmentOption = null;
            ClearSelectedEquipmentDetails();
            return;
        }

        IReadOnlyList<ContractMercenaryEquipmentOption> options =
            contractController.EquipmentOptions;

        bool selectedOptionStillValid = false;

        for (int index = 0; index < options.Count; index++)
        {
            ContractMercenaryEquipmentOption option = options[index];

            if (!IsEquipmentOptionVisible(option, activeEquipmentSlot))
                continue;

            ContractMercenaryEquipmentOption capturedOption = option;
            EquipmentData equipmentData = option.equipmentData;
            bool isEquipped =
                selectedSquad.GetEquippedEquipment(activeEquipmentSlot) == equipmentData;

            string label = BuildEquipmentCatalogLabel(option, isEquipped);

            ContractMercenaryMenuButtonUI button = Instantiate(
                menuButtonPrefab,
                equipmentCatalogContainer);

            button.Initialize(
                label,
                () => SelectEquipmentOption(capturedOption),
                true);

            spawnedEquipmentButtons.Add(button);

            if (selectedEquipmentOption == option)
                selectedOptionStillValid = true;
        }

        if (!selectedOptionStillValid)
            selectedEquipmentOption = FirstVisibleEquipmentOption(activeEquipmentSlot);

        RefreshSelectedEquipmentDetails();
    }

    bool IsEquipmentOptionVisible(
        ContractMercenaryEquipmentOption option,
        EquipmentSlot slot)
    {
        return option != null &&
               option.equipmentData != null &&
               option.equipmentData.slot == slot &&
               selectedSquad != null &&
               selectedSquad.squadData != null &&
               option.equipmentData.CanEquipTo(selectedSquad.squadData);
    }

    ContractMercenaryEquipmentOption FirstVisibleEquipmentOption(
        EquipmentSlot slot)
    {
        if (contractController == null)
            return null;

        IReadOnlyList<ContractMercenaryEquipmentOption> options =
            contractController.EquipmentOptions;

        for (int index = 0; index < options.Count; index++)
        {
            if (IsEquipmentOptionVisible(options[index], slot))
                return options[index];
        }

        return null;
    }

    bool HasEquipmentOptionsForSlot(EquipmentSlot slot)
    {
        return FirstVisibleEquipmentOption(slot) != null;
    }

    void SelectEquipmentOption(ContractMercenaryEquipmentOption option)
    {
        selectedEquipmentOption = option;
        RefreshSelectedEquipmentDetails();
    }

    void RefreshSelectedEquipmentDetails()
    {
        if (selectedEquipmentOption == null ||
            selectedEquipmentOption.equipmentData == null ||
            selectedSquad == null)
        {
            ClearSelectedEquipmentDetails();
            return;
        }

        EquipmentData equipmentData = selectedEquipmentOption.equipmentData;
        bool isEquipped =
            selectedSquad.GetEquippedEquipment(equipmentData.slot) == equipmentData;

        SetText(equipmentNameText, equipmentData.DisplayName);
        SetText(equipmentDescriptionText, equipmentData.description);
        SetText(
            equipmentEffectsText,
            string.IsNullOrWhiteSpace(equipmentData.effectSummary)
                ? "Stat modifiers authored on EquipmentData."
                : equipmentData.effectSummary);
        SetText(
            equipmentCostText,
            BuildEquipmentCostText(selectedEquipmentOption));

        if (isEquipped)
        {
            SetText(equipmentRequirementsText, "Currently Equipped");
        }
        else if (contractController.RunState != null &&
                 contractController.RunState.Prestige <
                 Mathf.Max(0, selectedEquipmentOption.minimumPrestige))
        {
            SetText(
                equipmentRequirementsText,
                $"Requires Prestige {Mathf.Max(0, selectedEquipmentOption.minimumPrestige)}");
        }
        else
        {
            SetText(equipmentRequirementsText, "Available");
        }

        SetButtonInteractable(
            purchaseEquipmentButton,
            contractController.CanPurchaseEquipment(
                selectedSquad,
                selectedEquipmentOption));
    }

    void HandlePurchaseEquipmentClicked()
    {
        if (contractController == null ||
            selectedSquad == null ||
            selectedEquipmentOption == null)
        {
            return;
        }

        if (!contractController.PurchaseEquipment(
                selectedSquad,
                selectedEquipmentOption))
        {
            RefreshSelectedEquipmentDetails();
            return;
        }

        // Purchase immediately replaces the previous item in this slot. Keep the
        // catalog open so the player can compare/replace again if desired.
        RefreshSelectedSquad();
        RebuildEquipmentCatalog();
    }

    static string BuildEquipmentCatalogLabel(
        ContractMercenaryEquipmentOption option,
        bool isEquipped)
    {
        string name = option?.equipmentData != null
            ? option.equipmentData.DisplayName
            : "Equipment";

        string suffix = isEquipped
            ? "  (Equipped)"
            : string.Empty;

        return $"{name}{suffix}";
    }

    static string BuildEquipmentCostText(
        ContractMercenaryEquipmentOption option)
    {
        if (option == null)
            return string.Empty;

        int gold = Mathf.Max(0, option.goldCost);
        int iron = Mathf.Max(0, option.ironCost);

        if (gold <= 0 && iron <= 0)
            return "Cost: Free";

        if (gold > 0 && iron > 0)
            return $"Cost: {gold} Gold / {iron} Iron";

        return gold > 0
            ? $"Cost: {gold} Gold"
            : $"Cost: {iron} Iron";
    }

    void ClearSelectedEquipmentDetails()
    {
        SetText(equipmentNameText, "No Equipment Selected");
        SetText(equipmentDescriptionText, string.Empty);
        SetText(equipmentEffectsText, string.Empty);
        SetText(equipmentCostText, string.Empty);
        SetText(equipmentRequirementsText, string.Empty);
        SetButtonInteractable(purchaseEquipmentButton, false);
    }

    bool IsEquipmentCatalogVisible()
    {
        return equipmentCatalogRoot != null && equipmentCatalogRoot.activeSelf;
    }

    void SetEquipmentCatalogVisible(bool visible)
    {
        if (equipmentCatalogRoot != null)
            equipmentCatalogRoot.SetActive(visible);
    }

    #endregion

    #region Presentation Helpers

    static string BuildSquadTypeText(SquadData squadData)
    {
        if (squadData == null)
            return string.Empty;

        return squadData.category.ToString();
    }

    string BuildStatsText(ContractMercenarySquadState squadState)
    {
        if (squadState == null ||
            squadState.squadData == null ||
            squadState.squadData.soldierData == null)
        {
            return "Stats unavailable";
        }

        SquadData squadData = squadState.squadData;
        Dictionary<UpgradeData, int> persistentUpgradeStacks =
            BuildPersistentUpgradeStackDictionary(squadState);

        SoldierRuntimeStats soldierStats = RuntimeStatResolver.ResolveSoldier(
            squadData.soldierData,
            squadData,
            faction: null,
            squadUpgradeStacks: persistentUpgradeStacks,
            equipmentLoadout: squadState.Equipment);

        if (soldierStats == null)
            return "Stats unavailable";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Health: {soldierStats.health.maxHealth}");
        builder.AppendLine($"Armor: {soldierStats.defense.armor}");
        builder.AppendLine($"Melee Attack: {soldierStats.melee.meleeAttack}");
        builder.AppendLine($"Weapon Damage: {soldierStats.melee.weaponDamage}");
        builder.AppendLine($"Melee Defense: {soldierStats.defense.meleeDefense}");

        if (soldierStats.rangedWeaponProfile != null)
        {
            builder.AppendLine($"Ranged Accuracy: {soldierStats.ranged.rangedAccuracy}");
            builder.AppendLine($"Missile Damage: {soldierStats.ranged.missileDamage}");
            builder.AppendLine($"Range: {soldierStats.ranged.attackRange:0.##}");
            builder.AppendLine(
                soldierStats.ranged.ammunition < 0
                    ? "Ammo: Unlimited"
                    : $"Ammo: {soldierStats.ranged.ammunition}");
        }

        builder.Append($"Move Speed: {soldierStats.movement.moveSpeed:0.##}");
        return builder.ToString();
    }

    Dictionary<UpgradeData, int> BuildPersistentUpgradeStackDictionary(
        ContractMercenarySquadState squadState)
    {
        Dictionary<UpgradeData, int> result = new Dictionary<UpgradeData, int>();

        ContractMercenaryRunState runState =
            contractController != null
                ? contractController.RunState
                : null;

        if (runState != null)
            AddUpgradeStacks(result, runState.CompanyUpgrades);

        if (squadState != null)
            AddUpgradeStacks(result, squadState.appliedUpgrades);

        return result;
    }

    static void AddUpgradeStacks(
        Dictionary<UpgradeData, int> destination,
        IReadOnlyList<ContractMercenaryUpgradeStack> source)
    {
        if (destination == null || source == null)
            return;

        for (int index = 0; index < source.Count; index++)
        {
            ContractMercenaryUpgradeStack stack = source[index];

            if (stack == null || stack.upgradeData == null || stack.stackCount <= 0)
                continue;

            destination.TryGetValue(stack.upgradeData, out int existingStacks);
            destination[stack.upgradeData] =
                existingStacks + Mathf.Max(0, stack.stackCount);
        }
    }

    static string BuildUpgradeSummary(ContractMercenarySquadState squad)
    {
        if (squad == null || squad.appliedUpgrades == null || squad.appliedUpgrades.Count == 0)
            return "Upgrades: None";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Upgrades");

        bool added = false;

        for (int index = 0; index < squad.appliedUpgrades.Count; index++)
        {
            ContractMercenaryUpgradeStack stack = squad.appliedUpgrades[index];
            if (stack == null || stack.upgradeData == null || stack.stackCount <= 0)
                continue;

            builder.Append(stack.upgradeData.upgradeName);
            if (stack.stackCount > 1)
                builder.Append($" x{stack.stackCount}");
            builder.AppendLine();
            added = true;
        }

        return added ? builder.ToString().TrimEnd() : "Upgrades: None";
    }

    static bool ContainsSquad(
        IReadOnlyList<ContractMercenarySquadState> army,
        ContractMercenarySquadState target)
    {
        for (int index = 0; index < army.Count; index++)
        {
            if (army[index] == target)
                return true;
        }

        return false;
    }

    static ContractMercenarySquadState FirstValidSquad(
        IReadOnlyList<ContractMercenarySquadState> army)
    {
        for (int index = 0; index < army.Count; index++)
        {
            ContractMercenarySquadState squad = army[index];
            if (squad != null && squad.squadData != null)
                return squad;
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

    static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    #endregion
}
