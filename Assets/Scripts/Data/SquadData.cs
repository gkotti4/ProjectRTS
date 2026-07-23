
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// -----------------------------------------------------------------------------
/// SquadData
/// -----------------------------------------------------------------------------
///
/// ScriptableObject blueprint for a squad type.
/// Stores squad identity, icon, category, prefab, soldier composition, default
/// stance/formation, movement profile, squad combat profile, morale data, and
/// available command set.
///
/// FormationCombat cleanup:
/// SoldierCombatProfile is no longer required because the old formation-combat /
/// old loose-combat soldier rhythm system has been removed.
///
[CreateAssetMenu(
    fileName = "SquadData_",
    menuName = "Scriptable Objects/Military/SquadData")]
public class SquadData : ScriptableObject
{
    [Header("Identity")]
    public string squadName = "Squad";
    public Sprite squadIcon;

    [Header("Classification")]
    [Tooltip("Nation/culture roster this unit belongs to. This is content classification, not runtime faction ownership.")]
    public NationData nation;

    [Tooltip("Broad battlefield role such as Infantry, Ranged, Cavalry, or Siege.")]
    public SquadCategory category = SquadCategory.Infantry;

    [Tooltip("Optional specialization beneath the broad category. Leave None until this unit needs it.")]
    public SquadCombatSubcategory combatSubcategory = SquadCombatSubcategory.None;

    [Tooltip("Lore/content families shared by related units across different combat roles, such as Marauder.")]
    public List<UnitFamilyData> unitFamilies = new List<UnitFamilyData>();

    [Tooltip("Mechanical traits used for upgrade targeting, such as Shielded, Shock, or AntiLarge.")]
    public UnitTrait unitTraits = UnitTrait.None;

    [FormerlySerializedAs("defaultCombatBehavior")]
    [FormerlySerializedAs("combatStyle")]
    [Header("Combat Behavior")]
    public SquadCombatStyle defaultCombatStyle = SquadCombatStyle.FormationCombat;

    [Header("Profiles")]
    public SquadMovementProfile movementProfile;
    public SquadCombatProfile squadCombatProfile;

    [Header("Prefab")]
    public SquadController squadPrefab;

    [Header("Soldiers")]
    public SoldierData soldierData;

    [FormerlySerializedAs("numMembers")]
    [Min(1)] public int startingSoldierCount = 15;

    [Tooltip("Future reinforcement/replenishment cap. Not used by current starting spawn logic.")]
    [Min(1)] public int maxSoldierCount = 50;

    [Header("Defaults")]
    public SquadFormation defaultFormation = SquadFormation.Line;
    public SquadStance defaultStance = SquadStance.Hold;

    [Header("Formation")]
    [Min(1)] public int defaultUnitsPerRow = 5;
    [Min(0.1f)] public float defaultSpacing = 2.5f;

    [Header("Morale")]
    public MoraleStats morale = MoraleStats.Default;

    [Header("Progression Defaults")]
    [Min(0)] public int reinforcementAmount = 1;
    [Min(0f)] public float reinforcementCostMultiplier = 1f;
    [Min(0)] public int officerSlots = 0;
    [Min(0)] public int specialistSlots = 0;
    // Banner carrier unit

    [Header("Formation Limits")]
    [Min(0.1f)] public float minimumSpacing = 0.5f;
    [Min(0.1f)] public float maximumSpacing = 5f;
    [Min(0f)] public float reformSpeedMultiplier = 1f;
    [Min(0f)] public float cohesionDistanceMultiplier = 1f;

    [Header("Commands")]
    public SquadCommandSet commandSet;

    public int ResolvedStartingSoldierCount =>
        Mathf.Max(1, startingSoldierCount);
}

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


