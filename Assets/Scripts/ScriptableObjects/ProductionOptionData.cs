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

    [Header("Squad (Military)")] // Military
    public bool producesSquad;
    public GameObject squadPrefab;
    public GameObject memberPrefab;
    public int squadMemberCount = 1;
    public SquadFormation startingFormation = SquadFormation.Line;
    public CombatStance startingStance = CombatStance.Aggressive;
    
    // Unit Production
    [Header("Unit Production")]
    public GameObject prefab;
    //public UnitType unitType;
    
    // Upgrade Production
    [Header("Upgrade Production")]
    public UpgradeData upgradeData;
    
    [Header("Hotkey")]
    public HotkeySlot hotkey;
}
