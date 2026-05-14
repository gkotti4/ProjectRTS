using UnityEngine;

[CreateAssetMenu(fileName = "FactionData", menuName = "Scriptable Objects/FactionData")]
public class FactionData : ScriptableObject
{
    [Header("Identity")]
    public string factionName = "Faction";
    [Tooltip("Unique faction identity. 0 = Neutral/Gaia. 1+ = named factions.")]
    public int factionId = 0;
    [Tooltip("0 = Neutral. 1+ = Teams. Units on the same team are allies.")]
    public int teamId = 0;
    public Color factionColor = Color.white;
    public bool isPlayerControlled = true;

    [Header("Visuals (not used currently)")] 
    public Material unitMaterial;
    public Material buildingMaterial;
}
