namespace aTES
{
    public class BrainFreezeSkill : Skill
    {
        public BrainFreezeSkill() : base("Brain Freeze", "Stuns the chef completely in place for 3 seconds.", SkillTree.Curse, 3, 14f) { }

        protected override bool Activate(SkillManager m)
        {
            m.Stats.ApplyStun(3f);
            return true;
        }
    }
}
