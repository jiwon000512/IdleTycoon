using System.Linq;
using NUnit.Framework;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 굴 격자 설계 v0.5 검증 1: 시작 8칸, 파기 규칙, 비용(길 찾기는 BurrowNavTests)
    public sealed class BurrowGridTests
    {
        private ZooState m_state;

        private BurrowGrid Create()
        {
            TableSet tables = TestTables.Load();
            m_state = ZooState.CreateNew(tables);
            return new BurrowGrid(m_state, tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery));
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
    }
}
