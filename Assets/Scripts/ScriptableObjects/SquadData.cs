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
    public SquadController squadPrefab; // CHECK: GameObject type?
    public SquadMemberController memberPrefab;
    
    [Header("Members")] 
    [Min(1)] public int startingMemberCount = 5;
        
    [Header("Formation and Stance")]
    public SquadFormation defaultFormation = SquadFormation.Line;
    public SquadStance defaultStance = SquadStance.Aggressive;
    
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