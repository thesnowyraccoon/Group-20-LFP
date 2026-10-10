using UnityEngine;

/// <summary>
/// Placed at a room's doorway by DungeonGenerator. When the player walks
/// into it, asks RoomChainManager to transition to the connected room.
/// Works for both the forward door (deeper into the dungeon) and the
/// backward door (back toward the previous room), so backtracking works
/// the same way.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class RoomExitTrigger : MonoBehaviour
{
    [HideInInspector] public int targetRoomIndex = -1;
    [HideInInspector] public int worldDirection = -1;

    [Tooltip("Draws the trigger as an orange box in the Scene view, even when it isn't selected.")]
    public bool showGizmo = true;

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        TryTransition(other);
    }

    // Also checked while the player stays inside the trigger. If the player
    // touched the door during another transition (so the request was
    // ignored), this makes sure it still goes through afterwards.
    void OnTriggerStay(Collider other)
    {
        TryTransition(other);
    }

    void TryTransition(Collider other)
    {
        if (targetRoomIndex < 0) return;

        // GetComponentInParent so it still works if the player's collider
        // is on a child object.
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        RoomChainManager.Instance?.RequestTransition(targetRoomIndex, worldDirection);
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