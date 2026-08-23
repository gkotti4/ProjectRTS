using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BuildingData_",
    menuName = "Scriptable Objects/Buildings/BuildingData")]
public class BuildingData : ScriptableObject
{
    [Header("Identity")]
    public string buildingName = "Building";
    public Sprite icon;
    public BuildingCategory category;

    [Header("Prefab / Placement")]
    public GameObject prefab;
    public GameObject ghostPrefab;
    [Min(1)] public int gridWidth = 2;
    [Min(1)] public int gridHeight = 2;

    [Header("Stats")]
    public HealthStats health = HealthStats.Default;
    public ProductionStats production = ProductionStats.Default;

    [Header("Production")]
    public List<ProductionOptionData> productionOptions = new List<ProductionOptionData>();

    [Header("Commands")]
    public List<CommandData> commands = new List<CommandData>();
}