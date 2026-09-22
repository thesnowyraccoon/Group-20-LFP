using UnityEngine;

public class Speed : MonoBehaviour, IInteractable
{
    PlayerMovement player;

    public float speedUpgrade = 3f;
    
    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerMovement>();
    }

    public void Interact(Collider col)
    {
        if (player != null)
        {
            Debug.Log("Picked up Speed");

            player.walkMod += speedUpgrade;
            player.sprintMod += speedUpgrade;

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("Player not found");
        }
    }
}
