using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Audio;
using GameKit.Events;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 10·23·25: 월드 사건 → 효과음 이름(SoundTable). 소리의 음량·간격·연속 피치는 표가, 재생은 GameKit SoundManager가 맡는다.
    // 2026-09-30 사용자: 곳에 매인 소리(사물 · 파기 · 밭 · 똥 · 점원 수다)는 웜뱃이 있는 곳의 것만 낸다. 곳과 무관한 소리(고용 · 해고 · 월급날 · 이동)는 늘.
    // WorldManager가 붙인다
    public sealed class WorldSound : MonoBehaviour
    {
        private readonly Dictionary<OvenInteractable, (bool Baking, int Ready)> m_ovens = new Dictionary<OvenInteractable, (bool, int)>();
        private readonly Dictionary<ShelfInteractable, int> m_shelves = new Dictionary<ShelfInteractable, int>();
        // 설계 25: 밭이 빈 밭이었나(심을 때만 소리)
        private readonly Dictionary<PlotInteractable, bool> m_plots = new Dictionary<PlotInteractable, bool>();
        private Mall m_mall;
        private IDisposable[] m_subscriptions;
        private int m_dugFrame = -1;

        public void Initialize(Mall mall, EventBus bus)
        {
            m_mall = mall;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.BakeryVisitorPaid>(_ => PlayIn(m_mall.Bakery, SoundTable.k_Pay)),
                bus.Subscribe<Events.BakeryVisitorPicked>(_ => PlayIn(m_mall.Bakery, SoundTable.k_Pick)),
                bus.Subscribe<Events.ThingChanged>(Bus_ThingChanged),
                bus.Subscribe<Events.Dug>(Bus_Dug),
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.Upgraded>(e => PlayIn(e.Area, SoundTable.k_Upgrade)),
                bus.Subscribe<Events.ClerkHired>(_ => Play(SoundTable.k_ClerkHired)),
                bus.Subscribe<Events.ClerkFired>(_ => Play(SoundTable.k_ClerkFired)),
                bus.Subscribe<Events.ClerkWoke>(e => PlayIn(e.Clerk.Bakery, SoundTable.k_Wake)),
                bus.Subscribe<Events.Payday>(_ => Play(SoundTable.k_Payday)),
                bus.Subscribe<Events.AreaChanged>(_ => Play(SoundTable.k_Passage)),
                bus.Subscribe<Events.DialogueLine>(e => PlayIn((e.SpeakerObject as Clerk)?.Bakery, SoundTable.k_Say)),
                bus.Subscribe<Events.PoopDropped>(e => PlayIn(e.Poop.Area, SoundTable.k_Poop)),
                bus.Subscribe<Events.PoopCleaned>(e => PlayIn(e.Poop.Area, SoundTable.k_Clean)),
                bus.Subscribe<Events.Harvested>(e => PlayIn(e.Plot.Area, SoundTable.k_Harvest)),
                // 설계 27: 갈기는 파기 소리(더미)
                bus.Subscribe<Events.Tilled>(e => PlayIn(e.Plot.Area, SoundTable.k_Dig)),
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

        private static void Play(string id)
        {
            SoundManager.Instance.Play(id);
        }

        // 그 곳에 웜뱃이 있을 때만. area가 null이면 곳과 무관한 소리
        private void PlayIn(WombatArea area, string id)
        {
            if (area == null || area == m_mall.Active)
            {
                Play(id);
            }
        }

        // 오븐: 굽기 시작 · 다 구운 빵이 없다가 생김 · 꺼냄. 진열대: 빵이 늘어남. 상태는 곳 밖에서도 따라간다(돌아왔을 때 헛소리가 안 나게)
        private void Bus_ThingChanged(Events.ThingChanged e)
        {
            if (e.Thing is OvenInteractable oven)
            {
                m_ovens.TryGetValue(oven, out (bool Baking, int Ready) was);
                bool baking = !oven.IsEmpty;

                if (oven.Ready > 0 && was.Ready == 0)
                {
                    PlayIn(oven.Area, SoundTable.k_OvenDone);
                }
                else if (oven.Ready < was.Ready)
                {
                    PlayIn(oven.Area, SoundTable.k_TakeOut);
                }
                else if (baking && !was.Baking)
                {
                    PlayIn(oven.Area, SoundTable.k_BakeStart);
                }

                m_ovens[oven] = (baking, oven.Ready);
            }
            else if (e.Thing is PlotInteractable plot)
            {
                // 처음 보는 밭은 빈 밭(밭의 첫 사건은 심기다)
                bool wasEmpty = !m_plots.TryGetValue(plot, out bool empty) || empty;

                if (wasEmpty && !plot.IsEmpty)
                {
                    PlayIn(plot.Area, SoundTable.k_Plant);
                }

                m_plots[plot] = plot.IsEmpty;
            }
            else if (e.Thing is ShelfInteractable shelf)
            {
                m_shelves.TryGetValue(shelf, out int was);

                if (shelf.Stock > was)
                {
                    PlayIn(shelf.Area, SoundTable.k_Put);
                }

                m_shelves[shelf] = shelf.Stock;
            }
        }

        private void Bus_Dug(Events.Dug e)
        {
            m_dugFrame = Time.frameCount;
            PlayIn(e.Grid == m_mall.Farm.Grid ? m_mall.Farm : m_mall.Bakery, SoundTable.k_Dig);
        }

        // 놓기·옮기기·치우기. 파기도 배치를 바꾸므로 같은 프레임의 파기는 뺀다
        private void Bus_LayoutChanged(Events.LayoutChanged e)
        {
            if (Time.frameCount != m_dugFrame)
            {
                PlayIn(e.Area, SoundTable.k_Place);
            }
        }
    }
}
