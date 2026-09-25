using System.Linq;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 굴 격자 설계 v0.5 검증 1: 시작 8칸, 파기 규칙, 비용(값을 치르는 건 파기 행동 — BakeryAreaTests, 길 찾기는 BurrowNavTests)
    public sealed class BurrowGridTests
    {
        private EventBus m_bus;

        private BurrowGrid Create()
        {
            TableSet tables = TestTables.Load();
            m_bus = new EventBus();
            return new BurrowGrid(tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery), m_bus);
        }

        [Test]
        public void Start_HasEightCellsInTwoColumns()
        {
            BurrowGrid grid = Create();

            Assert.That(grid.Cells.Count, Is.EqualTo(8));
            Assert.That(grid.Contains(new Cell(-1, 0)), Is.True);
            Assert.That(grid.Contains(new Cell(0, 3)), Is.True);
            Assert.That(grid.HasSlot(new Cell(0, 1)), Is.True);
            Assert.That(grid.HasSlot(new Cell(0, 2)), Is.False);
            Assert.That(grid.HasSlot(new Cell(0, 0)), Is.False);
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
    }
}
