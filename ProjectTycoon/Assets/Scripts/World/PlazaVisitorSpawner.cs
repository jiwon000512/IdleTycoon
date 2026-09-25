using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Events;
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
        private IDisposable[] m_subscriptions;

        public void Initialize(PlazaArea plaza, EventBus bus, TableSet tables, FrameCache frames)
        {
            m_plaza = plaza;
            m_tables = tables;
            m_frames = frames;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.PlazaVisitorArrived>(Bus_VisitorArrived),
                bus.Subscribe<Events.PlazaVisitorLeft>(Bus_VisitorLeft),
                bus.Subscribe<Events.PlazaVisitorEmoted>(Bus_VisitorEmoted),
            };
        }

        private void OnDestroy()
        {
            if (m_subscriptions != null)
            {
                foreach (IDisposable subscription in m_subscriptions)
                {
                    subscription.Dispose();
                }
            }
        }

        private void Bus_VisitorArrived(Events.PlazaVisitorArrived e)
        {
            if (e.Visitor.Plaza != m_plaza)
            {
                return;
            }

            VisitorView unit = Instantiate(m_prefab, transform);
            unit.Initialize(e.Visitor, m_frames, transform);
            m_units[e.Visitor] = unit;
        }

        private void Bus_VisitorLeft(Events.PlazaVisitorLeft e)
        {
            if (e.Visitor.Plaza == m_plaza)
            {
                Destroy(m_units[e.Visitor].gameObject);
                m_units.Remove(e.Visitor);
            }
        }

        private void Bus_VisitorEmoted(Events.PlazaVisitorEmoted e)
        {
            if (e.Visitor.Plaza == m_plaza)
            {
                m_units[e.Visitor].Emote(m_tables.Text(k_HappyKey));
            }
        }
    }
}
