using UnityEngine;
using TMPro;

public class RoomIndicatorUI : MonoBehaviour
{
    public TMP_Text roomIndicatorText;

    private int lastRoomIndex = -1;
    private int lastRoomCount = -1;

    void Update()
    {
        if (RoomChainManager.Instance == null)
            return;

        int currentRoom = RoomChainManager.Instance.CurrentRoomIndex;
        int totalRooms = RoomChainManager.Instance.TotalRooms;

        if (totalRooms <= 0)
            return;

        if (currentRoom != lastRoomIndex || totalRooms != lastRoomCount)
        {
            roomIndicatorText.text = $"ROOM {currentRoom + 1} / {totalRooms}";

            lastRoomIndex = currentRoom;
            lastRoomCount = totalRooms;
        }
    }
}