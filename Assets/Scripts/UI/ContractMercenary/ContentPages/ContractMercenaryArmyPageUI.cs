
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Army content page. Owns persistent squad inspection and squad-specific actions.
/// Owns persistent squad inspection and replenishment.
/// Current persistent upgrades are displayed here, but purchasing upgrades belongs
/// to ContractMercenaryShopPageUI so Army remains an owned-roster management page.
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
            if (squad == null || squad.squadData == null) continue;

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

            if (replenishButton != null)
                replenishButton.interactable = false;

            return;
        }

        SquadData squadData = selectedSquad.squadData;
        SetText(squadNameText, squadData.squadName);
        SetText(squadTypeText, BuildSquadTypeText(squadData));
        SetText(
            manpowerText,
            $"Manpower: {selectedSquad.currentSoldierCount}/{selectedSquad.MaximumSoldierCount}");

        SetText(statsText, BuildStatsText(squadData));
        SetText(upgradesText, BuildUpgradeSummary(selectedSquad));

        int missing = contractController.RunState.GetMissingSoldierCount(selectedSquad);
        int cost = contractController.GetReplenishmentGoldCost(selectedSquad);

        SetText(
            replenishCostText,
            missing > 0
                ? $"Replace {missing} soldiers: {cost} Gold"
                : "Full Strength");

        if (replenishButton != null)
            replenishButton.interactable = contractController.CanReplenishSquad(selectedSquad);

    }

    void HandleReplenishClicked()
    {
        if (contractController == null || selectedSquad == null)
            return;

        if (contractController.ReplenishSquadToFull(selectedSquad))
            RefreshPage();
    }

    static string BuildSquadTypeText(SquadData squadData)
    {
        if (squadData == null)
            return string.Empty;

        return squadData.category.ToString();
    }

    static string BuildStatsText(SquadData squadData)
    {
        if (squadData == null || squadData.soldierData == null)
            return "Stats unavailable";

        SoldierData soldier = squadData.soldierData;

        return
            $"Health: {soldier.health.maxHealth}\n" +
            $"Armor: {soldier.defense.armor}\n" +
            $"Melee Defense: {soldier.defense.meleeDefense}\n" +
            $"Missile Defense: {soldier.defense.missileDefense}\n" +
            $"Move Speed: {soldier.movement.moveSpeed:0.##}";
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
            if (stack == null || stack.upgradeData == null || stack.stackCount <= 0) continue;

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

    static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }
}
