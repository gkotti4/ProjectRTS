using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BuildOptionData", menuName = "Scriptable Objects/BuildOptionData")]
public class BuildOptionData : ScriptableObject
{
    public string buildingName = "Building";
    [FormerlySerializedAs("buildingData")] public EntityDetails buildingDetails;
    public BuildingController buildingPrefab; 
    public Sprite icon;
    public HotkeySlot hotkey = HotkeySlot.None;
    public ResourceCost cost;
}
