using UnityEngine;

public class Health : MonoBehaviour, IInteractable
{
    PlayerHealth player;

    public int health = 10;
    
    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
    }

    public void Interact(Collider col)
    {
        if (player != null)
        {
            Debug.Log("Picked up Health");
            
            player.OnHeal(health);

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("Player not found");
        }
    }
}
