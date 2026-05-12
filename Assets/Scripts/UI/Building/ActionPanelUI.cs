using System.Collections.Generic;
using UnityEngine;

public class ActionPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject productionButtonPrefab;
    [SerializeField] private int maxButtons = 15; // hotkey rows - q w e r t, a s d f g, z x c v b 
    
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
    
    public void HidePanel()
    {
        foreach (ActionButtonUI btn in buttons)
            btn.gameObject.SetActive(false);
    }
}
