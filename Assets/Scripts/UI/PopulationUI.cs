using TMPro;
using UnityEngine;

public class PopulationUI : MonoBehaviour
{
    private TextMeshProUGUI populationText;
    
    void Start()
    {
        populationText = GetComponent<TextMeshProUGUI>();
        GameManager.Instance.OnPopulationChanged += UpdatePopulationText;
    }

    void OnDestroy()
    {
        GameManager.Instance.OnPopulationChanged -= UpdatePopulationText;
    }

    void UpdatePopulationText()
    {
        populationText.text = "Pop: " + GameManager.Instance.GetCurrentPopulation() + 
                              "  /  " + GameManager.Instance.GetPopulationCap();
    }
    
}
