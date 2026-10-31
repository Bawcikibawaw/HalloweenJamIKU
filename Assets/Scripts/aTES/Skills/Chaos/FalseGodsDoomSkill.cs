namespace aTES
{
    public class FalseGodsDoomSkill : Skill
    {
        public FalseGodsDoomSkill() : base("False God's Doom", "Removes 20 seconds from the timer, teleports the chef to the farthest corner from the delivery point, and stuns him for 2.5 seconds.", SkillTree.Chaos, 5, 40f) { }

        protected override bool Activate(SkillManager m)
        {
            if (m.Timer != null) m.Timer.AddTime(-20f);

            var cells = m.Items != null ? m.Items.GetFreeCells(m.Map.WorldToCell(m.Player.DeliveryPosition)) : null;
            if (cells != null && cells.Count > 0)
                m.Player.Teleport(m.Map.CellToWorldCenter(cells[0]));

            m.Stats.ApplyStun(2.5f);
            return true;
        }
    }
}
