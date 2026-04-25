using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared flow-field navigation grid.
/// Computes one vector per cell that points toward the player's current cell.
/// Enemies can then sample the local vector instead of calculating per-agent paths.
/// </summary>
public class FlowFieldManager : MonoBehaviour
{
    public static FlowFieldManager Instance { get; private set; }

    [Header("Grid")]
    public Vector3 gridCenter = Vector3.zero;
    public Vector2 gridSize = new Vector2(140f, 140f);
    public float cellSize = 2f;

    [Header("Update")]
    [Tooltip("How often to rebuild integration/flow vectors.")]
    public float rebuildInterval = 0.2f;
    [Tooltip("Layers that block movement between cells.")]
    public LayerMask obstacleMask = 0;
    [Tooltip("Offset above ground for obstruction checks.")]
    public float sampleHeight = 0.6f;

    private int _width;
    private int _height;
    private Cell[,] _cells;
    private float _rebuildTimer;
    private Transform _player;

    private static readonly Vector2Int[] Neighbors8 =
    {
        new Vector2Int( 1,  0), new Vector2Int(-1,  0),
        new Vector2Int( 0,  1), new Vector2Int( 0, -1),
        new Vector2Int( 1,  1), new Vector2Int(-1,  1),
        new Vector2Int( 1, -1), new Vector2Int(-1, -1),
    };

    private struct Cell
    {
        public Vector3 worldPos;
        public bool walkable;
        public int cost;
        public int integration;
        public Vector3 flow;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        BuildGrid();
        ResolvePlayer();
        RebuildFlowField();
    }

    private void Update()
    {
        if (_cells == null || _cells.Length == 0)
            return;

        if (_player == null)
            ResolvePlayer();

        _rebuildTimer -= Time.deltaTime;
        if (_rebuildTimer <= 0f)
        {
            _rebuildTimer = Mathf.Max(0.05f, rebuildInterval);
            RebuildFlowField();
        }
    }

    public Vector3 GetFlowDirection(Vector3 worldPosition)
    {
        if (!TryWorldToCell(worldPosition, out int x, out int y))
            return Vector3.zero;

        return _cells[x, y].flow;
    }

    private void ResolvePlayer()
    {
        PlayerHealth p = FindFirstObjectByType<PlayerHealth>();
        _player = p != null ? p.transform : null;
    }

    public void BuildGrid()
    {
        _width = Mathf.Max(4, Mathf.RoundToInt(gridSize.x / Mathf.Max(0.5f, cellSize)));
        _height = Mathf.Max(4, Mathf.RoundToInt(gridSize.y / Mathf.Max(0.5f, cellSize)));
        _cells = new Cell[_width, _height];

        Vector3 origin = GetGridOrigin();
        float half = cellSize * 0.5f;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                Vector3 pos = new Vector3(
                    origin.x + (x * cellSize) + half,
                    gridCenter.y,
                    origin.z + (y * cellSize) + half);

                bool blocked = Physics.CheckBox(
                    pos + Vector3.up * sampleHeight,
                    new Vector3(half * 0.8f, sampleHeight, half * 0.8f),
                    Quaternion.identity,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore);

                _cells[x, y] = new Cell
                {
                    worldPos = pos,
                    walkable = !blocked,
                    cost = blocked ? 255 : 1,
                    integration = int.MaxValue,
                    flow = Vector3.zero
                };
            }
        }
    }

    public void RebuildFlowField()
    {
        if (_cells == null || _cells.Length == 0)
            BuildGrid();

        if (_player == null)
            return;

        if (!TryWorldToCell(_player.position, out int tx, out int ty))
            return;

        ResetIntegrationField();
        BuildIntegrationField(tx, ty);
        BuildFlowVectors();
    }

    private void ResetIntegrationField()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                Cell c = _cells[x, y];
                c.integration = int.MaxValue;
                c.flow = Vector3.zero;
                _cells[x, y] = c;
            }
        }
    }

    private void BuildIntegrationField(int targetX, int targetY)
    {
        if (!InBounds(targetX, targetY))
            return;

        Queue<Vector2Int> open = new Queue<Vector2Int>(256);

        Cell target = _cells[targetX, targetY];
        target.integration = 0;
        _cells[targetX, targetY] = target;
        open.Enqueue(new Vector2Int(targetX, targetY));

        while (open.Count > 0)
        {
            Vector2Int cur = open.Dequeue();
            int currentCost = _cells[cur.x, cur.y].integration;

            for (int i = 0; i < Neighbors8.Length; i++)
            {
                int nx = cur.x + Neighbors8[i].x;
                int ny = cur.y + Neighbors8[i].y;
                if (!InBounds(nx, ny))
                    continue;

                Cell next = _cells[nx, ny];
                if (!next.walkable)
                    continue;

                int stepCost = (Neighbors8[i].x != 0 && Neighbors8[i].y != 0) ? 14 : 10;
                int candidate = currentCost + stepCost + next.cost;

                if (candidate < next.integration)
                {
                    next.integration = candidate;
                    _cells[nx, ny] = next;
                    open.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }
    }

    private void BuildFlowVectors()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                Cell c = _cells[x, y];
                if (!c.walkable)
                {
                    c.flow = Vector3.zero;
                    _cells[x, y] = c;
                    continue;
                }

                int bestCost = c.integration;
                Vector3 bestDir = Vector3.zero;

                for (int i = 0; i < Neighbors8.Length; i++)
                {
                    int nx = x + Neighbors8[i].x;
                    int ny = y + Neighbors8[i].y;
                    if (!InBounds(nx, ny))
                        continue;

                    Cell n = _cells[nx, ny];
                    if (!n.walkable)
                        continue;

                    if (n.integration < bestCost)
                    {
                        bestCost = n.integration;
                        bestDir = (n.worldPos - c.worldPos);
                    }
                }

                bestDir.y = 0f;
                c.flow = bestDir.sqrMagnitude > 0.0001f ? bestDir.normalized : Vector3.zero;
                _cells[x, y] = c;
            }
        }
    }

    private bool TryWorldToCell(Vector3 world, out int x, out int y)
    {
        Vector3 origin = GetGridOrigin();
        float localX = world.x - origin.x;
        float localY = world.z - origin.z;

        x = Mathf.FloorToInt(localX / Mathf.Max(0.5f, cellSize));
        y = Mathf.FloorToInt(localY / Mathf.Max(0.5f, cellSize));
        return InBounds(x, y);
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < _width && y < _height;
    }

    private Vector3 GetGridOrigin()
    {
        return new Vector3(
            gridCenter.x - (gridSize.x * 0.5f),
            gridCenter.y,
            gridCenter.z - (gridSize.y * 0.5f));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.15f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireCube(gridCenter + Vector3.up * 0.05f, new Vector3(gridSize.x, 0.1f, gridSize.y));

        if (_cells == null)
            return;

        float line = Mathf.Max(0.2f, cellSize * 0.35f);
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                Cell c = _cells[x, y];
                if (!c.walkable)
                    continue;

                if (c.flow.sqrMagnitude > 0.001f)
                {
                    Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.6f);
                    Gizmos.DrawRay(c.worldPos + Vector3.up * 0.1f, c.flow * line);
                }
            }
        }
    }
}
