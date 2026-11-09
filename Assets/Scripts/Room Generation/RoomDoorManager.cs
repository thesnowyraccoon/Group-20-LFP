using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Added to every room by DungeonGenerator. Holds the room's exit door(s).
/// When Open() is called (by DoorUnlockTrigger) the camera pans to the door,
/// the door shrinks away with an optional particle effect, the camera pans
/// back, and only then does the walk-through trigger switch on.
/// </summary>
public class RoomDoorController : MonoBehaviour
{
    [Header("Camera")]
    public float panToDoorTime = 0.6f;
    public float holdTime = 0.5f;
    public float panBackTime = 0.6f;

    [Header("Vanish")]
    public float vanishDuration = 0.7f;
    public GameObject vanishEffectPrefab;

    List<Transform> exitDoors = new List<Transform>();
    Collider exitTrigger;
    bool opened;

    public bool IsOpen => opened;

    public void Setup(List<Transform> doors, Collider trigger)
    {
        exitDoors = doors ?? new List<Transform>();
        exitTrigger = trigger;

        if (exitTrigger != null) exitTrigger.enabled = false;
    }

    /// <summary>Opens with the full camera pan + vanish effect.</summary>
    public void Open()
    {
        if (opened) return;
        opened = true;

        StartCoroutine(OpenRoutine());
    }

    /// <summary>Opens instantly with no effects (used as a fallback).</summary>
    public void OpenImmediately()
    {
        opened = true;

        foreach (Transform door in exitDoors)
        {
            if (door != null) door.gameObject.SetActive(false);
        }

        if (exitTrigger != null) exitTrigger.enabled = true;
    }

    IEnumerator OpenRoutine()
    {
        RoomChainManager manager = RoomChainManager.Instance;
        Vector3 focus = GetDoorCentre();

        if (manager != null)
            yield return manager.PanCameraTo(focus, panToDoorTime);

        if (vanishEffectPrefab != null)
        {
            GameObject fx = Instantiate(vanishEffectPrefab, focus, Quaternion.identity);
            Destroy(fx, 4f);
        }

        foreach (Transform door in exitDoors)
        {
            if (door != null) StartCoroutine(Vanish(door));
        }

        yield return new WaitForSeconds(vanishDuration + holdTime);

        if (manager != null)
            yield return manager.PanCameraBack(panBackTime);

        if (exitTrigger != null) exitTrigger.enabled = true;
    }

    IEnumerator Vanish(Transform door)
    {
        foreach (Collider c in door.GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }

        Vector3 startScale = door.localScale;
        float t = 0f;

        while (t < vanishDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / vanishDuration);

            // Quick little "pop" outward, then shrink to nothing.
            float pop = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);
            float shrink = 1f - k * k;

            door.localScale = startScale * (pop * shrink);
            yield return null;
        }

        door.gameObject.SetActive(false);
        door.localScale = startScale;
    }

    Vector3 GetDoorCentre()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (Transform door in exitDoors)
        {
            if (door == null) continue;

            Renderer r = door.GetComponentInChildren<Renderer>();
            sum += r != null ? r.bounds.center : door.position;
            count++;
        }

        return count > 0 ? sum / count : transform.position;
    }
}