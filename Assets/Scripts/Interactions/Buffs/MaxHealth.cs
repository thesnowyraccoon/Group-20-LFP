using UnityEngine;

public class MaxHealth : MonoBehaviour, IInteractable
{
    PlayerHealth player;

    public int maxHealth = 100;
    
    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
    }

    public void Interact(Collider col)
    {
        if (player != null)
        {
            Debug.Log("Picked up Health Upgrade");
            
            player.SetMaxHealth(maxHealth);

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("Player not found");
        }
    }
}
