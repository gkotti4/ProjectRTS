using System.Collections.Generic;
using UnityEngine;

public sealed class SoldierRuntimeStats
{
    public HealthStats health;
    public MovementStats movement;
    public BodyStats body;
    public CombatDefenseStats defense;
    public MeleeCombatStats melee;
    public RangedCombatStats ranged;
    public WeaponProfile weaponProfile;
    public ArmorProfile armorProfile;
}

public sealed class SquadRuntimeStats
{
    public SquadCapacityStats capacity;
    public FormationStats formation;
    public MoraleStats morale;
}

public static class RuntimeStatResolver
{
    public static SquadRuntimeStats ResolveSquad(
        SquadData data,
        FactionInstance faction,
        IReadOnlyList<UpgradeData> squadUpgrades = null)
    {
        SquadRuntimeStats result = new SquadRuntimeStats();
        if (data == null)
        {
            result.capacity = SquadCapacityStats.Default;
            result.formation = FormationStats.Default;
            result.morale = MoraleStats.Default;
            return result;
        }

        result.capacity = new SquadCapacityStats
        {
            startingSoldierCount = Mathf.Max(1, data.startingSoldierCount),
            maximumSoldierCount = Mathf.Max(1, data.maxSoldierCount),
            reinforcementAmount = Mathf.Max(0, data.reinforcementAmount),
            reinforcementCostMultiplier = Mathf.Max(0f, data.reinforcementCostMultiplier),
            officerSlots = Mathf.Max(0, data.officerSlots),
            specialistSlots = Mathf.Max(0, data.specialistSlots)
        };
        result.formation = new FormationStats
        {
            defaultFormation = data.defaultFormation,
            defaultUnitsPerRow = Mathf.Max(1, data.defaultUnitsPerRow),
            spacing = Mathf.Max(0.1f, data.defaultSpacing),
            minimumSpacing = Mathf.Max(0.1f, data.minimumSpacing),
            maximumSpacing = Mathf.Max(data.minimumSpacing, data.maximumSpacing),
            reformSpeedMultiplier = Mathf.Max(0f, data.reformSpeedMultiplier),
            cohesionDistanceMultiplier = Mathf.Max(0f, data.cohesionDistanceMultiplier)
        };
        result.morale = data.morale;

        ApplyFactionSquadUpgrades(result, data, faction);
        if (squadUpgrades != null)
            for (int i = 0; i < squadUpgrades.Count; i++)
                if (squadUpgrades[i] != null) ApplySquadModifier(result, squadUpgrades[i].squadModifiers);

        ClampSquad(result);
        return result;
    }

    public static SoldierRuntimeStats ResolveSoldier(
        SoldierData data,
        SquadData squadData,
        FactionInstance faction,
        IReadOnlyList<UpgradeData> squadUpgrades = null)
    {
        SoldierRuntimeStats result = new SoldierRuntimeStats
        {
            health = data != null ? data.health : HealthStats.Default,
            movement = data != null ? data.movement : MovementStats.Default,
            body = data != null ? data.body : BodyStats.Default,
            defense = data != null ? data.defense : CombatDefenseStats.Default,
            weaponProfile = data != null ? data.weaponProfile : null,
            armorProfile = data != null ? data.armorProfile : null,
            melee = data != null && data.weaponProfile != null ? data.weaponProfile.melee : MeleeCombatStats.Default,
            ranged = data != null && data.weaponProfile != null ? data.weaponProfile.ranged : RangedCombatStats.Default
        };

        // if (result.defense.armor <= 0 && result.health.legacyArmor > 0)
        //     result.defense.armor = result.health.legacyArmor;

        if (result.armorProfile != null)
            ApplyArmor(result, result.armorProfile.stats);

        ApplyFactionSoldierUpgrades(result, squadData, faction);
        if (squadUpgrades != null)
            for (int i = 0; i < squadUpgrades.Count; i++)
                if (squadUpgrades[i] != null) ApplySoldierModifier(result, squadUpgrades[i].soldierModifiers);

        ClampSoldier(result);
        return result;
    }

    static void ApplyFactionSquadUpgrades(SquadRuntimeStats result, SquadData data, FactionInstance faction)
    {
        if (faction == null) return;
        foreach (UpgradeData upgrade in faction.appliedUpgrades)
            if (AffectsSquad(upgrade, data)) ApplySquadModifier(result, upgrade.squadModifiers);
    }

    static void ApplyFactionSoldierUpgrades(SoldierRuntimeStats result, SquadData data, FactionInstance faction)
    {
        if (faction == null) return;
        foreach (UpgradeData upgrade in faction.appliedUpgrades)
            if (AffectsSquad(upgrade, data)) ApplySoldierModifier(result, upgrade.soldierModifiers);
    }

    static bool AffectsSquad(UpgradeData upgrade, SquadData data)
    {
        if (upgrade == null || data == null) return false;
        if (upgrade.affectedSquadCategories == null || upgrade.affectedSquadCategories.Length == 0) return true;
        for (int i = 0; i < upgrade.affectedSquadCategories.Length; i++)
            if (upgrade.affectedSquadCategories[i] == data.category) return true;
        return false;
    }

    static void ApplyArmor(SoldierRuntimeStats s, ArmorStats a)
    {
        s.defense.armor += a.armor;
        s.defense.meleeDefense += a.meleeDefenseBonus;
        s.defense.missileDefense += a.missileDefenseBonus;
        s.defense.meleeBlockChance += a.meleeBlockChanceBonus;
        s.defense.missileBlockChance += a.missileBlockChanceBonus;
        s.defense.hitReactResistance += a.hitReactResistanceBonus;
        s.movement.moveSpeed *= Mathf.Max(0f, 1f + a.movementSpeedMultiplierDelta);
        s.movement.acceleration *= Mathf.Max(0f, 1f + a.accelerationMultiplierDelta);
        s.body.mass *= Mathf.Max(0.01f, 1f + a.massMultiplierDelta);
    }

    static void ApplySoldierModifier(SoldierRuntimeStats s, SoldierStatModifiers m)
    {
        s.health.maxHealth += m.maxHealth; s.health.healthRegenerationPerSecond += m.healthRegenerationPerSecond; s.health.healingReceivedMultiplier += m.healingReceivedMultiplierDelta;
        s.movement.moveSpeed += m.moveSpeed; s.movement.acceleration += m.acceleration; s.movement.deceleration += m.deceleration; s.movement.turnSpeed += m.turnSpeed; s.movement.backwardsSpeedMultiplier += m.backwardsSpeedMultiplierDelta; s.movement.combatMoveSpeedMultiplier += m.combatMoveSpeedMultiplierDelta;
        s.body.mass += m.bodyMass; s.body.radius += m.bodyRadius; s.body.height += m.bodyHeight; s.body.impulseResistance += m.impulseResistance; s.body.staggerResistance += m.staggerResistance; s.body.knockdownResistance += m.knockdownResistance;
        s.defense.armor += m.armor; s.defense.meleeDefense += m.meleeDefense; s.defense.missileDefense += m.missileDefense; s.defense.meleeBlockChance += m.meleeBlockChance; s.defense.missileBlockChance += m.missileBlockChance; s.defense.criticalHitResistance += m.criticalHitResistance; s.defense.armorPiercingResistance += m.armorPiercingResistance; s.defense.hitReactResistance += m.hitReactResistance;
        s.melee.meleeAttack += m.meleeAttack; s.melee.weaponDamage += m.meleeWeaponDamage; s.melee.armorPiercingDamage += m.meleeArmorPiercingDamage; s.melee.attackInterval += m.meleeAttackInterval; s.melee.attackRange += m.meleeAttackRange; s.melee.criticalHitChance += m.meleeCriticalHitChance; s.melee.criticalHitDamageMultiplier += m.meleeCriticalHitDamageMultiplierDelta; s.melee.impactForce += m.meleeImpactForce; s.melee.staggerStrength += m.meleeStaggerStrength; s.melee.knockdownChance += m.meleeKnockdownChance; s.melee.chargeDamageBonus += m.chargeDamageBonus; s.melee.chargeImpactBonus += m.chargeImpactBonus;
        s.ranged.rangedAccuracy += m.rangedAccuracy; s.ranged.missileDamage += m.missileDamage; s.ranged.armorPiercingDamage += m.rangedArmorPiercingDamage; s.ranged.attackInterval += m.rangedAttackInterval; s.ranged.attackRange += m.rangedAttackRange; s.ranged.minimumRange += m.rangedMinimumRange; s.ranged.criticalHitChance += m.rangedCriticalHitChance; s.ranged.criticalHitDamageMultiplier += m.rangedCriticalHitDamageMultiplierDelta; s.ranged.projectileSpeed += m.projectileSpeed; s.ranged.projectileGravityMultiplier += m.projectileGravityMultiplierDelta; s.ranged.spreadRadius += m.spreadRadius; s.ranged.suppressionStrength += m.suppressionStrength; s.ranged.ammunition += m.ammunition;
    }

    static void ApplySquadModifier(SquadRuntimeStats s, SquadStatModifiers m)
    {
        s.capacity.startingSoldierCount += m.startingSoldierCount; s.capacity.maximumSoldierCount += m.maximumSoldierCount; s.capacity.reinforcementAmount += m.reinforcementAmount; s.capacity.reinforcementCostMultiplier += m.reinforcementCostMultiplierDelta; s.capacity.officerSlots += m.officerSlots; s.capacity.specialistSlots += m.specialistSlots;
        s.formation.defaultUnitsPerRow += m.defaultUnitsPerRow; s.formation.spacing += m.spacing; s.formation.minimumSpacing += m.minimumSpacing; s.formation.maximumSpacing += m.maximumSpacing; s.formation.reformSpeedMultiplier += m.reformSpeedMultiplierDelta; s.formation.cohesionDistanceMultiplier += m.cohesionDistanceMultiplierDelta;
        s.morale.maxMorale += m.maxMorale; s.morale.leadership += m.leadership; s.morale.moraleRecoveryRate += m.moraleRecoveryRate; s.morale.casualtyMoraleResistance += m.casualtyMoraleResistance; s.morale.flankMoraleResistance += m.flankMoraleResistance; s.morale.terrorResistance += m.terrorResistance; s.morale.routingThreshold += m.routingThreshold; s.morale.shatteredThreshold += m.shatteredThreshold;
    }

    static void ClampSoldier(SoldierRuntimeStats s)
    {
        s.health.maxHealth = Mathf.Max(1, s.health.maxHealth); s.health.healthRegenerationPerSecond = Mathf.Max(0f, s.health.healthRegenerationPerSecond); s.health.healingReceivedMultiplier = Mathf.Max(0f, s.health.healingReceivedMultiplier <= 0f ? 1f : s.health.healingReceivedMultiplier);
        s.movement.moveSpeed = Mathf.Max(0f, s.movement.moveSpeed); s.movement.acceleration = Mathf.Max(0f, s.movement.acceleration); s.movement.deceleration = Mathf.Max(0f, s.movement.deceleration); s.movement.turnSpeed = Mathf.Max(0f, s.movement.turnSpeed); s.movement.backwardsSpeedMultiplier = Mathf.Max(0f, s.movement.backwardsSpeedMultiplier <= 0f ? 0.65f : s.movement.backwardsSpeedMultiplier); s.movement.combatMoveSpeedMultiplier = Mathf.Max(0f, s.movement.combatMoveSpeedMultiplier <= 0f ? 1f : s.movement.combatMoveSpeedMultiplier);
        s.body.mass = Mathf.Max(0.01f, s.body.mass); s.body.radius = Mathf.Max(0.01f, s.body.radius); s.body.height = Mathf.Max(0.1f, s.body.height); s.body.impulseResistance = Mathf.Max(0f, s.body.impulseResistance); s.body.staggerResistance = Mathf.Max(0f, s.body.staggerResistance); s.body.knockdownResistance = Mathf.Max(0f, s.body.knockdownResistance);
        s.defense.armor = Mathf.Max(0, s.defense.armor); s.defense.meleeDefense = Mathf.Max(0, s.defense.meleeDefense); s.defense.missileDefense = Mathf.Max(0, s.defense.missileDefense); s.defense.meleeBlockChance = Mathf.Clamp01(s.defense.meleeBlockChance); s.defense.missileBlockChance = Mathf.Clamp01(s.defense.missileBlockChance); s.defense.criticalHitResistance = Mathf.Clamp01(s.defense.criticalHitResistance); s.defense.armorPiercingResistance = Mathf.Clamp01(s.defense.armorPiercingResistance); s.defense.hitReactResistance = Mathf.Clamp01(s.defense.hitReactResistance);
        s.melee.attackInterval = Mathf.Max(0.05f, s.melee.attackInterval); s.melee.attackRange = Mathf.Max(0.1f, s.melee.attackRange); s.melee.criticalHitChance = Mathf.Clamp01(s.melee.criticalHitChance); s.melee.criticalHitDamageMultiplier = Mathf.Max(1f, s.melee.criticalHitDamageMultiplier); s.melee.knockdownChance = Mathf.Clamp01(s.melee.knockdownChance);
        s.ranged.attackInterval = Mathf.Max(0.05f, s.ranged.attackInterval); s.ranged.attackRange = Mathf.Max(0.1f, s.ranged.attackRange); s.ranged.minimumRange = Mathf.Clamp(s.ranged.minimumRange, 0f, s.ranged.attackRange); s.ranged.criticalHitChance = Mathf.Clamp01(s.ranged.criticalHitChance); s.ranged.criticalHitDamageMultiplier = Mathf.Max(1f, s.ranged.criticalHitDamageMultiplier); s.ranged.projectileSpeed = Mathf.Max(0.1f, s.ranged.projectileSpeed); s.ranged.ammunition = Mathf.Max(-1, s.ranged.ammunition);
    }

    static void ClampSquad(SquadRuntimeStats s)
    {
        s.capacity.startingSoldierCount = Mathf.Max(1, s.capacity.startingSoldierCount); s.capacity.maximumSoldierCount = Mathf.Max(s.capacity.startingSoldierCount, s.capacity.maximumSoldierCount); s.capacity.reinforcementAmount = Mathf.Max(0, s.capacity.reinforcementAmount); s.capacity.reinforcementCostMultiplier = Mathf.Max(0f, s.capacity.reinforcementCostMultiplier);
        s.formation.defaultUnitsPerRow = Mathf.Max(1, s.formation.defaultUnitsPerRow); s.formation.minimumSpacing = Mathf.Max(0.1f, s.formation.minimumSpacing); s.formation.maximumSpacing = Mathf.Max(s.formation.minimumSpacing, s.formation.maximumSpacing); s.formation.spacing = Mathf.Clamp(s.formation.spacing, s.formation.minimumSpacing, s.formation.maximumSpacing); s.formation.reformSpeedMultiplier = Mathf.Max(0f, s.formation.reformSpeedMultiplier <= 0f ? 1f : s.formation.reformSpeedMultiplier); s.formation.cohesionDistanceMultiplier = Mathf.Max(0f, s.formation.cohesionDistanceMultiplier <= 0f ? 1f : s.formation.cohesionDistanceMultiplier);
        s.morale.maxMorale = Mathf.Max(0f, s.morale.maxMorale); s.morale.leadership = Mathf.Max(0f, s.morale.leadership); s.morale.moraleRecoveryRate = Mathf.Max(0f, s.morale.moraleRecoveryRate); s.morale.casualtyMoraleResistance = Mathf.Clamp01(s.morale.casualtyMoraleResistance); s.morale.flankMoraleResistance = Mathf.Clamp01(s.morale.flankMoraleResistance); s.morale.terrorResistance = Mathf.Clamp01(s.morale.terrorResistance);
    }
}
