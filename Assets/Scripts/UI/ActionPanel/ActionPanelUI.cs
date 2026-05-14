using System.Collections.Generic;
using UnityEngine;

public class ActionPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject productionButtonPrefab;
    [SerializeField] private int maxButtons = 15;
    
    private List<ActionButtonUI> buttons = new List<ActionButtonUI>();
    
    void Start()
    {
        // Pre-spawn all buttons, hide them
        for (int i = 0; i < maxButtons; i++)
        {
            GameObject btn = Instantiate(productionButtonPrefab, transform);
            buttons.Add(btn.GetComponent<ActionButtonUI>());
            btn.SetActive(false);
        }
    }

    // Shows production buttons for a selected building
    public void ShowPanel(BuildingController building)
    {
        HidePanel();
        if (building.Stats.baseData.productionOptions == null) return;

        for (int i = 0; i < building.Stats.baseData.productionOptions.Count && i < maxButtons; i++)
        {
            buttons[i].Initialize(building.Stats.baseData.productionOptions[i], building);
            buttons[i].gameObject.SetActive(true);
        }
    }

    // Shows command buttons for a selected unit
    public void ShowUnitButtons(UnitController unit)
    {
        HidePanel();
        if (!unit.TryGetComponent(out CommandController commandController)) return;

        var commands = commandController.GetAllCommands();
        int index = 0;

        foreach (var cmd in commands)
        {
            if (!cmd.showButton) continue;
            buttons[index].InitializeFromCommand(cmd.icon, cmd.name, cmd.hotkey);
            buttons[index].gameObject.SetActive(true);
            index++;
            if (index >= maxButtons) break;
        }
    }

    public void HidePanel()
    {
        foreach (ActionButtonUI btn in buttons)
            btn.gameObject.SetActive(false);
    }
}