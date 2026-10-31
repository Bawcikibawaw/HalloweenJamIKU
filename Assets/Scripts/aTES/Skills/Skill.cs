using UnityEngine;

namespace aTES
{
    public enum SkillTree { Curse, Trickery, Chaos }

    /// <summary>
    /// Base for every skill. Create a new script that inherits this (with a parameterless constructor)
    /// and <see cref="SkillManager"/> picks it up automatically — no scene setup needed.
    /// Tier 1 is always choosable; tier N needs tier N-1 of the same tree.
    /// Optional icon: put a sprite at Resources/SkillIcons/&lt;ClassName&gt;.
    /// </summary>
    public abstract class Skill
    {
        public string Name { get; }
        public string Description { get; }
        public SkillTree Tree { get; }
        public int Tier { get; }
        public float Cooldown { get; }
        public Sprite Icon { get; }
        public Color Color => TreeColor(Tree);

        private float readyTime;
        public float CooldownLeft => Mathf.Max(0f, readyTime - Time.time);
        public float CooldownPercent => Cooldown > 0f ? CooldownLeft / Cooldown : 0f;

        protected Skill(string name, string description, SkillTree tree, int tier, float cooldown)
        {
            Name = name;
            Description = description;
            Tree = tree;
            Tier = tier;
            Cooldown = cooldown;
            Icon = Resources.Load<Sprite>($"SkillIcons/{GetType().Name}");
        }

        /// <summary>Uses the skill if it is off cooldown. Cooldown only starts if the skill actually did something.</summary>
        public bool TryUse(SkillManager m)
        {
            if (CooldownLeft > 0f || !Activate(m)) return false;
            readyTime = Time.time + Cooldown;
            return true;
        }

        /// <summary>Do the effect. Return false if it could not be used (nothing happens, no cooldown).</summary>
        protected abstract bool Activate(SkillManager m);

        public static Color TreeColor(SkillTree tree) => tree switch
        {
            SkillTree.Curse => new Color(0.65f, 0.35f, 0.9f),
            SkillTree.Trickery => new Color(0.35f, 0.8f, 0.45f),
            _ => new Color(1f, 0.55f, 0.2f),
        };

        // ---- Shared helpers for skills ----

        protected static Vector3Int ChefCell(SkillManager m) => m.Map.WorldToCell(m.Player.transform.position);

        /// <summary>Moves an item to a free reachable cell: the farthest from the chef, or a random one.</summary>
        protected static bool MoveItem(SkillManager m, Item item, bool farthest)
        {
            if (item == null || m.Items == null) return false;
            var cells = m.Items.GetFreeCells(ChefCell(m));
            return cells.Count > 0 && m.Items.TryMoveItem(item, farthest ? cells[0] : cells[Random.Range(0, cells.Count)]);
        }

        /// <summary>Random free reachable cell center (not the chef's, not an item's), or null if none.</summary>
        protected static Vector3? RandomFreeCell(SkillManager m)
        {
            var cells = m.Items != null ? m.Items.GetFreeCells(ChefCell(m)) : null;
            return cells != null && cells.Count > 0 ? m.Map.CellToWorldCenter(cells[Random.Range(0, cells.Count)]) : null;
        }
    }
}
