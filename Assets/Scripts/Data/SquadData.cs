using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "SquadData_",
    menuName = "Scriptable Objects/Military/SquadData")]
public class SquadData : ScriptableObject
{
    [Header("Identity")]
    public string squadName = "Squad";
    public Sprite squadIcon;
    public SquadCategory category;
    
    [Header("Profiles")]
    public SquadMovementProfile movementProfile;
    public SquadCombatProfile squadCombatProfile;
    public SoldierCombatProfile soldierCombatProfile;

    [Header("Prefab")]
    public SquadController squadPrefab;

    [Header("Soldiers")]
    public SoldierData soldierData;

    [FormerlySerializedAs("numMembers")]
    [Min(1)] public int startingSoldierCount = 5;

    [Min(1)] public int maxSoldierCount = 50;

    [Header("Defaults")]
    public SquadFormation defaultFormation = SquadFormation.Line;
    public SquadStance defaultStance = SquadStance.Defensive;

    [Header("Formation")]
    [Min(1)] public int defaultUnitsPerRow = 10;
    [Min(0.1f)] public float defaultSpacing = 2f;

    [Header("Movement")]
    public MovementStats movement = MovementStats.Default;

    [Header("Melee Combat (Fallback)")]
    [Tooltip("DEBUGGING PURPOSES, Fallback for unit melee stats.")]
    public MeleeCombatStats melee = MeleeCombatStats.Default;

    [Header("Morale")]
    public MoraleStats morale = MoraleStats.Default;

    [Header("Combat Behavior")]
    [Min(0f)] public float aggressiveAutoScanRange = 14f;
    [Min(0f)] public float defensiveAutoScanRange = 8f;
    [Min(0f)] public float standGroundScanPadding = 0.5f;
    [Min(0f)] public float combatDefensiveLeashRange = 8f;

    [Header("Commands")]
    public SquadCommandSet commandSet;

    public int ResolvedStartingSoldierCount =>
        Mathf.Max(1, startingSoldierCount);
}

/// -----------------------------------------------------------------------------
/// SquadData
/// -----------------------------------------------------------------------------
///
/// ScriptableObject blueprint for a squad type.
/// Stores squad identity, icon, category, prefab, soldier composition, default
/// stance/formation, movement profile, squad combat profile, soldier combat profile,
/// fallback stats, morale data, and available command set.
///
/// This data defines what a squad is. Runtime systems read this data but should
/// keep behavior logic in components rather than hardcoding unit-specific behavior.
///
/// Design role:
/// Designer-facing squad definition and profile hub.
///

[CreateAssetMenu(
    fileName = "SquadCommandSet_",
    menuName = "Scriptable Objects/Military/SquadCommandSet")]
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








