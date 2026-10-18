
using UnityEngine;

public class HealingCharm : MonoBehaviour, IInteractable
{
    public int healAmount = 25;

    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = 0.7f;

    public bool CanPickup()
    {
        return false;
    }

    public void Interact(Collider col)
    {
        PlayerHealth health =
            col.GetComponentInParent<PlayerHealth>();

        if (health == null)
           health = FindAnyObjectByType<PlayerHealth>();

        if (health == null)
            return;

        // Don't waste the pickup if the player has full health.
        if (health.GetHealth() >= health.maxHealth)
        {
            Debug.Log("Health already full");
            return;
        }

        health.OnHeal(healAmount);

        if (pickupSound != null)
        {
            Vector3 audioPosition = Camera.main != null
                ? Camera.main.transform.position
                : transform.position;

            AudioSource.PlayClipAtPoint(
                pickupSound,
                audioPosition,
                pickupVolume
            );
        }

        Debug.Log("Healing Charm collected!");
        Destroy(gameObject);
    }
}

