using UnityEngine;

public class DoubleJump : MonoBehaviour, IInteractable
{
    PlayerMovement player;

    public int maxJumpUpgrade = 2;
    
    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerMovement>();
    }

    public void Interact(Collider col)
    {
        if (player != null)
        {
            Debug.Log("Picked up Double Jump");
            
            player.maxJumps = maxJumpUpgrade;

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("Player not found");
        }
    }
}
