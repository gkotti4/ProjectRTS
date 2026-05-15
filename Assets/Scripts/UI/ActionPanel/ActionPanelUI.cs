using System.Collections.Generic;
using UnityEngine;

public class ActionPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject productionButtonPrefab;
    [SerializeField] private int maxButtons = 15;

    private List<ActionButtonUI> buttons = new List<ActionButtonUI>();
    private UnitController currentUnit;
    private bool inBuildSubmenu = false;

    void Start()
    {
        // Pre-spawn all buttons, hide them
        for (int i = 0; i < maxButtons; i++)
        {
            GameObject btn = Instantiate(productionButtonPrefab, transform);
            buttons.Add(btn.GetComponent<ActionButtonUI>());
            btn.SetActive(false);
        }

        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
    }

    void OnDestroy()
    {
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
    }

    // Shows production buttons for a selected building
    public void ShowPanel(BuildingController building)
    {
        currentUnit = null;
        inBuildSubmenu = false;
        HidePanel();
        if (building.Stats.baseData.productionOptions == null ||
            building.Stats.baseData.productionOptions.Count == 0) return;

        for (int i = 0; i < building.Stats.baseData.productionOptions.Count && i < maxButtons; i++)
        {
            buttons[i].InitializeFromProductionOption(building.Stats.baseData.productionOptions[i], building);
            buttons[i].gameObject.SetActive(true);
        }
    }

    // Shows base command buttons for a selected unit
    public void ShowUnitButtons(UnitController unit)
    {
        currentUnit = unit;
        inBuildSubmenu = false;
        ShowBaseCommands(unit);
    }

    // Shows build submenu for villager — called by Build command button click
    public void ShowBuildSubmenu(UnitController unit)
    {
        currentUnit = unit;
        inBuildSubmenu = true;
        HidePanel();

        if (unit.Stats.baseData.buildOptions == null ||
            unit.Stats.baseData.buildOptions.Count == 0) return;

        int index = 0;
        foreach (BuildingOptionData option in unit.Stats.baseData.buildOptions)
        {
            if (index >= maxButtons) break;
            buttons[index].InitializeFromBuildOption(option, unit);
            buttons[index].gameObject.SetActive(true);
            index++;
        }
    }

    // Returns to base commands from build submenu
    public void ExitBuildSubmenu()
    {
        if (currentUnit == null) return;
        inBuildSubmenu = false;
        ShowBaseCommands(currentUnit);
    }

    public void HidePanel()
    {
        foreach (ActionButtonUI btn in buttons)
            btn.gameObject.SetActive(false);
    }

    // Shows base unit commands
    void ShowBaseCommands(UnitController unit)
    {
        HidePanel();
        if (!unit.TryGetComponent(out CommandController cc)) return;

        List<CommandData> commands = cc.GetAllCommands();
        int index = 0;

        foreach (CommandData cmd in commands)
        {
            if (!cmd.showButton) continue;
            buttons[index].InitializeFromCommand(cmd, unit);
            buttons[index].gameObject.SetActive(true);
            index++;
            if (index >= maxButtons) break;
        }
    }

    // Exit build submenu when placement is cancelled or confirmed
    void HandlePlacementModeChanged(bool isPlacing)
    {
        if (!isPlacing && inBuildSubmenu)
            ExitBuildSubmenu();
    }
}