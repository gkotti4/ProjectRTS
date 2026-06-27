using UnityEngine;

/// -----------------------------------------------------------------------------
/// DamageResult
/// -----------------------------------------------------------------------------
///
/// Lightweight result returned by combat resolution before SoldierHealth applies
/// armor and health changes.
///
public struct DamageResult
{
    public bool didHit;
    public int normalDamage;
    public int armorPiercingDamage;
    public int totalDamage;

    public static DamageResult Miss => new DamageResult
    {
        didHit = false,
        normalDamage = 0,
        armorPiercingDamage = 0,
        totalDamage = 0
    };
}

/// -----------------------------------------------------------------------------
/// CombatResolver
/// -----------------------------------------------------------------------------
///
/// Central first-pass combat math.
/// WeaponProfile owns offensive stat blocks: MeleeCombatStats and RangedCombatStats.
/// SoldierData.defense owns defensive skill/block values. SoldierHealth still owns
/// health and armor reduction.
///
public static class CombatResolver
{
    public static DamageResult ResolveMeleeHit(
        MeleeCombatStats attacker,
        CombatDefenseStats defender)
    {
        float hitChance = CalculateOpposedChance(
            attacker.meleeAttack,
            defender.meleeDefense);

        if (Random.value > hitChance)
            return DamageResult.Miss;

        return BuildDamageResult(
            attacker.weaponDamage,
            attacker.armorPiercingDamage);
    }

    public static DamageResult ResolveRangedHit(
        RangedCombatStats attacker,
        CombatDefenseStats defender)
    {
        if (Random.value < Mathf.Clamp01(defender.shieldBlockChance))
            return DamageResult.Miss;

        float hitChance = CalculateOpposedChance(
            attacker.rangedAccuracy,
            defender.missileDefense);

        if (Random.value > hitChance)
            return DamageResult.Miss;

        return BuildDamageResult(
            attacker.missileDamage,
            attacker.armorPiercingDamage);
    }

    static DamageResult BuildDamageResult(
        int damage,
        int armorPiercingDamage)
    {
        int normalDamage = Mathf.Max(0, damage);
        int resolvedArmorPiercingDamage = Mathf.Max(0, armorPiercingDamage);

        return new DamageResult
        {
            didHit = true,
            normalDamage = normalDamage,
            armorPiercingDamage = resolvedArmorPiercingDamage,
            totalDamage = normalDamage + resolvedArmorPiercingDamage
        };
    }

    static float CalculateOpposedChance(int attackSkill, int defenseSkill)
    {
        // Simple TW-ish first pass:
        // equal stats = 50%
        // each point advantage shifts chance by 1%
        // clamped so combat always has uncertainty.
        float chance = 0.5f + (attackSkill - defenseSkill) * 0.01f;
        return Mathf.Clamp(chance, 0.15f, 0.85f);
    }
}
