using System;
using System.Collections.Generic;
using System.Linq;

namespace ZooTycoon.Core
{
    // 기획서 6.1·8장: 등급 가중치 × 등급 안 동물 가중치를 한 표로 만들어 난수 1회로 뽑는다
    public sealed class GachaTable
    {
        private readonly List<AnimalRecord> m_animals;
        private readonly double[] m_cumulative;
        private readonly Dictionary<string, double> m_probabilityByAnimal = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> m_probabilityByGrade = new Dictionary<string, double>(StringComparer.Ordinal);

        public GachaTable(GameTables tables)
        {
            // 기획서 6.1(v0.11): 동물이 있는 등급만 확률에 넣는다. 빈 등급은 0
            Dictionary<string, int> animalWeightSumByGrade = tables.Animals
                .GroupBy(a => a.Grade)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.GachaWeight), StringComparer.Ordinal);
            int gradeWeightSum = tables.Grades
                .Where(g => animalWeightSumByGrade.ContainsKey(g.Id))
                .Sum(g => g.GachaWeight);

            foreach (GradeRecord grade in tables.Grades)
            {
                m_probabilityByGrade[grade.Id] = animalWeightSumByGrade.ContainsKey(grade.Id)
                    ? (double)grade.GachaWeight / gradeWeightSum
                    : 0d;
            }

            m_animals = tables.Animals.OrderBy(a => a.SortOrder).ToList();
            m_cumulative = new double[m_animals.Count];
            double cumulative = 0d;

            for (int i = 0; i < m_animals.Count; i++)
            {
                AnimalRecord animal = m_animals[i];
                int gradeWeight = tables.GetGrade(animal.Grade).GachaWeight;
                double probability = (double)(gradeWeight * animal.GachaWeight)
                    / (gradeWeightSum * animalWeightSumByGrade[animal.Grade]);

                m_probabilityByAnimal[animal.Id] = probability;
                cumulative += probability;
                m_cumulative[i] = cumulative;
            }
        }

        public double ProbabilityOf(string animalId)
        {
            return m_probabilityByAnimal[animalId];
        }

        public double GradeProbability(string gradeId)
        {
            return m_probabilityByGrade[gradeId];
        }

        public AnimalRecord Pick(double roll)
        {
            for (int i = 0; i < m_cumulative.Length; i++)
            {
                if (roll < m_cumulative[i])
                {
                    return m_animals[i];
                }
            }

            return m_animals[m_animals.Count - 1];
        }
    }
}
