using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Contract Mercenary Equipment acquisition page.
///
/// Responsibilities:
/// - select one persistent company squad as the equipment target
/// - show that squad's current Weapon / Armor / Kit loadout
/// - filter the authored CM Equipment catalog by slot and squad compatibility
/// - purchase and immediately equip one item, replacing the old item in that slot
///
/// Equipment ownership/stat resolution remains in the CM/runtime backend. This page
/// only owns the Equipment storefront presentation and target selection.
/// </summary>
[DisallowMultipleComponent]
public class ContractMercenaryEquipmentPageUI : MonoBehaviour
{
    private ContractMercenaryController contractController;

    [Header("Squad Selection")]
    [SerializeField] private Transform squadListContainer;
    [SerializeField] private ContractMercenaryMenuButtonUI menuButtonPrefab;

    [Header("Selected Squad")]
    [SerializeField] private TextMeshProUGUI squadNameText;
    [SerializeField] private TextMeshProUGUI squadTypeText;
    [SerializeField] private TextMeshProUGUI manpowerText;

    [Header("Current Loadout")]
    [SerializeField] private TextMeshProUGUI weaponEquipmentText;
    [SerializeField] private TextMeshProUGUI armorEquipmentText;
    [SerializeField] private TextMeshProUGUI kitEquipmentText;

    [Header("Equipment Slot Filter")]
    [SerializeField] private Button weaponSlotButton;
    [SerializeField] private Button armorSlotButton;
    [SerializeField] private Button kitSlotButton;
    [SerializeField] private TextMeshProUGUI equipmentCatalogTitleText;

    [Header("Equipment Catalog")]
    [SerializeField] private Transform equipmentCatalogContainer;

    [Header("Selected Equipment")]
    [SerializeField] private TextMeshProUGUI equipmentNameText;
    [SerializeField] private TextMeshProUGUI equipmentDescriptionText;
    [SerializeField] private TextMeshProUGUI equipmentEffectsText;
    [SerializeField] private TextMeshProUGUI equipmentCostText;
    [SerializeField] private TextMeshProUGUI equipmentRequirementsText;
    [SerializeField] private Button purchaseEquipmentButton;

    private readonly List<ContractMercenaryMenuButtonUI> spawnedSquadButtons =
        new List<ContractMercenaryMenuButtonUI>();

    private readonly List<ContractMercenaryMenuButtonUI> spawnedEquipmentButtons =
        new List<ContractMercenaryMenuButtonUI>();

    private ContractMercenarySquadState selectedSquad;
    private ContractMercenaryEquipmentOption selectedEquipmentOption;
    private EquipmentSlot activeEquipmentSlot = EquipmentSlot.Weapon;

    void Awake()
    {
        weaponSlotButton?.onClick.AddListener(ShowWeaponEquipment);
        armorSlotButton?.onClick.AddListener(ShowArmorEquipment);
        kitSlotButton?.onClick.AddListener(ShowKitEquipment);
        purchaseEquipmentButton?.onClick.AddListener(HandlePurchaseEquipmentClicked);
    }

    void OnDestroy()
    {
        weaponSlotButton?.onClick.RemoveListener(ShowWeaponEquipment);
        armorSlotButton?.onClick.RemoveListener(ShowArmorEquipment);
        kitSlotButton?.onClick.RemoveListener(ShowKitEquipment);
        purchaseEquipmentButton?.onClick.RemoveListener(HandlePurchaseEquipmentClicked);
    }

    /// <summary>
    /// Bound by ContractMercenaryHubUI. This page intentionally does not resolve
    /// ContractMercenaryController independently.
    /// </summary>
    public void Initialize(ContractMercenaryController controller)
    {
        contractController = controller;
    }

    public void RefreshPage()
    {
        ResolveSquadSelection();
        RebuildSquadList();
        RefreshSelectedSquad();
        RebuildEquipmentCatalog();
    }

    #region Squad Selection

    public void SelectSquad(ContractMercenarySquadState squadState)
    {
        selectedSquad = squadState;
        selectedEquipmentOption = null;
        RefreshSelectedSquad();
        RebuildEquipmentCatalog();
    }

    void ResolveSquadSelection()
    {
        ContractMercenaryRunState runState =
            contractController != null
                ? contractController.RunState
                : null;

        if (runState == null || runState.Army.Count == 0)
        {
            selectedSquad = null;
            selectedEquipmentOption = null;
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
            SetText(weaponEquipmentText, "Weapon: Standard Issue");
            SetText(armorEquipmentText, "Armor: Standard Issue");
            SetText(kitEquipmentText, "Kit: None");
            SetButtonInteractable(weaponSlotButton, false);
            SetButtonInteractable(armorSlotButton, false);
            SetButtonInteractable(kitSlotButton, false);
            return;
        }

        SquadData squadData = selectedSquad.squadData;

        SetText(squadNameText, squadData.squadName);
        SetText(squadTypeText, squadData.category.ToString());
        SetText(
            manpowerText,
            $"Manpower: {selectedSquad.currentSoldierCount}/{selectedSquad.MaximumSoldierCount}");

        SetText(
            weaponEquipmentText,
            $"Weapon: {GetEquipmentSlotDisplayName(EquipmentSlot.Weapon, "Standard Issue")}");
        SetText(
            armorEquipmentText,
            $"Armor: {GetEquipmentSlotDisplayName(EquipmentSlot.Armor, "Standard Issue")}");
        SetText(
            kitEquipmentText,
            $"Kit: {GetEquipmentSlotDisplayName(EquipmentSlot.Kit, "None")}");

        SetButtonInteractable(
            weaponSlotButton,
            HasEquipmentOptionsForSlot(EquipmentSlot.Weapon));
        SetButtonInteractable(
            armorSlotButton,
            HasEquipmentOptionsForSlot(EquipmentSlot.Armor));
        SetButtonInteractable(
            kitSlotButton,
            HasEquipmentOptionsForSlot(EquipmentSlot.Kit));
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

    #endregion

    #region Slot Selection

    public void ShowWeaponEquipment() => SelectEquipmentSlot(EquipmentSlot.Weapon);
    public void ShowArmorEquipment() => SelectEquipmentSlot(EquipmentSlot.Armor);
    public void ShowKitEquipment() => SelectEquipmentSlot(EquipmentSlot.Kit);

    public void SelectEquipmentSlot(EquipmentSlot slot)
    {
        activeEquipmentSlot = slot;
        selectedEquipmentOption = null;
        RebuildEquipmentCatalog();
    }

    #endregion

    #region Equipment Catalog

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

            ContractMercenaryMenuButtonUI button = Instantiate(
                menuButtonPrefab,
                equipmentCatalogContainer);

            button.Initialize(
                BuildEquipmentCatalogLabel(option, isEquipped),
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

    public void SelectEquipmentOption(ContractMercenaryEquipmentOption option)
    {
        selectedEquipmentOption = option;
        RefreshSelectedEquipmentDetails();
    }

    void RefreshSelectedEquipmentDetails()
    {
        if (contractController == null ||
            selectedEquipmentOption == null ||
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

        ContractMercenaryRunState runState = contractController.RunState;

        if (isEquipped)
        {
            SetText(equipmentRequirementsText, "Currently Equipped");
        }
        else if (runState != null && runState.HasActiveContract)
        {
            SetText(equipmentRequirementsText, "Unavailable during an active contract");
        }
        else if (runState != null &&
                 runState.Prestige < Mathf.Max(0, selectedEquipmentOption.minimumPrestige))
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

        RefreshSelectedSquad();
        RebuildEquipmentCatalog();
    }

    static string BuildEquipmentCatalogLabel(
        ContractMercenaryEquipmentOption option,
        bool isEquipped)
    {
        string displayName = option?.equipmentData != null
            ? option.equipmentData.DisplayName
            : "Equipment";

        return isEquipped
            ? $"{displayName}  (Equipped)"
            : displayName;
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

    #endregion

    #region Helpers

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
