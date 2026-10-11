using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 50;

    int currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        if (currentHealth <= 0)
        {
            Destroy(gameObject);
            Debug.Log("Dead");
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
    }

    public void OnHeal(int healing) 
    {
        currentHealth += healing;
    }

    public void SetMaxHealth(int healthMax) 
    {
        maxHealth = healthMax;
    }

    public int GetHealth()  // Allows UI to access player's current health
    {
        return currentHealth;
    }
}
