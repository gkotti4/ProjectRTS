using System.Collections.Generic;
using UnityEngine;

public class ActionPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject actionButtonPrefab;
    [SerializeField] private int maxButtons = 15;

    private List<ActionButtonUI> buttons = new List<ActionButtonUI>();
    private UnitController currentUnit;
    private bool inBuildSubmenu = false;

    void Start()
    {
        for (int i = 0; i < maxButtons; i++)
        {
            GameObject btn = Instantiate(actionButtonPrefab, transform);
            var cg = btn.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            buttons.Add(btn.GetComponent<ActionButtonUI>());
        }

        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
    }

    void OnDestroy()
    {
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
    }

    // Shows production options for a selected building
    public void ShowProductionPanel(BuildingController building)
    {
        currentUnit = null;
        inBuildSubmenu = false;
        HidePanel();

        if (building.Stats.baseData.productionOptions == null ||
            building.Stats.baseData.productionOptions.Count == 0) return;

        for (int i = 0; i < building.Stats.baseData.productionOptions.Count && i < maxButtons; i++)
        {
            int index = i; // capture local copy
            FillSlot(index, btn => btn.InitializeFromProductionOption(building.Stats.baseData.productionOptions[index], building));
        }    
    }

    // Shows command buttons for a selected unit
    public void ShowUnitPanel(UnitController unit)
    {
        currentUnit = unit;
        inBuildSubmenu = false;
        ShowUnitCommands(unit);
    }

    // Shows build options submenu for villager
    public void ShowBuildPanel(UnitController unit)
    {
        currentUnit = unit;
        inBuildSubmenu = true;
        HidePanel();

        if (unit.Stats.baseData.buildOptions == null ||
            unit.Stats.baseData.buildOptions.Count == 0) return;

        foreach (BuildOptionData option in unit.Stats.baseData.buildOptions)
        {
            if (option.hotkey == HotkeySlot.None) continue;
            int slotIndex = (int)option.hotkey - 1;
            FillSlot(slotIndex, btn => btn.InitializeFromBuildOption(option, unit));
        }
    }
    

    // Returns to base unit commands from build submenu
    public void ExitBuildPanel()
    {
        if (currentUnit == null) return;
        inBuildSubmenu = false;
        ShowUnitCommands(currentUnit);
    }

    public void HidePanel()
    {
        for (int i = 0; i < buttons.Count; i++)
            HideSlot(i);
    }

    // Fills a specific slot with a button — generic for all button types
    void FillSlot(int index, System.Action<ActionButtonUI> initialize)
    {
        if (index < 0 || index >= maxButtons) return;
        initialize(buttons[index]);
        ShowSlot(index);
    }

    // Shows base unit commands in hotkey slot positions
    void ShowUnitCommands(UnitController unit)
    {
        HidePanel();
        if (!unit.TryGetComponent(out CommandController cc)) return;

        foreach (CommandData cmd in cc.GetAllCommands())
        {
            if (!cmd.showButton || cmd.hotkey == HotkeySlot.None) continue;
            int slotIndex = (int)cmd.hotkey - 1;
            FillSlot(slotIndex, btn => btn.InitializeFromCommand(cmd, unit));
        }
    }

    void ShowSlot(int index)
    {
        var cg = buttons[index].GetComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
    }

    void HideSlot(int index)
    {
        var cg = buttons[index].GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
    }

    void HandlePlacementModeChanged(bool isPlacing)
    {
        if (!isPlacing && inBuildSubmenu)
            ExitBuildPanel();
    }
}