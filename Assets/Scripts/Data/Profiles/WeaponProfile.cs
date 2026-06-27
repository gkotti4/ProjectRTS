using UnityEngine;

public enum WeaponKind
{
    Melee,
    Ranged
}

[CreateAssetMenu(
    fileName = "WeaponProfile_",
    menuName = "Scriptable Objects/Military/WeaponProfile")]
public class WeaponProfile : ScriptableObject
{
    [Header("Identity")]
    public string weaponName = "Weapon";
    public WeaponKind weaponKind = WeaponKind.Melee;

    [Header("Melee Stats")]
    public MeleeCombatStats melee = MeleeCombatStats.Default;

    [Header("Ranged Stats")]
    public RangedCombatStats ranged = RangedCombatStats.Default;

    void OnValidate()
    {
        melee.meleeAttack = Mathf.Max(0, melee.meleeAttack);
        melee.weaponDamage = Mathf.Max(0, melee.weaponDamage);
        melee.armorPiercingDamage = Mathf.Max(0, melee.armorPiercingDamage);
        melee.attackInterval = Mathf.Max(0.05f, melee.attackInterval);
        melee.attackRange = Mathf.Max(0.1f, melee.attackRange);

        ranged.rangedAccuracy = Mathf.Max(0, ranged.rangedAccuracy);
        ranged.missileDamage = Mathf.Max(0, ranged.missileDamage);
        ranged.armorPiercingDamage = Mathf.Max(0, ranged.armorPiercingDamage);
        ranged.attackInterval = Mathf.Max(0.05f, ranged.attackInterval);
        ranged.attackRange = Mathf.Max(0.1f, ranged.attackRange);
        ranged.projectileSpeed = Mathf.Max(0.1f, ranged.projectileSpeed);
    }
}