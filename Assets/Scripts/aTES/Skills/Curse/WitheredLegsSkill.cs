namespace aTES
{
    public class WitheredLegsSkill : Skill
    {
        public WitheredLegsSkill() : base("Withered Legs", "Permanently reduces the chef's base speed by 20% for the rest of the level, plus slows him by 70% for 5 seconds.", SkillTree.Curse, 5, 30f) { }

        protected override bool Activate(SkillManager m)
        {
            m.Stats.ModifyBaseSpeed(-m.Stats.BaseMoveSpeed * 0.2f);
            m.Stats.ApplySlow(0.3f, 5f);
            return true;
        }
    }
}
