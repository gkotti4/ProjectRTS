using System;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierHealth
/// -----------------------------------------------------------------------------
///
/// Owns individual soldier health, armor, damage intake, healing, and death events.
/// Does not know about squads, UI, animation, or combat behavior beyond applying
/// damage and notifying listeners when health changes or death occurs.
///
/// Design role:
/// Pure individual health/death state.
///
public class SoldierHealth : MonoBehaviour
{
    public event Action<SoldierHealth> OnHealthChanged;
    public event Action<SoldierHealth> OnDied;

    private int currentHealth;
    private int maxHealth;
    private int armor;

    private bool isDead = false;

    public bool IsAlive => !isDead && currentHealth > 0;
    public int CurrentHealth => Mathf.Max(0, currentHealth);
    public int MaxHealth => maxHealth;
    public int Armor => armor;

    public float HealthPercent =>
        maxHealth > 0 ? (float)CurrentHealth / maxHealth : 0f;

    public void Initialize(HealthStats stats)
    {
        maxHealth = Mathf.Max(1, stats.maxHealth);
        armor = Mathf.Max(0, stats.armor);

        currentHealth = maxHealth;
        isDead = false;

        OnHealthChanged?.Invoke(this);
    }

    public void TakeDamage(int rawDamage, int armorPiercingDamage = 0)
    {
        if (!IsAlive)
            return;

        int normalDamage = Mathf.Max(0, rawDamage - armor);
        int totalDamage = Mathf.Max(1, normalDamage + armorPiercingDamage);

        currentHealth -= totalDamage;

        OnHealthChanged?.Invoke(this);

        if (currentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        if (!IsAlive)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, amount));
        OnHealthChanged?.Invoke(this);
    }

    public void Kill()
    {
        if (!IsAlive)
            return;

        currentHealth = 0;
        OnHealthChanged?.Invoke(this);
        Die();
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;
        currentHealth = 0;

        OnDied?.Invoke(this);
    }
}