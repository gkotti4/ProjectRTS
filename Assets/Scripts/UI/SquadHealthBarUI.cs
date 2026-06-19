using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SquadHealthBarUI : MonoBehaviour
{
    [SerializeField] private SquadHealth squadHealth;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI countLabel;

    void Awake()
    {
        if (squadHealth == null)
            squadHealth = GetComponentInParent<SquadHealth>();
    }

    void OnEnable()
    {
        if (squadHealth != null)
            squadHealth.OnSquadHealthChanged += HandleHealthChanged;

        Refresh();
    }

    void OnDisable()
    {
        if (squadHealth != null)
            squadHealth.OnSquadHealthChanged -= HandleHealthChanged;
    }

    void HandleHealthChanged(SquadHealth health)
    {
        Refresh();
    }

    void Refresh()
    {
        if (squadHealth == null)
            return;

        if (healthSlider != null)
            healthSlider.value = squadHealth.HealthPercent;

        if (countLabel != null)
            countLabel.text = $"{squadHealth.LivingSoldiers}/{squadHealth.TotalSoldiers}";
    }
}