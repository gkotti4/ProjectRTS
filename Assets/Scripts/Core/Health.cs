using UnityEngine;

public class Health : MonoBehaviour
{
    protected int maxHealth;
    protected int currentHealth;
    
    void Start()
    {
    }

    public void Initialize(int health)
    {
        maxHealth = health;
        currentHealth = health;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log(gameObject.name + " has " + currentHealth + " health left after taking " + damage + " damage");
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
            {
                rb.isKinematic = false;
            }
        }
        
        Debug.Log(gameObject.name + " died");
        
        GameManager.Instance.RegisterDespawn();
        Destroy(gameObject, 2f);
        
        //gameObject.SetActive(false); 
        // Spawn dead body / blood here?
    }

    public bool IsAlive() => currentHealth > 0;
    public int GetHealth() => currentHealth;
    
}
