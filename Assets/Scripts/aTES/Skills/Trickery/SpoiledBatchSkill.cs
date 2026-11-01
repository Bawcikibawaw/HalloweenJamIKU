namespace aTES
{
    public class SpoiledBatchSkill : Skill
    {
        public SpoiledBatchSkill() : base("Spoiled Batch", "Steals 1 already-gathered ingredient out of the chef's collection and spawns it back at the farthest corner of the maze.", SkillTree.Trickery, 5, 35f) { }

        protected override bool Activate(SkillManager m)
        {
            if (m.Items == null) return false;
            var cells = m.Items.GetFreeCells(ChefCell(m));
            return cells.Count > 0 && m.Items.RespawnGatheredIngredient(cells[0]);
        }
    }
}
