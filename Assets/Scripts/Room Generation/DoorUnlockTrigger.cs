using UnityEngine;

/// <summary>
/// Goes on the little "player holder" capsule inside each room.
/// DungeonGenerator adds this automatically. When the player touches it,
/// the room's exit door opens.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorUnlockTrigger : MonoBehaviour
{
    RoomDoorController controller;

    void Awake()
    {
        // Must be a trigger so the player walks into it instead of bumping off it.
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<CharacterController>() == null) return;

        if (controller == null) controller = GetComponentInParent<RoomDoorController>();
        if (controller != null) controller.Open();
    }
}
