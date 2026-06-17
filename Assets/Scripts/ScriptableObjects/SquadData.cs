using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "SquadData_", menuName = "Scriptable Objects/SquadData")]
public class SquadData : ScriptableObject
{
    [Header("Identity")] 
    public string squadName = "Squad";
    public Sprite squadIcon;
    public SquadCategory category;

    [Header("Prefabs")]
    [Tooltip("The squad gameObject/prefab containing SquadController")]
    public SquadController squadPrefab;
    [Tooltip("The squad member gameObject/prefab containing SquadMemberController")]
    public SquadMemberController memberPrefab;
    
    [Header("Members")] 
    [Min(1)] public int startingMemberCount = 5;
        
    [Header("Formation")]
    public SquadFormation defaultFormation = SquadFormation.Line;
    [Min(1)] public int defaultUnitsPerRow = 10;
    [Min(1)] public float defaultSpacing = 2f;
    
    [Header("Stance")]
    public SquadStance defaultStance = SquadStance.Aggressive;
    
    [Header("Combat Behavior")]
    [Space(10), Header("Auto Combat Scan Ranges")]
    [Min(0)] public float aggressiveAutoScanRange = 14f;
    [Min(0)] public float defensiveAutoScanRange = 8f;
    [Min(0)] public float standGroundScanPadding = 0.5f;
    [Space(10), Header("Combat Leash Ranges")]
    [Min(0)] public float combatDefensiveLeashRange = 8f;
    
    [Header("Commands")]
    public SquadCommandSet commandSet;
}


[CreateAssetMenu(fileName = "SquadCommandSet_", menuName = "Scriptable Objects/SquadCommandSet")]
public class SquadCommandSet : ScriptableObject
{
    [Header("Core")]
    public List<CommandData> coreCommands = new List<CommandData>();

    [Header("Stances")]
    public List<CommandData> stanceCommands = new List<CommandData>();

    [Header("Formations")]
    public List<CommandData> formationCommands = new List<CommandData>();

    [Header("Abilities")]
    public List<CommandData> abilityCommands = new List<CommandData>();

    public List<CommandData> GetAllCommands()
    {
        List<CommandData> result = new List<CommandData>();

        result.AddRange(coreCommands);
        result.AddRange(stanceCommands);
        result.AddRange(formationCommands);
        result.AddRange(abilityCommands);

        return result;
    }
}