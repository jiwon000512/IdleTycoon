using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 06 P6: 살아 있는 관광객이 TargetCount보다 적으면 간격을 두고 한 명씩 채운다
    public sealed class VisitorSpawner : MonoBehaviour
    {
        [SerializeField] private Visitor m_prefab;
        [Tooltip("관광객을 한 명씩 추가하는 간격(초)")]
        [SerializeField] private float m_spawnInterval = 1.5f;

        private readonly List<Visitor> m_alive = new List<Visitor>();
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

        // 설계 06 P4·07 P8: 존은 첫 존(z01) 하나(08까지). 동물이 있으면 구경하고 없으면 지나간다(Visitor가 판단)
        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장)
        private void Spawn()
        {
            Visitor visitor = Instantiate(m_prefab, transform);
            WorldManager world = WorldManager.Instance;
            visitor.Initialize(PickRecord(), world.Zone, world.Frames);
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

        private void Visitor_Exited(Visitor visitor)
        {
            visitor.Exited -= Visitor_Exited;
            m_alive.Remove(visitor);
        }
    }
}
