using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierEquipmentController
/// -----------------------------------------------------------------------------
///
/// Optional presentation bridge for resolved runtime equipment.
/// SoldierController keeps the gameplay body alive while this component swaps only
/// the child weapon visual when upgrades resolve a different WeaponProfile.
///
/// Armor replacement is exposed as resolved state now, but model/material swapping
/// remains intentionally dormant until armor presentation rules are finalized.
///
[DisallowMultipleComponent]
public class SoldierEquipmentController : MonoBehaviour
{
    [Header("Weapon Presentation")]
    [SerializeField] private Transform weaponSocket;
    // [SerializeField] private bool clearExistingSocketChildrenOnFirstApply = false;

    private WeaponProfile currentWeaponProfile;
    private ArmorProfile currentArmorProfile;
    private GameObject runtimeWeaponPrefab;
    private bool hasAppliedEquipment;

    public WeaponProfile CurrentWeaponProfile => currentWeaponProfile;
    public ArmorProfile CurrentArmorProfile => currentArmorProfile;
    public GameObject RuntimeWeaponPrefab => runtimeWeaponPrefab;

    public void ApplyResolvedEquipment(
        WeaponProfile weaponProfile,
        ArmorProfile armorProfile)
    {
        // Clear previous weapon from socket
        // if (!hasAppliedEquipment && clearExistingSocketChildrenOnFirstApply)
        ClearWeaponSocketChildren();
        // hasAppliedEquipment = true;

        if (currentWeaponProfile != weaponProfile)
            ApplyWeaponProfile(weaponProfile);

        currentArmorProfile = armorProfile;
    }

    void ApplyWeaponProfile(WeaponProfile weaponProfile)
    {
        currentWeaponProfile = weaponProfile;

        if (runtimeWeaponPrefab != null)
        {
            Destroy(runtimeWeaponPrefab);
            runtimeWeaponPrefab = null;
        }

        if (weaponProfile == null ||
            weaponProfile.weaponPrefab == null ||
            weaponSocket == null) return;

        runtimeWeaponPrefab = Instantiate(
            weaponProfile.weaponPrefab,
            weaponSocket);

        runtimeWeaponPrefab.transform.localPosition += weaponProfile.attachedLocalPositionOffset;
        runtimeWeaponPrefab.transform.localRotation = Quaternion.Euler(weaponProfile.attachedLocalEulerAnglesOffset + runtimeWeaponPrefab.transform.localRotation.eulerAngles);
        runtimeWeaponPrefab.transform.localScale += weaponProfile.attachedLocalScaleOffset;
    }

    void ClearWeaponSocketChildren()
    {
        if (weaponSocket == null)
            return;

        for (int childIndex = weaponSocket.childCount - 1; childIndex >= 0; childIndex--)
        {
            Transform child = weaponSocket.GetChild(childIndex);

            if (child != null)
                Destroy(child.gameObject);
        }
    }
}
