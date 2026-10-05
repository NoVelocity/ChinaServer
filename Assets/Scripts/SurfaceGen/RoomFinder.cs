using System.Collections.Generic;
using UnityEngine;

public class RoomFinder : MonoBehaviour
{
    public Renderer floor; 
    public float cellSize = 0.5f; 
    public LayerMask wallMask = ~0;
    public float checkHeight = 1f; 
    public Transform wallsRoot;

    public class Room
    {
        public List<Vector2Int> cells = new();
        public Vector3 center;
        public float area;
    }

    public List<Room> rooms = new();

    [ContextMenu("Find Rooms")]
    public void FindRooms()
    {
        rooms.Clear();


        Bounds b = floor.bounds;
        int w = Mathf.CeilToInt(b.size.x / cellSize);
        int h = Mathf.CeilToInt(b.size.z / cellSize);

        bool[,] blocked = new bool[w, h];
        Vector3 half = new Vector3(cellSize, 0.1f, cellSize) * 0.45f;

        var walls = new List<Renderer>();
        foreach (var r in wallsRoot.GetComponentsInChildren<Renderer>())
            if (r != floor)
                walls.Add(r);

        float pad = cellSize * 0.5f;

        for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
        {
            float px = b.min.x + (x + 0.5f) * cellSize;
            float pz = b.min.z + (z + 0.5f) * cellSize;

            foreach (var r in walls)
            {
                Bounds wb = r.bounds;
                if (px >= wb.min.x - pad && px <= wb.max.x + pad &&
                    pz >= wb.min.z - pad && pz <= wb.max.z + pad)
                {
                    blocked[x, z] = true;
                    break;
                }
            }
        }

        int cnt = 0;
        foreach (var v in blocked)
            if (v)
                cnt++;
        Debug.Log($"Cells: {cnt} из {w * h}");

        bool[,] visited = new bool[w, h];
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
        {
            if (blocked[x, z] || visited[x, z]) continue;

            var room = new Room();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(x, z));
            visited[x, z] = true;

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                room.cells.Add(c);

                foreach (var d in dirs)
                {
                    int nx = c.x + d.x, nz = c.y + d.y;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    if (blocked[nx, nz] || visited[nx, nz]) continue;
                    visited[nx, nz] = true;
                    queue.Enqueue(new Vector2Int(nx, nz));
                }
            }

            Vector3 sum = Vector3.zero;
            foreach (var c in room.cells) sum += CellToWorld(c.x, c.y, b);
            room.center = sum / room.cells.Count;
            room.area = room.cells.Count * cellSize * cellSize;
            rooms.Add(room);
        }

        Debug.Log($"Rooms: {rooms.Count}");
    }

    Vector3 CellToWorld(int x, int z, Bounds b) =>
        new Vector3(b.min.x + (x + 0.5f) * cellSize,
            b.min.y + checkHeight,
            b.min.z + (z + 0.5f) * cellSize);

    void OnDrawGizmos()
    {
        if (rooms == null) return;
        Random.InitState(1);
        foreach (var r in rooms)
        {
            Gizmos.color = Color.HSVToRGB(Random.value, 0.8f, 1f) * new Color(1, 1, 1, 0.5f);
            foreach (var c in r.cells)
                Gizmos.DrawCube(CellToWorld(c.x, c.y, floor.bounds) - Vector3.up * checkHeight + Vector3.up * 0.05f,
                    new Vector3(cellSize, 0.02f, cellSize) * 0.95f);
        }
    }
}