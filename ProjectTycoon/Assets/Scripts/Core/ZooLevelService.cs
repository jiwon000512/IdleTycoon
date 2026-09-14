using System;

namespace ZooTycoon.Core
{
    public sealed class ZooLevelService
    {
        private readonly GameTables m_tables;
        private readonly ZooState m_state;
        private int m_level;

        public int Level => m_level;
        public int CageCount => RecordOf(m_level).CageCount;

        public bool IsPromotionUnlocked
        {
            get
            {
                for (int i = 0; i < m_tables.ZooLevels.Count; i++)
                {
                    ZooLevelRecord record = m_tables.ZooLevels[i];
                    if (record.UnlocksPromotion && record.Level <= m_level)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public double? NextThreshold
        {
            get
            {
                for (int i = 0; i < m_tables.ZooLevels.Count; i++)
                {
                    if (m_tables.ZooLevels[i].Level == m_level + 1)
                    {
                        return m_tables.ZooLevels[i].RequiredTotalCoins;
                    }
                }

                return null;
            }
        }

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

        private ZooLevelRecord RecordOf(int level)
        {
            for (int i = 0; i < m_tables.ZooLevels.Count; i++)
            {
                if (m_tables.ZooLevels[i].Level == level)
                {
                    return m_tables.ZooLevels[i];
                }
            }

            throw new InvalidOperationException($"동물원 레벨 {level}이 zoo_levels.json에 없다.");
        }
    }
}
