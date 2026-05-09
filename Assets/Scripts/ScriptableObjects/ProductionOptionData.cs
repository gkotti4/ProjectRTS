using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ProductionOptionSO", menuName = "Scriptable Objects/ProductionOptionSO")]
public class ProductionOptionData : ScriptableObject
{
    public string productionName = "Production";
    public Sprite icon;
    public float productionTime = 0f;
    public ResourceCost cost = new ResourceCost();
    
    public ProductionType productionType; // Unit or Upgrade 
    
    // Unit Production
    public GameObject prefab;
    public UnitType unitType;
    
    // Upgrade Production
    public UpgradeData upgradeData;
}
