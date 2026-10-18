
using UnityEngine;

public class Jump : MonoBehaviour, IInteractable
{
    PlayerMovement player;

    public float jumpUpgrade = 2f;

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
            Debug.Log("Picked up Jump");

            // Preserve Quinn's original jump upgrade
            player.jumpForce += jumpUpgrade;

            // Play the voice announcement
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

