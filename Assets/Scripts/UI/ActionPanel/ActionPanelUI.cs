// SESSION: Squad Control Refactor

using System.Collections.Generic;
using UnityEngine;

public class ActionPanelUI : MonoBehaviour
{
    #region Fields

    [Header("Buttons")]
    [SerializeField] private GameObject actionButtonPrefab;
    [SerializeField] private int maxButtons = 15;

    [Header("Squad Commands")]
    private readonly List<ActionButtonUI> buttons = new List<ActionButtonUI>();

    private VillagerController currentVillager;
    private SquadController currentSquad;

    private bool inBuildSubmenu = false;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        BuildButtonPool();
        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
    }

    void OnDestroy()
    {
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
    }

    #endregion

    #region Public Panel API

    public void ShowProductionPanel(BuildingController building)
    {
        currentVillager = null;
        currentSquad = null;
        inBuildSubmenu = false;

        HidePanel();

        if (building == null) return;
        if (building.Stats == null) return;
        if (building.Stats.baseDetails == null) return;
        if (building.Stats.baseDetails.productionOptions == null) return;

        List<ProductionOptionData> options = building.Stats.baseDetails.productionOptions;

        for (int i = 0; i < options.Count && i < maxButtons; i++)
        {
            int index = i;
            FillSlot(index, button =>
            {
                button.InitializeFromProductionOption(options[index], building);
            });
        }
    }

    // public void ShowUnitPanel(UnitController unit) // DEPRECIATED: Replacing with Villager specific panel (Squad panel (military) and Villager panel, rather than Unit)
    // {
    //     currentUnit = unit;
    //     currentSquad = null;
    //     inBuildSubmenu = false;
    //
    //     ShowUnitCommands(unit);
    // }

    public void ShowVillagerPanel(VillagerController villager)
    {
        currentVillager = villager;
        currentSquad = null;
        inBuildSubmenu = false;
        
        // ShowUnitCommands(villager);
        ShowVillagerCommands(villager);
    }

    public void ShowSquadPanel(SquadController squad)
    {
        currentVillager = null;
        currentSquad = squad;
        inBuildSubmenu = false;

        ShowSquadCommands(squad);
    }

    public void ShowBuildPanel(VillagerController villager)
    {
        currentVillager = villager;
        currentSquad = null;
        inBuildSubmenu = true;

        HidePanel();

        if (villager == null) return;
        if (villager.Stats == null) return;
        if (villager.Stats.baseDetails == null) return;
        if (villager.Stats.baseDetails.buildOptions == null) return;

        foreach (BuildOptionData option in villager.Stats.baseDetails.buildOptions)
        {
            if (option == null) continue;
            if (option.hotkey == HotkeySlot.None) continue;

            int slotIndex = GetSlotIndex(option.hotkey);

            FillSlot(slotIndex, button =>
            {
                button.InitializeFromBuildOption(option, villager);
            });
        }
    }

    public void ExitBuildPanel()
    {
        if (!inBuildSubmenu) return;

        inBuildSubmenu = false;

        if (currentVillager != null)
            ShowVillagerCommands(currentVillager);
            // ShowUnitCommands(currentUnit);
        else if (currentSquad != null)
            ShowSquadCommands(currentSquad);
        else
            HidePanel();
    }

    public void HidePanel()
    {
        for (int i = 0; i < buttons.Count; i++)
            HideSlot(i);
    }

    #endregion

    #region Show Commands

    // void ShowUnitCommands(UnitController unit) // DEPRECIATED
    // {
    //     HidePanel();
    //
    //     if (unit == null) return;
    //     if (!unit.TryGetComponent(out CommandController commandController)) return;
    //
    //     foreach (CommandData command in commandController.GetAllCommands())
    //     {
    //         if (command == null) continue;
    //         if (!command.showButton) continue;
    //         if (command.hotkey == HotkeySlot.None) continue;
    //
    //         int slotIndex = GetSlotIndex(command.hotkey);
    //
    //         FillSlot(slotIndex, button =>
    //         {
    //             button.InitializeFromUnitCommand(command, unit);
    //         });
    //     }
    // }
    
    void ShowVillagerCommands(VillagerController villager)
    {
        HidePanel();

        if (villager == null) return;
        if (!villager.TryGetComponent(out CommandController commandController)) return;

        foreach (CommandData command in commandController.GetAllCommands())
        {
            if (command == null) continue;
            if (!command.showButton) continue;
            if (command.hotkey == HotkeySlot.None) continue;

            int slotIndex = GetSlotIndex(command.hotkey);

            FillSlot(slotIndex, button =>
            {
                button.InitializeFromVillagerCommand(command, villager);
            });
        }
    }

    void ShowSquadCommands(SquadController squad)
    {
        Debug.Log("Show Squad Commands");
        HidePanel();

        if (squad == null) return;

        foreach (CommandData command in currentSquad.SquadData.commandSet.GetAllCommands())
        {
            if (command == null) continue;
            if (!command.showButton) continue;
            if (command.hotkey == HotkeySlot.None) continue;

            int slotIndex = GetSlotIndex(command.hotkey);

            FillSlot(slotIndex, button =>
            {
                button.InitializeFromSquadCommand(command, squad);
            });
        }
    }

    #endregion

    #region Button Pool

    void BuildButtonPool()
    {
        buttons.Clear();

        for (int i = 0; i < maxButtons; i++)
        {
            GameObject buttonObject = Instantiate(actionButtonPrefab, transform);

            CanvasGroup canvasGroup = buttonObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = buttonObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            ActionButtonUI button = buttonObject.GetComponent<ActionButtonUI>();

            if (button == null)
            {
                Debug.LogError("Action button prefab is missing ActionButtonUI.");
                continue;
            }

            buttons.Add(button);
        }
    }

    void FillSlot(int index, System.Action<ActionButtonUI> initialize)
    {
        if (index < 0 || index >= buttons.Count)
            return;

        ActionButtonUI button = buttons[index];
        if (button == null) return;

        initialize(button);
        ShowSlot(index);
    }

    void ShowSlot(int index)
    {
        if (!IsValidButtonIndex(index)) return;

        CanvasGroup canvasGroup = buttons[index].GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }

    void HideSlot(int index)
    {
        if (!IsValidButtonIndex(index)) return;

        ActionButtonUI button = buttons[index];
        button.Clear();

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    bool IsValidButtonIndex(int index)
    {
        return index >= 0 &&
               index < buttons.Count &&
               buttons[index] != null;
    }

    int GetSlotIndex(HotkeySlot hotkey)
    {
        return (int)hotkey - 1;
    }

    #endregion

    #region Placement

    void HandlePlacementModeChanged(bool isPlacing)
    {
        if (!isPlacing && inBuildSubmenu)
            ExitBuildPanel();
    }

    #endregion
}

