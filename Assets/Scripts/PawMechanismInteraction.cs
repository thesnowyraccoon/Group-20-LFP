using UnityEngine;

public class PawMechanismInteraction : MonoBehaviour, IInteractable
{
    [Header("Gate")]
    public GameObject gateBlocker;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip activationSound;

    private bool activated;

    public bool CanPickup()
    {
        return false;
    }

    public void Interact(Collider col)
    {
        if (activated)
            return;

        activated = true;

        Debug.Log("Paw Mechanism activated!");

        if (audioSource != null && activationSound != null)
            audioSource.PlayOneShot(activationSound);

        if (gateBlocker != null)
        {
            gateBlocker.SetActive(false);
            Debug.Log("Gate unlocked!");
        }
        else
        {
            Debug.LogWarning(
                "PawMechanism: Gate blocker has not been assigned."
            );
        }
    }
}