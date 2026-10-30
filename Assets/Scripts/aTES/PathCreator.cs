using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace aTES
{
    /// <summary>
    /// One grid cell: its coordinate, exact world center, whether it can be walked on,
    /// and which of its sides have a barrier (a walkable cell can still have closed sides).
    /// </summary>
    [Serializable]
    public struct GridCell
    {
        public Vector3Int cell;
        public Vector3 center;
        public bool walkable;
        public TileSides blockedSides;
    }

    /// <summary>
    /// Scans 2D tilemaps and builds a grid of cell centers marked walkable / unwalkable.
    /// Walkable  = the ground tilemap has a tile AND the obstacle tilemap has no solid tile there.
    /// Unwalkable = a solid obstacle tile is present. Cells with no tile at all are treated as unwalkable too.
    /// A <see cref="BarrierTile"/> on the obstacle tilemap keeps the cell walkable but closes some of its sides.
    /// Rescans automatically whenever one of the two tilemaps changes, so new maps need no extra setup.
    /// </summary>
    [ExecuteAlways]
    public class PathCreator : MonoBehaviour
    {
        [Tooltip("Floor tilemap. Every painted tile here is a candidate walkable cell.")]
        [SerializeField] private Tilemap groundTilemap;

        [Tooltip("Optional walls/obstacles tilemap. A tile here makes the cell unwalkable.")]
        [SerializeField] private Tilemap obstacleTilemap;

        [SerializeField] private bool drawGizmos = true;

        private readonly Dictionary<Vector3Int, GridCell> _lookup = new Dictionary<Vector3Int, GridCell>();
        private readonly List<GridCell> _cells = new List<GridCell>();

        /// <summary>Raised after every rescan.</summary>
        public event Action GridRebuilt;

        /// <summary>All known cells (walkable and unwalkable).</summary>
        public IReadOnlyList<GridCell> Cells => _cells;

        private void OnEnable()
        {
            Tilemap.tilemapTileChanged += OnTilemapChanged;
            ScanMap();
        }

        private void OnDisable()
        {
            Tilemap.tilemapTileChanged -= OnTilemapChanged;
        }

        private void OnTilemapChanged(Tilemap tilemap, Tilemap.SyncTile[] changes)
        {
            if (tilemap == groundTilemap || tilemap == obstacleTilemap)
            {
                ScanMap();
            }
        }

        /// <summary>Recalculates every cell center and its walkable state.</summary>
        [ContextMenu("Rescan Grid")]
        public void ScanMap()
        {
            _cells.Clear();
            _lookup.Clear();

            if (groundTilemap == null)
            {
                groundTilemap = GetComponentInChildren<Tilemap>();
            }

            if (groundTilemap == null)
            {
                return;
            }

            // Union of both tilemap bounds so obstacle-only cells are included as well.
            BoundsInt ground = groundTilemap.cellBounds;
            Vector3Int min = ground.min;
            Vector3Int max = ground.max;

            if (obstacleTilemap != null)
            {
                BoundsInt obstacle = obstacleTilemap.cellBounds;
                if (obstacle.size.x > 0 && obstacle.size.y > 0)
                {
                    if (ground.size.x <= 0 || ground.size.y <= 0)
                    {
                        min = obstacle.min;
                        max = obstacle.max;
                    }
                    else
                    {
                        min = Vector3Int.Min(min, obstacle.min);
                        max = Vector3Int.Max(max, obstacle.max);
                    }
                }
            }

            for (int x = min.x; x < max.x; x++)
            {
                for (int y = min.y; y < max.y; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);

                    bool hasGround = groundTilemap.HasTile(cell);
                    TileBase obstacleTile = obstacleTilemap != null ? obstacleTilemap.GetTile(cell) : null;

                    // A BarrierTile on top of ground only closes some sides; any other obstacle tile is solid.
                    BarrierTile barrier = obstacleTile as BarrierTile;
                    bool solid = obstacleTile != null && (barrier == null || !hasGround);

                    if (!hasGround && obstacleTile == null)
                    {
                        continue;
                    }

                    GridCell data = new GridCell
                    {
                        cell = cell,
                        center = groundTilemap.GetCellCenterWorld(cell),
                        walkable = hasGround && !solid,
                        blockedSides = barrier != null && hasGround ? barrier.blockedSides : TileSides.None
                    };

                    _cells.Add(data);
                    _lookup[cell] = data;
                }
            }

            GridRebuilt?.Invoke();
        }

        /// <summary>True if the cell exists and can be walked on.</summary>
        public bool IsWalkable(Vector3Int cell)
        {
            return _lookup.TryGetValue(cell, out GridCell data) && data.walkable;
        }

        /// <summary>
        /// True if the player can step between two neighbouring cells: both are walkable
        /// and no barrier closes the shared edge on either side.
        /// </summary>
        public bool CanMove(Vector3Int from, Vector3Int to)
        {
            Vector3Int delta = to - from;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                return false;
            }

            if (!_lookup.TryGetValue(from, out GridCell a) || !a.walkable) return false;
            if (!_lookup.TryGetValue(to, out GridCell b) || !b.walkable) return false;

            TileSides leaving = delta.y > 0 ? TileSides.Up : delta.y < 0 ? TileSides.Down : delta.x < 0 ? TileSides.Left : TileSides.Right;
            TileSides entering = delta.y > 0 ? TileSides.Down : delta.y < 0 ? TileSides.Up : delta.x < 0 ? TileSides.Right : TileSides.Left;

            return (a.blockedSides & leaving) == 0 && (b.blockedSides & entering) == 0;
        }

        /// <summary>True if the world position lies inside a walkable cell.</summary>
        public bool IsWorldPositionWalkable(Vector3 worldPosition)
        {
            return IsWalkable(WorldToCell(worldPosition));
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            return groundTilemap != null ? groundTilemap.WorldToCell(worldPosition) : Vector3Int.zero;
        }

        /// <summary>Exact world-space center of a cell.</summary>
        public Vector3 CellToWorldCenter(Vector3Int cell)
        {
            return groundTilemap != null ? groundTilemap.GetCellCenterWorld(cell) : (Vector3)cell;
        }

        /// <summary>Snaps any world position to the center of the cell it is in.</summary>
        public Vector3 SnapToCellCenter(Vector3 worldPosition)
        {
            return CellToWorldCenter(WorldToCell(worldPosition));
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || groundTilemap == null)
            {
                return;
            }

            Vector3 cellSize = groundTilemap.cellSize;
            Vector3 boxSize = new Vector3(cellSize.x * 0.9f, cellSize.y * 0.9f, 0.01f);

            for (int i = 0; i < _cells.Count; i++)
            {
                GridCell c = _cells[i];
                Color color = c.walkable ? new Color(0.1f, 0.9f, 0.3f) : new Color(1f, 0.2f, 0.2f);

                Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
                Gizmos.DrawCube(c.center, boxSize);

                Gizmos.color = color;
                Gizmos.DrawWireCube(c.center, boxSize);
                Gizmos.DrawSphere(c.center, 0.05f);

                // Closed sides of walkable cells: thick orange bar on that edge.
                if (c.blockedSides != TileSides.None)
                {
                    Gizmos.color = new Color(1f, 0.55f, 0f);
                    float hx = cellSize.x * 0.5f;
                    float hy = cellSize.y * 0.5f;
                    if ((c.blockedSides & TileSides.Up) != 0) Gizmos.DrawCube(c.center + new Vector3(0, hy, 0), new Vector3(cellSize.x, 0.1f, 0.01f));
                    if ((c.blockedSides & TileSides.Down) != 0) Gizmos.DrawCube(c.center + new Vector3(0, -hy, 0), new Vector3(cellSize.x, 0.1f, 0.01f));
                    if ((c.blockedSides & TileSides.Left) != 0) Gizmos.DrawCube(c.center + new Vector3(-hx, 0, 0), new Vector3(0.1f, cellSize.y, 0.01f));
                    if ((c.blockedSides & TileSides.Right) != 0) Gizmos.DrawCube(c.center + new Vector3(hx, 0, 0), new Vector3(0.1f, cellSize.y, 0.01f));
                }
            }
        }
    }
}
