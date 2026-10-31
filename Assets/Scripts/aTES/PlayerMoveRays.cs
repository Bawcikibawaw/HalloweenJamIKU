using UnityEngine;

namespace aTES
{
    /// <summary>Debug gizmo: a ray from the player to each neighbouring cell center. Green = can move there, red = blocked.</summary>
    public class PlayerMoveRays : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            PathCreator map = PathCreator.Active;
            if (map == null) return;

            Vector3Int cell = map.WorldToCell(transform.position);
            foreach (Vector3Int dir in Sides.Dir)
            {
                Gizmos.color = map.CanMove(cell, cell + dir) ? Color.green : Color.red;
                Gizmos.DrawLine(transform.position, map.CellToWorldCenter(cell + dir));
                Gizmos.DrawSphere(map.CellToWorldCenter(cell + dir), 0.12f);
            }
        }
    }
}
