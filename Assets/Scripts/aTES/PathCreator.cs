using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace aTES
{
    /// <summary>One grid cell: exact world center, whether it can be walked on, and which sides are closed.</summary>
    public readonly struct GridCell
    {
        public readonly Vector3 center;
        public readonly bool walkable;
        public readonly TileSides blockedSides;

        public GridCell(Vector3 center, bool walkable, TileSides blockedSides)
        {
            this.center = center;
            this.walkable = walkable;
            this.blockedSides = blockedSides;
        }
    }

    /// <summary>
    /// Put this on a level's root (the object with the Grid). It finds its own child tilemaps named "Ground" and "Walls",
    /// scans them into a grid of cell centers and rescans whenever tiles change.
    /// Walkable = ground tile and no obstacle tile. A <see cref="BarrierTile"/> keeps the cell walkable but closes some sides.
    /// With several levels in one scene, <see cref="Active"/> is the one that is currently enabled.
    /// </summary>
    [ExecuteAlways]
    public class PathCreator : MonoBehaviour
    {
        private static PathCreator _active;

        private Tilemap groundTilemap;
        private Tilemap obstacleTilemap;

        private readonly Dictionary<Vector3Int, GridCell> _cells = new();

        /// <summary>The enabled PathCreator (the current level's grid). Found automatically.</summary>
        public static PathCreator Active => _active && _active.isActiveAndEnabled ? _active : _active = FindFirstObjectByType<PathCreator>();

        public IReadOnlyDictionary<Vector3Int, GridCell> Cells => _cells;

        private void OnEnable()
        {
            _active = this;
            Tilemap.tilemapTileChanged += OnTilesChanged;
            ScanMap();
        }

        private void OnDisable()
        {
            Tilemap.tilemapTileChanged -= OnTilesChanged;
            _active = _active == this ? null : _active;
        }

        private void OnTilesChanged(Tilemap map, Tilemap.SyncTile[] changes) => ScanMap();

        [ContextMenu("Rescan Grid")]
        public void ScanMap()
        {
            groundTilemap = transform.Find("Ground")?.GetComponent<Tilemap>();
            obstacleTilemap = transform.Find("Walls")?.GetComponent<Tilemap>();

            _cells.Clear();
            if (groundTilemap == null || obstacleTilemap == null)
            {
                Debug.LogWarning("PathCreator needs child tilemaps named \"Ground\" and \"Walls\".", this);
                return;
            }

            // Union of both tilemap bounds, so obstacle-only cells (the outer wall) are included too.
            BoundsInt area = groundTilemap.cellBounds;
            area.SetMinMax(Vector3Int.Min(area.min, obstacleTilemap.cellBounds.min), Vector3Int.Max(area.max, obstacleTilemap.cellBounds.max));

            foreach (Vector3Int cell in area.allPositionsWithin)
            {
                bool floor = groundTilemap.HasTile(cell);
                TileBase wall = obstacleTilemap.GetTile(cell);
                if (!floor && wall == null) continue;

                BarrierTile barrier = wall as BarrierTile;
                _cells[cell] = new GridCell(
                    groundTilemap.GetCellCenterWorld(cell),
                    floor && (wall == null || barrier != null),
                    barrier != null ? barrier.BlockedSides : TileSides.None);
            }
        }

        public Vector3Int WorldToCell(Vector3 worldPosition) => groundTilemap.WorldToCell(worldPosition);

        public Vector3 CellToWorldCenter(Vector3Int cell) => groundTilemap.GetCellCenterWorld(cell);

        public bool IsWalkable(Vector3Int cell) => _cells.TryGetValue(cell, out GridCell c) && c.walkable;

        /// <summary>True if both neighbouring cells are walkable and no barrier closes the edge between them.</summary>
        public bool CanMove(Vector3Int from, Vector3Int to)
        {
            int side = System.Array.IndexOf(Sides.Dir, to - from);
            return side >= 0
                && _cells.TryGetValue(from, out GridCell a) && a.walkable && ((int)a.blockedSides >> side & 1) == 0
                && _cells.TryGetValue(to, out GridCell b) && b.walkable && ((int)b.blockedSides >> (side ^ 1) & 1) == 0;
        }

        private void OnDrawGizmos()
        {
            if (groundTilemap == null) return;

            Vector3 size = groundTilemap.cellSize;
            foreach (GridCell c in _cells.Values)
            {
                Color color = c.walkable ? Color.green : Color.red;
                Vector3 box = Vector3.Scale(size, new Vector3(0.9f, 0.9f, 0.01f));

                Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
                Gizmos.DrawCube(c.center, box);
                Gizmos.color = color;
                Gizmos.DrawWireCube(c.center, box);
                Gizmos.DrawSphere(c.center, 0.05f);

                // Closed sides: orange bar on that edge.
                Gizmos.color = new Color(1f, 0.55f, 0f);
                for (int i = 0; i < 4; i++)
                {
                    if (((int)c.blockedSides >> i & 1) == 0) continue;
                    Vector3 d = (Vector3)Sides.Dir[i];
                    Vector3 bar = Vector3.Scale(size, new Vector3(1f - 0.9f * Mathf.Abs(d.x), 1f - 0.9f * Mathf.Abs(d.y), 0.01f));
                    Gizmos.DrawCube(c.center + Vector3.Scale(d, size) * 0.5f, bar);
                }
            }
        }
    }
}
