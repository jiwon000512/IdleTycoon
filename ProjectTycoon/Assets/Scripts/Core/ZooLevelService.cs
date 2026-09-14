using System;

namespace ZooTycoon.Core
{
    public sealed class ZooLevelService
    {
        private readonly GameTables m_tables;
        private readonly ZooState m_state;
        private int m_level;

        public int Level => m_level;

        // zoo_levels는 검증기가 level == 인덱스 + 1을 보장한다
        public double? NextThreshold =>
            m_level < m_tables.ZooLevels.Count ? m_tables.ZooLevels[m_level].RequiredTotalCoins : (double?)null;

        public event Action<int> ZooLevelReached;

        public ZooLevelService(GameTables tables, ZooState state)
        {
            m_tables = tables;
            m_state = state;
            m_level = CalculateLevel(state.TotalCoinsEarned);
        }

        // 기획서 6.4: 누적 획득 코인이 동물원 레벨을 올린다
        public void Refresh()
        {
            int level = CalculateLevel(m_state.TotalCoinsEarned);

            while (m_level < level)
            {
                m_level++;
                OnZooLevelReached(m_level);
            }
        }

        private void OnZooLevelReached(int level)
        {
            ZooLevelReached?.Invoke(level);
        }

        private int CalculateLevel(double totalCoinsEarned)
        {
            int level = m_tables.ZooLevels[0].Level;

            for (int i = 0; i < m_tables.ZooLevels.Count; i++)
            {
                if (totalCoinsEarned >= m_tables.ZooLevels[i].RequiredTotalCoins)
                {
                    level = m_tables.ZooLevels[i].Level;
                }
            }

            return level;
        }
    }
}
