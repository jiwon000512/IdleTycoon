using System.Linq;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 굴 격자 설계 v0.5 검증 1: 시작 8칸, 파기 규칙, 비용, 경로·거리
    public sealed class BurrowGridTests
    {
        private ZooState m_state;

        private BurrowGrid Create()
        {
            GameConfig config = TestTables.LoadConfig();
            m_state = ZooState.CreateNew(config);
            return new BurrowGrid(m_state, config.Shop);
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
        public void TryDig_ChargesGrowingCostAndRefusesTwice()
        {
            BurrowGrid grid = Create();
            m_state.AddCoins(1000d);
            double coins = m_state.Coins;

            Assert.That(grid.DigCost, Is.EqualTo(150d));
            Assert.That(grid.TryDig(new Cell(1, 1)), Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins - 150d));
            Assert.That(grid.DigCost, Is.EqualTo(210d).Within(1e-9));
            Assert.That(grid.TryDig(new Cell(1, 1)), Is.False);
            Assert.That(grid.TryDig(new Cell(2, 1)), Is.True);
            Assert.That(grid.DigCost, Is.EqualTo(294d).Within(1e-9));
        }

        [Test]
        public void TryDig_WithoutCoins_Fails()
        {
            BurrowGrid grid = Create();
            m_state.TrySpendCoins(m_state.Coins);

            Assert.That(grid.TryDig(new Cell(1, 1)), Is.False);
            Assert.That(grid.Cells.Count, Is.EqualTo(8));
        }

        [Test]
        public void Path_IsShortestAndDistanceUsesCellSizes()
        {
            BurrowGrid grid = Create();
            m_state.AddCoins(1000d);
            grid.TryDig(new Cell(-2, 3));

            Assert.That(grid.Path(new Cell(0, 0), new Cell(-2, 3)).Count, Is.EqualTo(6));
            Assert.That(grid.Distance(new Cell(0, 2), new Cell(-1, 3)), Is.EqualTo(2.4d + 3.375d).Within(1e-9));
            Assert.That(grid.Distance(new Cell(-1, 2), new Cell(-2, 3)), Is.EqualTo(2.4d + 3.375d).Within(1e-9));
            Assert.That(grid.EntranceNear(new Cell(-2, 3)), Is.EqualTo(new Cell(-1, 0)));
            Assert.That(grid.CounterNear(new Cell(1, 1)), Is.EqualTo(new Cell(0, 2)));
        }
    }
}
