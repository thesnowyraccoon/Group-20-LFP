using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Implement this on a camera-follow script if your camera lerps/smooths
/// toward the player. RoomChainManager will call SnapToTarget() right
/// after teleporting the player so the camera doesn't visibly "catch up"
/// while the screen fades back in. Optional - safe to leave unimplemented.
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

        // Real half-size of the room's floor (middle to edge). Set by DungeonGenerator.
        public float halfWidth;   // along world X
        public float halfHeight;  // along world Z

        // Height of the top of the floor. Set by DungeonGenerator.
        public float floorTopY;
    }

    [Header("Transition")]

    [Tooltip("Seconds to fade to black, and again to fade back in.")]
    public float fadeDuration = 0.35f;

    [Tooltip("How far inside the room, measured from the doorway, the player appears after a transition.")]
    public float spawnInset = 3f;

    [Tooltip("Seconds after a transition during which doors are ignored. Stops the player bouncing back and forth.")]
    public float doorCooldown = 0.75f;

    [Header("Safety")]

    [Tooltip("Turn the fall safety net on or off completely.")]
    public bool enableFallSafety = true;

    [Tooltip("Player is OUTSIDE the room (fell off the edge or out of the world). If they drop this far below the floor, they are put back at the last safe spot.")]
    public float fallLimit = 12f;

    [Tooltip("Player is INSIDE the room (falling into a pit while platforming). The safety net stays out of the way unless they drop this far, which only happens if something is really broken. Your own pit respawn should kick in long before this.")]
    public float pitFallLimit = 100f;

    [Tooltip("Extra distance around the room's floor that still counts as 'inside the room'.")]
    public float insideRoomMargin = 2f;

    [Tooltip("How far the script looks for solid ground around the spawn point.")]
    public float groundSearchRadius = 8f;

    List<RoomEntry> rooms = new List<RoomEntry>();
    int currentIndex;
    bool isTransitioning;
    float ignoreDoorsUntil;

    public int CurrentRoomIndex => currentIndex;
    public int TotalRooms => rooms != null ? rooms.Count : 0;

    CharacterController player;
    Transform playerTransform;

    Vector3 lastSafePosition;
    Quaternion lastSafeRotation;
    bool hasSafePosition;

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
    /// Hides every room except the starting one and puts the player on its floor.
    /// </summary>
  public void Initialize(
    List<RoomEntry> generatedRooms,
    int startIndex,
    CharacterController playerController)
{
    rooms = generatedRooms;
    currentIndex = startIndex;
    player = playerController;
    playerTransform = playerController != null
        ? playerController.transform
        : null;

    for (int i = 0; i < rooms.Count; i++)
    {
        if (rooms[i].instance != null)
        {
            rooms[i].instance.SetActive(i == currentIndex);
        }
    }

    if (player == null || playerTransform == null || rooms.Count == 0)
        return;

    RoomEntry startRoom = rooms[currentIndex];

    Vector3 nominal = playerTransform.position;
    Quaternion facing = playerTransform.rotation;
    Vector3 inward = Vector3.forward;

    if (startRoom.instance != null)
    {
        Transform[] markers =
            startRoom.instance.GetComponentsInChildren<Transform>(true);

        foreach (Transform marker in markers)
        {
            if (marker.name == "Player Spawn")
            {
                nominal = marker.position;
                facing = marker.rotation;
                inward = marker.forward;

                Debug.Log("Using Level 1 Player Spawn marker.");
                break;
            }
        }
    }

    if (TryFindGroundedSpawn(
        nominal,
        inward,
        startRoom,
        out Vector3 position))
    {
        MovePlayer(position, facing);
        RememberSafeSpot(position, facing);

        Debug.Log("Player spawned at: " + position);
    }
    else
    {
        Debug.LogWarning(
            "Could not find safe ground near Player Spawn."
        );
    }
}

    void Update()
    {
        if (player == null || playerTransform == null) return;
        if (isTransitioning || !hasSafePosition) return;
        if (rooms == null || rooms.Count == 0) return;

        if (!enableFallSafety) return;

        RoomEntry room = rooms[currentIndex];
        Vector3 p = playerTransform.position;

        // Is the player above the room's footprint? If so, a fall is just a
        // pit (part of the platforming), not the player leaving the room.
        bool insideRoom =
            Mathf.Abs(p.x - room.worldPosition.x) <= room.halfWidth + insideRoomMargin &&
            Mathf.Abs(p.z - room.worldPosition.z) <= room.halfHeight + insideRoomMargin;

        float limit = insideRoom ? pitFallLimit : fallLimit;

        if (p.y < room.floorTopY - limit)
        {
            Debug.LogWarning("RoomChainManager: player fell " +
                             (insideRoom ? "through room " : "out of room ") + currentIndex +
                             ". Putting them back at the last safe spot.");

            RespawnAtLastSafeSpot();
        }
    }

    /// <summary>
    /// Puts the player back where they entered the current room.
    /// Call this from your own pit / kill-zone script, for example:
    /// RoomChainManager.Instance.RespawnAtLastSafeSpot();
    /// </summary>
    public void RespawnAtLastSafeSpot()
    {
        if (player == null || playerTransform == null || !hasSafePosition) return;

        MovePlayer(lastSafePosition, lastSafeRotation);
    }

    /// <summary>
    /// Called by a RoomExitTrigger when the player walks through a doorway.
    /// travelDirection is the world direction (0=Up/+Z, 1=Down/-Z, 2=Right/+X,
    /// 3=Left/-X) the player is walking in as they cross that door.
    /// </summary>
    public void RequestTransition(int targetIndex, int travelDirection)
    {
        if (isTransitioning) return;
        if (Time.time < ignoreDoorsUntil) return;
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

        int previousIndex = currentIndex;
        if (rooms[currentIndex].instance != null)
        {
            rooms[currentIndex].instance.SetActive(false);
        }

        currentIndex = targetIndex;

        if (rooms[currentIndex].instance != null)
        {
            rooms[currentIndex].instance.SetActive(true);
        }

        if (!PlaceInRoom(rooms[currentIndex], travelDirection))
        {
            rooms[currentIndex].instance.SetActive(false);
            currentIndex = previousIndex;
            rooms[currentIndex].instance.SetActive(true);
            RespawnAtLastSafeSpot();
            Debug.LogWarning("Room transition cancelled: destination has no safe landing near its doorway.");
        }

        Camera sceneCamera = FindAnyObjectByType<Camera>();
        ICameraSnap camSnap = sceneCamera != null ? sceneCamera.GetComponent<ICameraSnap>() : null;
        camSnap?.SnapToTarget();

        yield return Fade(1f, 0f);

        movement?.SetInputLocked(false);

        ignoreDoorsUntil = Time.time + doorCooldown;
        isTransitioning = false;
    }

    bool PlaceInRoom(RoomEntry room, int travelDirection)
    {
        if (player == null || playerTransform == null) return false;

        Vector3 travelDirWorld = DirectionToWorldVector(travelDirection);

        // The player enters through the wall they are walking away from.
        bool movingSideways = (travelDirection == 2 || travelDirection == 3);
        float halfToDoor = movingSideways ? room.halfWidth : room.halfHeight;

        // Start at the door, then step inward by spawnInset.
        float distanceFromMiddle = Mathf.Max(0f, halfToDoor - spawnInset);

        Vector3 nominal = room.worldPosition - travelDirWorld * distanceFromMiddle;

        // The generator aligns exits with authored doorways, which may be offset
        // from the floor centre. Arrive inside that same doorway, past its trigger.
        Physics.SyncTransforms();
        foreach (RoomExitTrigger exit in room.instance.GetComponentsInChildren<RoomExitTrigger>())
        {
            if (exit.worldDirection != (travelDirection ^ 1)) continue;
            BoxCollider doorway = exit.GetComponent<BoxCollider>();
            float triggerHalfDepth = movingSideways ? doorway.bounds.extents.x : doorway.bounds.extents.z;
            float radius = player.radius * Mathf.Max(Mathf.Abs(playerTransform.lossyScale.x), Mathf.Abs(playerTransform.lossyScale.z));
            nominal = doorway.bounds.center + travelDirWorld * Mathf.Max(spawnInset, triggerHalfDepth + radius + 0.25f);
            break;
        }

        // Find real solid ground near that spot (the floor may have pits or rocks).
        if (!TryFindGroundedSpawn(nominal, travelDirWorld, room, out Vector3 spawnPosition)) return false;

        Quaternion facing = travelDirWorld.sqrMagnitude > 0f
            ? Quaternion.LookRotation(travelDirWorld, Vector3.up)
            : playerTransform.rotation;

        MovePlayer(spawnPosition, facing);
        RememberSafeSpot(spawnPosition, facing);
        return true;
    }

    /// <summary>
    /// Finds a spot with solid floor under it, as close to 'nominal' as possible.
    /// Searches further into the room and to the sides if the first spot has no ground.
    /// Returns a position for the player's transform (not the feet).
    /// </summary>
    bool TryFindGroundedSpawn(Vector3 nominal, Vector3 inward, RoomEntry room, out Vector3 position)
    {
        Physics.SyncTransforms();

        inward.y = 0f;
        if (inward.sqrMagnitude < 0.0001f) inward = Vector3.forward;
        inward.Normalize();

        Vector3 sideways = new Vector3(inward.z, 0f, -inward.x);
        var lateral = new List<float> { 0f };
        for (float offset = 1.5f; offset <= groundSearchRadius; offset += 1.5f)
        {
            lateral.Add(offset);
            lateral.Add(-offset);
        }

        for (float step = 0f; step <= groundSearchRadius; step += 1f)
        {
            foreach (float side in lateral)
            {
                Vector3 candidate = nominal + inward * step + sideways * side;

                if (TryFindGround(candidate, room, out float groundY))
                {
                    Vector3 landing = new Vector3(candidate.x, GroundToTransformY(groundY), candidate.z);
                    if (!HasLandingClearance(landing, room, groundY)) continue;
                    position = landing;
                    return true;
                }
            }
        }

        position = default;
        return false;
    }

    bool TryFindGround(Vector3 point, RoomEntry room, out float groundY)
    {
        groundY = float.MinValue;
        // Entrances may use raised rocks/platforms above the base floor. Keep
        // the search below wall tops and above the deliberately lowered pits.
        Vector3 origin = new Vector3(point.x, room.floorTopY + 4f, point.z);
        foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(room.instance.transform)) continue;
            if (hit.normal.y < Mathf.Cos(player.slopeLimit * Mathf.Deg2Rad)) continue;
            if (hit.point.y < room.floorTopY - 1f || hit.point.y > room.floorTopY + 3f) continue;
            groundY = Mathf.Max(groundY, hit.point.y);
        }
        return groundY > float.MinValue;
    }

    bool HasLandingClearance(Vector3 position, RoomEntry room, float groundY)
    {
        float radius = player.radius * Mathf.Max(Mathf.Abs(playerTransform.lossyScale.x), Mathf.Abs(playerTransform.lossyScale.z));
        // Check support around the feet too, so a ray hitting a narrow edge
        // cannot approve a capsule hanging over the pit.
        foreach (Vector3 offset in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            if (!TryFindGround(position + offset * radius, room, out float supportY) || Mathf.Abs(supportY - groundY) > 0.35f)
                return false;
        }
        float height = Mathf.Max(player.height * Mathf.Abs(playerTransform.lossyScale.y), radius * 2f);
        Vector3 bottom = new Vector3(position.x, groundY + radius + 0.08f, position.z);
        Vector3 top = bottom + Vector3.up * (height - radius * 2f);
        foreach (Collider obstacle in Physics.OverlapCapsule(bottom, top, radius * 0.95f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!obstacle.transform.IsChildOf(playerTransform)) return false;
        }
        return true;
    }

    // Converts "height of the ground" into "height for the player's transform",
    // so the bottom of the capsule rests just above the ground.
    float GroundToTransformY(float groundY)
    {
        float scaleY = Mathf.Abs(playerTransform.lossyScale.y);
        float feetToPivot = (player.height * 0.5f - player.center.y) * scaleY;
        return groundY + feetToPivot + 0.05f;
    }

    void MovePlayer(Vector3 position, Quaternion rotation)
    {
        // The CharacterController resists direct transform changes while
        // enabled, so we briefly disable it to move the player cleanly.
        player.enabled = false;
        playerTransform.SetPositionAndRotation(position, rotation);
        player.enabled = true;

        // Optional: if your PlayerMovement has a method called OnTeleported,
        // it is called here so you can reset falling speed.
        player.SendMessage("OnTeleported", SendMessageOptions.DontRequireReceiver);
    }

    void RememberSafeSpot(Vector3 position, Quaternion rotation)
    {
        lastSafePosition = position;
        lastSafeRotation = rotation;
        hasSafePosition = true;
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
