using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierData
/// -----------------------------------------------------------------------------
///
/// ScriptableObject blueprint for an individual soldier type.
/// Stores identity, icon, prefab reference, health stats, movement stats, defense stats,
/// and a weapon profile reference.
///
/// This data is used by SquadRoster when spawning soldiers and by soldier systems
/// when initializing runtime stats.
///
/// Design role:
/// Designer-facing soldier stat/prefab data.
///

[CreateAssetMenu(
    fileName = "SoldierData_",
    menuName = "Scriptable Objects/Military/SoldierData")]
public class SoldierData : ScriptableObject
{
    [Header("Identity")]
    public string soldierName = "Soldier";
    public Sprite icon;

    [Header("Prefab")]
    public SoldierController prefab;

    [Header("Stats")]
    public HealthStats health = HealthStats.Default;
    public MovementStats movement = MovementStats.Default;
    public BodyStats body = BodyStats.Default;
    public CombatDefenseStats defense = CombatDefenseStats.Default;

    [Header("Weapon")]
    [Tooltip("Required for combat. WeaponProfile is the source of truth for attack stats, timing, range, and projectile data.")]
    public WeaponProfile weaponProfile;

    [Header("Armor")]
    public ArmorProfile armorProfile;
    
}