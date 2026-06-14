using TMPro;
using UnityEngine;

public class InfoPanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI entityText; // or IdentityText
    //[SerializeField] private TextMeshProUGUI healthText; // check later if needed or combined in identity
    
    void Start()
    {
        GameEvents.OnSelectionChanged += HandleSelectionChanged;
        GameEvents.OnDeselected += Hide;
        Hide();
    }

    void OnDestroy()
    {
        GameEvents.OnSelectionChanged -= HandleSelectionChanged;
        GameEvents.OnDeselected -= Hide;
        
    }

    void HandleSelectionChanged()
    {
        var selected = SelectionManager.Instance.GetSelectedObjects();
        
        if (selected.Count != 1) { Hide();  return; } // Only show when 1 unit selected (handle group text later)

        if (selected[0] is UnitController unit)
        {
            Show(unit.Stats);
            return;
        }

        Hide();
    }

    void Show(EntityStats stats)
    {
        gameObject.SetActive(true);
        string txt = 
                "Name: " + stats.baseDetails.entityName + "\n" +
                "ID: " + stats.baseDetails.GetEntityId().ToString() + "\n" +
                "Health: " + stats.CurrentHealth.ToString();
        
        entityText.text = txt;
    }

    void Hide()
    {
        gameObject.SetActive(false);
    }
}
