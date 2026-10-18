
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomBehaviour : MonoBehaviour
{
    // 0 = Up, 1 = Down, 2 = Right, 3 = Left
    public GameObject[] walls;
    public GameObject[] doors;

    [Header("Final Demo Room")]
    public bool keepVictoryExitOpen = false;
    public int victoryExitDirection = 2;

    public void UpdateRoom(bool[] status)
    {
        for (int i = 0; i < status.Length; i++)
        {
            bool connected = status[i];

            // Keep Level 3's victory doorway open
            // even without a connected Level 4.
            if (keepVictoryExitOpen &&
                i == victoryExitDirection)
            {
                doors[i].SetActive(true);
                walls[i].SetActive(false);
            }
            else
            {
                // Quinn's original room generation behaviour
                doors[i].SetActive(connected);
                walls[i].SetActive(!connected);
            }
        }
    }
}
