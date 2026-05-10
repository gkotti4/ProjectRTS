using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeData", menuName = "Scriptable Objects/Upgrade")]
public class UpgradeData : ScriptableObject
{
    public string upgradeName;
    public int upgradeID;
    public ResourceCost cost;
    public float researchTime;
    public Sprite icon;
    
    public UpgradeType upgradeType;
    
    // Global
    [Header("Global Upgrade")]
    public EntityTag[] affectedTags; // Infantry, Archer, Villager, MilitaryBuilding, AllUnits
    public StatModifier[] statModifiers; // +attack, +speed, +health, etc.
    
    // Unit evolution
    [Header("Unit Evolution Upgrade")]
    public EntityData fromUnit;
    public EntityData toUnit;
    
    // Prerequisites (later)
    // public UpgradeSO[] prerequisites;
    // public BuildingType requiredBuilding;
}
