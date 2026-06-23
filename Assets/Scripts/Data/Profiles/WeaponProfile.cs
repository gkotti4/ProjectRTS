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

    [Header("Damage")]
    [Min(0)] public int damage = 20;
    [Min(0)] public int armorPiercingDamage = 0;

    [Header("Timing / Range")]
    [Min(0.05f)] public float attackInterval = 1.5f;
    [Min(0.1f)] public float attackRange = 1.5f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    [Min(0.1f)] public float projectileSpeed = 18f;
}