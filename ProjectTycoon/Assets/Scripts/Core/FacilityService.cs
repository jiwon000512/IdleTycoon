using System;

namespace ZooTycoon.Core
{
    // 기획서 6.4 시설(설계 07 P4): 동물원 레벨로 해금, 코인으로 단계를 올린다. 배수 계산은 IncomeCalculator
    public sealed class FacilityService
    {
        private readonly ZooState m_state;
        private readonly ZooLevelService m_zooLevelService;

        public FacilityService(ZooState state, ZooLevelService zooLevelService)
        {
            m_state = state;
            m_zooLevelService = zooLevelService;
        }

        // 기획서 6.4: 비용 = baseCost × costGrowth^단계
        public double Cost(FacilityRecord facility)
        {
            return facility.BaseCost * Math.Pow(facility.CostGrowth, m_state.GetFacilityStage(facility.Id));
        }

        public bool IsUnlocked(FacilityRecord facility)
        {
            return m_zooLevelService.Level >= facility.UnlockLevel;
        }

        public bool IsMaxed(FacilityRecord facility)
        {
            return m_state.GetFacilityStage(facility.Id) >= facility.MaxStage;
        }

        public bool CanUpgrade(FacilityRecord facility)
        {
            return IsUnlocked(facility) && !IsMaxed(facility) && m_state.Coins >= Cost(facility);
        }

        public bool TryUpgrade(FacilityRecord facility)
        {
            if (!IsUnlocked(facility) || IsMaxed(facility) || !m_state.TrySpendCoins(Cost(facility)))
            {
                return false;
            }

            m_state.UpgradeFacility(facility.Id);
            return true;
        }
    }
}
