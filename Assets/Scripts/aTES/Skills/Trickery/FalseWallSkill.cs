using System.Collections;
using UnityEngine;

namespace aTES
{
    public class FalseWallSkill : Skill
    {
        public FalseWallSkill() : base("False Wall", "Places a phantom wall right in front of the chef for 6 seconds, forcing him to take another route.", SkillTree.Trickery, 2, 14f) { }

        protected override bool Activate(SkillManager m)
        {
            if (m.Player.NextStep is not { } stepPos) return false;

            PathCreator map = m.Map;
            Vector3Int blockedCell = map.WorldToCell(stepPos);
            if (!map.IsWalkable(blockedCell)) return false;

            m.StartCoroutine(SpawnPhantomWall(m, map, blockedCell, 6f));
            return true;
        }

        private static IEnumerator SpawnPhantomWall(SkillManager m, PathCreator map, Vector3Int cell, float duration)
        {
            map.SetWalkable(cell, false);
            m.Player.Replan();

            GameObject wallVisual = new GameObject("PhantomWall");
            wallVisual.transform.SetParent(map.transform, false);
            wallVisual.transform.position = map.CellToWorldCenter(cell);
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
