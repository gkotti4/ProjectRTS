using System.Collections.Generic;
using UnityEngine;

public enum WeaponKind
{
    Melee,
    Ranged
}

public enum WeaponSlot
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

    [Header("Runtime Effects")]
    [Tooltip("Effects this weapon has before upgrades add or remove effects.")]
    public List<WeaponEffectData> baseWeaponEffects = new List<WeaponEffectData>();

    [Header("Animation")]
    [Tooltip("Optional weapon-owned idle/attack/upper-body animation package.")]
    public WeaponAnimationProfile animationProfile;

    [Header("Presentation")]
    [Tooltip("Optional child visual spawned by SoldierEquipmentController when this weapon is resolved.")]
    public GameObject weaponVisualPrefab;

    void OnValidate()
    {
        melee.meleeAttack = Mathf.Max(0, melee.meleeAttack);
        melee.weaponDamage = Mathf.Max(0, melee.weaponDamage);
        melee.armorPiercingDamage = Mathf.Max(0, melee.armorPiercingDamage);
        melee.attackInterval = Mathf.Max(0.05f, melee.attackInterval);
        melee.attackRange = Mathf.Max(0.1f, melee.attackRange);
        melee.criticalHitChance = Mathf.Clamp01(melee.criticalHitChance);
        melee.criticalHitDamageMultiplier = Mathf.Max(1f, melee.criticalHitDamageMultiplier);
        melee.knockdownChance = Mathf.Clamp01(melee.knockdownChance);

        ranged.rangedAccuracy = Mathf.Max(0, ranged.rangedAccuracy);
        ranged.missileDamage = Mathf.Max(0, ranged.missileDamage);
        ranged.armorPiercingDamage = Mathf.Max(0, ranged.armorPiercingDamage);
        ranged.attackInterval = Mathf.Max(0.05f, ranged.attackInterval);
        ranged.attackRange = Mathf.Max(0.1f, ranged.attackRange);
        ranged.minimumRange = Mathf.Clamp(ranged.minimumRange, 0f, ranged.attackRange);
        ranged.criticalHitChance = Mathf.Clamp01(ranged.criticalHitChance);
        ranged.criticalHitDamageMultiplier = Mathf.Max(1f, ranged.criticalHitDamageMultiplier);
        ranged.projectileSpeed = Mathf.Max(0.1f, ranged.projectileSpeed);
        ranged.ammunition = Mathf.Max(-1, ranged.ammunition);
    }
}
