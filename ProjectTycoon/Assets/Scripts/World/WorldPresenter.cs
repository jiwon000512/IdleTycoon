using System;
using System.Collections.Generic;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    public sealed class WorldPresenter : IDisposable
    {
        private readonly WorldView m_view;
        private readonly ZooState m_state;
        private readonly IncomeService m_income;
        private readonly GameTables m_tables;
        private readonly Dictionary<string, int> m_spawnedByAnimal = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> m_shownFacilities = new HashSet<string>(StringComparer.Ordinal);

        public WorldPresenter(WorldView view, ZooState state, IncomeService income, GameTables tables)
        {
            m_view = view;
            m_state = state;
            m_income = income;
            m_tables = tables;

            m_view.Visitors.Initialize(tables.Visitors);
            m_state.AnimalsChanged += State_AnimalsChanged;
            m_state.FacilitiesChanged += State_FacilitiesChanged;
            m_income.IncomeChanged += Income_IncomeChanged;
            SyncAnimals();
            SyncFacilities();
            SyncVisitors();
        }

        public void Dispose()
        {
            m_state.AnimalsChanged -= State_AnimalsChanged;
            m_state.FacilitiesChanged -= State_FacilitiesChanged;
            m_income.IncomeChanged -= Income_IncomeChanged;
        }

        private void State_AnimalsChanged()
        {
            SyncAnimals();
        }

        private void State_FacilitiesChanged()
        {
            SyncFacilities();
        }

        private void Income_IncomeChanged()
        {
            SyncVisitors();
        }

        // 설계 03 P7: 종별 마리 수만큼 개체가 있도록 부족분을 스폰한다
        private void SyncAnimals()
        {
            for (int i = 0; i < m_state.OwnedAnimals.Count; i++)
            {
                OwnedAnimal owned = m_state.OwnedAnimals[i];
                m_spawnedByAnimal.TryGetValue(owned.AnimalId, out int spawned);

                if (spawned >= owned.Count)
                {
                    continue;
                }

                AnimalRecord record = m_tables.GetAnimal(owned.AnimalId);

                for (int n = spawned; n < owned.Count; n++)
                {
                    m_view.SpawnAnimal(record);
                }

                m_spawnedByAnimal[owned.AnimalId] = owned.Count;
            }
        }

        // 설계 07 P6: 1단계 이상인 시설을 한 번씩 세운다
        private void SyncFacilities()
        {
            for (int i = 0; i < m_tables.Facilities.Count; i++)
            {
                FacilityRecord facility = m_tables.Facilities[i];

                if (m_state.GetFacilityStage(facility.Id) > 0 && m_shownFacilities.Add(facility.Id))
                {
                    m_view.ShowFacility(facility);
                }
            }
        }

        // 설계 06 P1·P9: 관광객 수는 초당 수입에서 계산한다
        private void SyncVisitors()
        {
            m_view.Visitors.TargetCount = VisitorCalculator.Count(m_income.IncomePerSecond, m_tables.Config.Visitors);
        }
    }
}
