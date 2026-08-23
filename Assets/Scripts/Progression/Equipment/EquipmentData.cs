using System;
using UnityEngine;

public enum EquipmentSlot
{
    Weapon,
    Armor,
    Kit
}

/// -----------------------------------------------------------------------------
/// EquipmentData
/// -----------------------------------------------------------------------------
///
/// Persistent per-squad equipment definition.
///
/// V1 equipment is intentionally stat-only. It does not replace WeaponProfile,
/// ArmorProfile, soldier models, materials, VFX, or animation controllers.
/// The slot describes the player's loadout category; the actual combat-stat changes
/// are resolved through RuntimeStatResolver together with UpgradeData modifiers.
/// -----------------------------------------------------------------------------
[CreateAssetMenu(
    fileName = "EquipmentData_",
    menuName = "Scriptable Objects/Equipment/EquipmentData")]
public class EquipmentData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable save/catalog key. Do not change after this item ships in persistent save data.")]
    public string equipmentId;
    public string equipmentName = "Equipment";
    [TextArea] public string description;
    [TextArea]
    [Tooltip("Optional player-facing summary of the authored modifiers, e.g. '+4 Weapon Damage, +2 Melee Attack'.")]
    public string effectSummary;
    public Sprite icon;

    [Header("Slot")]
    public EquipmentSlot slot = EquipmentSlot.Weapon;

    [Header("Targeting")]
    [Tooltip("Uses the same squad-classification language as upgrades. Empty filters mean this item may be equipped by any squad.")]
    public UpgradeTargetFilter target;

    [Header("Stat Modifiers")]
    public SoldierStatModifiers soldierModifiers;
    public SquadStatModifiers squadModifiers;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(equipmentName)
            ? equipmentName
            : name;

    public bool CanEquipTo(SquadData squadData)
    {
        return squadData != null &&
               UpgradeTargetMatcher.MatchesSquad(target, squadData);
    }

    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(equipmentName))
            equipmentName = name;

        // CM V1 persistent manpower/replenishment still uses the base SquadData
        // capacity. Keep equipment focused on battle/runtime stats until persistent
        // capacity modifiers are deliberately supported by the company layer.
        if (squadModifiers.startingSoldierCount != 0 ||
            squadModifiers.maximumSoldierCount != 0 ||
            squadModifiers.reinforcementAmount != 0 ||
            !Mathf.Approximately(squadModifiers.reinforcementCostMultiplierDelta, 0f) ||
            squadModifiers.officerSlots != 0 ||
            squadModifiers.specialistSlots != 0)
        {
            Debug.LogWarning(
                $"{name}: Equipment V1 does not support persistent squad-capacity/replenishment modifiers. " +
                "Use soldier combat stats, formation stats, or morale stats for Equipment V1.",
                this);
        }
    }
}

/// <summary>
/// Exactly three persistent equipment slots for one squad.
/// Null means no additional equipment modifier in that slot; the squad still uses
/// its normal authored SoldierData / WeaponProfile / ArmorProfile baseline.
/// </summary>
[Serializable]
public sealed class EquipmentLoadout
{
    public EquipmentData weapon;
    public EquipmentData armor;
    public EquipmentData kit;

    public EquipmentLoadout()
    {
    }

    public EquipmentLoadout(EquipmentLoadout source)
    {
        CopyFrom(source);
    }

    public EquipmentData Get(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Weapon => weapon,
            EquipmentSlot.Armor => armor,
            EquipmentSlot.Kit => kit,
            _ => null
        };
    }

    public bool Set(EquipmentData equipmentData)
    {
        if (equipmentData == null)
            return false;

        Set(equipmentData.slot, equipmentData);
        return true;
    }

    public void Set(EquipmentSlot slot, EquipmentData equipmentData)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon:
                weapon = equipmentData;
                break;

            case EquipmentSlot.Armor:
                armor = equipmentData;
                break;

            case EquipmentSlot.Kit:
                kit = equipmentData;
                break;
        }
    }

    public void CopyFrom(EquipmentLoadout source)
    {
        weapon = source != null ? source.weapon : null;
        armor = source != null ? source.armor : null;
        kit = source != null ? source.kit : null;
    }

    public EquipmentLoadout Clone()
    {
        return new EquipmentLoadout(this);
    }
}
