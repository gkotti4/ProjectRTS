using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ProductionOptionData", menuName = "Scriptable Objects/ProductionOption")]
public class ProductionOptionData : ScriptableObject
{
    [Header("Basic")]
    public string productionName = "Production";
    public Sprite icon;
    public float productionTime = 0f;
    public ResourceCost cost = new ResourceCost();
    
    public ProductionType productionType; // Unit or Upgrade 

    [Header("Squad Production")] // Military
    public SquadData squadData;
    
    [Header("Unit Production")]
    public UnitController unitPrefab; // just for villagers for now
    
    [Header("Upgrade Production")]
    public UpgradeData upgradeData;
    
    [Header("Hotkey")]
    public HotkeySlot hotkey;
}
