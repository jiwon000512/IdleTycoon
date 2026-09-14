using System;

namespace ZooTycoon.Core
{
    public sealed class GachaService
    {
        private readonly GameTables m_tables;
        private readonly ZooState m_state;
        private readonly IRandom m_random;

        public GachaTable Table { get; }
        public double CurrentCost => GachaCostCalculator.Cost(m_tables.Config.Gacha, m_state.PullCount);
        public bool CanPull => m_state.Coins >= CurrentCost;

        public event Action<PullResult> AnimalPulled;

        public GachaService(GameTables tables, ZooState state, IRandom random)
        {
            m_tables = tables;
            m_state = state;
            m_random = random;
            Table = new GachaTable(tables);
        }

        // 기획서 3장: 코인을 내고 1마리. 새 종은 등장, 중복은 한 마리 추가
        public bool TryPull(out PullResult result)
        {
            double cost = CurrentCost;

            if (!m_state.TrySpendCoins(cost))
            {
                result = null;
                return false;
            }

            m_state.RecordPull();
            AnimalRecord animal = Table.Pick(m_random.NextDouble());
            PullOutcome outcome;
            int level;

            if (m_state.Owns(animal.Id))
            {
                level = m_state.LevelUpAnimal(animal.Id);
                outcome = PullOutcome.LevelUp;
            }
            else
            {
                m_state.AddAnimal(animal.Id);
                outcome = PullOutcome.Placed;
                level = 1;
            }

            result = new PullResult(animal, outcome, level, cost);
            OnAnimalPulled(result);
            return true;
        }

        private void OnAnimalPulled(PullResult result)
        {
            AnimalPulled?.Invoke(result);
        }
    }
}
