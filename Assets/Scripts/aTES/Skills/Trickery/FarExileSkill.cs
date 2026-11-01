using System.Linq;

namespace aTES
{
    public class FarExileSkill : Skill
    {
        public FarExileSkill() : base("Far Exile", "Sends every ingredient currently on the floor to the farthest reachable cells away from the chef.", SkillTree.Trickery, 4, 22f) { }

        protected override bool Activate(SkillManager m)
        {
            if (m.Items == null || m.Items.ActiveItems.Count == 0) return false;

            bool moved = false;
            foreach (Item item in m.Items.ActiveItems.Keys.ToList())
                moved |= MoveItem(m, item, true);

            return moved;
        }
    }
}
