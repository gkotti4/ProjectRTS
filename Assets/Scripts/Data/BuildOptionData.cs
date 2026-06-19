using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "BuildOptionData_",
    menuName = "Scriptable Objects/Buildings/BuildOptionData")]
public class BuildOptionData : ScriptableObject
{
    [Header("Building")]
    [FormerlySerializedAs("buildingDetails")]
    public BuildingData buildingData;

    [Header("UI")]
    public string buildingName = "Building";
    public Sprite icon;
    public HotkeySlot hotkey = HotkeySlot.None;

    [Header("Cost")]
    public ResourceCost cost;

    public GameObject Prefab => buildingData != null ? buildingData.prefab : null;
    public GameObject GhostPrefab => buildingData != null ? buildingData.ghostPrefab : null;

    public int GridWidth => buildingData != null ? buildingData.gridWidth : 1;
    public int GridHeight => buildingData != null ? buildingData.gridHeight : 1;

    void OnValidate()
    {
        if (buildingData == null)
            return;

        if (string.IsNullOrWhiteSpace(buildingName) || buildingName == "Building")
            buildingName = buildingData.buildingName;

        if (icon == null)
            icon = buildingData.icon;
    }
}