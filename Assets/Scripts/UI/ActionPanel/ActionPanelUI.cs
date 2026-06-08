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
    [SerializeField] private List<CommandData> squadCommands = new List<CommandData>();

    private readonly List<ActionButtonUI> buttons = new List<ActionButtonUI>();

    private UnitController currentUnit;
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
        currentUnit = null;
        currentSquad = null;
        inBuildSubmenu = false;

        HidePanel();

        if (building == null) return;
        if (building.Stats == null) return;
        if (building.Stats.baseData == null) return;
        if (building.Stats.baseData.productionOptions == null) return;

        List<ProductionOptionData> options = building.Stats.baseData.productionOptions;

        for (int i = 0; i < options.Count && i < maxButtons; i++)
        {
            int index = i;
            FillSlot(index, button =>
            {
                button.InitializeFromProductionOption(options[index], building);
            });
        }
    }

    public void ShowUnitPanel(UnitController unit)
    {
        currentUnit = unit;
        currentSquad = null;
        inBuildSubmenu = false;

        ShowUnitCommands(unit);
    }

    public void ShowSquadPanel(SquadController squad)
    {
        currentUnit = null;
        currentSquad = squad;
        inBuildSubmenu = false;

        ShowSquadCommands(squad);
    }

    public void ShowBuildPanel(UnitController unit)
    {
        currentUnit = unit;
        currentSquad = null;
        inBuildSubmenu = true;

        HidePanel();

        if (unit == null) return;
        if (unit.Stats == null) return;
        if (unit.Stats.baseData == null) return;
        if (unit.Stats.baseData.buildOptions == null) return;

        foreach (BuildOptionData option in unit.Stats.baseData.buildOptions)
        {
            if (option == null) continue;
            if (option.hotkey == HotkeySlot.None) continue;

            int slotIndex = GetSlotIndex(option.hotkey);

            FillSlot(slotIndex, button =>
            {
                button.InitializeFromBuildOption(option, unit);
            });
        }
    }

    public void ExitBuildPanel()
    {
        if (!inBuildSubmenu) return;

        inBuildSubmenu = false;

        if (currentUnit != null)
            ShowUnitCommands(currentUnit);
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

    void ShowUnitCommands(UnitController unit)
    {
        HidePanel();

        if (unit == null) return;
        if (!unit.TryGetComponent(out CommandController commandController)) return;

        foreach (CommandData command in commandController.GetAllCommands())
        {
            if (command == null) continue;
            if (!command.showButton) continue;
            if (command.hotkey == HotkeySlot.None) continue;

            int slotIndex = GetSlotIndex(command.hotkey);

            FillSlot(slotIndex, button =>
            {
                button.InitializeFromUnitCommand(command, unit);
            });
        }
    }

    void ShowSquadCommands(SquadController squad)
    {
        HidePanel();

        if (squad == null) return;

        foreach (CommandData command in squadCommands)
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

// using System.Collections.Generic;
// using UnityEngine;
//
// public class ActionPanelUI : MonoBehaviour
// {
//     [SerializeField] private GameObject actionButtonPrefab;
//     [SerializeField] private int maxButtons = 15;
//
//     private List<ActionButtonUI> buttons = new List<ActionButtonUI>();
//     private UnitController currentUnit;
//     private bool inBuildSubmenu = false;
//
//     void Start()
//     {
//         for (int i = 0; i < maxButtons; i++)
//         {
//             GameObject btn = Instantiate(actionButtonPrefab, transform);
//             var cg = btn.AddComponent<CanvasGroup>();
//             cg.alpha = 0f;
//             cg.blocksRaycasts = false;
//             buttons.Add(btn.GetComponent<ActionButtonUI>());
//         }
//
//         GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
//     }
//
//     void OnDestroy()
//     {
//         GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
//     }
//
//     // Shows production options for a selected building
//     public void ShowProductionPanel(BuildingController building)
//     {
//         currentUnit = null;
//         inBuildSubmenu = false;
//         HidePanel();
//
//         if (building.Stats.baseData.productionOptions == null ||
//             building.Stats.baseData.productionOptions.Count == 0) return;
//
//         for (int i = 0; i < building.Stats.baseData.productionOptions.Count && i < maxButtons; i++)
//         {
//             int index = i; // capture local copy
//             FillSlot(index, btn => btn.InitializeFromProductionOption(building.Stats.baseData.productionOptions[index], building));
//         }    
//     }
//
//     // Shows command buttons for a selected unit
//     public void ShowUnitPanel(UnitController unit)
//     {
//         currentUnit = unit;
//         inBuildSubmenu = false;
//         ShowUnitCommands(unit);
//     }
//
//     // Shows build options submenu for villager
//     public void ShowBuildPanel(UnitController unit)
//     {
//         currentUnit = unit;
//         inBuildSubmenu = true;
//         HidePanel();
//
//         if (unit.Stats.baseData.buildOptions == null ||
//             unit.Stats.baseData.buildOptions.Count == 0) return;
//
//         foreach (BuildOptionData option in unit.Stats.baseData.buildOptions)
//         {
//             if (option.hotkey == HotkeySlot.None) continue;
//             int slotIndex = (int)option.hotkey - 1;
//             FillSlot(slotIndex, btn => btn.InitializeFromBuildOption(option, unit));
//         }
//     }
//     
//
//     // Returns to base unit commands from build submenu
//     public void ExitBuildPanel()
//     {
//         if (currentUnit == null) return;
//         inBuildSubmenu = false;
//         ShowUnitCommands(currentUnit);
//     }
//
//     public void HidePanel()
//     {
//         for (int i = 0; i < buttons.Count; i++)
//             HideSlot(i);
//     }
//
//     // Fills a specific slot with a button — generic for all button types
//     void FillSlot(int index, System.Action<ActionButtonUI> initialize)
//     {
//         if (index < 0 || index >= maxButtons) return;
//         initialize(buttons[index]);
//         ShowSlot(index);
//     }
//
//     // Shows base unit commands in hotkey slot positions
//     void ShowUnitCommands(UnitController unit)
//     {
//         HidePanel();
//         if (!unit.TryGetComponent(out CommandController cc)) return;
//
//         foreach (CommandData cmd in cc.GetAllCommands())
//         {
//             if (!cmd.showButton || cmd.hotkey == HotkeySlot.None) continue;
//             int slotIndex = (int)cmd.hotkey - 1;
//             FillSlot(slotIndex, btn => btn.InitializeFromCommand(cmd, unit));
//         }
//     }
//
//     void ShowSlot(int index)
//     {
//         var cg = buttons[index].GetComponent<CanvasGroup>();
//         cg.alpha = 1f;
//         cg.blocksRaycasts = true;
//     }
//
//     void HideSlot(int index)
//     {
//         if (!buttons[index]) // CHECK: started after squad control was implemented for the first time, possible connection? - sinlge button was referencing as null (implying it was destroyed first)
//         {
//             Debug.Log("Trying to access buttons[" + index + "] to hide slot but is NULL");
//             return;
//         }
//         
//         var cg = buttons[index].GetComponent<CanvasGroup>();
//         cg.alpha = 0f;
//         cg.blocksRaycasts = false;
//     }
//
//     void HandlePlacementModeChanged(bool isPlacing)
//     {
//         if (!isPlacing && inBuildSubmenu)
//             ExitBuildPanel();
//     }
// }