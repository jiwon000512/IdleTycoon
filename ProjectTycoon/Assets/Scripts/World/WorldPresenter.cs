using System;
using System.Collections.Generic;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    public sealed class WorldPresenter : IDisposable
    {
        private readonly WorldView m_view;
        private readonly ZooState m_state;
        private readonly GameTables m_tables;
        private readonly Dictionary<string, int> m_spawnedByAnimal = new Dictionary<string, int>(StringComparer.Ordinal);

        public WorldPresenter(WorldView view, ZooState state, GameTables tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;

            m_state.AnimalsChanged += State_AnimalsChanged;
            SyncAnimals();
        }

        public void Dispose()
        {
            m_state.AnimalsChanged -= State_AnimalsChanged;
        }

        private void State_AnimalsChanged()
        {
            SyncAnimals();
        }

        // 설계 03 P7: 종별 마리 수(Level)만큼 개체가 있도록 부족분을 스폰한다
        // 설계 03 A2(임시): 종은 보유 순서대로 우리에 돌아가며 들어간다. 우리 배정 규칙은 06에서 정한다
        private void SyncAnimals()
        {
            for (int i = 0; i < m_state.OwnedAnimals.Count; i++)
            {
                OwnedAnimal owned = m_state.OwnedAnimals[i];
                m_spawnedByAnimal.TryGetValue(owned.AnimalId, out int spawned);

                if (spawned >= owned.Level)
                {
                    continue;
                }

                AnimalRecord record = m_tables.GetAnimal(owned.AnimalId);
                int cageIndex = i % m_view.CageCount;

                for (int n = spawned; n < owned.Level; n++)
                {
                    m_view.SpawnAnimal(record, cageIndex);
                }

                m_spawnedByAnimal[owned.AnimalId] = owned.Level;
            }
        }
    }
}
