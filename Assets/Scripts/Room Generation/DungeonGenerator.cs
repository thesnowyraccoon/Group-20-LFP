using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public enum RoomDifficulty { Plain, Easy, Medium, Hard }

    public class Cell
    {
        public bool visited = false;
        public bool[] status = new bool[4]; // 0-Up 1-Down 2-Right 3-Left
        public RoomDifficulty difficulty = RoomDifficulty.Plain;

        // Only meaningful when difficulty != Plain. These are the WORLD-space
        // directions the challenge room's entry/exit must line up with.
        public int incomingDir = -1;
        public int outgoingDir = -1;
    }

    [System.Serializable]
    public class RoomVariant
    {
        public GameObject prefab;

        [Tooltip("Door index this prefab was BUILT with as the entrance, before any rotation is applied. 0-Up 1-Down 2-Right 3-Left")]
        public int entryDoor = 3;

        [Tooltip("Door index this prefab was BUILT with as the exit, before any rotation is applied. 0-Up 1-Down 2-Right 3-Left. Must currently be the OPPOSITE side from entryDoor (straight-through layout only).")]
        public int exitDoor = 2;
    }

    [Header("Grid")]
    public Vector2Int size;
    public int startPos = 0;
    public Vector2 offset;

    [Header("Generic / connector rooms (junctions, corners, dead-ends, start & end)")]
    public GameObject[] plainRooms;

    [Header("Challenge room variants - straight two-door segments of the main route only")]
    public RoomVariant[] easyRooms;
    public RoomVariant[] mediumRooms;
    public RoomVariant[] hardRooms;

    List<Cell> board;
    List<int> mainPath;

    // World-space angle (degrees, Y rotation) for each direction index. Adjust
    // these if your prefabs' "forward" axis doesn't match this convention -
    // easiest way to check is to drop one challenge prefab in a test cell and
    // see which way it's actually facing after rotation.
    static readonly float[] directionAngle = { 0f, 180f, 90f, 270f }; // Up, Down, Right, Left

    Dictionary<RoomDifficulty, List<RoomVariant>> shuffleBags = new Dictionary<RoomDifficulty, List<RoomVariant>>();
    RoomVariant lastUsedEasy, lastUsedMedium, lastUsedHard;

    void Start()
    {
        MazeGenerator();
    }

    void MazeGenerator()
    {
        // Without this, the Editor can carry UnityEngine.Random's state across
        // Play sessions (depends on your Enter Play Mode / Domain Reload
        // settings), so the exact same sequence of "random" calls produces the
        // exact same maze and the exact same room picks every time you hit Play.
        Random.InitState(System.Environment.TickCount);

        board = new List<Cell>();
        for (int i = 0; i < size.x; i++)
            for (int j = 0; j < size.y; j++)
                board.Add(new Cell());

        int currentCell = startPos;
        Stack<int> path = new Stack<int>();
        List<int> traveled = new List<int> { currentCell };

        int k = 0;
        while (k < 1000)
        {
            k++;
            board[currentCell].visited = true;

            if (currentCell == board.Count - 1)
                break;

            List<int> neighbors = CheckNeighbors(currentCell);

            if (neighbors.Count == 0)
            {
                if (path.Count == 0)
                    break;

                currentCell = path.Pop();
                traveled.Add(currentCell); // log the backtrack so we can reconstruct the real route later
            }
            else
            {
                path.Push(currentCell);
                int newCell = neighbors[Random.Range(0, neighbors.Count)];

                if (newCell > currentCell)
                {
                    if (newCell - 1 == currentCell)
                    {
                        board[currentCell].status[2] = true; // Right
                        currentCell = newCell;
                        board[currentCell].status[3] = true; // Left
                    }
                    else
                    {
                        board[currentCell].status[1] = true; // Down
                        currentCell = newCell;
                        board[currentCell].status[0] = true; // Up
                    }
                }
                else
                {
                    if (newCell + 1 == currentCell)
                    {
                        board[currentCell].status[3] = true; // Left
                        currentCell = newCell;
                        board[currentCell].status[2] = true; // Right
                    }
                    else
                    {
                        board[currentCell].status[0] = true; // Up
                        currentCell = newCell;
                        board[currentCell].status[1] = true; // Down
                    }
                }

                traveled.Add(currentCell);
            }
        }

        BuildMainPath(traveled);
        AssignDifficulties();
        GenerateDungeon();
    }

    // Collapses the raw walk (which includes backtracks) down to the single
    // start -> end route, by dropping anything between a cell's first visit
    // and the next time that same cell shows up in the log.
    void BuildMainPath(List<int> traveled)
    {
        mainPath = new List<int>();
        foreach (int cell in traveled)
        {
            int idx = mainPath.IndexOf(cell);
            if (idx >= 0)
                mainPath.RemoveRange(idx + 1, mainPath.Count - idx - 1);
            else
                mainPath.Add(cell);
        }
    }

    void AssignDifficulties()
    {
        if (mainPath == null || mainPath.Count < 3)
            return;

        int total = mainPath.Count;

        // Skip index 0 (start) and the last index (end) - those stay Plain.
        for (int step = 1; step < total - 1; step++)
        {
            int prev = mainPath[step - 1];
            int cur = mainPath[step];
            int next = mainPath[step + 1];

            int incoming = Opposite(DirBetween(prev, cur));
            int outgoing = DirBetween(cur, next);

            if (incoming < 0 || outgoing < 0)
                continue;

            Cell cell = board[cur];

            // Only treat this as a challenge slot if those are the ONLY two
            // active doors (no stray branch door from an earlier backtrack)
            // and they're on opposite sides (straight segment, not a corner).
            if (!IsExactlyStraight(cell, incoming, outgoing))
                continue;

            float t = (float)step / (total - 1);
            if (t < 1f / 3f)
                cell.difficulty = RoomDifficulty.Easy;
            else if (t < 2f / 3f)
                cell.difficulty = RoomDifficulty.Medium;
            else
                cell.difficulty = RoomDifficulty.Hard;

            cell.incomingDir = incoming;
            cell.outgoingDir = outgoing;
        }
    }

    bool IsExactlyStraight(Cell cell, int a, int b)
    {
        if (Opposite(a) != b) return false; // not opposite sides -> it's a corner, leave as Plain

        for (int d = 0; d < 4; d++)
        {
            bool shouldBeOpen = (d == a || d == b);
            if (cell.status[d] != shouldBeOpen)
                return false; // extra branch door present -> treat as junction
        }
        return true;
    }

    int DirBetween(int from, int to)
    {
        if (to == from + 1) return 2;        // Right
        if (to == from - 1) return 3;        // Left
        if (to == from + size.x) return 1;   // Down
        if (to == from - size.x) return 0;   // Up
        return -1;
    }

    int Opposite(int d)
    {
        if (d == 0) return 1;
        if (d == 1) return 0;
        if (d == 2) return 3;
        if (d == 3) return 2;
        return -1;
    }

    void GenerateDungeon()
    {
        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                Cell currentCell = board[i + j * size.x];
                if (!currentCell.visited) continue;

                RoomVariant variant = null;
                if (currentCell.difficulty != RoomDifficulty.Plain)
                    variant = PickVariant(currentCell.difficulty);

                GameObject prefabToSpawn;
                float rotationY = 0f;
                bool[] localStatus = currentCell.status;

                if (variant != null)
                {
                    prefabToSpawn = variant.prefab;
                    rotationY = NormalizeAngle(directionAngle[currentCell.incomingDir] - directionAngle[variant.entryDoor]);
                    localStatus = RemapStatusForRotation(currentCell.status, rotationY);
                }
                else
                {
                    if (plainRooms == null || plainRooms.Length == 0)
                    {
                        Debug.LogWarning("No plainRooms assigned - skipping cell " + i + "," + j);
                        continue;
                    }
                    prefabToSpawn = plainRooms[Random.Range(0, plainRooms.Length)];
                }

                if (prefabToSpawn == null) continue;

                GameObject instanceGO = Instantiate(
                    prefabToSpawn,
                    new Vector3(i * offset.x, 0, -j * offset.y),
                    Quaternion.identity,
                    transform
                );

                // Rotate around the room's actual visual center, not whatever
                // point the prefab happens to use as its transform pivot -
                // otherwise a 90/270 rotation can swing the mesh sideways out
                // of its assigned grid cell and into a neighbor.
                if (!Mathf.Approximately(rotationY, 0f))
                {
                    Bounds bounds = GetRendererBounds(instanceGO);
                    instanceGO.transform.RotateAround(bounds.center, Vector3.up, rotationY);
                }

                var instance = instanceGO.GetComponent<RoomBehaviour>();
                instance.UpdateRoom(localStatus);
                instance.name += " " + i + "-" + j;
            }
        }
    }

    // A rotated room's LOCAL door index no longer matches the WORLD direction
    // stored in Cell.status, so we translate world -> local before calling
    // UpdateRoom, otherwise a rotated room opens the wrong doors/walls.
    bool[] RemapStatusForRotation(bool[] worldStatus, float rotationY)
    {
        bool[] local = new bool[4];
        for (int w = 0; w < 4; w++)
        {
            if (!worldStatus[w]) continue;
            int l = IndexFromAngle(directionAngle[w] - rotationY);
            if (l >= 0) local[l] = true;
        }
        return local;
    }

    int IndexFromAngle(float angle)
    {
        angle = NormalizeAngle(angle);
        for (int i = 0; i < 4; i++)
            if (Mathf.Approximately(directionAngle[i], angle))
                return i;
        return -1;
    }

    float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a < 0) a += 360f;
        return a;
    }

    RoomVariant PickVariant(RoomDifficulty difficulty)
    {
        RoomVariant[] source = null;
        if (difficulty == RoomDifficulty.Easy) source = easyRooms;
        else if (difficulty == RoomDifficulty.Medium) source = mediumRooms;
        else if (difficulty == RoomDifficulty.Hard) source = hardRooms;

        if (source == null || source.Length == 0)
            return null;

        List<RoomVariant> bag;
        if (!shuffleBags.TryGetValue(difficulty, out bag) || bag.Count == 0)
        {
            bag = new List<RoomVariant>(source);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int r = Random.Range(0, i + 1);
                RoomVariant tmp = bag[i];
                bag[i] = bag[r];
                bag[r] = tmp;
            }

            RoomVariant last = null;
            if (difficulty == RoomDifficulty.Easy) last = lastUsedEasy;
            else if (difficulty == RoomDifficulty.Medium) last = lastUsedMedium;
            else if (difficulty == RoomDifficulty.Hard) last = lastUsedHard;

            if (bag.Count > 1 && bag[0] == last)
            {
                RoomVariant tmp = bag[0];
                bag[0] = bag[1];
                bag[1] = tmp;
            }

            shuffleBags[difficulty] = bag;
        }

        RoomVariant picked = bag[0];
        bag.RemoveAt(0);

        if (difficulty == RoomDifficulty.Easy) lastUsedEasy = picked;
        else if (difficulty == RoomDifficulty.Medium) lastUsedMedium = picked;
        else if (difficulty == RoomDifficulty.Hard) lastUsedHard = picked;

        return picked;
    }

    Bounds GetRendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    List<int> CheckNeighbors(int cell)
    {
        List<int> neighbors = new List<int>();

        if (cell - size.x >= 0 && !board[cell - size.x].visited)
            neighbors.Add(cell - size.x);

        if (cell + size.x < board.Count && !board[cell + size.x].visited)
            neighbors.Add(cell + size.x);

        if ((cell + 1) % size.x != 0 && !board[cell + 1].visited)
            neighbors.Add(cell + 1);

        if (cell % size.x != 0 && !board[cell - 1].visited)
            neighbors.Add(cell - 1);

        return neighbors;
    }
}