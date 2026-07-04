using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierCombatProfile
/// -----------------------------------------------------------------------------
///
/// Legacy compatibility asset.
///
/// PrototypeMelee no longer reads SoldierCombatProfile. The old formation-combat /
/// old loose-combat recovery, pressure-waiting, old row-scoring, and target-crowding values
/// were intentionally removed from code. Existing assets can be deleted later.
///
[CreateAssetMenu(
    fileName = "SoldierCombatProfile_",
    menuName = "Scriptable Objects/Military/Profiles/SoldierCombatProfile")]
public class SoldierCombatProfile : ScriptableObject
{
}