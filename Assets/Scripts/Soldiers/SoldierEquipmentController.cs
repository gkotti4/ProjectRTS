using UnityEngine;
using UnityEngine.Serialization;

/// -----------------------------------------------------------------------------
/// SoldierEquipmentController
/// -----------------------------------------------------------------------------
///
/// Owns authored equipment presentation references for one soldier. Default
/// weapons, shields, off-hand items, quivers, and similar equipment are positioned
/// directly on the prefab in the editor. Switching combat weapons only enables the
/// authored object set for the active melee/ranged slot; it does not clear sockets
/// or instantiate weapon prefabs.
///
/// Right/left hand and VFX sockets remain available as stable attachment points for
/// future runtime replacements or effects if the game later needs them.
///
[DisallowMultipleComponent]
public class SoldierEquipmentController : MonoBehaviour
{
    #region Authored Equipment

    [Header("Authored Equipment")]
    [Tooltip("Prefab-authored objects shown while the soldier is using its melee weapon. This set may contain multiple objects, such as a sword + shield or two dual-wielded weapons.")]
    [SerializeField] private GameObject[] meleeEquipmentObjects;

    [Tooltip("Prefab-authored objects shown while the soldier is using its ranged weapon. This set may contain multiple objects, such as a bow + quiver or a firearm + supporting equipment.")]
    [SerializeField] private GameObject[] rangedEquipmentObjects;

    #endregion

    #region Attachment Sockets

    [Header("Attachment Sockets")]
    [FormerlySerializedAs("weaponSocket_RightHand")]
    [FormerlySerializedAs("weaponSocketRightHand")]
    [SerializeField] private Transform rightHandSocket;

    [FormerlySerializedAs("weaponSocketLeftHand")]
    [SerializeField] private Transform leftHandSocket;

    [Header("VFX Sockets")]
    [SerializeField] private Transform rightHandVfxSocket;
    [SerializeField] private Transform leftHandVfxSocket;

    public Transform RightHandSocket => rightHandSocket;
    public Transform LeftHandSocket => leftHandSocket;
    public Transform RightHandVfxSocket => rightHandVfxSocket;
    public Transform LeftHandVfxSocket => leftHandVfxSocket;

    #endregion

    #region Runtime State

    private WeaponProfile currentWeaponProfile;
    private WeaponSlot currentWeaponSlot;
    private ArmorProfile currentArmorProfile;
    private bool hasActiveWeaponPresentation;

    public WeaponProfile CurrentWeaponProfile => currentWeaponProfile;
    public WeaponSlot CurrentWeaponSlot => currentWeaponSlot;
    public ArmorProfile CurrentArmorProfile => currentArmorProfile;

    #endregion

    #region Public API

    /// <summary>
    /// Updates the active weapon metadata and toggles the prefab-authored equipment
    /// objects for the requested melee/ranged slot. No equipment is destroyed or
    /// instantiated here.
    /// </summary>
    public bool SetActiveWeapon(
        WeaponProfile weaponProfile,
        WeaponSlot weaponSlot)
    {
        if (hasActiveWeaponPresentation &&
            currentWeaponProfile == weaponProfile &&
            currentWeaponSlot == weaponSlot)
        {
            return false;
        }

        currentWeaponProfile = weaponProfile;
        currentWeaponSlot = weaponSlot;
        hasActiveWeaponPresentation = true;

        SetEquipmentObjectsActive(
            meleeEquipmentObjects,
            weaponProfile != null && weaponSlot == WeaponSlot.Melee);

        SetEquipmentObjectsActive(
            rangedEquipmentObjects,
            weaponProfile != null && weaponSlot == WeaponSlot.Ranged);

        return true;
    }

    /// <summary>
    /// Stores the active armor profile. Armor presentation remains authored on the
    /// prefab until runtime armor replacement is actually needed.
    /// </summary>
    public bool SetArmorProfile(ArmorProfile armorProfile)
    {
        if (currentArmorProfile == armorProfile)
            return false;

        currentArmorProfile = armorProfile;
        return true;
    }

    /// <summary>
    /// Returns the physical hand socket requested by equipment metadata. Intended
    /// for future runtime replacement/effect code; default equipment does not use it.
    /// </summary>
    public Transform GetHandSocket(WeaponSocketType socketType)
    {
        return socketType == WeaponSocketType.LeftHand
            ? leftHandSocket
            : rightHandSocket;
    }

    /// <summary>
    /// Returns the VFX attachment point associated with the requested hand.
    /// </summary>
    public Transform GetHandVfxSocket(WeaponSocketType socketType)
    {
        return socketType == WeaponSocketType.LeftHand
            ? leftHandVfxSocket
            : rightHandVfxSocket;
    }

    #endregion

    #region Helpers

    void SetEquipmentObjectsActive(
        GameObject[] equipmentObjects,
        bool active)
    {
        if (equipmentObjects == null)
            return;

        for (int index = 0; index < equipmentObjects.Length; index++)
        {
            GameObject equipmentObject = equipmentObjects[index];

            if (equipmentObject != null && equipmentObject.activeSelf != active)
                equipmentObject.SetActive(active);
        }
    }

    #endregion
}
