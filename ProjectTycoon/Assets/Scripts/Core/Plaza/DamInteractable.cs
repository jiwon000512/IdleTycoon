using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 65 냇물 마을: 광장 굴길의 다음 줄을 막은 둑. 치면(break_dam) 첫 타에 파기 값을 내고, damHits번째에 터져 그 줄 굴길 칸이 모두 열린다(물이 한 줄 내려간다).
    // 기준 네모는 냇물 끝 둘레라 둑 곁에 서야 닿는다
    public sealed class DamInteractable : Interactable
    {
        public const string k_Id = "dam";
        private const float k_Reach = 0.4f;

        private readonly int m_needed;
        private readonly NavRect m_rect;

        public int Row { get; }
        public BurrowGrid Grid { get; }
        // 터지면 열리는 굴길 칸
        public IReadOnlyList<Cell> Cells { get; }
        // 둑 그림 자리(냇물 끝 가운데)
        public Vector2 Position { get; }
        public int Hits { get; private set; }
        public int Left => m_needed - Hits;
        // 첫 타에만 낸다
        public double Cost => Hits == 0 ? Grid.DigCost : 0d;

        public DamInteractable(InteractableTable table, WombatArea area, BurrowGrid grid, int row, IReadOnlyList<Cell> cells, NavRect stream, int needed, int hits) : base(table, area)
        {
            Grid = grid;
            Row = row;
            Cells = cells;
            m_needed = needed;
            Hits = hits;
            Position = new Vector2((stream.XMin + stream.XMax) / 2f, stream.YMin);
            m_rect = new NavRect(stream.XMin - k_Reach, stream.YMin - k_Reach, stream.XMax + k_Reach, stream.YMin);
        }

        public override float DistanceTo(Vector2 p)
        {
            return m_rect.DistanceTo(p);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return Position;
        }

        // 한 번 친다. 첫 타에 코인이 모자라면 false
        internal bool TryStrike(Worker worker)
        {
            if (Left <= 0 || Hits == 0 && !worker.Wallet.TrySpendCoins(Grid.DigCost))
            {
                return false;
            }

            Hits++;
            Area.Wombat.Dig(Position);
            Area.Bus.Publish(new Events.DamStruck(this));

            if (Left <= 0)
            {
                foreach (Cell cell in Cells)
                {
                    if (!Grid.Contains(cell))
                    {
                        Grid.Dig(cell);
                    }
                }
            }

            return true;
        }
    }
}
