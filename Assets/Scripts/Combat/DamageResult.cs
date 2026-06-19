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