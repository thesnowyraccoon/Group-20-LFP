using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Data
    // ------------------------------------------------------------------

    [System.Serializable]
    public class RoomVariant
    {
        [Tooltip("Just a name for the Inspector, e.g. 'Stage 1 Base Room' or 'Stage 1 Variant Room 1'.")]
        public string label = "Stage 1 Base Room";

        public GameObject prefab;

        [Tooltip("ON = this prefab has a fixed layout (e.g. a straight corridor room) and gets ROTATED so its Entry Door faces where the player comes from. " +
                 "OFF = prefab is never rotated and RoomBehaviour opens the correct walls.")]
        public bool fixedLayout = false;

        // 0 = Up (+Z), 1 = Down (-Z), 2 = Right (+X), 3 = Left (-X). Only used if Fixed Layout is ON.
        public int entryDoor = 3;
        public int exitDoor = 2;
    }

    [System.Serializable]
    public class Stage
    {
        [Tooltip("Just a name for the Inspector, e.g. 'Stage 1'.")]
        public string label = "Stage 1";

        [Tooltip("ON = the player must leave this room on the opposite side to where they came in (straight through).")]
        public bool straightPath = false;

        [Tooltip("Base room + variants. One of these is picked at random (equal chance) each time the game starts.")]
        public RoomVariant[] variants;
    }

    private class GeneratedRoom
    {
        public Vector2Int gridPosition;
        public int stageIndex;
        public RoomVariant variant;
        public int incomingDir = -1;
        public int outgoingDir = -1;
        public bool[] status = new bool[4];
        public GameObject prefab;
        public float rotationY;
        public Vector3 worldPosition;
        public GameObject spawnedInstance;

        public float halfWidth;
        public float halfHeight;
        public float floorTopY;
    }

    // ------------------------------------------------------------------
    // Inspector
    // ------------------------------------------------------------------

    [Header("Stages (set Size to 8)")]
    [Tooltip("Element 0 = Stage 1 ... Element 7 = Stage 8. The number of stages is the number of rooms.")]
    public Stage[] stages;

    [Header("Dungeon Settings")]
    public Vector2Int gridSize = new Vector2Int(9, 9);
    public Vector2Int startPosition = new Vector2Int(4, 4);

    [Header("Dungeon Origin")]
    public Vector3 dungeonOrigin = new Vector3(0f, -0.81f, -6.61f);
    [Tooltip("Optional. If assigned, the dungeon X and Z position will use this Transform instead of Dungeon Origin.")]
    public Transform startTransform;

    [Header("Room Size / Spacing")]
    [Tooltip("ON = the script measures your room prefabs and sets the spacing for you. Recommended.")]
    public bool autoMeasureRooms = true;
    public float gapBetweenRooms = 2f;
    public float horizontalRoomSpacing = 30f;
    public float verticalRoomSpacing = 30f;

    [Header("Doors")]
    [Tooltip("Tag used on the door objects inside your room prefabs.")]
    public string doorTag = "Door";

    [Tooltip("Name of the little capsule the player touches to open the door. Matching ignores spaces and capital letters, and only needs to CONTAIN this text. " +
             "Only used as a fallback when the room has no DoorUnlockItem.")]
    public string unlockTriggerName = "PlayerHolder";

    [Tooltip("Optional particle effect spawned where the door vanishes.")]
    public GameObject vanishEffectPrefab;
    public float vanishDuration = 0.7f;

    [Header("Door Camera Pan")]
    public float panToDoorTime = 0.6f;
    public float holdOnDoorTime = 0.5f;
    public float panBackTime = 0.6f;

    [Header("Exit Trigger (walk-through trigger)")]
    public float doorTriggerWidth = 3f;
    public float doorTriggerHeight = 3f;
    public float doorTriggerDepth = 1.5f;
    public float doorTriggerOffset = 0.5f;
    public float doorTriggerSideMargin = 0.25f;

    [Header("Floor Measuring")]
    [Tooltip("Name of the floor object inside your room prefabs.")]
    public string floorObjectName = "Floor";

    [Tooltip("Any floor piece (or a parent of it, inside the Floor object) whose name CONTAINS this text is ignored when measuring the floor. " +
             "Use it on pieces you raised or lowered, e.g. rename them 'FloorPiece_Ignore'. Not case sensitive. Leave empty to measure every piece.")]
    public string floorIgnoreKeyword = "Ignore";

    // 0 = Up (+Z), 1 = Down (-Z), 2 = Right (+X), 3 = Left (-X)
    static readonly float[] directionAngle = { 0f, 180f, 90f, 270f };

    List<GeneratedRoom> generatedRooms = new List<GeneratedRoom>();

    float referenceFloorTopY;
    bool hasReferenceFloor;

    int RoomCount => stages != null ? stages.Length : 0;

    void Start()
    {
        GenerateDungeon();
    }

    void GenerateDungeon()
    {
        Random.InitState(System.Environment.TickCount);
        generatedRooms.Clear();
        hasReferenceFloor = false;

        if (!ValidateStages()) return;

        if (autoMeasureRooms) MeasureRoomsAndSetSpacing();

        if (!GenerateControlledPath())
        {
            Debug.LogError("DungeonGenerator: Could not generate a valid room path.");
            return;
        }

        BuildRoomConnections();
        PickVariantsAndOrientations();
        ComputeRoomWorldPositions();
        SpawnRooms();
        InitializeRoomChain();

        Debug.Log("Dungeon generated successfully with " + generatedRooms.Count + " rooms.");
    }

    // ------------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------------

    bool ValidateStages()
    {
        if (stages == null || stages.Length < 2)
        {
            Debug.LogError("DungeonGenerator: Add your stages (Stages > Size = 8).");
            return false;
        }

        for (int i = 0; i < stages.Length; i++)
        {
            Stage stage = stages[i];

            if (stage == null || stage.variants == null || stage.variants.Length == 0)
            {
                Debug.LogError("DungeonGenerator: Stage " + (i + 1) + " has no room variants.");
                return false;
            }

            foreach (RoomVariant v in stage.variants)
            {
                if (v == null || v.prefab == null)
                {
                    Debug.LogError("DungeonGenerator: Stage " + (i + 1) + " has a variant with no prefab assigned.");
                    return false;
                }

                if (v.fixedLayout && !stage.straightPath && i > 0 && i < stages.Length - 1)
                {
                    Debug.LogWarning("DungeonGenerator: '" + v.label + "' uses Fixed Layout but Stage " + (i + 1) +
                                     " is not set to Straight Path. Its exit may not line up.");
                }
            }
        }

        return true;
    }

    // ------------------------------------------------------------------
    // Measuring
    // ------------------------------------------------------------------

    void MeasureRoomsAndSetSpacing()
    {
        float biggest = 0f;

        foreach (GameObject prefab in AllPrefabs())
        {
            if (prefab == null) continue;

            GameObject temp = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            Bounds b = GetRendererBounds(temp);
            DestroyImmediate(temp);

            biggest = Mathf.Max(biggest, b.size.x, b.size.z);
        }

        horizontalRoomSpacing = biggest + gapBetweenRooms;
        verticalRoomSpacing = biggest + gapBetweenRooms;

        Debug.Log("DungeonGenerator: biggest room side = " + biggest +
                  " | spacing set to " + horizontalRoomSpacing);
    }

    IEnumerable<GameObject> AllPrefabs()
    {
        foreach (Stage stage in stages)
        {
            foreach (RoomVariant v in stage.variants)
            {
                if (v != null) yield return v.prefab;
            }
        }
    }

    // ------------------------------------------------------------------
    // Path generation
    // ------------------------------------------------------------------

    bool GenerateControlledPath()
    {
        generatedRooms.Clear();

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        occupied.Add(startPosition);

        generatedRooms.Add(new GeneratedRoom
        {
            gridPosition = startPosition,
            stageIndex = 0
        });

        return GeneratePathRecursive(1, startPosition, -1, occupied);
    }

    bool GeneratePathRecursive(int roomIndex, Vector2Int currentPosition, int previousDirection, HashSet<Vector2Int> occupied)
    {
        if (roomIndex >= RoomCount) return true;

        List<int> possibleDirections = GetAvailableDirections(currentPosition, occupied);
        Shuffle(possibleDirections);

        int currentRoomIndex = roomIndex - 1;
        bool mustGoStraight = stages[currentRoomIndex].straightPath && previousDirection >= 0;

        foreach (int direction in possibleDirections)
        {
            if (mustGoStraight && direction != previousDirection) continue;

            Vector2Int nextPosition = MoveInDirection(currentPosition, direction);

            if (occupied.Contains(nextPosition)) continue;

            occupied.Add(nextPosition);

            generatedRooms.Add(new GeneratedRoom
            {
                gridPosition = nextPosition,
                stageIndex = roomIndex
            });

            if (GeneratePathRecursive(roomIndex + 1, nextPosition, direction, occupied))
            {
                return true;
            }

            occupied.Remove(nextPosition);
            generatedRooms.RemoveAt(generatedRooms.Count - 1);
        }

        return false;
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
        }

        // The final room has no next room, but it still needs an ESCAPE door.
        // It uses the wall opposite to where the player came in.
        GeneratedRoom last = generatedRooms[generatedRooms.Count - 1];
        if (last.incomingDir >= 0)
        {
            last.outgoingDir = Opposite(last.incomingDir);
            last.status[last.outgoingDir] = true;
        }
    }

    // ------------------------------------------------------------------
    // One random variant per stage
    // ------------------------------------------------------------------

    void PickVariantsAndOrientations()
    {
        for (int i = 0; i < generatedRooms.Count; i++)
        {
            GeneratedRoom room = generatedRooms[i];
            RoomVariant[] options = stages[room.stageIndex].variants;

            // Equal chance for every variant of this stage (1 in 3 with three).
            room.variant = options[Random.Range(0, options.Length)];
            room.prefab = room.variant.prefab;

            room.rotationY = room.variant.fixedLayout
                ? CalculateRotation(room.incomingDir, room.variant.entryDoor)
                : 0f;

            Debug.Log("Stage " + (i + 1) + " -> " + room.variant.label);
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

            if (room.variant.fixedLayout && !Mathf.Approximately(rotationY, 0f))
            {
                Bounds bounds = GetRendererBounds(instance);
                instance.transform.RotateAround(bounds.center, Vector3.up, rotationY);
            }

            Bounds floorBounds;
            bool floorFound = TryGetFloorBounds(instance, out floorBounds);

            if (!floorFound)
            {
                Debug.LogWarning("DungeonGenerator: no usable '" + floorObjectName +
                                 "' pieces found in " + prefab.name + ". Using the whole room instead.");
            }

            Vector3 correction = targetPosition - floorBounds.center;
            correction.y = 0f;

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

            TryGetFloorBounds(instance, out floorBounds);
            room.halfWidth = floorBounds.extents.x;
            room.halfHeight = floorBounds.extents.z;
            room.floorTopY = floorBounds.max.y;

            bool[] localStatus = RemapStatusForRotation(room.status, rotationY);

            RoomBehaviour behaviour = instance.GetComponent<RoomBehaviour>();

            if (behaviour != null)
                behaviour.UpdateRoom(localStatus);
            else
                Debug.LogWarning("Room " + prefab.name + " does not have a RoomBehaviour.");

            // Walk-through trigger to the next room (starts switched OFF, the
            // door controller switches it on once the door has opened).
            BoxCollider exitTrigger = CreateExitTrigger(room, instance, i);

            // Hide every door except the exit door, and wire up the unlock item.
            SetupDoors(room, instance, exitTrigger);

            instance.name = "Stage" + (i + 1) + "_" + room.variant.label;
        }
    }

    // ------------------------------------------------------------------
    // Doors
    // ------------------------------------------------------------------

    void SetupDoors(GeneratedRoom room, GameObject instance, BoxCollider exitTrigger)
    {
        RoomDoorController controller = instance.GetComponent<RoomDoorController>();
        if (controller == null) controller = instance.AddComponent<RoomDoorController>();

        controller.vanishEffectPrefab = vanishEffectPrefab;
        controller.vanishDuration = vanishDuration;
        controller.panToDoorTime = panToDoorTime;
        controller.holdTime = holdOnDoorTime;
        controller.panBackTime = panBackTime;

        List<Transform> exitDoors = new List<Transform>();

        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
        {
            if (t == instance.transform) continue;
            if (!t.CompareTag(doorTag)) continue;
            if (HasTaggedAncestor(t, instance.transform)) continue;

            if (room.outgoingDir >= 0 && DoorSide(room, t.position) == room.outgoingDir)
                exitDoors.Add(t);
            else
                t.gameObject.SetActive(false);
        }

        controller.Setup(exitDoors, exitTrigger);

        if (exitDoors.Count == 0)
        {
            Debug.LogWarning("DungeonGenerator: no '" + doorTag + "' object found on the exit side of " +
                             instance.name + ". The exit is open from the start.");
            controller.OpenImmediately();
            return;
        }

        // If the room has a DoorUnlockItem (the floating collectible), it opens
        // the door by itself. Don't add the simple DoorUnlockTrigger on top of
        // it, or two scripts would both try to open the door.
        if (instance.GetComponentInChildren<DoorUnlockItem>(true) != null)
            return;

        // Fallback: a plain capsule found by name gets the simple trigger.
        Transform unlock = FindByLooseName(instance.transform, unlockTriggerName);

        if (unlock == null)
        {
            Debug.LogWarning("DungeonGenerator: no DoorUnlockItem and no '" + unlockTriggerName +
                             "' object found in " + instance.name + ". The exit door is open from the start.");
            controller.OpenImmediately();
            return;
        }

        if (unlock.GetComponent<DoorUnlockTrigger>() == null)
            unlock.gameObject.AddComponent<DoorUnlockTrigger>();
    }

    bool HasTaggedAncestor(Transform t, Transform root)
    {
        Transform p = t.parent;

        while (p != null && p != root)
        {
            if (p.CompareTag(doorTag)) return true;
            p = p.parent;
        }

        return false;
    }

    // Which wall (0=Up,1=Down,2=Right,3=Left) a door sits on, in WORLD space.
    int DoorSide(GeneratedRoom room, Vector3 doorPosition)
    {
        Vector3 d = doorPosition - room.worldPosition;

        if (Mathf.Abs(d.x) > Mathf.Abs(d.z))
            return d.x > 0f ? 2 : 3;

        return d.z > 0f ? 0 : 1;
    }

    Transform FindByLooseName(Transform root, string wanted)
    {
        string key = Normalize(wanted);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root) continue;
            if (Normalize(t.name).Contains(key)) return t;
        }

        return null;
    }

    string Normalize(string s)
    {
        return s.Replace(" ", "").Replace("_", "").ToLowerInvariant();
    }

    // ------------------------------------------------------------------
    // Exit trigger
    // ------------------------------------------------------------------

    BoxCollider CreateExitTrigger(GeneratedRoom room, GameObject instance, int index)
    {
        if (room.outgoingDir < 0) return null;

        // For the LAST room, index + 1 == room count, which RoomChainManager
        // treats as "the player escaped".
        return AddExitTrigger(room, instance, room.outgoingDir, index + 1);
    }

    BoxCollider AddExitTrigger(GeneratedRoom room, GameObject instance, int doorDirection, int targetRoomIndex)
    {
        GameObject door = new GameObject("Exit_" + doorDirection);

        door.transform.SetParent(instance.transform, true);

        bool horizontal = (doorDirection == 2 || doorDirection == 3);

        float halfToEdge = horizontal ? room.halfWidth : room.halfHeight;
        float centreDistance = Mathf.Max(0f, halfToEdge - doorTriggerDepth * 0.5f + doorTriggerOffset);

        Vector3 triggerPosition = room.worldPosition + DirectionToWorldVector(doorDirection) * centreDistance;
        triggerPosition.y = room.floorTopY + doorTriggerHeight * 0.5f - 0.25f;

        float triggerWidth = doorTriggerWidth;

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

        Vector3 s = door.transform.lossyScale;

        box.size = new Vector3(
            worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
            worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
            worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(s.z))
        );

        RoomExitTrigger exit = door.AddComponent<RoomExitTrigger>();
        exit.targetRoomIndex = targetRoomIndex;
        exit.worldDirection = doorDirection;

        // Stays off until the door has opened.
        box.enabled = false;

        return box;
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
            // Find where the player actually comes in, so the spawn point lines up
            // with the real entrance doorway instead of the middle of the wall.
            Vector3 entryCentre = Vector3.zero;
            float entryWidth;
            bool hasEntry = room.incomingDir >= 0 &&
                            TryGetDoorway(room.spawnedInstance, room, room.incomingDir, out entryCentre, out entryWidth);

            entries.Add(new RoomChainManager.RoomEntry
            {
                instance = room.spawnedInstance,
                worldPosition = room.worldPosition,
                incomingDir = room.incomingDir,
                outgoingDir = room.outgoingDir,
                halfWidth = room.halfWidth,
                halfHeight = room.halfHeight,
                floorTopY = room.floorTopY,
                hasEntryPoint = hasEntry,
                entryPoint = entryCentre
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
    // Helpers
    // ------------------------------------------------------------------

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

    bool TryGetDoorway(GameObject instance, GeneratedRoom room, int worldDirection, out Vector3 centre, out float width)
    {
        centre = Vector3.zero;
        width = 0f;

        int localDir = IndexFromAngle(directionAngle[worldDirection] - room.rotationY);
        if (localDir < 0) return false;

        string[] names = { "North_Door", "South_Door", "East_Door", "West_Door" };

        Transform doorway = FindShallowestChild(instance.transform, names[localDir]);
        if (doorway == null) return false;

        BoxCollider doorBox = doorway.GetComponent<BoxCollider>();
        if (doorBox == null) return false;

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

    // Measures only the BASE floor pieces. Any piece whose name (or whose
    // parent's name, up to the Floor object) contains floorIgnoreKeyword is
    // skipped, so raised / lowered pieces don't distort the measurement.
    bool TryGetFloorBounds(GameObject room, out Bounds bounds)
    {
        Transform floor = FindShallowestChild(room.transform, floorObjectName);

        if (floor != null)
        {
            Renderer[] renderers = floor.GetComponentsInChildren<Renderer>();

            bool started = false;
            bounds = new Bounds(room.transform.position, Vector3.zero);

            foreach (Renderer r in renderers)
            {
                if (IsIgnoredFloorPiece(r.transform, floor)) continue;

                if (!started)
                {
                    bounds = r.bounds;
                    started = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (started) return true;
        }

        bounds = GetRendererBounds(room);
        return false;
    }

    bool IsIgnoredFloorPiece(Transform piece, Transform floorRoot)
    {
        if (string.IsNullOrEmpty(floorIgnoreKeyword)) return false;

        string key = floorIgnoreKeyword.ToLowerInvariant();
        Transform t = piece;

        // Walk up from the renderer to the Floor object, so a child mesh of an
        // ignored parent is also ignored.
        while (t != null)
        {
            if (t.name.ToLowerInvariant().Contains(key)) return true;
            if (t == floorRoot) break;
            t = t.parent;
        }

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
}