using UnityEngine;

public class Jump : MonoBehaviour, IInteractable
{
    PlayerMovement player;

    public float jumpUpgrade = 2f;
    
    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerMovement>();
    }

    public void Interact(Collider col)
    {
        if (player != null)
        {
            Debug.Log("Picked up Jump");
            
            player.jumpForce += jumpUpgrade;

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("Player not found");
        }
    }
}
