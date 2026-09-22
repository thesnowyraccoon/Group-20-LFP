using UnityEngine;

public class InteractTest : MonoBehaviour, IInteractable
{
    public void Interact(Collider col)
    {
        Debug.Log("Interacting");

        Destroy(gameObject);
    }
}
