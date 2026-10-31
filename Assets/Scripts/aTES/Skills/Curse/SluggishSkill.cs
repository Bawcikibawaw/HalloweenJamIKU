namespace aTES
{
    public class SluggishSkill : Skill
    {
        public SluggishSkill() : base("Sluggish", "Curses the chef's legs: he moves 50% slower for 4 seconds.", SkillTree.Curse, 1, 8f) { }

        protected override bool Activate(SkillManager m)
        {
            m.Stats.ApplySlow(0.5f, 4f);
            return true;
        }
    }
}
