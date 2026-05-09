using UnityEngine;
using TMPro;

public class ResourceUI : MonoBehaviour
{
    private TextMeshProUGUI resourceText;

    void Start()
    {
        resourceText = GetComponent<TextMeshProUGUI>();
        GameManager.Instance.OnResourcesChanged += UpdateResourceText;
    }

    void OnDestroy()
    {
        GameManager.Instance.OnResourcesChanged -= UpdateResourceText;
    }
    
    void UpdateResourceText()
    {
        resourceText.text = "Wood: " + GameManager.Instance.GetCurrentResources(ResourceType.Wood) +
                            "   Food: " + GameManager.Instance.GetCurrentResources(ResourceType.Food) +
                            "   Gold: " + GameManager.Instance.GetCurrentResources(ResourceType.Gold) +
                            "   Stone: " + GameManager.Instance.GetCurrentResources(ResourceType.Stone);
        
        // Create event system for this later. Not in Update.
    }
}
