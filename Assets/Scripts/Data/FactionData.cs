using UnityEngine;

[CreateAssetMenu(fileName = "FactionData", menuName = "Scriptable Objects/FactionData")]
public class FactionData : ScriptableObject
{
    [Header("Identity")]
    public string factionName = "Faction";
    public int factionId = 0; // Factions start at 1,

    [Header("Visuals (not used currently)")] 
    public Color factionColor = Color.white;
    
    public Material unitMaterial;
    public Material buildingMaterial;
}
