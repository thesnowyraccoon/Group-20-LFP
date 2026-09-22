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

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (targetRoomIndex < 0) return;

        // Identify the player by the movement component rather than a
        // tag, so there's no extra scene setup required.
        if (other.GetComponent<PlayerMovement>() == null) return;

        RoomChainManager.Instance?.RequestTransition(targetRoomIndex, worldDirection);
    }
}
