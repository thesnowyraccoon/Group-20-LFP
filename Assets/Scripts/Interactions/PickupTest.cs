using System.Reflection.Metadata.Ecma335;
using UnityEngine;

public class PickupTest : MonoBehaviour, IInteractable
{
    public void Interact(Collider col)
    {
        Debug.Log("Picking Up");

        Destroy(gameObject);
    }

    public bool CanPickup()
    {
        return true;
    }
}
