using UnityEngine;

[CreateAssetMenu(
    fileName = "UpgradeData_",
    menuName = "Scriptable Objects/Upgrades/UpgradeData")]
public class UpgradeData : ScriptableObject
{
    [Header("Identity")]
    public string upgradeName = "Upgrade";
    public int upgradeID;
    public Sprite icon;

    [Header("Research")]
    public ResourceCost cost;
    public float researchTime = 1f;

    [Header("Targeting")]
    // public UpgradeType upgradeType;
    public SquadCategory[] affectedSquadCategories;
    public BuildingCategory[] affectedBuildingCategories;

    [Header("Stat Modifiers")]
    public SoldierStatModifiers soldierModifiers;
    public SquadStatModifiers squadModifiers;

    [Header("Future Evolution / Replacement")]
    public SoldierData fromSoldier;
    public SoldierData toSoldier;
    public SquadData fromSquad;
    public SquadData toSquad;
}