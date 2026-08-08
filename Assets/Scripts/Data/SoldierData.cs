using UnityEngine;
using UnityEngine.Serialization;

/// -----------------------------------------------------------------------------
/// SoldierData
/// -----------------------------------------------------------------------------
///
/// ScriptableObject blueprint for an individual soldier type.
/// Explicit melee and ranged weapon slots define the soldier's combat capabilities.
/// Either slot may be empty; assigning both creates a hybrid unit.
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

    [Header("Weapons")]
    [Tooltip("Optional melee capability. Leave empty for a unit that cannot fight in melee.")]
    public WeaponProfile meleeWeaponProfile;

    [Tooltip("Optional ranged capability. Leave empty for a unit that cannot perform ranged attacks.")]
    public WeaponProfile rangedWeaponProfile;

    [Header("Armor")]
    public ArmorProfile armorProfile;

    // Migration fields preserve assets created before the explicit slot model.
    [FormerlySerializedAs("weaponProfile")]
    [SerializeField, HideInInspector]
    private WeaponProfile legacyPrimaryWeaponProfile;

    [FormerlySerializedAs("secondaryWeaponProfile")]
    [SerializeField, HideInInspector]
    private WeaponProfile legacySecondaryWeaponProfile;

    void OnValidate()
    {
        MigrateLegacyWeapon(ref legacyPrimaryWeaponProfile);
        MigrateLegacyWeapon(ref legacySecondaryWeaponProfile);

        ValidateWeaponSlot(meleeWeaponProfile, WeaponKind.Melee, "meleeWeaponProfile");
        ValidateWeaponSlot(rangedWeaponProfile, WeaponKind.Ranged, "rangedWeaponProfile");
    }

    void MigrateLegacyWeapon(ref WeaponProfile legacyWeapon)
    {
        if (legacyWeapon == null)
            return;

        if (legacyWeapon.weaponKind == WeaponKind.Ranged)
        {
            if (rangedWeaponProfile == null)
                rangedWeaponProfile = legacyWeapon;
        }
        else if (meleeWeaponProfile == null)
        {
            meleeWeaponProfile = legacyWeapon;
        }

        legacyWeapon = null;
    }

    void ValidateWeaponSlot(
        WeaponProfile weaponProfile,
        WeaponKind expectedKind,
        string slotName)
    {
        if (weaponProfile == null || weaponProfile.weaponKind == expectedKind)
            return;

        Debug.LogWarning(
            $"{name}: {slotName} expects a {expectedKind} WeaponProfile, but '{weaponProfile.name}' is {weaponProfile.weaponKind}.",
            this);
    }
}
