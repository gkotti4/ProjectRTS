using TMPro;
using UnityEngine;

public class PopulationUI : MonoBehaviour
{
    private TextMeshProUGUI populationText;

    void Awake()
    {
        populationText = GetComponent<TextMeshProUGUI>();
    }

    void Start()
    {
        GameEvents.OnPopulationChanged += UpdatePopulationText;
        //UpdatePopulationText(GameManager.Instance.PlayerFaction);
    }

    void OnDestroy()
    {
        GameEvents.OnPopulationChanged -= UpdatePopulationText;
    }

    void UpdatePopulationText(FactionInstance f)
    {
        // FactionInstance f = GameManager.Instance.PlayerFaction; OLD
        // Updated:
        if (f != GameManager.Instance.PlayerFaction) return;
        populationText.text = "Pop: " + f.currentPopulation + " / " + f.populationCap;
    }
}
