using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 21 → 설계 38: 곳 하나의 점원 그림(빵집 · 농장이 하나씩 든다, 똥의 PoopViews처럼). 손님과 같은 프리팹(외형은 VisitorTable clerk 행).
    // 고용되면 만들고 이름을 말하며, 자리에 처음 닿으면 사물이 튄다(bounce가 있으면). 든 빵 · 월급 코인 · 대화 줄 · 구멍으로 사라지면 지운다.
    // 외출 중(Away)엔 광장이 그림을 세우므로 여기선 띄우지 않는다. 코루틴은 곳 화면(host) 밑에서 돈다
    public sealed class ClerkViews : IDisposable
    {
        private const float k_HelloSeconds = 1.2f;
        private const string k_CoinKey = "coin_popup";

        private readonly MonoBehaviour m_host;
        private readonly WombatArea m_area;
        private readonly VisitorView m_prefab;
        private readonly TableSet m_tables;
        private readonly FrameCache m_frames;
        private readonly Transform m_origin;
        private readonly Action<Interactable> m_bounce;
        private readonly Dictionary<Clerk, VisitorView> m_units = new Dictionary<Clerk, VisitorView>();
        private readonly Dictionary<Clerk, Action<Interactable>> m_hands = new Dictionary<Clerk, Action<Interactable>>();
        private readonly IDisposable[] m_subscriptions;

        // origin: 곳 화면(그림 좌표의 원점이자 부모). bounce: 점원이 자리에 처음 닿을 때 그 사물을 튀긴다(없으면 null)
        public ClerkViews(MonoBehaviour host, WombatArea area, EventBus bus, VisitorView prefab, TableSet tables, FrameCache frames, Transform origin, Action<Interactable> bounce)
        {
            m_host = host;
            m_area = area;
            m_prefab = prefab;
            m_tables = tables;
            m_frames = frames;
            m_origin = origin;
            m_bounce = bounce;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.ClerkHired>(Bus_ClerkHired),
                bus.Subscribe<Events.ClerkLeft>(Bus_ClerkLeft),
                bus.Subscribe<Events.ClerkPaid>(Bus_ClerkPaid),
                bus.Subscribe<Events.DialogueLine>(Bus_DialogueLine),
            };

            // 설계 43: 불러온 점원(인사 · 튐 없이)
            foreach (Clerk clerk in area.Clerks)
            {
                AddUnit(clerk);
            }
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }

            foreach (KeyValuePair<Clerk, Action<Interactable>> pair in m_hands)
            {
                pair.Key.Worker.Hands.Changed -= pair.Value;
            }
        }

        private void Bus_ClerkHired(Events.ClerkHired e)
        {
            Clerk clerk = e.Clerk;

            if (clerk.Home != m_area)
            {
                return;
            }

            // 굴에서 나오면서 이름을 말한다(카메라가 당겨 비추는 동안)
            AddUnit(clerk).Say(clerk.Name, k_HelloSeconds);

            if (m_bounce != null)
            {
                m_host.StartCoroutine(BounceOnArrive(clerk));
            }
        }

        private VisitorView AddUnit(Clerk clerk)
        {
            VisitorView unit = UnityEngine.Object.Instantiate(m_prefab, m_origin);
            unit.Initialize(clerk, m_frames, m_origin);
            // 재료가 모자라 기다리는 오븐 점원은 「밀 모자라요」(기다림 말풍선만으로는 왜 멈췄는지 모른다)
            unit.SayWhile(() => clerk.Missing != null && clerk.Bubble.Id == BubbleTable.k_Wait ? m_tables.Format("clerk_missing", clerk.Missing.Name) : null);
            m_units[clerk] = unit;
            Action<Interactable> handler = _ => unit.ShowCarry(clerk.Worker.Hands.Bread != null ? m_frames.Get(clerk.Worker.Hands.Bread.Sprite)[0] : null);
            clerk.Worker.Hands.Changed += handler;
            m_hands[clerk] = handler;
            return unit;
        }

        // 고용되어 자리로 가는 중: 처음 닿으면 사물이 튄다(그 전에 그만두면 그만)
        private IEnumerator BounceOnArrive(Clerk clerk)
        {
            while (m_units.ContainsKey(clerk) && !clerk.Working)
            {
                yield return null;
            }

            if (m_units.ContainsKey(clerk))
            {
                m_bounce(clerk.Thing);
            }
        }

        private void Bus_ClerkPaid(Events.ClerkPaid e)
        {
            if (!e.Clerk.Away && m_units.TryGetValue(e.Clerk, out VisitorView unit))
            {
                unit.PopCoin(m_tables.Format(k_CoinKey, e.Wage.ToString()));
            }
        }

        // 설계 22: 대화 줄을 말하는 점원의 머리 위에(외출 중인 점원의 줄은 광장이 띄운다)
        private void Bus_DialogueLine(Events.DialogueLine e)
        {
            if (e.SpeakerObject is Clerk clerk && !clerk.Away && m_units.TryGetValue(clerk, out VisitorView unit))
            {
                unit.Say(m_tables.Text(e.TextId), (float)e.Seconds);
            }
        }

        private void Bus_ClerkLeft(Events.ClerkLeft e)
        {
            if (!m_units.TryGetValue(e.Clerk, out VisitorView unit))
            {
                return;
            }

            e.Clerk.Worker.Hands.Changed -= m_hands[e.Clerk];
            m_hands.Remove(e.Clerk);
            UnityEngine.Object.Destroy(unit.gameObject);
            m_units.Remove(e.Clerk);
        }
    }
}
