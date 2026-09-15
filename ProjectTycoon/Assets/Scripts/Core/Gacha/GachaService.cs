using System;

namespace ZooTycoon.Core
{
    public sealed class GachaService
    {
        private readonly GameTables m_tables;
        private readonly ZooState m_state;
        private readonly IRandom m_random;
        private readonly IncomeService m_income;

        public GachaTable Table { get; }
        public double CurrentCost => GachaCostCalculator.Cost(m_tables.Config.Gacha, m_state.PullCount);
        public bool CanPull => m_state.Coins >= CurrentCost;

        // 기획서 4장: 코인 부족 시 남은 시간 = 부족분 ÷ 초당 수입. 수입이 0이면 무한대
        public double SecondsUntilAffordable =>
            CanPull ? 0d : (CurrentCost - m_state.Coins) / m_income.IncomePerSecond;

        public event Action<PullResult> AnimalPulled;

        public GachaService(GameTables tables, ZooState state, IRandom random, IncomeService income)
        {
            m_tables = tables;
            m_state = state;
            m_random = random;
            m_income = income;
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
            PullOutcome outcome = m_state.Owns(animal.Id) ? PullOutcome.Duplicate : PullOutcome.NewSpecies;
            int count = m_state.AddAnimal(animal.Id);

            result = new PullResult(animal, outcome, count, cost);
            OnAnimalPulled(result);
            return true;
        }

        private void OnAnimalPulled(PullResult result)
        {
            AnimalPulled?.Invoke(result);
        }
    }
}
