using UnityEngine;

namespace aTES
{
    /// <summary>
    /// Debug visual: draws a ray from the player to each of the 4 neighbouring cell centers.
    /// Green = the player can move there, red = blocked. Uses the grid built by <see cref="PathCreator"/>.
    /// Visible in the Scene view, and in the Game view when the Gizmos toggle is on.
    /// </summary>
    [ExecuteAlways]
    public class PlayerMoveRays : MonoBehaviour
    {
        [SerializeField] private PathCreator pathCreator;

        private static readonly Vector3Int[] Directions =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
        };

        private void OnDrawGizmos()
        {
            if (pathCreator == null)
            {
                return;
            }

            Vector3 origin = transform.position;
            Vector3Int currentCell = pathCreator.WorldToCell(origin);

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(pathCreator.CellToWorldCenter(currentCell), 0.15f);

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector3Int neighbour = currentCell + Directions[i];
                Vector3 end = pathCreator.CellToWorldCenter(neighbour);

                Gizmos.color = pathCreator.CanMove(currentCell, neighbour) ? Color.green : Color.red;
                Gizmos.DrawLine(origin, end);
                Gizmos.DrawSphere(end, 0.12f);
            }
        }
    }
}
