using UnityEngine;

[CreateAssetMenu(
    fileName = "ArmorProfile_",
    menuName = "Scriptable Objects/Military/Profiles/ArmorProfile")]
public class ArmorProfile : ScriptableObject
{
    [Header("Identity")]
    public string armorName = "Armor";
    public Sprite icon;

    [Header("Stats")]
    public ArmorStats stats = ArmorStats.Default;

    [Header("Visual")]
    public GameObject visualPrefab;
}