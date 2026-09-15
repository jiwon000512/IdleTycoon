using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.2 수입 수식
    public sealed class IncomeCalculatorTests
    {
        [TestCase(1, 1.0d)]
        [TestCase(10, 5.5d)]
        [TestCase(20, 10.5d)]
        public void AnimalIncome_ByCount_MatchesMultiplier(int count, double expectedMultiplier)
        {
            double income = IncomeCalculator.AnimalIncome(1d, count, 0.5d);

            Assert.That(income, Is.EqualTo(expectedMultiplier).Within(1e-9d));
        }

        [Test]
        public void AnimalIncome_UsesBaseIncome()
        {
            double income = IncomeCalculator.AnimalIncome(30d, 10, 0.5d);

            Assert.That(income, Is.EqualTo(165d).Within(1e-9d));
        }

        [Test]
        public void FacilityMultiplier_WithNoStage_IsOne()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);

            Assert.That(IncomeCalculator.FacilityMultiplier(state, tables), Is.EqualTo(1d));
        }

        // 기획서 6.4: 홍보 10단계 = ×2.0, 벤치 5단계 = ×1.25, 둘 다면 곱 2.5
        [Test]
        public void FacilityMultiplier_MultipliesEachFacility()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);

            for (int i = 0; i < 10; i++)
            {
                state.UpgradeFacility("f01");
            }

            Assert.That(IncomeCalculator.FacilityMultiplier(state, tables), Is.EqualTo(2d).Within(1e-9d));

            for (int i = 0; i < 5; i++)
            {
                state.UpgradeFacility("f02");
            }

            Assert.That(IncomeCalculator.FacilityMultiplier(state, tables), Is.EqualTo(2.5d).Within(1e-9d));
        }

        [Test]
        public void TotalIncomePerSecond_WithNoPlacedAnimal_IsZero()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);

            Assert.That(IncomeCalculator.TotalIncomePerSecond(state, tables), Is.EqualTo(0d));
        }

        [Test]
        public void TotalIncomePerSecond_SumsOwnedAnimals()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            state.AddAnimal("a01");
            state.AddAnimal("a01");
            state.AddAnimal("a01");

            double income = IncomeCalculator.TotalIncomePerSecond(state, tables);

            // 웜뱃 3마리 = 1 × (1 + 0.5 × 2) = 2 → × 시설 배수 1
            Assert.That(income, Is.EqualTo(2d).Within(1e-9d));
        }
    }
}
