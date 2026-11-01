using System.Collections;
using UnityEngine;

namespace aTES
{
    public class FalseWallSkill : Skill
    {
        public FalseWallSkill() : base("False Wall", "Places a phantom wall right in front of the chef for 6 seconds, forcing him to take another route.", SkillTree.Trickery, 2, 14f) { }

        protected override bool Activate(SkillManager m)
        {
            PathCreator map = m.Map;
            if (map == null || m.Player == null) return false;

            Vector3Int? targetCellOpt = m.Player.GetStepAheadCell(map);
            if (!targetCellOpt.HasValue) return false;

            Vector3Int blockedCell = targetCellOpt.Value;
            if (!map.IsWalkable(blockedCell)) return false;

            m.StartCoroutine(SpawnPhantomWall(m, map, blockedCell, 6f));
            return true;
        }

        private static IEnumerator SpawnPhantomWall(SkillManager m, PathCreator map, Vector3Int cell, float duration)
        {
            Vector3 cellCenter = map.CellToWorldCenter(cell);
            if (m.Player != null && Vector3.Distance(m.Player.transform.position, cellCenter) < 0.85f)
            {
                Vector3Int playerCell = map.WorldToCell(m.Player.transform.position);
                if (playerCell == cell || !map.IsWalkable(playerCell))
                {
                    Vector3Int safeCell = playerCell;
                    float minDist = float.MaxValue;
                    foreach (Vector3Int dir in Sides.Dir)
                    {
                        Vector3Int n = cell + dir;
                        if (n != cell && map.IsWalkable(n))
                        {
                            float d = (n - cell).sqrMagnitude;
                            if (d < minDist) { minDist = d; safeCell = n; }
                        }
                    }
                    if (minDist < float.MaxValue)
                        m.Player.transform.position = map.CellToWorldCenter(safeCell);
                }
            }

            map.SetWalkable(cell, false);
            if (m.Player != null) m.Player.Replan();

            GameObject wallVisual = new GameObject("PhantomWall");
            wallVisual.transform.SetParent(map.transform, false);
            wallVisual.transform.position = cellCenter;
            wallVisual.transform.localScale = Vector3.one * 0.85f;

            SpriteRenderer sr = wallVisual.AddComponent<SpriteRenderer>();
            SpriteRenderer playerSr = m.Player.GetComponent<SpriteRenderer>();
            if (playerSr != null) sr.sprite = playerSr.sprite;
            sr.color = new Color(0.35f, 0.9f, 0.55f, 0.75f);
            sr.sortingOrder = 6;

            yield return new WaitForSeconds(duration);

            if (wallVisual != null) Object.Destroy(wallVisual);
            if (map != null && map.isActiveAndEnabled)
            {
                map.SetWalkable(cell, true);
                if (m.Player != null) m.Player.Replan();
            }
        }
    }
}
