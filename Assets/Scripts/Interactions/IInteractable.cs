using UnityEngine;

public interface IInteractable
{
    void Interact(Collider col);

    bool CanPickup()
    {
        return false;
    }
}
