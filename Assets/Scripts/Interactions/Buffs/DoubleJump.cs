
using UnityEngine;

public class DoubleJump : MonoBehaviour, IInteractable
{
    PlayerMovement player;

    public int maxJumpUpgrade = 2;

    [Header("Pickup Audio")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = 0.7f;

    void Awake()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.GetComponent<PlayerMovement>();
    }

    public bool CanPickup()
    {
        return false;
    }

    public void Interact(Collider col)
    {
        if (player != null)
        {
            Debug.Log("Picked up Double Jump");

            // Quinn's original double-jump functionality
            player.maxJumps = maxJumpUpgrade;

            // Play audio when the buff is collected
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(
                    pickupSound,
                    Camera.main != null
                        ? Camera.main.transform.position
                        : transform.position,
                    pickupVolume
                );
            }

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("Player not found");
        }
    }
}
