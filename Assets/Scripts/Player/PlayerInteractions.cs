using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class PlayerInteractions : MonoBehaviour
{
    public IInteractable interactable;
    public LayerMask interactMask;

    public float range = 2f;

    bool interactInput;

    void Update()
    {
        HandleInteraction();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        interactInput = context.performed;
    }

    void HandleInteraction()
    {
        Vector3 origin = transform.position;
        Vector3 direction = transform.TransformDirection(Vector3.forward);

        RaycastHit hit;
        Physics.Raycast(origin, direction, out hit, range, interactMask);
        Debug.DrawRay(origin, direction * hit.distance, Color.purple);
        
        bool interactableInRange = hit.collider != null && hit.collider.TryGetComponent(out interactable);

        if (interactableInRange)
        {
            // Shows interact UI/GUI
        }
        else
        {
            // Shows nothing
        }

        if (interactableInRange && interactable.CanPickup())
        {
            foreach (var script in hit.collider.GetComponents<IInteractable>())
            {
                if (script != null)
                {
                    script.Interact(hit.collider);
                }
                else
                {
                    Debug.LogWarning("Interaction not found");
                }
            }
        }

        if (interactInput && interactableInRange)
        {
            foreach (var script in hit.collider.GetComponents<IInteractable>())
            {
                if (script != null)
                {
                    script.Interact(hit.collider);
                }
                else
                {
                    Debug.LogWarning("Interaction not found");
                }
            }
        }
    }
}
