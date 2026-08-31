using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Army content page. Owns persistent squad overview and maintenance:
/// - squad roster/manpower inspection
/// - resolved stat inspection
/// - current per-squad upgrade/equipment overview
/// - replenishment
///
/// Equipment acquisition/equipping is intentionally owned by
/// ContractMercenaryEquipmentPageUI. Army is not a storefront.
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

    [Header("Selected Squad - Veterancy")]
    [SerializeField] private TextMeshProUGUI veterancyText;

    [Header("Selected Squad - Equipment Overview")]
    [SerializeField] private TextMeshProUGUI weaponEquipmentText;
    [SerializeField] private TextMeshProUGUI armorEquipmentText;
    [SerializeField] private TextMeshProUGUI kitEquipmentText;

    [Header("Selected Squad - Replenishment")]
    [SerializeField] private TextMeshProUGUI replenishCostText;
    [SerializeField] private Button replenishButton;

    private readonly List<ContractMercenaryMenuButtonUI> spawnedSquadButtons =
        new List<ContractMercenaryMenuButtonUI>();

    private ContractMercenarySquadState selectedSquad;

    void Awake()
    {
        replenishButton?.onClick.AddListener(HandleReplenishClicked);
    }

    void OnDestroy()
    {
        replenishButton?.onClick.RemoveListener(HandleReplenishClicked);
    }

    public void RefreshPage()
    {
        ResolveSelection();
        RebuildSquadList();
        RefreshSelectedSquad();
    }

    public void SelectSquad(ContractMercenarySquadState squadState)
    {
        selectedSquad = squadState;
        RefreshSelectedSquad();
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
            return;
        }

        if (selectedSquad != null && ContainsSquad(runState.Army, selectedSquad))
            return;

        selectedSquad = FirstValidSquad(runState.Army);
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
            SetText(veterancyText, string.Empty);
            SetText(replenishCostText, string.Empty);
            SetText(weaponEquipmentText, "Weapon: Standard Issue");
            SetText(armorEquipmentText, "Armor: Standard Issue");
            SetText(kitEquipmentText, "Kit: None");
            SetButtonInteractable(replenishButton, false);
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
        SetText(veterancyText, BuildVeterancyText(selectedSquad));
        RefreshEquipmentLoadoutSummary();

        ContractMercenaryRunState runState = contractController.RunState;
        int missing = runState.GetMissingSoldierCount(selectedSquad);
        int cost = contractController.GetReplenishmentGoldCost(selectedSquad);

        SetText(
            replenishCostText,
            missing > 0
                ? $"Replace {missing} soldiers: {cost} Gold"
                : "Full Strength");

        SetButtonInteractable(
            replenishButton,
            contractController.CanReplenishSquad(selectedSquad));
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
            equipmentLoadout: squadState.Equipment,
            veterancyData: squadData.veterancyData,
            veterancyRank: squadState.veterancyRank);

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

    string BuildVeterancyText(ContractMercenarySquadState squadState)
    {
        if (squadState == null)
            return string.Empty;

        VeterancyData veterancyData =
            squadState.squadData != null
                ? squadState.squadData.veterancyData
                : null;

        if (veterancyData == null)
            return "Veterancy: Not Configured";

        int rank = Mathf.Clamp(
            squadState.veterancyRank,
            0,
            veterancyData.MaximumRank);

        string rankName = veterancyData.GetRankDisplayName(rank);

        if (veterancyData.IsMaximumRank(rank))
        {
            return
                $"Veterancy: Rank {rank} - {rankName}\n" +
                $"XP: {squadState.veterancyExperience} (Max Rank)";
        }

        int nextRankExperience =
            veterancyData.GetTotalExperienceRequiredForNextRank(rank);

        return
            $"Veterancy: Rank {rank} - {rankName}\n" +
            $"XP: {squadState.veterancyExperience}/{nextRankExperience}";
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

