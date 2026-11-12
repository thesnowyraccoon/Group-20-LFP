using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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
/// Keeps only one dungeon room active at a time. DungeonGenerator calls
/// Initialize() once, after it has spawned every room. RoomExitTrigger doors
/// call RequestTransition() when the player walks through an OPEN doorway.
/// Walking through the last room's door (target index == room count) means
/// the player has escaped.
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

        public float halfWidth;
        public float halfHeight;
        public float floorTopY;

        // World position of the ENTRANCE doorway centre, measured by DungeonGenerator.
        // hasEntryPoint is false if the doorway object couldn't be found.
        public bool hasEntryPoint;
        public Vector3 entryPoint;
    }

    [Header("Transition")]
    public float fadeDuration = 0.35f;
    public float spawnInset = 3f;
    public float doorCooldown = 0.75f;

    [Header("Door Camera Pan")]
    [Tooltip("The script that makes your camera follow the player. It is switched off while the camera pans to a door. " +
             "Leave empty to auto-detect a script on the camera that implements ICameraSnap.")]
    public MonoBehaviour cameraFollowScript;

    [Tooltip("Where on the screen the door should end up. (0.5, 0.5) = dead centre. " +
             "X and Y go from 0 (left / bottom) to 1 (right / top).")]
    public Vector2 doorViewportPosition = new Vector2(0.5f, 0.5f);

    [Tooltip("Aim this far ABOVE the door's pivot, so a door whose pivot is on the floor is framed by its middle.")]
    public float doorFocusHeight = 1f;

    [Tooltip("Safety limit on how far the camera may slide from its starting position (flat distance). " +
             "0 = no limit. Keep this larger than your biggest room, or far doors will end up off-screen.")]
    public float maxPanDistance = 60f;

    [Header("Escape")]
    [Tooltip("Fires after the player walks through the final door and the screen has faded to black. Hook up your win screen / scene load here.")]
    public UnityEvent onEscaped;

    [Header("Safety")]
    public bool enableFallSafety = true;
    public float fallLimit = 12f;
    public float pitFallLimit = 100f;
    public float insideRoomMargin = 2f;
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

    // Camera pan state
    bool panning;
    Transform panCamera;
    Vector3 panStartPosition;
    MonoBehaviour pausedFollow;

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

        if (player != null && playerTransform != null && rooms.Count > 0)
        {
            RoomEntry startRoom = rooms[currentIndex];

            Vector3 nominal = playerTransform.position;
            Vector3 position = FindGroundedSpawn(nominal, Vector3.forward, startRoom);

            MovePlayer(position, playerTransform.rotation);
            RememberSafeSpot(position, playerTransform.rotation);
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

    public void RespawnAtLastSafeSpot()
    {
        if (player == null || playerTransform == null || !hasSafePosition) return;

        MovePlayer(lastSafePosition, lastSafeRotation);
    }

    // ------------------------------------------------------------------
    // Door camera pan (called by RoomDoorController)
    // ------------------------------------------------------------------

    /// <summary>
    /// Locks the player and slides the camera so the focus point (the door)
    /// ends up at doorViewportPosition on screen. Always follow with PanCameraBack().
    /// </summary>
    public IEnumerator PanCameraTo(Vector3 focusPoint, float duration)
    {
        Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (cam == null || playerTransform == null) yield break;

        if (!panning)
        {
            panning = true;
            panCamera = cam.transform;
            panStartPosition = panCamera.position;

            SetPlayerLocked(true);

            pausedFollow = cameraFollowScript;

            if (pausedFollow == null)
            {
                ICameraSnap snap = cam.GetComponent<ICameraSnap>();
                pausedFollow = snap as MonoBehaviour;
            }

            if (pausedFollow != null) pausedFollow.enabled = false;
        }

        Vector3 target = CalculatePanTarget(cam, focusPoint);

        yield return MoveCamera(panCamera.position, target, duration);
    }

    /// <summary>
    /// Works out where the camera has to be for the door to appear at
    /// doorViewportPosition. A ray is fired from the camera through that
    /// screen point down to the door's height; the gap between where the ray
    /// lands and the door is exactly how far the camera has to slide.
    /// </summary>
    Vector3 CalculatePanTarget(Camera cam, Vector3 focusPoint)
    {
        Vector3 focus = focusPoint + Vector3.up * doorFocusHeight;

        Vector3 delta;

        Ray ray = cam.ViewportPointToRay(new Vector3(doorViewportPosition.x, doorViewportPosition.y, 0f));
        Plane doorHeightPlane = new Plane(Vector3.up, focus);

        if (doorHeightPlane.Raycast(ray, out float enter) && enter > 0f)
        {
            Vector3 whereCameraLooks = ray.GetPoint(enter);
            delta = focus - whereCameraLooks;
        }
        else
        {
            // Camera is looking level or upward, so the ray never reaches the plane.
            // Fall back to simply moving by the player-to-door offset.
            delta = focus - playerTransform.position;
        }

        delta.y = 0f;

        Vector3 target = panCamera.position + delta;

        if (maxPanDistance > 0f)
        {
            Vector3 fromStart = target - panStartPosition;
            fromStart.y = 0f;

            if (fromStart.magnitude > maxPanDistance)
            {
                Debug.LogWarning("RoomChainManager: the door is further than Max Pan Distance (" + maxPanDistance +
                                 ") from the camera, so it may not be fully in frame. Increase Max Pan Distance.");

                fromStart = Vector3.ClampMagnitude(fromStart, maxPanDistance);
                target = new Vector3(panStartPosition.x + fromStart.x, panCamera.position.y, panStartPosition.z + fromStart.z);
            }
        }

        // Height never changes during a pan.
        target.y = panCamera.position.y;

        return target;
    }

    public IEnumerator PanCameraBack(float duration)
    {
        if (!panning) yield break;

        yield return MoveCamera(panCamera.position, panStartPosition, duration);

        if (pausedFollow != null) pausedFollow.enabled = true;
        pausedFollow = null;

        Camera cam = panCamera != null ? panCamera.GetComponent<Camera>() : null;
        ICameraSnap snap = cam != null ? cam.GetComponent<ICameraSnap>() : null;
        snap?.SnapToTarget();

        SetPlayerLocked(false);
        panning = false;
    }

    IEnumerator MoveCamera(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            panCamera.position = Vector3.Lerp(from, to, k);
            yield return null;
        }

        panCamera.position = to;
    }

    void SetPlayerLocked(bool locked)
    {
        if (player == null) return;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        movement?.SetInputLocked(locked);
    }

    // ------------------------------------------------------------------
    // Transitions
    // ------------------------------------------------------------------

    public void RequestTransition(int targetIndex, int travelDirection)
    {
        if (isTransitioning || panning) return;
        if (Time.time < ignoreDoorsUntil) return;
        if (rooms == null) return;

        // Walking through the final room's door = escaped.
        if (targetIndex == rooms.Count)
        {
            StartCoroutine(EscapeRoutine());
            return;
        }

        if (targetIndex < 0 || targetIndex >= rooms.Count) return;
        if (targetIndex == currentIndex) return;

        StartCoroutine(TransitionRoutine(targetIndex, travelDirection));
    }

    IEnumerator EscapeRoutine()
    {
        isTransitioning = true;

        SetPlayerLocked(true);

        yield return Fade(0f, 1f);

        Debug.Log("RoomChainManager: the player escaped all " + rooms.Count + " rooms!");
        onEscaped?.Invoke();
    }

    IEnumerator TransitionRoutine(int targetIndex, int travelDirection)
    {
        isTransitioning = true;

        SetPlayerLocked(true);

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

        SetPlayerLocked(false);

        ignoreDoorsUntil = Time.time + doorCooldown;
        isTransitioning = false;
    }

    void PlaceInRoom(RoomEntry room, int travelDirection)
    {
        if (player == null || playerTransform == null) return;

        Vector3 travelDirWorld = DirectionToWorldVector(travelDirection);

        Vector3 nominal;

        if (room.hasEntryPoint)
        {
            // Step inward from the middle of the entrance doorway, so the player
            // appears lined up with the door they just came through.
            nominal = room.entryPoint + travelDirWorld * spawnInset;
            nominal.y = room.floorTopY;
        }
        else
        {
            // Fallback: the middle of the wall the player came in through.
            bool movingSideways = (travelDirection == 2 || travelDirection == 3);
            float halfToDoor = movingSideways ? room.halfWidth : room.halfHeight;

            float distanceFromMiddle = Mathf.Max(0f, halfToDoor - spawnInset);

            nominal = room.worldPosition - travelDirWorld * distanceFromMiddle;
        }

        Vector3 spawnPosition = FindGroundedSpawn(nominal, travelDirWorld, room);

        Quaternion facing = travelDirWorld.sqrMagnitude > 0f
            ? Quaternion.LookRotation(travelDirWorld, Vector3.up)
            : playerTransform.rotation;

        MovePlayer(spawnPosition, facing);
        RememberSafeSpot(spawnPosition, facing);
    }

    Vector3 FindGroundedSpawn(Vector3 nominal, Vector3 inward, RoomEntry room)
    {
        Physics.SyncTransforms();

        inward.y = 0f;
        if (inward.sqrMagnitude < 0.0001f) inward = Vector3.forward;
        inward.Normalize();

        Vector3 sideways = new Vector3(inward.z, 0f, -inward.x);
        float[] lateral = { 0f, 1.5f, -1.5f, 3f, -3f };

        for (float step = 0f; step <= groundSearchRadius; step += 1f)
        {
            foreach (float side in lateral)
            {
                Vector3 candidate = nominal + inward * step + sideways * side;

                if (TryFindGround(candidate, room.floorTopY, out float groundY))
                {
                    return new Vector3(candidate.x, GroundToTransformY(groundY), candidate.z);
                }
            }
        }

        Debug.LogWarning("RoomChainManager: no solid ground found near the spawn point. " +
                         "Using the floor height instead.");

        return new Vector3(nominal.x, GroundToTransformY(room.floorTopY), nominal.z);
    }

    bool TryFindGround(Vector3 point, float floorTopY, out float groundY)
    {
        groundY = 0f;

        Vector3 origin = new Vector3(point.x, floorTopY + 6f, point.z);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            30f,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        bool found = false;
        float best = float.MinValue;

        foreach (RaycastHit hit in hits)
        {
            if (playerTransform != null && hit.collider.transform.IsChildOf(playerTransform)) continue;
            if (hit.normal.y < 0.5f) continue;

            if (hit.point.y < floorTopY - 1f || hit.point.y > floorTopY + 0.5f) continue;

            if (hit.point.y > best)
            {
                best = hit.point.y;
                found = true;
            }
        }

        if (found) groundY = best;
        return found;
    }

    float GroundToTransformY(float groundY)
    {
        float scaleY = Mathf.Abs(playerTransform.lossyScale.y);
        float feetToPivot = (player.height * 0.5f - player.center.y) * scaleY;
        return groundY + feetToPivot + 0.05f;
    }

    void MovePlayer(Vector3 position, Quaternion rotation)
    {
        player.enabled = false;
        playerTransform.SetPositionAndRotation(position, rotation);
        player.enabled = true;

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