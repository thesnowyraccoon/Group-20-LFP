using TMPro;
using UnityEngine;

public class HealthUI : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public TMP_Text text;

    void Awake()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
    }

    void Update()
    {
        text.text = "Health: " + playerHealth.GetHealth();
    }
}
