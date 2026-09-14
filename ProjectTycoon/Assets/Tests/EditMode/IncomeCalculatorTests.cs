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
        public void AnimalIncome_ByLevel_MatchesMultiplier(int level, double expectedMultiplier)
        {
            double income = IncomeCalculator.AnimalIncome(1d, level, 0.5d);

            Assert.That(income, Is.EqualTo(expectedMultiplier).Within(1e-9d));
        }

        [Test]
        public void AnimalIncome_UsesBaseIncome()
        {
            double income = IncomeCalculator.AnimalIncome(30d, 10, 0.5d);

            Assert.That(income, Is.EqualTo(165d).Within(1e-9d));
        }

        [Test]
        public void PromotionMultiplier_AtStageZero_IsOne()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);

            Assert.That(IncomeCalculator.PromotionMultiplier(state, tables), Is.EqualTo(1d));
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
            state.AddAnimal("a05");
            state.LevelUpAnimal("a05");
            state.LevelUpAnimal("a05");

            double income = IncomeCalculator.TotalIncomePerSecond(state, tables);

            // 토끼 1마리 = 1, 여우 3마리 = 10 → (1 + 10) × 홍보 배수 1
            Assert.That(income, Is.EqualTo(11d).Within(1e-9d));
        }
    }
}
