using System;
using System.Collections.Generic;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 08 v0.5 · 설계 13: 빵집 업그레이드 단계·비용·효과. 비용 = baseCost × costGrowth^단계, 효과 = effectPerLevel × 단계
    public sealed class Upgrades
    {
        private readonly TableSet m_tables;
        private readonly BakeryConfigTable m_config;
        private readonly Dictionary<string, int> m_levels = new Dictionary<string, int>(StringComparer.Ordinal);

        // 오븐 외형 2단계(oven_speed.lookLevel 이상)
        public bool OvenLookUpgraded
        {
            get
            {
                ShopUpgradeTable upgrade = m_tables.Get<ShopUpgradeTable>(ShopUpgradeTable.k_OvenSpeed);
                return upgrade.LookLevel > 0 && Level(ShopUpgradeTable.k_OvenSpeed) >= upgrade.LookLevel;
            }
        }

        public Upgrades(TableSet tables, BakeryConfigTable config)
        {
            m_tables = tables;
            m_config = config;
        }

        public int Level(string upgradeId)
        {
            m_levels.TryGetValue(upgradeId, out int level);
            return level;
        }

        public double Cost(string upgradeId)
        {
            ShopUpgradeTable upgrade = m_tables.Get<ShopUpgradeTable>(upgradeId);
            return upgrade.BaseCost * Math.Pow(upgrade.CostGrowth, Level(upgradeId));
        }

        public bool IsMaxed(string upgradeId)
        {
            return Level(upgradeId) >= m_tables.Get<ShopUpgradeTable>(upgradeId).MaxLevel;
        }

        public double Effect(string upgradeId)
        {
            return m_tables.Get<ShopUpgradeTable>(upgradeId).EffectPerLevel * Level(upgradeId);
        }

        // 사물 시트 효과 전후 표시용: 그 단계일 때의 게임 값(진열대 용량·오븐 수·속도 배수)
        public double Value(string upgradeId, int level)
        {
            double effect = m_tables.Get<ShopUpgradeTable>(upgradeId).EffectPerLevel * level;

            switch (upgradeId)
            {
                case ShopUpgradeTable.k_ShelfCapacity: return m_config.ShelfCapacity + effect;
                case ShopUpgradeTable.k_OvenCount: return m_config.OvenCount + effect;
                default: return 1d + effect;
            }
        }

        public bool TryBuy(string upgradeId, ZooState wallet)
        {
            if (IsMaxed(upgradeId) || !wallet.TrySpendCoins(Cost(upgradeId)))
            {
                return false;
            }

            m_levels[upgradeId] = Level(upgradeId) + 1;
            return true;
        }
    }
}
