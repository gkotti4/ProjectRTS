using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    public Sprite weaponIcon = null;
    
    [Header("Presentation")]
    [Tooltip("Canonical weapon prefab reserved for future runtime replacement. Default soldier equipment is authored directly on the soldier prefab and is not spawned automatically.")]
    public GameObject weaponPrefab; 
    
    [Space(10), Header("Attachment")]
    [Tooltip("Physical hand used by this weapon when a runtime replacement or hand-specific effect needs an attachment socket. Melee/ranged type does not imply a hand.")]
    public WeaponSocketType weaponSocketType = WeaponSocketType.RightHand;

    [Header("Melee Stats")]
    public MeleeCombatStats melee = MeleeCombatStats.Default;

    [Header("Ranged Stats")]
    public RangedCombatStats ranged = RangedCombatStats.Default;

    [Header("Runtime Effects")]
    [Tooltip("Effects this weapon has before upgrades add or remove effects.")]
    public List<WeaponEffectData> baseWeaponEffects = new List<WeaponEffectData>();

    [Header("Animation Presentation")]
    [Tooltip("Pre-authored Animator Override Controller applied while this weapon is active. It must use the soldier's compatible base controller.")]
    public AnimatorOverrideController animatorOverrideController;

    [Min(1)]
    [Tooltip("Number of authored AttackVariant melee states available while this weapon is active.")]
    public int animationAttackVariantCount = 1;

    [Tooltip("Temporarily disables the UpperBody layer while this weapon's attack animation plays as a full-body action.")]
    public bool animationDisableUpperBodyLayerDuringAttack = true;
    
    
    void OnValidate()
    {
        animationAttackVariantCount = Mathf.Max(1, animationAttackVariantCount);

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
