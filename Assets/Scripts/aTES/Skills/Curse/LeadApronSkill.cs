namespace aTES
{
    public class LeadApronSkill : Skill
    {
        public LeadApronSkill() : base("Lead Apron", "While the chef carries an ingredient, his speed drops by 75% for 6 seconds.", SkillTree.Curse, 2, 12f) { }

        protected override bool Activate(SkillManager m)
        {
            m.Stats.ApplyCarrySlow(0.25f, 6f);
            return true;
        }
    }
}
