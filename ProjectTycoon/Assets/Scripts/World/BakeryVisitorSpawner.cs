using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.5 · 손님 동선 설계 v0.2: BakeryArea 손님 사건 → 손님 개체 생성·연출·삭제. 걷기와 판단은 Core가 하고 개체는 그 위치를 그린다
    public sealed class BakeryVisitorSpawner : MonoBehaviour
    {
        private const string k_CoinKey = "coin_popup";
        private const string k_HappyKey = "emote_happy";

        [SerializeField] private VisitorView m_prefab;

        private readonly Dictionary<BakeryVisitor, VisitorView> m_units = new Dictionary<BakeryVisitor, VisitorView>();
        private BakeryArea m_shop;
        private BakeryView m_view;
        private TableSet m_tables;
        private FrameCache m_frames;
        private IDisposable[] m_subscriptions;

        public void Initialize(BakeryArea shop, BakeryView view, EventBus bus, TableSet tables, FrameCache frames)
        {
            m_shop = shop;
            m_view = view;
            m_tables = tables;
            m_frames = frames;

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.BakeryVisitorArrived>(Bus_VisitorArrived),
                bus.Subscribe<Events.BakeryVisitorPicked>(Bus_VisitorPicked),
                bus.Subscribe<Events.BakeryVisitorPaid>(Bus_VisitorPaid),
                bus.Subscribe<Events.BakeryVisitorLeft>(Bus_VisitorLeft),
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

        // 이 빵집 손님만(설계 16: 사건은 발신자의 곳으로 거른다)
        private bool Mine(BakeryVisitor visitor)
        {
            return visitor.Bakery == m_shop;
        }

        // 설계 11: 외형은 광장에서 정해져 온다(visitor.Look)
        private void Bus_VisitorArrived(Events.BakeryVisitorArrived e)
        {
            if (!Mine(e.Visitor))
            {
                return;
            }

            VisitorView unit = Instantiate(m_prefab, transform);
            unit.Initialize(e.Visitor, m_frames, m_view.transform);
            m_units[e.Visitor] = unit;
        }

        // 빵 그림이 진열대의 빵 자리에서 손으로 날아온다
        private void Bus_VisitorPicked(Events.BakeryVisitorPicked e)
        {
            if (Mine(e.Visitor))
            {
                m_units[e.Visitor].Pick(m_frames.Get(e.Visitor.Bread.Sprite)[0], m_view.ShelfIconPosition(e.Visitor.Shelf));
            }
        }

        private void Bus_VisitorPaid(Events.BakeryVisitorPaid e)
        {
            if (Mine(e.Visitor))
            {
                string amount = m_tables.Format(k_CoinKey, e.Coins.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                m_units[e.Visitor].Pay(amount, m_tables.Text(k_HappyKey));
            }
        }

        private void Bus_VisitorLeft(Events.BakeryVisitorLeft e)
        {
            if (Mine(e.Visitor))
            {
                Destroy(m_units[e.Visitor].gameObject);
                m_units.Remove(e.Visitor);
            }
        }
    }
}
