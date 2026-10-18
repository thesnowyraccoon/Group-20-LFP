
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoomExitTrigger : MonoBehaviour
{
    [HideInInspector] public int targetRoomIndex = -1;
    [HideInInspector] public int worldDirection = -1;

    [Header("Optional Gate Lock")]
    [Tooltip("If assigned, the player cannot transition while this object is active.")]
    public GameObject gateBlocker;

    [Tooltip("Draws the trigger as an orange box in the Scene view.")]
    public bool showGizmo = true;

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        TryTransition(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryTransition(other);
    }

    void TryTransition(Collider other)
    {
        if (targetRoomIndex < 0) return;

        // A locked gate prevents transitioning.
        if (gateBlocker != null && gateBlocker.activeInHierarchy)
            return;

        if (other.GetComponentInParent<PlayerMovement>() == null)
            return;

        RoomChainManager.Instance?.RequestTransition(
            targetRoomIndex,
            worldDirection
        );
    }

    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
