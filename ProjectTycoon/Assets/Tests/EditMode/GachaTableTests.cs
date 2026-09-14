using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.1(v0.11): 웜뱃 1종. 동물이 있는 등급만 확률에 들어간다
    public sealed class GachaTableTests
    {
        [Test]
        public void ProbabilityOf_OnlyAnimal_IsOne()
        {
            GachaTable table = new GachaTable(TestTables.Build());

            Assert.That(table.ProbabilityOf("a01"), Is.EqualTo(1d).Within(1e-9d));
        }

        [TestCase("common", 1d)]
        [TestCase("rare", 0d)]
        [TestCase("legendary", 0d)]
        public void GradeProbability_OnlyGradesWithAnimals_ShareOne(string gradeId, double expected)
        {
            GachaTable table = new GachaTable(TestTables.Build());

            Assert.That(table.GradeProbability(gradeId), Is.EqualTo(expected).Within(1e-9d));
        }

        [Test]
        public void ProbabilityOf_AllAnimals_SumToOne()
        {
            GameTables tables = TestTables.Build();
            GachaTable table = new GachaTable(tables);
            double sum = 0d;

            foreach (AnimalRecord animal in tables.Animals)
            {
                sum += table.ProbabilityOf(animal.Id);
            }

            Assert.That(sum, Is.EqualTo(1d).Within(1e-9d));
        }

        [TestCase(0d)]
        [TestCase(0.5d)]
        [TestCase(0.9999d)]
        public void Pick_AnyRoll_ReturnsOnlyAnimal(double roll)
        {
            GachaTable table = new GachaTable(TestTables.Build());

            Assert.That(table.Pick(roll).Id, Is.EqualTo("a01"));
        }
    }
}
