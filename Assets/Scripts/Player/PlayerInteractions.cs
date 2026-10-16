using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractions : MonoBehaviour
{
    public IInteractable interactable;

    public LayerMask interactMask;
    public GameObject interactIcon;
    public float range = 2f;

    private bool interactInput;

    void Update()
    {
        HandleInteraction();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
            interactInput = true;
    }

    void HandleInteraction()
    {
        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        bool hasHit = Physics.Raycast(
    origin,
    direction,
    out RaycastHit hit,
    range,
    interactMask
);

        bool interactableInRange =
            hasHit &&
            hit.collider.TryGetComponent(out interactable);

        if (interactIcon != null)
            interactIcon.SetActive(interactableInRange);

        if (interactableInRange)
        {
            if (interactable.CanPickup())
            {
                ActivateInteractions(hit.collider);
            }
            else if (interactInput)
            {
                ActivateInteractions(hit.collider);
            }
        }

        interactInput = false;
    }

    void ActivateInteractions(Collider target)
    {
        foreach (var interaction in target.GetComponents<IInteractable>())
        {
            interaction?.Interact(target);
        }
    }
}