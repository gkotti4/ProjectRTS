using UnityEngine;

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
    public MeleeCombatStats melee = MeleeCombatStats.Default;

    [Header("Visuals")]
    public RuntimeAnimatorController animatorController;
}