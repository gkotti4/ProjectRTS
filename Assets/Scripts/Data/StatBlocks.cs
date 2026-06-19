using UnityEngine;

[System.Serializable]
public struct HealthStats
{
    [Min(1)] public int maxHealth;
    [Min(0)] public int armor;

    public static HealthStats Default => new HealthStats
    {
        maxHealth = 100,
        armor = 0
    };
}

[System.Serializable]
public struct MovementStats
{
    [Min(0f)] public float moveSpeed;
    [Min(0f)] public float acceleration;
    [Min(0f)] public float turnSpeed;

    public static MovementStats Default => new MovementStats
    {
        moveSpeed = 4f,
        acceleration = 99999f,
        turnSpeed = 900f
    };
}

[System.Serializable]
public struct MeleeCombatStats
{
    [Min(0)] public int meleeAttack;
    [Min(0)] public int meleeDefense;

    [Min(0)] public int weaponDamage;
    [Min(0)] public int armorPiercingDamage;

    [Min(0.05f)] public float attackInterval;
    [Min(0f)] public float attackRange;

    public static MeleeCombatStats Default => new MeleeCombatStats
    {
        meleeAttack = 20,
        meleeDefense = 20,
        weaponDamage = 20,
        armorPiercingDamage = 0,
        attackInterval = 1.5f,
        attackRange = 1.5f
    };
}

[System.Serializable]
public struct RangedCombatStats
{
    [Min(0)] public int missileDamage;
    [Min(0)] public int armorPiercingDamage;

    [Min(0.05f)] public float attackInterval;
    [Min(0f)] public float attackRange;
    [Min(0f)] public float projectileSpeed;
}

[System.Serializable]
public struct MoraleStats
{
    [Min(0f)] public float maxMorale;
    [Min(0f)] public float leadership;

    public static MoraleStats Default => new MoraleStats
    {
        maxMorale = 100f,
        leadership = 50f
    };
}

[System.Serializable]
public struct FormationStats
{
    public SquadFormation defaultFormation;

    [Min(1)] public int defaultUnitsPerRow;
    [Min(0.1f)] public float spacing;

    public static FormationStats Default => new FormationStats
    {
        defaultFormation = SquadFormation.Line,
        defaultUnitsPerRow = 10,
        spacing = 2f
    };
}

[System.Serializable]
public struct GatheringStats
{
    [Min(0)] public int gatherAmount;
    [Min(0f)] public float gatherRange;
    [Min(0.05f)] public float gatherInterval;
}

[System.Serializable]
public struct ProductionStats
{
    [Min(0.05f)] public float productionSpeed;
    [Min(0)] public int garrisonCapacity;

    public static ProductionStats Default => new ProductionStats
    {
        productionSpeed = 1f,
        garrisonCapacity = 0
    };
}