using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace aTES
{
    /// <summary>Sides of a cell. Up = +Y, Right = +X.</summary>
    [Flags]
    public enum TileSides
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8
    }

    /// <summary>
    /// A wall tile that does NOT make its cell unwalkable (when it sits on a ground tile).
    /// It only blocks movement across the ticked sides, e.g. a half barrier along the top of a cell.
    /// Put these on the obstacle tilemap. Plain tiles there still make the whole cell unwalkable.
    /// </summary>
    [CreateAssetMenu(menuName = "aTES/Barrier Tile")]
    public class BarrierTile : Tile
    {
        public TileSides blockedSides;
    }
}
