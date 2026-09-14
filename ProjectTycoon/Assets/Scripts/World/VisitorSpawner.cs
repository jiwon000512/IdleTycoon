using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 06 P6: 살아 있는 관광객이 TargetCount보다 적으면 간격을 두고 한 명씩 채운다
    public sealed class VisitorSpawner : MonoBehaviour
    {
        [SerializeField] private VisitorActor m_prefab;
        [SerializeField] private WorldView m_world;
        [Tooltip("관광객을 한 명씩 추가하는 간격(초)")]
        [SerializeField] private float m_spawnInterval = 1.5f;

        private readonly List<VisitorActor> m_alive = new List<VisitorActor>();
        private IReadOnlyList<VisitorRecord> m_records;
        private int m_weightSum;
        private float m_cooldown;

        public int TargetCount { get; set; }

        public void Initialize(IReadOnlyList<VisitorRecord> records)
        {
            m_records = records;
            m_weightSum = 0;

            for (int i = 0; i < records.Count; i++)
            {
                m_weightSum += records[i].Weight;
            }
        }

        private void Update()
        {
            if (m_records == null || m_alive.Count >= TargetCount)
            {
                return;
            }

            m_cooldown -= Time.deltaTime;

            if (m_cooldown > 0f)
            {
                return;
            }

            m_cooldown = m_spawnInterval;
            Spawn();
        }

        // 설계 06 P4: 동물이 있는 우리가 있으면 그중 하나를 골라 구경하고, 없으면 아무 우리 앞을 지나간다
        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장)
        private void Spawn()
        {
            List<CageView> withAnimals = m_world.CagesWithAnimals();
            bool views = withAnimals.Count > 0;
            IReadOnlyList<CageView> candidates = views ? withAnimals : m_world.Cages;
            CageView cage = candidates[Random.Range(0, candidates.Count)];
            VisitorRecord record = PickRecord();

            VisitorActor visitor = Instantiate(m_prefab, transform);
            visitor.Initialize(
                record,
                WorldView.LoadFrames(record.IdleSheet ?? record.Sprite),
                WorldView.LoadFrames(record.MoveSheet ?? record.Sprite),
                cage,
                views,
                m_world.BillboardRotation);
            visitor.Exited += Visitor_Exited;
            m_alive.Add(visitor);
        }

        private VisitorRecord PickRecord()
        {
            int roll = Random.Range(0, m_weightSum);

            for (int i = 0; i < m_records.Count; i++)
            {
                roll -= m_records[i].Weight;

                if (roll < 0)
                {
                    return m_records[i];
                }
            }

            return m_records[m_records.Count - 1];
        }

        private void Visitor_Exited(VisitorActor visitor)
        {
            visitor.Exited -= Visitor_Exited;
            m_alive.Remove(visitor);
        }
    }
}
