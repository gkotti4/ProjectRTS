using UnityEngine;

public class Health : MonoBehaviour
{
    protected int maxHealth;
    protected int currentHealth;

    public void Initialize(int health)
    {
        maxHealth = health;
        currentHealth = health;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        if (TryGetComponent(out UnitAnimator unitAnimator))
            unitAnimator.TriggerDeath();

        if (TryGetComponent(out UnitController unitController))
        {
            unitController.Agent.enabled = false;
            if (TryGetComponent(out Rigidbody rb))
                rb.isKinematic = false;
        }

        GameManager.Instance.RegisterDespawn();
        GameEvents.UnitDied(gameObject);
        Destroy(gameObject, 2f);
    }

    public bool IsAlive() => currentHealth > 0;
    public int GetHealth() => currentHealth;
}