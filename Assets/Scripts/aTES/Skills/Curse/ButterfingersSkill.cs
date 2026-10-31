namespace aTES
{
    public class ButterfingersSkill : Skill
    {
        public ButterfingersSkill() : base("Butterfingers", "Forces the chef to drop the ingredient he is carrying and stuns him for 1.5 seconds.", SkillTree.Curse, 4, 18f) { }

        protected override bool Activate(SkillManager m)
        {
            if (!m.Player.IsCarrying || RandomFreeCell(m) is not { } pos) return false;
            m.Player.DropCarried(pos);
            m.Stats.ApplyStun(1.5f);
            return true;
        }
    }
}
