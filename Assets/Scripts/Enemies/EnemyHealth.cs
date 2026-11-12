using TMPro;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int maxHealth = 50;

    int currentHealth;

    public TMP_Text text;

    void Start()
    {
        currentHealth = maxHealth;
        text = GetComponentInChildren<TMP_Text>();
    }

    void Update()
    {
        if (currentHealth <= 0)
        {
            Destroy(gameObject);
        }

        SetHealth();
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

    void SetHealth()
    {
        text.text = "Health: " + GetHealth();
    }
}
