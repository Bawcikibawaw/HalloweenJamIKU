using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace aTES
{
    [Flags]
    public enum TileSides
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8
    }

    /// <summary>Direction of each <see cref="TileSides"/> bit: bit i is Dir[i], and the opposite side is bit (i ^ 1).</summary>
    public static class Sides
    {
        public static readonly Vector3Int[] Dir = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
    }

    /// <summary>
    /// Wall tile that keeps its cell walkable and only closes the ticked sides (e.g. a half barrier).
    /// Place it on the obstacle tilemap, over a ground tile. Any other tile there makes the cell unwalkable.
    /// </summary>
    [CreateAssetMenu(menuName = "aTES/Barrier Tile")]
    public class BarrierTile : Tile
    {
        [SerializeField] private TileSides blockedSides;

        public TileSides BlockedSides => blockedSides;
    }
}
