using System.Collections.Generic;
using UnityEngine;

public class BuildingPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject productionButtonPrefab;
    [SerializeField] private int maxButtons = 9;
    
    private List<ProductionButton> buttons = new List<ProductionButton>();
    
    void Start()
    {
        // Pre-spawn all buttons, hide them
        for (int i = 0; i < maxButtons; i++)
        {
            GameObject btn = Instantiate(productionButtonPrefab, transform);
            buttons.Add(btn.GetComponent<ProductionButton>());
            btn.SetActive(false);
        }
    }

    public void ShowBuildingButtons(BuildingController building)
    {
        HideAll();
        if (building.Stats.baseData.productionOptions == null) return;

        for (int i = 0; i < building.Stats.baseData.productionOptions.Count && i < maxButtons; i++)
        {
            buttons[i].Initialize(building.Stats.baseData.productionOptions[i], building);
            buttons[i].gameObject.SetActive(true); 
        }
    }
    
    public void HideAll()
    {
        foreach (ProductionButton btn in buttons)
            btn.gameObject.SetActive(false);
    }
}
