using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5 · 설계 13 → 설계 27: 팔 수 있는 흙 칸(빵집·농장 공용). 거리는 칸 네모까지, 시트에서 판다(ActionFactory.Dig가 Grid에 값을 치른다).
    public sealed class DigInteractable : Interactable
    {
        public const string k_Id = "dig";

        private readonly Func<Cell, NavRect> m_rect;

        public Cell Cell { get; }
        public BurrowGrid Grid { get; }

        public DigInteractable(InteractableTable table, Cell cell, BurrowGrid grid, Func<Cell, NavRect> rect, WombatArea area) : base(table, area)
        {
            Cell = cell;
            Grid = grid;
            m_rect = rect;
        }

        public override float DistanceTo(Vector2 p)
        {
            return m_rect(Cell).DistanceTo(p);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return ClosestPoint(from);
        }

        // 칸 네모에서 p에 가장 가까운 점(웜뱃이 파는 쪽을 본다)
        public Vector2 ClosestPoint(Vector2 p)
        {
            NavRect r = m_rect(Cell);
            return new Vector2(Math.Clamp(p.X, r.XMin, r.XMax), Math.Clamp(p.Y, r.YMin, r.YMax));
        }
    }
}
