using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 11: PlazaArea 손님 사건 → 손님 개체 생성·삭제. 그림은 빵집 손님과 같은 VisitorView 프리팹이고, 걷기·판단은 Core가 한다.
    // 설계 22: 외출한 점원의 광장 그림도 같은 개체. 그 점원의 대화 줄은 광장 그림 위에, 웜뱃 줄은 광장 웜뱃 위에
    public sealed class PlazaVisitorSpawner : MonoBehaviour
    {
        [SerializeField] private VisitorView m_prefab;

        private readonly Dictionary<PlazaVisitor, VisitorView> m_units = new Dictionary<PlazaVisitor, VisitorView>();
        private PlazaArea m_plaza;
        private PlazaView m_view;
        private TableSet m_tables;
        private FrameCache m_frames;
        private IDisposable[] m_subscriptions;

        public void Initialize(PlazaArea plaza, PlazaView view, EventBus bus, TableSet tables, FrameCache frames)
        {
            m_plaza = plaza;
            m_view = view;
            m_tables = tables;
            m_frames = frames;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.PlazaVisitorArrived>(Bus_VisitorArrived),
                bus.Subscribe<Events.PlazaVisitorLeft>(Bus_VisitorLeft),
                bus.Subscribe<Events.DialogueLine>(Bus_DialogueLine),
            };
        }

        private void Bus_DialogueLine(Events.DialogueLine e)
        {
            string text = m_tables.Text(e.TextId);

            if (e.SpeakerObject is Clerk clerk && clerk.Away)
            {
                foreach (KeyValuePair<PlazaVisitor, VisitorView> pair in m_units)
                {
                    if (pair.Key.Clerk == clerk)
                    {
                        pair.Value.Say(text, (float)e.Seconds);
                    }
                }
            }
            else if (e.SpeakerObject is Wombat && m_plaza.WombatPresent)
            {
                m_view.WombatView.Say(text, (float)e.Seconds);
            }
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
    }
}
