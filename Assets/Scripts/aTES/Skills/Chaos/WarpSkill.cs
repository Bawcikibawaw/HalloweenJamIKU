namespace aTES
{
    public class WarpSkill : Skill
    {
        public WarpSkill() : base("Warp", "Teleports the chef to a random reachable cell in the maze.", SkillTree.Chaos, 3, 16f) { }

        protected override bool Activate(SkillManager m)
        {
            if (RandomFreeCell(m) is not { } pos) return false;
            m.Player.Teleport(pos);
            return true;
        }
    }
}
