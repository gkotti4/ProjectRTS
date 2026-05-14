using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]

public class ActionButtonUI : MonoBehaviour
{
    private ProductionOptionData optionData;
    private BuildingController targetBuilding;
    private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Color affordableColor = Color.darkGray;
    [SerializeField] private Color unaffordableColor = Color.red;

    private bool canAfford = false;

    void Awake()
    {
        button = GetComponent<Button>();         
        button.onClick.AddListener(OnClick);
    }
    
    void Start()
    {
        //GameManager.Instance.OnResourcesChanged += UpdateAffordability;
        GameEvents.OnResourcesChanged += UpdateAffordability;
    }

    private void OnDestroy()
    {
        //GameManager.Instance.OnResourcesChanged -= UpdateAffordability;
        GameEvents.OnResourcesChanged -= UpdateAffordability;
    }

    public void Initialize(ProductionOptionData data, BuildingController building)
    {
        this.optionData = data;
        this.targetBuilding = building;
        
        if (data.icon != null)
            iconImage.sprite = data.icon;
        
        canAfford = GameManager.Instance.CanAfford(optionData.cost);
        button.image.color = canAfford ? affordableColor : unaffordableColor;
        //iconImage.color
    }
    
    public void InitializeFromCommand(Sprite icon, string name, HotkeySlot hotkey) // NEW
    {
        optionData = null;
        targetBuilding = null;
        if (icon != null) iconImage.sprite = icon;
        // hotkey tooltip later
    }

    void OnClick()
    {
        if (targetBuilding == null || optionData == null) return;
        if (canAfford)
            targetBuilding.EnqueueProduction(optionData);
    }

    void UpdateAffordability()
    {
        if (optionData == null || !gameObject.activeSelf) return;
        canAfford = GameManager.Instance.CanAfford(optionData.cost);
        button.image.color = canAfford ? affordableColor : unaffordableColor;
    }
    
    
    
}
