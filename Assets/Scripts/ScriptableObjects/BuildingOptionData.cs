using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BuildingOptionData", menuName = "Scriptable Objects/BuildingOptionData")]
public class BuildingOptionData : ScriptableObject
{
    public string buildingName;
    public EntityData buildingData;
    public Sprite icon;
    public HotkeySlot hotkey;
}
