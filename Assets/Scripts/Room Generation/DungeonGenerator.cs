using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public enum RoomDifficulty
    {
        Plain,
        Easy,
        Medium,
        Hard
    }

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
    }

    [Header("Dungeon Settings")]

    [Min(7)]
    public int roomCount = 7;

    public Vector2Int gridSize = new Vector2Int(9, 9);

    public Vector2Int startPosition = new Vector2Int(4, 4);


    [Header("Dungeon Origin")]

    [Tooltip("The exact world position of the first room. Based on your manual calibration this is X 0, Y -0.81, Z -6.61.")]
    public Vector3 dungeonOrigin =
        new Vector3(0f, -0.81f, -6.61f);

    [Tooltip("Optional. If assigned, the dungeon X and Z position will use this Transform instead of Dungeon Origin.")]
    public Transform startTransform;


    [Header("Room Connection Spacing")]

    [Tooltip("Horizontal centre-to-centre distance between rooms. Your manually measured value is 5.93.")]
    public float horizontalRoomSpacing = 5.93f;

    [Tooltip("Vertical centre-to-centre distance between rooms. Your manually measured value is 10.26.")]
    public float verticalRoomSpacing = 10.26f;


    [Header("Room Footprint")]

    [Tooltip("Room width along local X.")]
    public float roomWidth = 6f;

    [Tooltip("Room height along local Z.")]
    public float roomHeight = 11f;


    [Header("Plain Rooms")]

    public GameObject[] plainRooms;


    [Header("Easy Rooms")]

    public RoomVariant[] easyRooms;


    [Header("Medium Rooms")]

    public RoomVariant[] mediumRooms;


    [Header("Hard Rooms")]

    public RoomVariant[] hardRooms;


    // Direction convention:
    //
    // 0 = Up    = +Z
    // 1 = Down  = -Z
    // 2 = Right = +X
    // 3 = Left  = -X

    static readonly float[] directionAngle =
    {
        0f,
        180f,
        90f,
        270f
    };


    List<GeneratedRoom> generatedRooms =
        new List<GeneratedRoom>();


    void Start()
    {
        GenerateDungeon();
    }


    void GenerateDungeon()
    {
        Random.InitState(
            System.Environment.TickCount
        );

        generatedRooms.Clear();


        if (roomCount < 7)
        {
            roomCount = 7;
        }


        if (plainRooms == null ||
            plainRooms.Length == 0)
        {
            Debug.LogError(
                "DungeonGenerator: No plain rooms assigned."
            );

            return;
        }


        if (!ValidateChallengeRooms())
        {
            return;
        }


        if (!GenerateControlledPath())
        {
            Debug.LogError(
                "DungeonGenerator: Could not generate a valid room path."
            );

            return;
        }


        AssignRoomDifficulties();

        BuildRoomConnections();

        ComputeRoomOrientationsAndPrefabs();

        ComputeRoomWorldPositions();

        SpawnRooms();


        Debug.Log(
            "Dungeon generated successfully with " +
            generatedRooms.Count +
            " rooms."
        );
    }


    bool ValidateChallengeRooms()
    {
        if (easyRooms == null ||
            easyRooms.Length < 2)
        {
            Debug.LogError(
                "DungeonGenerator: You need at least 2 Easy room variants."
            );

            return false;
        }


        if (mediumRooms == null ||
            mediumRooms.Length < 2)
        {
            Debug.LogError(
                "DungeonGenerator: You need at least 2 Medium room variants."
            );

            return false;
        }


        if (hardRooms == null ||
            hardRooms.Length < 2)
        {
            Debug.LogError(
                "DungeonGenerator: You need at least 2 Hard room variants."
            );

            return false;
        }


        return true;
    }


    bool GenerateControlledPath()
    {
        generatedRooms.Clear();


        HashSet<Vector2Int> occupied =
            new HashSet<Vector2Int>();


        occupied.Add(startPosition);


        generatedRooms.Add(
            new GeneratedRoom
            {
                gridPosition = startPosition,

                difficulty =
                    RoomDifficulty.Plain
            }
        );


        return GeneratePathRecursive(
            1,
            startPosition,
            -1,
            occupied
        );
    }


    bool GeneratePathRecursive(
        int roomIndex,
        Vector2Int currentPosition,
        int previousDirection,
        HashSet<Vector2Int> occupied)
    {
        if (roomIndex >= roomCount)
        {
            return true;
        }


        List<int> possibleDirections =
            GetAvailableDirections(
                currentPosition,
                occupied
            );


        Shuffle(possibleDirections);


        int currentRoomIndex =
            roomIndex - 1;


        bool currentRoomIsChallenge =
            IsChallengeIndex(
                currentRoomIndex
            );


        foreach (int direction
                 in possibleDirections)
        {
            if (currentRoomIsChallenge)
            {
                if (previousDirection < 0)
                {
                    continue;
                }


                if (direction != previousDirection)
                {
                    continue;
                }
            }


            Vector2Int nextPosition =
                MoveInDirection(
                    currentPosition,
                    direction
                );


            if (occupied.Contains(
                    nextPosition))
            {
                continue;
            }


            occupied.Add(nextPosition);


            GeneratedRoom newRoom =
                new GeneratedRoom();


            newRoom.gridPosition =
                nextPosition;


            newRoom.difficulty =
                RoomDifficulty.Plain;


            generatedRooms.Add(
                newRoom
            );


            if (GeneratePathRecursive(
                    roomIndex + 1,
                    nextPosition,
                    direction,
                    occupied))
            {
                return true;
            }


            occupied.Remove(
                nextPosition
            );


            generatedRooms.RemoveAt(
                generatedRooms.Count - 1
            );
        }


        return false;
    }


    bool IsChallengeIndex(int index)
    {
        return index == 1 ||
               index == 3 ||
               index == 5;
    }


    void AssignRoomDifficulties()
    {
        if (generatedRooms.Count < 7)
        {
            return;
        }


        generatedRooms[0].difficulty =
            RoomDifficulty.Plain;


        generatedRooms[1].difficulty =
            RoomDifficulty.Easy;


        generatedRooms[2].difficulty =
            RoomDifficulty.Plain;


        generatedRooms[3].difficulty =
            RoomDifficulty.Medium;


        generatedRooms[4].difficulty =
            RoomDifficulty.Plain;


        generatedRooms[5].difficulty =
            RoomDifficulty.Hard;


        generatedRooms[6].difficulty =
            RoomDifficulty.Plain;
    }


    void BuildRoomConnections()
    {
        for (int i = 0;
             i < generatedRooms.Count;
             i++)
        {
            GeneratedRoom room =
                generatedRooms[i];


            room.status =
                new bool[4];


            // Incoming connection

            if (i > 0)
            {
                Vector2Int previous =
                    generatedRooms[i - 1]
                    .gridPosition;


                int directionFromPrevious =
                    DirectionBetween(
                        previous,
                        room.gridPosition
                    );


                room.incomingDir =
                    Opposite(
                        directionFromPrevious
                    );


                room.status[
                    room.incomingDir
                ] = true;
            }


            // Outgoing connection

            if (i <
                generatedRooms.Count - 1)
            {
                Vector2Int next =
                    generatedRooms[i + 1]
                    .gridPosition;


                room.outgoingDir =
                    DirectionBetween(
                        room.gridPosition,
                        next
                    );


                room.status[
                    room.outgoingDir
                ] = true;
            }


            // Validate challenge rooms

            if (room.difficulty !=
                RoomDifficulty.Plain)
            {
                if (room.incomingDir < 0 ||
                    room.outgoingDir < 0)
                {
                    Debug.LogError(
                        room.difficulty +
                        " room does not have two connections."
                    );
                }


                if (Opposite(
                        room.incomingDir) !=
                    room.outgoingDir)
                {
                    Debug.LogError(
                        room.difficulty +
                        " room is not straight. " +
                        "Incoming: " +
                        room.incomingDir +
                        " Outgoing: " +
                        room.outgoingDir
                    );
                }
            }
        }
    }


    void ComputeRoomOrientationsAndPrefabs()
    {
        for (int i = 0;
             i < generatedRooms.Count;
             i++)
        {
            GeneratedRoom room =
                generatedRooms[i];


            room.prefab =
                GetPrefabForRoom(
                    room.difficulty
                );


            if (room.prefab == null)
            {
                room.rotationY = 0f;

                continue;
            }


            if (room.difficulty !=
                RoomDifficulty.Plain)
            {
                RoomVariant variant =
                    GetVariant(
                        room.difficulty,
                        room.prefab
                    );


                if (variant != null)
                {
                    room.rotationY =
                        CalculateRotation(
                            room.incomingDir,
                            variant.entryDoor
                        );
                }
                else
                {
                    room.rotationY = 0f;
                }
            }
            else
            {
                room.rotationY = 0f;
            }
        }
    }


    void ComputeRoomWorldPositions()
    {
        if (generatedRooms.Count == 0)
        {
            return;
        }


        Vector3 origin;


        if (startTransform != null)
        {
            origin =
                new Vector3(
                    startTransform.position.x,
                    dungeonOrigin.y,
                    startTransform.position.z
                );
        }
        else
        {
            origin =
                dungeonOrigin;
        }


        generatedRooms[0].worldPosition =
            origin;


        for (int i = 1;
             i < generatedRooms.Count;
             i++)
        {
            GeneratedRoom previous =
                generatedRooms[i - 1];


            GeneratedRoom current =
                generatedRooms[i];


            int direction =
                DirectionBetween(
                    previous.gridPosition,
                    current.gridPosition
                );


            float spacing;


            // Horizontal movement

            if (direction == 2 ||
                direction == 3)
            {
                spacing =
                    horizontalRoomSpacing;
            }

            // Vertical movement

            else
            {
                spacing =
                    verticalRoomSpacing;
            }


            Vector3 directionVector =
                DirectionToWorldVector(
                    direction
                );


            current.worldPosition =
                previous.worldPosition +
                directionVector *
                spacing;
        }
    }


    Vector3 DirectionToWorldVector(
        int direction)
    {
        switch (direction)
        {
            // Grid UP = World +Z

            case 0:
                return new Vector3(
                    0f,
                    0f,
                    1f
                );


            // Grid DOWN = World -Z

            case 1:
                return new Vector3(
                    0f,
                    0f,
                    -1f
                );


            // Grid RIGHT = World +X

            case 2:
                return new Vector3(
                    1f,
                    0f,
                    0f
                );


            // Grid LEFT = World -X

            case 3:
                return new Vector3(
                    -1f,
                    0f,
                    0f
                );
        }


        return Vector3.zero;
    }


    void SpawnRooms()
    {
        for (int i = 0;
             i < generatedRooms.Count;
             i++)
        {
            GeneratedRoom room =
                generatedRooms[i];


            GameObject prefab =
                room.prefab;


            if (prefab == null)
            {
                Debug.LogWarning(
                    "No prefab found for room " +
                    i
                );

                continue;
            }


            Vector3 targetPosition =
                room.worldPosition;


            GameObject instance =
                Instantiate(
                    prefab,
                    targetPosition,
                    Quaternion.identity,
                    transform
                );


            float rotationY =
                room.rotationY;


            if (room.difficulty !=
                RoomDifficulty.Plain &&
                !Mathf.Approximately(
                    rotationY,
                    0f))
            {
                Bounds bounds =
                    GetRendererBounds(
                        instance
                    );


                instance.transform
                    .RotateAround(
                        bounds.center,
                        Vector3.up,
                        rotationY
                    );


                Debug.Log(
                    room.difficulty +
                    " ROOM " +
                    i +
                    " = " +
                    prefab.name +
                    " | Rotation: " +
                    rotationY
                );
            }


            // Re-centre the room after rotation.
            //
            // This prevents a prefab with an offset
            // pivot from shifting away from the
            // calculated connection position.

            Bounds finalBounds =
                GetRendererBounds(
                    instance
                );


            Vector3 correction =
                targetPosition -
                finalBounds.center;


            correction.y = 0f;


            instance.transform.position +=
                correction;


            // Convert world connection directions
            // back into the room's local directions
            // after rotation.

            bool[] localStatus =
                RemapStatusForRotation(
                    room.status,
                    rotationY
                );


            RoomBehaviour behaviour =
                instance.GetComponent<
                    RoomBehaviour
                >();


            if (behaviour != null)
            {
                behaviour.UpdateRoom(
                    localStatus
                );
            }
            else
            {
                Debug.LogWarning(
                    "Room " +
                    prefab.name +
                    " does not have a RoomBehaviour."
                );
            }


            instance.name =
                GetRoomName(
                    room.difficulty
                ) +
                "_" +
                i;
        }
    }


    GameObject GetPrefabForRoom(
        RoomDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RoomDifficulty.Easy:

                return PickRandomVariant(
                    easyRooms
                );


            case RoomDifficulty.Medium:

                return PickRandomVariant(
                    mediumRooms
                );


            case RoomDifficulty.Hard:

                return PickRandomVariant(
                    hardRooms
                );


            default:

                return plainRooms[
                    Random.Range(
                        0,
                        plainRooms.Length
                    )
                ];
        }
    }


    GameObject PickRandomVariant(
        RoomVariant[] variants)
    {
        if (variants == null ||
            variants.Length == 0)
        {
            return null;
        }


        RoomVariant selected =
            variants[
                Random.Range(
                    0,
                    variants.Length
                )
            ];


        if (selected == null)
        {
            return null;
        }


        return selected.prefab;
    }


    RoomVariant GetVariant(
        RoomDifficulty difficulty,
        GameObject prefab)
    {
        RoomVariant[] variants =
            null;


        switch (difficulty)
        {
            case RoomDifficulty.Easy:

                variants = easyRooms;

                break;


            case RoomDifficulty.Medium:

                variants = mediumRooms;

                break;


            case RoomDifficulty.Hard:

                variants = hardRooms;

                break;
        }


        if (variants == null)
        {
            return null;
        }


        foreach (RoomVariant variant
                 in variants)
        {
            if (variant != null &&
                variant.prefab == prefab)
            {
                return variant;
            }
        }


        return null;
    }


    float CalculateRotation(
        int worldEntryDirection,
        int prefabEntryDirection)
    {
        if (worldEntryDirection < 0 ||
            prefabEntryDirection < 0)
        {
            return 0f;
        }


        return NormalizeAngle(
            directionAngle[
                worldEntryDirection
            ]
            -
            directionAngle[
                prefabEntryDirection
            ]
        );
    }


    bool[] RemapStatusForRotation(
        bool[] worldStatus,
        float rotationY)
    {
        bool[] localStatus =
            new bool[4];


        for (int worldDirection = 0;
             worldDirection < 4;
             worldDirection++)
        {
            if (!worldStatus[
                    worldDirection])
            {
                continue;
            }


            int localDirection =
                IndexFromAngle(
                    directionAngle[
                        worldDirection
                    ]
                    -
                    rotationY
                );


            if (localDirection >= 0)
            {
                localStatus[
                    localDirection
                ] = true;
            }
        }


        return localStatus;
    }


    int IndexFromAngle(
        float angle)
    {
        angle =
            NormalizeAngle(
                angle
            );


        for (int i = 0;
             i < 4;
             i++)
        {
            if (Mathf.Approximately(
                directionAngle[i],
                angle))
            {
                return i;
            }
        }


        return -1;
    }


    List<int> GetAvailableDirections(
        Vector2Int position,
        HashSet<Vector2Int> occupied)
    {
        List<int> directions =
            new List<int>();


        for (int direction = 0;
             direction < 4;
             direction++)
        {
            Vector2Int next =
                MoveInDirection(
                    position,
                    direction
                );


            if (!IsInsideGrid(next))
            {
                continue;
            }


            if (occupied.Contains(
                    next))
            {
                continue;
            }


            directions.Add(
                direction
            );
        }


        return directions;
    }


    Vector2Int MoveInDirection(
        Vector2Int position,
        int direction)
    {
        switch (direction)
        {
            // Up

            case 0:

                return position +
                       Vector2Int.up;


            // Down

            case 1:

                return position +
                       Vector2Int.down;


            // Right

            case 2:

                return position +
                       Vector2Int.right;


            // Left

            case 3:

                return position +
                       Vector2Int.left;
        }


        return position;
    }


    int DirectionBetween(
        Vector2Int from,
        Vector2Int to)
    {
        Vector2Int difference =
            to - from;


        if (difference ==
            Vector2Int.up)
        {
            return 0;
        }


        if (difference ==
            Vector2Int.down)
        {
            return 1;
        }


        if (difference ==
            Vector2Int.right)
        {
            return 2;
        }


        if (difference ==
            Vector2Int.left)
        {
            return 3;
        }


        return -1;
    }


    int Opposite(
        int direction)
    {
        switch (direction)
        {
            case 0:

                return 1;


            case 1:

                return 0;


            case 2:

                return 3;


            case 3:

                return 2;
        }


        return -1;
    }


    bool IsInsideGrid(
        Vector2Int position)
    {
        return position.x >= 0 &&
               position.x < gridSize.x &&
               position.y >= 0 &&
               position.y < gridSize.y;
    }


    void Shuffle(
        List<int> list)
    {
        for (int i =
                 list.Count - 1;
             i > 0;
             i--)
        {
            int randomIndex =
                Random.Range(
                    0,
                    i + 1
                );


            int temporary =
                list[i];


            list[i] =
                list[randomIndex];


            list[randomIndex] =
                temporary;
        }
    }


    float NormalizeAngle(
        float angle)
    {
        angle %= 360f;


        if (angle < 0f)
        {
            angle += 360f;
        }


        return angle;
    }


    Bounds GetRendererBounds(
        GameObject go)
    {
        Renderer[] renderers =
            go.GetComponentsInChildren<
                Renderer
            >();


        if (renderers.Length == 0)
        {
            return new Bounds(
                go.transform.position,
                Vector3.zero
            );
        }


        Bounds bounds =
            renderers[0].bounds;


        for (int i = 1;
             i < renderers.Length;
             i++)
        {
            bounds.Encapsulate(
                renderers[i].bounds
            );
        }


        return bounds;
    }


    string GetRoomName(
        RoomDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RoomDifficulty.Easy:

                return "EasyRoom";


            case RoomDifficulty.Medium:

                return "MediumRoom";


            case RoomDifficulty.Hard:

                return "HardRoom";


            default:

                return "PlainRoom";
        }
    }
}