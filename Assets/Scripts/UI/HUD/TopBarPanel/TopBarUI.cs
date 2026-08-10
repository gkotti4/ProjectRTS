using TMPro;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// TopBarUI
/// -----------------------------------------------------------------------------
///
/// Owns the static top-bar HUD presentation.
///
/// Current responsibilities:
/// - player resource display
/// - player population display
///
/// Battle timer, balance of power, menu, and game-speed controls can be added here
/// as their supporting gameplay systems are finalized.
///
public class TopBarUI : MonoBehaviour
{
    #region References

    [Header("Economy / Management")]
    [SerializeField] private GameObject economySection;
    [SerializeField] private TextMeshProUGUI resourceText;
    // [SerializeField] private TextMeshProUGUI populationText;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        GameEvents.OnResourcesChanged += HandleResourcesChanged;
        GameEvents.OnPopulationChanged += HandlePopulationChanged;

        RefreshEconomyDisplay();
    }

    void OnDestroy()
    {
        GameEvents.OnResourcesChanged -= HandleResourcesChanged;
        GameEvents.OnPopulationChanged -= HandlePopulationChanged;
    }

    #endregion

    #region Economy / Management

    public void SetEconomySectionVisible(bool visible)
    {
        if (economySection != null)
            economySection.SetActive(visible);
    }

    void HandleResourcesChanged(FactionInstance faction)
    {
        if (!IsPlayerFaction(faction))
            return;

        RefreshResourceText(faction);
    }

    void HandlePopulationChanged(FactionInstance faction)
    {
        if (!IsPlayerFaction(faction))
            return;

        // RefreshPopulationText(faction);
    }

    void RefreshEconomyDisplay()
    {
        if (GameManager.Instance == null || GameManager.Instance.PlayerFaction == null)
            return;

        FactionInstance playerFaction = GameManager.Instance.PlayerFaction;

        RefreshResourceText(playerFaction);
        // RefreshPopulationText(playerFaction);
    }

    void RefreshResourceText(FactionInstance faction)
    {
        if (resourceText == null || faction == null)
            return;

        resourceText.text =
            "Wood: " + faction.GetResources(ResourceType.Wood) +
            "   Food: " + faction.GetResources(ResourceType.Food) +
            "   Gold: " + faction.GetResources(ResourceType.Gold) +
            "   Stone: " + faction.GetResources(ResourceType.Stone);
    }

    // void RefreshPopulationText(FactionInstance faction)
    // {
    //     if (populationText == null || faction == null)
    //         return;
    //
    //     populationText.text =
    //         "Pop: " + faction.currentPopulation +
    //         " / " + faction.populationCap;
    // }

    bool IsPlayerFaction(FactionInstance faction)
    {
        return faction != null &&
               GameManager.Instance != null &&
               faction == GameManager.Instance.PlayerFaction;
    }

    #endregion
}
