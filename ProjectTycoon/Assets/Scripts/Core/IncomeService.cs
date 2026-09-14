namespace ZooTycoon.Core
{
    public sealed class IncomeService
    {
        private readonly ZooState m_state;
        private readonly GameTables m_tables;
        private readonly ZooLevelService m_zooLevelService;

        public IncomeService(ZooState state, GameTables tables, ZooLevelService zooLevelService)
        {
            m_state = state;
            m_tables = tables;
            m_zooLevelService = zooLevelService;
        }

        // 기획서 3장 루프 3·5: 초당 수입 × 경과 초를 적립하고 동물원 레벨을 판정한다
        public void Tick(double deltaSeconds)
        {
            m_state.AddCoins(IncomeCalculator.TotalIncomePerSecond(m_state, m_tables) * deltaSeconds);
            m_zooLevelService.Refresh();
        }
    }
}
