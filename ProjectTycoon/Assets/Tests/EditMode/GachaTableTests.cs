using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.1 뽑기 확률 표
    public sealed class GachaTableTests
    {
        [TestCase("a01", 0.175d)]
        [TestCase("a05", 0.09d)]
        [TestCase("a08", 0.03d)]
        public void ProbabilityOf_ByAnimal_MatchesTable(string animalId, double expected)
        {
            GachaTable table = new GachaTable(TestTables.Build());

            Assert.That(table.ProbabilityOf(animalId), Is.EqualTo(expected).Within(1e-9d));
        }

        [TestCase("common", 0.70d)]
        [TestCase("rare", 0.27d)]
        [TestCase("legendary", 0.03d)]
        public void GradeProbability_ByGrade_MatchesTable(string gradeId, double expected)
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

        [TestCase(0d, "a01")]
        [TestCase(0.1749d, "a01")]
        [TestCase(0.1751d, "a02")]
        [TestCase(0.9699d, "a07")]
        [TestCase(0.9701d, "a08")]
        [TestCase(0.9999d, "a08")]
        public void Pick_ByRoll_FollowsCumulativeRanges(double roll, string expectedAnimalId)
        {
            GachaTable table = new GachaTable(TestTables.Build());

            Assert.That(table.Pick(roll).Id, Is.EqualTo(expectedAnimalId));
        }
    }
}
