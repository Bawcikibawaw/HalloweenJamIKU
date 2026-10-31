namespace aTES
{
    public class BackToKitchenSkill : Skill
    {
        public BackToKitchenSkill() : base("Back to the Kitchen", "Teleports the chef all the way back to his starting point.", SkillTree.Chaos, 4, 24f) { }

        protected override bool Activate(SkillManager m)
        {
            m.Player.Teleport(m.Player.StartPosition);
            return true;
        }
    }
}
