using System;

namespace ZooTycoon.Core
{
    // 초당 수입의 단일 소유자. 입력(동물, 시설 단계)이 바뀔 때만 다시 계산하고 IncomeChanged로 알린다
    public sealed class IncomeService
    {
        private readonly ZooState m_state;
        private readonly GameTables m_tables;
        private readonly ZooLevelService m_zooLevelService;

        public double IncomePerSecond { get; private set; }

        public event Action IncomeChanged;

        public IncomeService(ZooState state, GameTables tables, ZooLevelService zooLevelService)
        {
            m_state = state;
            m_tables = tables;
            m_zooLevelService = zooLevelService;
            m_state.AnimalsChanged += State_AnimalsChanged;
            m_state.FacilitiesChanged += State_FacilitiesChanged;
            IncomePerSecond = IncomeCalculator.TotalIncomePerSecond(m_state, m_tables);
        }

        // 기획서 3장 루프 3·5: 초당 수입 × 경과 초를 적립하고 동물원 레벨을 판정한다
        public void Tick(double deltaSeconds)
        {
            m_state.AddCoins(IncomePerSecond * deltaSeconds);
            m_zooLevelService.Refresh();
        }

        private void State_AnimalsChanged()
        {
            Recalculate();
        }

        private void State_FacilitiesChanged()
        {
            Recalculate();
        }

        private void Recalculate()
        {
            IncomePerSecond = IncomeCalculator.TotalIncomePerSecond(m_state, m_tables);
            IncomeChanged?.Invoke();
        }
    }
}
