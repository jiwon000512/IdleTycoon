using System.Linq;
using NUnit.Framework;
using GameKit.Events;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 굴 격자 설계 v0.5 검증 1: 시작 8칸, 파기 규칙, 비용(값을 치르는 건 파기 행동 — BakeryAreaTests, 길 찾기는 BurrowNavTests)
    public sealed class BurrowGridTests
    {
        private EventBus m_bus;

        private BurrowGrid Create()
        {
            BakeryConfigTable config = TestTables.Load().Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery);
            m_bus = new EventBus();
            return new BurrowGrid(BurrowGrid.Columns(2, 4), config.DigBaseCost, config.DigCostGrowth, CellBounds.None, m_bus);
        }

        [Test]
        public void Start_HasEightCellsInTwoColumns()
        {
            BurrowGrid grid = Create();

            Assert.That(grid.Cells.Count, Is.EqualTo(8));
            Assert.That(grid.Contains(new Cell(-1, 0)), Is.True);
            Assert.That(grid.Contains(new Cell(0, 3)), Is.True);
        }

        [Test]
        public void CanDig_OnlyNeighborsBelowEntranceRow()
        {
            BurrowGrid grid = Create();

            Assert.That(grid.CanDig(new Cell(1, 1)), Is.True);
            Assert.That(grid.CanDig(new Cell(0, 4)), Is.True);
            Assert.That(grid.CanDig(new Cell(1, 4)), Is.False);
            Assert.That(grid.CanDig(new Cell(-2, 0)), Is.False);
            Assert.That(grid.CanDig(new Cell(0, 1)), Is.False);
            Assert.That(grid.Frontier().Count(), Is.EqualTo(8));
            Assert.That(grid.Frontier(-1, 0).ToList(), Is.EquivalentTo(new[] { new Cell(-2, 1), new Cell(-2, 2), new Cell(-2, 3) }));
            Assert.That(grid.Frontier(0, 1).ToList(), Is.EquivalentTo(new[] { new Cell(-1, 4), new Cell(0, 4) }));
        }

        [Test]
        public void Dig_GrowsCostAndDugCellCannotBeDugAgain()
        {
            BurrowGrid grid = Create();
            int dug = 0;
            m_bus.Subscribe<Events.Dug>(e => dug += e.Grid == grid ? 1 : 0);

            Assert.That(grid.DigCost, Is.EqualTo(150d));
            grid.Dig(new Cell(1, 1));
            Assert.That(grid.DigCost, Is.EqualTo(210d).Within(1e-9));
            Assert.That(grid.CanDig(new Cell(1, 1)), Is.False);
            Assert.That(grid.CanDig(new Cell(2, 1)), Is.True);
            grid.Dig(new Cell(2, 1));
            Assert.That(grid.DigCost, Is.EqualTo(294d).Within(1e-9));
            Assert.That(dug, Is.EqualTo(2));
        }

        // 설계 27: 층 범위(가로 4 × 세로 4) 밖은 팔 수 없고, 비용은 시작 칸 수 기준이며, 층이 다 차면 팔 칸이 없다
        [Test]
        public void Bounds_LimitDiggingToFloor()
        {
            BurrowGrid grid = new BurrowGrid(BurrowGrid.Columns(2, 2), 100d, 1.3d, new CellBounds(4, 4), new EventBus());

            Assert.That(grid.Cells.Count, Is.EqualTo(4));
            Assert.That(grid.DigCost, Is.EqualTo(100d));
            Assert.That(grid.CanDig(new Cell(-2, 1)), Is.True);
            Assert.That(grid.CanDig(new Cell(1, 1)), Is.True);
            Assert.That(grid.CanDig(new Cell(0, 2)), Is.True);
            Assert.That(grid.CanDig(new Cell(2, 1)), Is.False);
            Assert.That(grid.CanDig(new Cell(-3, 1)), Is.False);
            Assert.That(grid.Frontier().Count(), Is.EqualTo(4));

            while (grid.Frontier().Any())
            {
                grid.Dig(grid.Frontier().First());
            }

            Assert.That(grid.Cells.Count, Is.EqualTo(2 + 4 * 3));
            Assert.That(grid.CanDig(new Cell(0, 4)), Is.False);
            Assert.That(grid.DigCost, Is.EqualTo(100d * System.Math.Pow(1.3d, 10)).Within(1e-6));
        }
    }
}
