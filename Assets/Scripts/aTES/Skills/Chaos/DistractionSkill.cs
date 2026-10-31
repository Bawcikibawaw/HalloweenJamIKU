namespace aTES
{
    public class DistractionSkill : Skill
    {
        public DistractionSkill() : base("Distraction", "Forces the chef to abandon his current path and walk toward the farthest ingredient instead.", SkillTree.Chaos, 2, 14f) { }

        protected override bool Activate(SkillManager m) => m.Player.TargetFarthestItem();
    }
}
