namespace aTES
{
    public class TimeTheftSkill : Skill
    {
        public TimeTheftSkill() : base("Time Theft", "Steals 10 seconds from the chef's timer.", SkillTree.Chaos, 1, 15f) { }

        protected override bool Activate(SkillManager m)
        {
            if (m.Timer == null) return false;
            m.Timer.AddTime(-10f);
            return true;
        }
    }
}
