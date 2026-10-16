using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public enum RoomDifficulty { Plain, Easy, Medium, Hard }

    [System.Serializable]
    public class RoomVariant
    {
        public GameObject prefab;
        public int entryDoor = 3;
        public int exitDoor = 2;
    }

    private class GeneratedRoom
    {
        public Vector2Int gridPosition;
        public RoomDifficulty difficulty;
        public int incomingDir = -1;
        public int outgoingDir = -1;
        public bool[] status = new bool[4];
        public GameObject prefab;
        public float rotationY;
        public Vector3 worldPosition;
        public GameObject spawnedInstance;

        // Real size of the room in the world (measured after it is placed + rotated).
        public float halfWidth;   // half size along world X
        public float halfHeight;  // half size along world Z

        // Height of the top of the room's floor in the world.
        public float floorTopY;
    }

    [Header("Dungeon Settings")]
    [Min(7)]
    public int roomCount = 7;
    public Vector2Int gridSize = new Vector2Int(9, 9);
    public Vector2Int startPosition = new Vector2Int(4, 4);

    [Header("Dungeon Origin")]
    public Vector3 dungeonOrigin = new Vector3(0f, -0.81f, -6.61f);
    [Tooltip("Optional. If assigned, the dungeon X and Z position will use this Transform instead of Dungeon Origin.")]
    public Transform startTransform;

    [Header("Room Size / Spacing")]
    [Tooltip("ON = the script measures your room prefabs and sets the spacing for you. Recommended.")]
    public bool autoMeasureRooms = true;

    [Tooltip("Empty space left between rooms (rooms are never visible together, so this is just a safety gap).")]
    public float gapBetweenRooms = 2f;

    [Tooltip("Only used if Auto Measure Rooms is OFF.")]
    public float horizontalRoomSpacing = 30f;

    [Tooltip("Only used if Auto Measure Rooms is OFF.")]
    public float verticalRoomSpacing = 30f;

    [Header("Plain Rooms")]
    public GameObject[] plainRooms;

[Header("Starting Room")]
public GameObject startingRoomPrefab;

    [Header("Easy Rooms")]
    public RoomVariant[] easyRooms;

    [Header("Medium Rooms")]
    public RoomVariant[] mediumRooms;

    [Header("Hard Rooms")]
    public RoomVariant[] hardRooms;

    [Header("Room Isolation (Cult of the Lamb style)")]
    [Tooltip("Width of the trigger along the doorway.")]
    public float doorTriggerWidth = 3f;

    [Tooltip("Height of the trigger.")]
    public float doorTriggerHeight = 3f;

    [Tooltip("Depth of the trigger (how far the player walks into it). It sits just INSIDE the room, touching the wall.")]
    public float doorTriggerDepth = 1.5f;

    [Tooltip("Slides every door trigger along the doorway. Positive = toward the wall/outside, negative = further into the room. Use this to fine-tune.")]
    public float doorTriggerOffset = 0.5f;

    [Tooltip("Extra width added to each side of the doorway opening when the script can find the doorway in the prefab.")]
    public float doorTriggerSideMargin = 0.25f;

    [Tooltip("Name of the floor object inside your room prefabs. The room is measured from this, so lamps and pillars can't throw the size off.")]
    public string floorObjectName = "Floor";

    // 0 = Up (+Z), 1 = Down (-Z), 2 = Right (+X), 3 = Left (-X)
    static readonly float[] directionAngle = { 0f, 180f, 90f, 270f };

    List<GeneratedRoom> generatedRooms = new List<GeneratedRoom>();

    float referenceFloorTopY;
    bool hasReferenceFloor;

    void Start()
    {
        GenerateDungeon();
    }

    void GenerateDungeon()
    {
        Random.InitState(System.Environment.TickCount);
        generatedRooms.Clear();
        hasReferenceFloor = false;

        if (roomCount < 7) roomCount = 7;

        if (plainRooms == null || plainRooms.Length == 0)
        {
            Debug.LogError("DungeonGenerator: No plain rooms assigned.");
            return;
        }

        if (!ValidateChallengeRooms()) return;

        if (autoMeasureRooms) MeasureRoomsAndSetSpacing();

        if (!GenerateControlledPath())
        {
            Debug.LogError("DungeonGenerator: Could not generate a valid room path.");
            return;
        }

        AssignRoomDifficulties();
        BuildRoomConnections();
        ComputeRoomOrientationsAndPrefabs();
        ComputeRoomWorldPositions();
        SpawnRooms();
        InitializeRoomChain();

        Debug.Log("Dungeon generated successfully with " + generatedRooms.Count + " rooms.");
    }

    // ------------------------------------------------------------------
    // Measuring
    // ------------------------------------------------------------------

    /// <summary>
    /// Measures every room prefab, finds the biggest one, and sets the
    /// spacing so rooms never overlap. No manual numbers needed.
    /// </summary>
    void MeasureRoomsAndSetSpacing()
    {
        float biggest = 0f;

        foreach (GameObject prefab in AllPrefabs())
        {
            if (prefab == null) continue;

            GameObject temp = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            Bounds b = GetRendererBounds(temp);
            DestroyImmediate(temp);

            // Use the larger side, because challenge rooms get rotated 90 degrees.
            biggest = Mathf.Max(biggest, b.size.x, b.size.z);
        }

        horizontalRoomSpacing = biggest + gapBetweenRooms;
        verticalRoomSpacing = biggest + gapBetweenRooms;

        Debug.Log("DungeonGenerator: biggest room side = " + biggest +
                  " | spacing set to " + horizontalRoomSpacing);
    }

    IEnumerable<GameObject> AllPrefabs()
    {
        foreach (GameObject p in plainRooms) yield return p;
        foreach (RoomVariant v in easyRooms) if (v != null) yield return v.prefab;
        foreach (RoomVariant v in mediumRooms) if (v != null) yield return v.prefab;
        foreach (RoomVariant v in hardRooms) if (v != null) yield return v.prefab;
    }

    // ------------------------------------------------------------------
    // Validation + path generation
    // ------------------------------------------------------------------

    bool ValidateChallengeRooms()
    {
        if (easyRooms == null || easyRooms.Length < 2)
        {
            Debug.LogError("DungeonGenerator: You need at least 2 Easy room variants.");
            return false;
        }

        if (mediumRooms == null || mediumRooms.Length < 2)
        {
            Debug.LogError("DungeonGenerator: You need at least 2 Medium room variants.");
            return false;
        }

        if (hardRooms == null || hardRooms.Length < 2)
        {
            Debug.LogError("DungeonGenerator: You need at least 2 Hard room variants.");
            return false;
        }

        return true;
    }

    bool GenerateControlledPath()
    {
        generatedRooms.Clear();

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        occupied.Add(startPosition);

        generatedRooms.Add(new GeneratedRoom
        {
            gridPosition = startPosition,
            difficulty = RoomDifficulty.Plain
        });

        return GeneratePathRecursive(1, startPosition, -1, occupied);
    }

    bool GeneratePathRecursive(int roomIndex, Vector2Int currentPosition, int previousDirection, HashSet<Vector2Int> occupied)
    {
        if (roomIndex >= roomCount) return true;

        List<int> possibleDirections = GetAvailableDirections(currentPosition, occupied);
        Shuffle(possibleDirections);

        int currentRoomIndex = roomIndex - 1;
        bool currentRoomIsChallenge = IsChallengeIndex(currentRoomIndex);

        foreach (int direction in possibleDirections)
        {
            if (currentRoomIsChallenge)
            {
                if (previousDirection < 0) continue;
                if (direction != previousDirection) continue;
            }

            Vector2Int nextPosition = MoveInDirection(currentPosition, direction);

            if (occupied.Contains(nextPosition)) continue;

            occupied.Add(nextPosition);

            GeneratedRoom newRoom = new GeneratedRoom();
            newRoom.gridPosition = nextPosition;
            newRoom.difficulty = RoomDifficulty.Plain;
            generatedRooms.Add(newRoom);

            if (GeneratePathRecursive(roomIndex + 1, nextPosition, direction, occupied))
            {
                return true;
            }

            occupied.Remove(nextPosition);
            generatedRooms.RemoveAt(generatedRooms.Count - 1);
        }

        return false;
    }

    bool IsChallengeIndex(int index)
    {
        return index == 1 || index == 3 || index == 5;
    }

    void AssignRoomDifficulties()
    {
        if (generatedRooms.Count < 7) return;

        generatedRooms[0].difficulty = RoomDifficulty.Plain;
        generatedRooms[1].difficulty = RoomDifficulty.Easy;
        generatedRooms[2].difficulty = RoomDifficulty.Plain;
        generatedRooms[3].difficulty = RoomDifficulty.Medium;
        generatedRooms[4].difficulty = RoomDifficulty.Plain;
        generatedRooms[5].difficulty = RoomDifficulty.Hard;
        generatedRooms[6].difficulty = RoomDifficulty.Plain;
    }

    void BuildRoomConnections()
    {
        for (int i = 0; i < generatedRooms.Count; i++)
        {
            GeneratedRoom room = generatedRooms[i];
            room.status = new bool[4];

            if (i > 0)
            {
                Vector2Int previous = generatedRooms[i - 1].gridPosition;
                int directionFromPrevious = DirectionBetween(previous, room.gridPosition);
                room.incomingDir = Opposite(directionFromPrevious);
                room.status[room.incomingDir] = true;
            }

            if (i < generatedRooms.Count - 1)
            {
                Vector2Int next = generatedRooms[i + 1].gridPosition;
                room.outgoingDir = DirectionBetween(room.gridPosition, next);
                room.status[room.outgoingDir] = true;
            }

            if (room.difficulty != RoomDifficulty.Plain)
            {
                if (room.incomingDir < 0 || room.outgoingDir < 0)
                {
                    Debug.LogError(room.difficulty + " room does not have two connections.");
                }

                if (Opposite(room.incomingDir) != room.outgoingDir)
                {
                    Debug.LogError(room.difficulty + " room is not straight. Incoming: " +
                                   room.incomingDir + " Outgoing: " + room.outgoingDir);
                }
            }
        }
    }

    void ComputeRoomOrientationsAndPrefabs()
    {
        for (int i = 0; i < generatedRooms.Count; i++)
        {
            GeneratedRoom room = generatedRooms[i];
            room.prefab = (i == 0 && startingRoomPrefab != null)
    ? startingRoomPrefab
    : GetPrefabForRoom(room.difficulty);

            if (room.prefab == null)
            {
                room.rotationY = 0f;
                continue;
            }

            if (room.difficulty != RoomDifficulty.Plain)
            {
                RoomVariant variant = GetVariant(room.difficulty, room.prefab);

                if (variant != null)
                    room.rotationY = CalculateRotation(room.incomingDir, variant.entryDoor);
                else
                    room.rotationY = 0f;
            }
            else
            {
                room.rotationY = 0f;
            }
        }
    }

    void ComputeRoomWorldPositions()
    {
        if (generatedRooms.Count == 0) return;

        Vector3 origin;

        if (startTransform != null)
            origin = new Vector3(startTransform.position.x, dungeonOrigin.y, startTransform.position.z);
        else
            origin = dungeonOrigin;

        generatedRooms[0].worldPosition = origin;

        for (int i = 1; i < generatedRooms.Count; i++)
        {
            GeneratedRoom previous = generatedRooms[i - 1];
            GeneratedRoom current = generatedRooms[i];

            int direction = DirectionBetween(previous.gridPosition, current.gridPosition);

            float spacing = (direction == 2 || direction == 3)
                ? horizontalRoomSpacing
                : verticalRoomSpacing;

            current.worldPosition = previous.worldPosition + DirectionToWorldVector(direction) * spacing;
        }
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

    // ------------------------------------------------------------------
    // Spawning
    // ------------------------------------------------------------------

    void SpawnRooms()
    {
        for (int i = 0; i < generatedRooms.Count; i++)
        {
            GeneratedRoom room = generatedRooms[i];
            GameObject prefab = room.prefab;

            if (prefab == null)
            {
                Debug.LogWarning("No prefab found for room " + i);
                continue;
            }

            Vector3 targetPosition = room.worldPosition;

            GameObject instance = Instantiate(prefab, targetPosition, Quaternion.identity, transform);
            room.spawnedInstance = instance;

            float rotationY = room.rotationY;

            if (room.difficulty != RoomDifficulty.Plain && !Mathf.Approximately(rotationY, 0f))
            {
                Bounds bounds = GetRendererBounds(instance);
                instance.transform.RotateAround(bounds.center, Vector3.up, rotationY);

                Debug.Log(room.difficulty + " ROOM " + i + " = " + prefab.name + " | Rotation: " + rotationY);
            }

            // Centre the room using its FLOOR, not every renderer.
            // Lamps, pillars and door frames can stick out on one side and
            // would push the room off-centre, which moves the doors.
            Bounds floorBounds;
            bool floorFound = TryGetFloorBounds(instance, out floorBounds);

            if (!floorFound)
            {
                Debug.LogWarning("DungeonGenerator: no object named '" + floorObjectName +
                                 "' found in " + prefab.name + ". Using the whole room instead.");
            }

            Vector3 correction = targetPosition - floorBounds.center;
            correction.y = 0f;

            // Keep every room's floor at the same height so the player
            // never spawns inside or under the floor of a different room.
            if (floorFound)
            {
                if (!hasReferenceFloor)
                {
                    referenceFloorTopY = floorBounds.max.y;
                    hasReferenceFloor = true;
                }
                else
                {
                    correction.y = referenceFloorTopY - floorBounds.max.y;
                }
            }

            instance.transform.position += correction;

            // Measure again after moving. These are the real numbers used
            // for the door triggers and the player's spawn point.
            TryGetFloorBounds(instance, out floorBounds);
            room.halfWidth = floorBounds.extents.x;
            room.halfHeight = floorBounds.extents.z;
            room.floorTopY = floorBounds.max.y;

            Debug.Log("Room " + i + " (" + prefab.name + ") floor size: " +
                      (room.halfWidth * 2f) + " x " + (room.halfHeight * 2f) +
                      " | floor found: " + floorFound);

            bool[] localStatus = RemapStatusForRotation(room.status, rotationY);

            RoomBehaviour behaviour = instance.GetComponent<RoomBehaviour>();

            if (behaviour != null)
                behaviour.UpdateRoom(localStatus);
            else
                Debug.LogWarning("Room " + prefab.name + " does not have a RoomBehaviour.");

            CreateExitTriggers(room, instance, i);

            instance.name = GetRoomName(room.difficulty) + "_" + i;
        }
    }

    void CreateExitTriggers(GeneratedRoom room, GameObject instance, int index)
    {
        if (room.outgoingDir >= 0 && index + 1 < generatedRooms.Count)
        {
            AddExitTrigger(room, instance, room.outgoingDir, index + 1);
        }

        if (room.incomingDir >= 0 && index - 1 >= 0)
        {
            AddExitTrigger(room, instance, room.incomingDir, index - 1);
        }
    }

    void AddExitTrigger(GeneratedRoom room, GameObject instance, int doorDirection, int targetRoomIndex)
    {
        GameObject door = new GameObject("Exit_" + doorDirection);

        door.transform.SetParent(instance.transform, true);

        bool horizontal = (doorDirection == 2 || doorDirection == 3);

        // Distance from the room's middle to the edge of the floor.
        float halfToEdge = horizontal ? room.halfWidth : room.halfHeight;

        // Trigger covers the last stretch of floor before the doorway, and
        // reaches slightly into the doorway itself (see Door Trigger Offset).
        float centreDistance = Mathf.Max(0f, halfToEdge - doorTriggerDepth * 0.5f + doorTriggerOffset);

        Vector3 triggerPosition = room.worldPosition + DirectionToWorldVector(doorDirection) * centreDistance;

        // Height is based on the real floor, so it always covers the player's body.
        triggerPosition.y = room.floorTopY + doorTriggerHeight * 0.5f - 0.25f;

        float triggerWidth = doorTriggerWidth;

        // Best case: line the trigger up with the prefab's own doorway
        // (North_Door / South_Door / East_Door / West_Door), so it matches
        // the real opening exactly instead of relying on the floor edge.
        Vector3 doorCentre;
        float doorwayWidth;

        if (TryGetDoorway(instance, room, doorDirection, out doorCentre, out doorwayWidth))
        {
            Vector3 outward = DirectionToWorldVector(doorDirection);
            float keepY = triggerPosition.y;

            triggerPosition = doorCentre + outward * (doorTriggerOffset - doorTriggerDepth * 0.5f);
            triggerPosition.y = keepY;

            triggerWidth = doorwayWidth + doorTriggerSideMargin * 2f;
        }
        else
        {
            Debug.LogWarning("DungeonGenerator: could not find the doorway object in " +
                             instance.name + " for direction " + doorDirection +
                             ". Using the floor edge instead.");
        }

        door.transform.position = triggerPosition;
        door.transform.rotation = Quaternion.identity;

        BoxCollider box = door.AddComponent<BoxCollider>();
        box.isTrigger = true;

        Vector3 worldSize = horizontal
            ? new Vector3(doorTriggerDepth, doorTriggerHeight, triggerWidth)
            : new Vector3(triggerWidth, doorTriggerHeight, doorTriggerDepth);

        // If the room prefab is scaled, a collider's size gets scaled too.
        // Divide that out so the trigger is exactly the size typed in the inspector.
        Vector3 s = door.transform.lossyScale;

        box.size = new Vector3(
            worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
            worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
            worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(s.z))
        );

        RoomExitTrigger exit = door.AddComponent<RoomExitTrigger>();
        exit.targetRoomIndex = targetRoomIndex;
        exit.worldDirection = doorDirection;
    }

    void InitializeRoomChain()
    {
        if (RoomChainManager.Instance == null)
        {
            Debug.LogWarning("DungeonGenerator: No RoomChainManager found in the scene.");
            return;
        }

        List<RoomChainManager.RoomEntry> entries = new List<RoomChainManager.RoomEntry>();

        foreach (GeneratedRoom room in generatedRooms)
        {
            entries.Add(new RoomChainManager.RoomEntry
            {
                instance = room.spawnedInstance,
                worldPosition = room.worldPosition,
                incomingDir = room.incomingDir,
                outgoingDir = room.outgoingDir,
                halfWidth = room.halfWidth,
                halfHeight = room.halfHeight,
                floorTopY = room.floorTopY
            });
        }

        CharacterController playerController = FindAnyObjectByType<CharacterController>();

        if (playerController == null)
        {
            Debug.LogWarning("DungeonGenerator: No CharacterController found in the scene for RoomChainManager to move.");
        }

        RoomChainManager.Instance.Initialize(entries, 0, playerController);
    }

    // ------------------------------------------------------------------
    // Helpers (unchanged)
    // ------------------------------------------------------------------

    GameObject GetPrefabForRoom(RoomDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RoomDifficulty.Easy: return PickRandomVariant(easyRooms);
            case RoomDifficulty.Medium: return PickRandomVariant(mediumRooms);
            case RoomDifficulty.Hard: return PickRandomVariant(hardRooms);
            default: return plainRooms[Random.Range(0, plainRooms.Length)];
        }
    }

    GameObject PickRandomVariant(RoomVariant[] variants)
    {
        if (variants == null || variants.Length == 0) return null;

        RoomVariant selected = variants[Random.Range(0, variants.Length)];
        if (selected == null) return null;

        return selected.prefab;
    }

    RoomVariant GetVariant(RoomDifficulty difficulty, GameObject prefab)
    {
        RoomVariant[] variants = null;

        switch (difficulty)
        {
            case RoomDifficulty.Easy: variants = easyRooms; break;
            case RoomDifficulty.Medium: variants = mediumRooms; break;
            case RoomDifficulty.Hard: variants = hardRooms; break;
        }

        if (variants == null) return null;

        foreach (RoomVariant variant in variants)
        {
            if (variant != null && variant.prefab == prefab) return variant;
        }

        return null;
    }

    float CalculateRotation(int worldEntryDirection, int prefabEntryDirection)
    {
        if (worldEntryDirection < 0 || prefabEntryDirection < 0) return 0f;

        return NormalizeAngle(directionAngle[worldEntryDirection] - directionAngle[prefabEntryDirection]);
    }

    bool[] RemapStatusForRotation(bool[] worldStatus, float rotationY)
    {
        bool[] localStatus = new bool[4];

        for (int worldDirection = 0; worldDirection < 4; worldDirection++)
        {
            if (!worldStatus[worldDirection]) continue;

            int localDirection = IndexFromAngle(directionAngle[worldDirection] - rotationY);

            if (localDirection >= 0) localStatus[localDirection] = true;
        }

        return localStatus;
    }

    int IndexFromAngle(float angle)
    {
        angle = NormalizeAngle(angle);

        for (int i = 0; i < 4; i++)
        {
            if (Mathf.Approximately(directionAngle[i], angle)) return i;
        }

        return -1;
    }

    List<int> GetAvailableDirections(Vector2Int position, HashSet<Vector2Int> occupied)
    {
        List<int> directions = new List<int>();

        for (int direction = 0; direction < 4; direction++)
        {
            Vector2Int next = MoveInDirection(position, direction);

            if (!IsInsideGrid(next)) continue;
            if (occupied.Contains(next)) continue;

            directions.Add(direction);
        }

        return directions;
    }

    Vector2Int MoveInDirection(Vector2Int position, int direction)
    {
        switch (direction)
        {
            case 0: return position + Vector2Int.up;
            case 1: return position + Vector2Int.down;
            case 2: return position + Vector2Int.right;
            case 3: return position + Vector2Int.left;
        }

        return position;
    }

    int DirectionBetween(Vector2Int from, Vector2Int to)
    {
        Vector2Int difference = to - from;

        if (difference == Vector2Int.up) return 0;
        if (difference == Vector2Int.down) return 1;
        if (difference == Vector2Int.right) return 2;
        if (difference == Vector2Int.left) return 3;

        return -1;
    }

    int Opposite(int direction)
    {
        switch (direction)
        {
            case 0: return 1;
            case 1: return 0;
            case 2: return 3;
            case 3: return 2;
        }

        return -1;
    }

    bool IsInsideGrid(Vector2Int position)
    {
        return position.x >= 0 && position.x < gridSize.x &&
               position.y >= 0 && position.y < gridSize.y;
    }

    void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            int temporary = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temporary;
        }
    }

    float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }

    /// <summary>
    /// Finds the doorway opening of a room in WORLD space, using the prefab's
    /// own door trigger objects (North_Door, South_Door, East_Door, West_Door).
    /// </summary>
    bool TryGetDoorway(GameObject instance, GeneratedRoom room, int worldDirection, out Vector3 centre, out float width)
    {
        centre = Vector3.zero;
        width = 0f;

        // Which side of the PREFAB is this world direction, after rotation?
        int localDir = IndexFromAngle(directionAngle[worldDirection] - room.rotationY);
        if (localDir < 0) return false;

        string[] names = { "North_Door", "South_Door", "East_Door", "West_Door" };

        Transform doorway = FindShallowestChild(instance.transform, names[localDir]);
        if (doorway == null) return false;

        BoxCollider doorBox = doorway.GetComponent<BoxCollider>();
        if (doorBox == null) return false;

        // Work out the doorway box in world space from its 8 corners.
        // (Done by hand because a disabled collider reports empty bounds.)
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < 8; i++)
        {
            Vector3 sign = new Vector3(
                (i & 1) == 0 ? -1f : 1f,
                (i & 2) == 0 ? -1f : 1f,
                (i & 4) == 0 ? -1f : 1f
            );

            Vector3 localCorner = doorBox.center + Vector3.Scale(doorBox.size * 0.5f, sign);
            Vector3 worldCorner = doorway.TransformPoint(localCorner);

            min = Vector3.Min(min, worldCorner);
            max = Vector3.Max(max, worldCorner);
        }

        centre = (min + max) * 0.5f;

        bool horizontal = (worldDirection == 2 || worldDirection == 3);
        width = horizontal ? (max.z - min.z) : (max.x - min.x);

        return width > 0.05f;
    }

    bool TryGetFloorBounds(GameObject room, out Bounds bounds)
    {
        Transform floor = FindShallowestChild(room.transform, floorObjectName);

        if (floor != null)
        {
            Renderer[] renderers = floor.GetComponentsInChildren<Renderer>();

            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;

                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                return true;
            }
        }

        bounds = GetRendererBounds(room);
        return false;
    }

    Transform FindShallowestChild(Transform root, string childName)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();

            if (current != root && current.name == childName) return current;

            foreach (Transform child in current)
            {
                queue.Enqueue(child);
            }
        }

        return null;
    }

    Bounds GetRendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    string GetRoomName(RoomDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RoomDifficulty.Easy: return "EasyRoom";
            case RoomDifficulty.Medium: return "MediumRoom";
            case RoomDifficulty.Hard: return "HardRoom";
            default: return "PlainRoom";
        }
    }
}