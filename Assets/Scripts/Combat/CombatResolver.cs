using UnityEngine;

public static class CombatResolver
{
    public static DamageResult ResolveMeleeHit(
        MeleeCombatStats attacker,
        MeleeCombatStats defender)
    {
        float hitChance = CalculateHitChance(
            attacker.meleeAttack,
            defender.meleeDefense);

        bool hit = Random.value <= hitChance;

        if (!hit)
            return DamageResult.Miss;

        int normalDamage = Mathf.Max(0, attacker.weaponDamage);
        int armorPiercingDamage = Mathf.Max(0, attacker.armorPiercingDamage);

        return new DamageResult
        {
            didHit = true,
            normalDamage = normalDamage,
            armorPiercingDamage = armorPiercingDamage,
            totalDamage = normalDamage + armorPiercingDamage
        };
    }

    static float CalculateHitChance(int meleeAttack, int meleeDefense)
    {
        // Simple TW-ish first pass:
        // equal stats = 50%
        // each point advantage shifts chance by 1%
        // clamped so combat always has uncertainty.
        float chance = 0.5f + (meleeAttack - meleeDefense) * 0.01f;
        return Mathf.Clamp(chance, 0.15f, 0.85f);
    }
}