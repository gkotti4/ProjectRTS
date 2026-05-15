using UnityEngine;
using TMPro;

public class ResourceUI : MonoBehaviour
{
    private TextMeshProUGUI resourceText;

    void Awake()
    {
        resourceText = GetComponent<TextMeshProUGUI>();
    }

    void Start()
    {
        GameEvents.OnResourcesChanged += UpdateResourceText;
        UpdateResourceText(GameManager.Instance.PlayerFaction);
    }

    void OnDestroy()
    {
        GameEvents.OnResourcesChanged -= UpdateResourceText;
    }

    void UpdateResourceText(FactionInstance f)
    {
        // FactionInstance f = GameManager.Instance.PlayerFaction; OLD
        if (f != GameManager.Instance.PlayerFaction) return;
        resourceText.text =
            "Wood: " + f.GetResources(ResourceType.Wood) +
            "   Food: " + f.GetResources(ResourceType.Food) +
            "   Gold: " + f.GetResources(ResourceType.Gold) +
            "   Stone: " + f.GetResources(ResourceType.Stone);
    }
}
