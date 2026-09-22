using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Implement this on a camera-follow script if your camera lerps/smooths
/// toward the player. RoomChainManager will call SnapToTarget() right
/// after teleporting the player so the camera doesn't visibly "catch up"
/// while the screen fades back in. Optional — safe to leave unimplemented.
/// </summary>
public interface ICameraSnap
{
    void SnapToTarget();
}

/// <summary>
/// Keeps only one dungeon room active at a time (Cult of the Lamb style
/// isolation). DungeonGenerator calls Initialize() once, after it has
/// spawned every room, handing over the room list plus the player's
/// CharacterController. From then on, RoomExitTrigger doors call
/// RequestTransition() whenever the player walks through a doorway.
/// </summary>
public class RoomChainManager : MonoBehaviour
{
    public static RoomChainManager Instance { get; private set; }

    [System.Serializable]
    public class RoomEntry
    {
        public GameObject instance;
        public Vector3 worldPosition;
        public int incomingDir = -1;
        public int outgoingDir = -1;
    }

    [Header("Transition")]

    [Tooltip("Seconds to fade to black, and again to fade back in.")]
    public float fadeDuration = 0.35f;

    [Tooltip("How far inside the room, from the doorway, the player is placed after a transition.")]
    public float spawnInset = 2f;

    List<RoomEntry> rooms = new List<RoomEntry>();
    int currentIndex;
    bool isTransitioning;

    CharacterController player;
    Transform playerTransform;

    CanvasGroup fadeGroup;

    void Awake()
    {
        Instance = this;
        BuildFadeCanvas();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void BuildFadeCanvas()
    {
        GameObject canvasGO = new GameObject("RoomFadeCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject imageGO = new GameObject("Fade");
        imageGO.transform.SetParent(canvasGO.transform, false);

        Image image = imageGO.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeGroup = imageGO.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;
    }

    /// <summary>
    /// Called once by DungeonGenerator after all rooms are spawned.
    /// Hides every room except the starting one.
    /// </summary>
    public void Initialize(List<RoomEntry> generatedRooms, int startIndex, CharacterController playerController)
    {
        rooms = generatedRooms;
        currentIndex = startIndex;
        player = playerController;
        playerTransform = playerController != null ? playerController.transform : null;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].instance != null)
            {
                rooms[i].instance.SetActive(i == currentIndex);
            }
        }
    }

    /// <summary>
    /// Called by a RoomExitTrigger when the player walks through a doorway.
    /// travelDirection is the world direction (0=Up/+Z, 1=Down/-Z, 2=Right/+X,
    /// 3=Left/-X) the player is walking in as they cross that door.
    /// </summary>
    public void RequestTransition(int targetIndex, int travelDirection)
    {
        if (isTransitioning) return;
        if (rooms == null) return;
        if (targetIndex < 0 || targetIndex >= rooms.Count) return;
        if (targetIndex == currentIndex) return;

        StartCoroutine(TransitionRoutine(targetIndex, travelDirection));
    }

    IEnumerator TransitionRoutine(int targetIndex, int travelDirection)
    {
        isTransitioning = true;

        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        movement?.SetInputLocked(true);

        yield return Fade(0f, 1f);

        if (rooms[currentIndex].instance != null)
        {
            rooms[currentIndex].instance.SetActive(false);
        }

        currentIndex = targetIndex;

        if (rooms[currentIndex].instance != null)
        {
            rooms[currentIndex].instance.SetActive(true);
        }

        PlaceInRoom(rooms[currentIndex], travelDirection);

        Camera sceneCamera = FindAnyObjectByType<Camera>();
        ICameraSnap camSnap = sceneCamera != null ? sceneCamera.GetComponent<ICameraSnap>() : null;
        camSnap?.SnapToTarget();

        yield return Fade(1f, 0f);

        movement?.SetInputLocked(false);

        isTransitioning = false;
    }

    void PlaceInRoom(RoomEntry room, int travelDirection)
    {
        if (player == null || playerTransform == null) return;

        Vector3 doorDirWorld = DirectionToWorldVector(travelDirection);

        // Step the player in just past the doorway, facing the direction
        // they were already walking, so it reads as continuous movement.
        Vector3 spawnPosition = room.worldPosition - doorDirWorld * spawnInset;
        spawnPosition.y = playerTransform.position.y;

        Quaternion facing = doorDirWorld.sqrMagnitude > 0f
            ? Quaternion.LookRotation(doorDirWorld, Vector3.up)
            : playerTransform.rotation;

        // The CharacterController resists direct transform changes while
        // enabled, so we briefly disable it to move the player cleanly.
        player.enabled = false;
        playerTransform.SetPositionAndRotation(spawnPosition, facing);
        player.enabled = true;
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = to;
    }

    Vector3 DirectionToWorldVector(int direction)
    {
        switch (direction)
        {
            case 0: return new Vector3(0f, 0f, 1f);
            case 1: return new Vector3(0f, 0f, -1f);
            case 2: return new Vector3(1f, 0f, 0f);
            case 3: return new Vector3(-1f, 0f, 0f);
        }

        return Vector3.zero;
    }
}
