using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int maxHealth = 50;

    int currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (currentHealth <= 0)
        {
            Destroy(gameObject);
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log(currentHealth);
    }

    public int GetHealth()
    {
        return currentHealth;
    }
}
