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
        Debug.Log(gameObject.name + " died");
        Destroy(gameObject);
        //gameObject.SetActive(false);
    }

    public bool IsAlive() => currentHealth > 0;
    public int GetHealth() => currentHealth;
    
}
