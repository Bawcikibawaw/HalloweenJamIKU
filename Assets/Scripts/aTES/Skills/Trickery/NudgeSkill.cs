namespace aTES
{
    public class NudgeSkill : Skill
    {
        public NudgeSkill() : base("Nudge", "The ingredient the chef is walking to jumps to a random spot in the maze.", SkillTree.Trickery, 1, 8f) { }

        protected override bool Activate(SkillManager m) => MoveItem(m, m.Player.CurrentTarget, false);
    }
}
