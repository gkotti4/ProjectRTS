
using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// UpgradeData
/// -----------------------------------------------------------------------------
///
/// Designer-authored package describing one faction-wide or squad-local upgrade.
/// Upgrade eligibility always targets SquadData unit definitions. Soldier-stat
/// effects then modify every repeated soldier body belonging to a matching squad.
/// Most upgrades use the targeted stat-effect lists. Asset-effect lists support
/// weapon, weapon-effect, armor, and squad-visual changes without replacing the
/// live soldier or squad gameplay prefab.
///
/// Targeting:
/// - defaultTarget is shared by every effect unless that effect enables its override.
/// - empty normal-filter collections mean no restriction.
/// - OR is used inside one populated collection.
/// - AND is used between separate populated filter groups.
/// - explicit exclusions always win.
/// - explicit additional inclusions bypass normal classification filters.
///
/// Design role:
/// Immutable upgrade definition. Runtime ownership and stack counts belong to
/// FactionInstance or SquadController.
///
[CreateAssetMenu(
    fileName = "UpgradeData_",
    menuName = "Scriptable Objects/Upgrades/UpgradeData")]
public class UpgradeData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable save/catalog key. Do not change after this upgrade ships in persistent save data.")]
    public string upgradeId;
    public string upgradeName = "Upgrade";
    [TextArea]
    public string description;
    public Sprite icon;

    [Header("Research")]
    public ResourceCost cost;
    [Min(0f)]
    public float researchTime = 1f;

    [Header("Application")]
    [Tooltip("Faction upgrades affect every matching squad owned by the faction. Squad upgrades affect one squad only.")]
    public UpgradeScope scope = UpgradeScope.Faction;
    [Tooltip("Allows this UpgradeData asset to be applied more than once.")]
    public bool repeatable = false;
    [Min(1)]
    public int maximumStacks = 1;

    [Header("Requirements")]
    public List<UpgradeData> requiredUpgrades = new List<UpgradeData>();
    public List<UpgradeData> blockedByUpgrades = new List<UpgradeData>();

    [Header("Default Targeting")]
    [Tooltip("Shared target used by effects whose overrideDefaultTarget field is false.")]
    public UpgradeTargetFilter defaultTarget;
    
    [Header("Stat Effects - Primary Upgrade System")]
    [Tooltip("Modifies the individual stats of every soldier member belonging to a matching SquadData unit type.")]
    public List<TargetedSoldierStatModifier> soldierStatEffects = new List<TargetedSoldierStatModifier>();

    [Tooltip("Modifies collective squad stats such as capacity, formation, and morale for matching SquadData unit types.")]
    public List<TargetedSquadStatModifier> squadStatEffects = new List<TargetedSquadStatModifier>();

    // -------------------------------------------------------------------------
    // Temporary RuntimeStatResolver compatibility
    // -------------------------------------------------------------------------
    // The current resolver still reads these legacy single-effect fields. They are
    // hidden so new assets are authored through the modular lists above. Remove
    // these after RuntimeStatResolver is migrated to the new effect collections.
    [HideInInspector] public SquadCategory[] affectedSquadCategories;
    [HideInInspector] public BuildingCategory[] affectedBuildingCategories;
    [HideInInspector] public SoldierStatModifiers soldierModifiers;
    [HideInInspector] public SquadStatModifiers squadModifiers;

    [Header("Asset Effects")]
    [Tooltip("Replaces the resolved runtime weapon profile. The live soldier prefab is not replaced.")]
    public List<WeaponReplacementEffect> weaponReplacementEffects = new List<WeaponReplacementEffect>();

    [Tooltip("Adds or removes modular effects from the resolved runtime weapon.")]
    public List<WeaponEffectUpgradeEffect> weaponEffectEffects = new List<WeaponEffectUpgradeEffect>();

    [Tooltip("Reserved for runtime armor-profile replacement and presentation wiring.")]
    public List<ArmorReplacementEffect> armorReplacementEffects = new List<ArmorReplacementEffect>();

    [Tooltip("Reserved for model/material/attachment replacement through a future visual controller.")]
    public List<SquadVisualReplacementEffect> squadVisualReplacementEffects = new List<SquadVisualReplacementEffect>();

    void OnValidate()
    {
        researchTime = Mathf.Max(0f, researchTime);
        maximumStacks = Mathf.Max(1, maximumStacks);

        if (!repeatable)
            maximumStacks = 1;
    }
}


