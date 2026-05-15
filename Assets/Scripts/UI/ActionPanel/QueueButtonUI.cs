using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class QueueButtonUI : MonoBehaviour
{
    [SerializeField] private Image progressFill;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI label;
    private Button button;

    private ProductionOptionData optionData;
    private BuildingController targetBuilding;
    private int queueIndex;

    private Action onCancelled;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }
    

    public void Initialize(ProductionOptionData data, BuildingController building, int index, Action onCancelled) // Initializes slot with queue item data - called from panel
    {
        optionData = data;
        targetBuilding = building;
        queueIndex = index;
        
        iconImage.sprite = optionData.icon;
        
        // Only slot 0 shows progress
        if (progressFill != null)
            progressFill.gameObject.SetActive(index==0);
        
        if (progressFill != null && index == 0)
            progressFill.fillAmount = 0f;

        // Text Label
        if (label != null)
            label.text = data.productionName;
        
        this.onCancelled = onCancelled;
    }

    public void UpdateProgress(float progress) // Updates progress fill on slot 0 - called every frame by QueuePanelUI
    {
        if (progressFill != null)
            progressFill.fillAmount = progress;
    }


    void OnClick()
    {
        if (targetBuilding == null) return;
        targetBuilding.CancelProduction(queueIndex);
        onCancelled?.Invoke();
    }
}
