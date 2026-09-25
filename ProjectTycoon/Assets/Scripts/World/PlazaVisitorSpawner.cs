using System.Collections.Generic;
using UnityEngine;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 11: PlazaArea 손님 사건 → 손님 개체 생성·♥·삭제. 그림은 빵집 손님과 같은 VisitorView 프리팹이고, 걷기·판단은 Core가 한다
    public sealed class PlazaVisitorSpawner : MonoBehaviour
    {
        private const string k_HappyKey = "emote_happy";

        [SerializeField] private VisitorView m_prefab;

        private readonly Dictionary<PlazaVisitor, VisitorView> m_units = new Dictionary<PlazaVisitor, VisitorView>();
        private PlazaArea m_plaza;
        private TableSet m_tables;
        private FrameCache m_frames;

        public void Initialize(PlazaArea plaza, TableSet tables, FrameCache frames)
        {
            m_plaza = plaza;
            m_tables = tables;
            m_frames = frames;
            m_plaza.VisitorArrived += Plaza_VisitorArrived;
            m_plaza.VisitorRemoved += Plaza_VisitorRemoved;
            m_plaza.VisitorEmoted += Plaza_VisitorEmoted;
        }

        private void OnDestroy()
        {
            if (m_plaza != null)
            {
                m_plaza.VisitorArrived -= Plaza_VisitorArrived;
                m_plaza.VisitorRemoved -= Plaza_VisitorRemoved;
                m_plaza.VisitorEmoted -= Plaza_VisitorEmoted;
            }
        }

        private void Plaza_VisitorArrived(PlazaVisitor visitor)
        {
            VisitorView unit = Instantiate(m_prefab, transform);
            unit.Initialize(visitor, m_frames, transform);
            m_units[visitor] = unit;
        }

        private void Plaza_VisitorRemoved(PlazaVisitor visitor)
        {
            Destroy(m_units[visitor].gameObject);
            m_units.Remove(visitor);
        }

        private void Plaza_VisitorEmoted(PlazaVisitor visitor)
        {
            m_units[visitor].Emote(m_tables.Text(k_HappyKey));
        }
    }
}
