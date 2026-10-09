using System.Collections.Generic;
using System.Linq;
using GameKit.Events;

namespace ZooTycoon.Core
{
    // 설계 54: 「가기」 길잡이. 대상(곳 id + 사물 종류)까지 곳 사이 통로(문 · 구멍 · 계단)를 넓이 우선으로 잇고(닫힌 문이면 그 문 앞까지)
    // 다리마다 웜뱃 시스템 이동(설계 19 Wombat.Guide). 통로 띠에 닿으면 원래처럼 Passed로 곳이 바뀌고(AreaChanged) 다음 다리를 잇는다.
    // 조이스틱을 건드리면 걷기만 멈추고, 가리키기(화살표)는 걸음이 끝날 때(Stop)까지 남는다
    public sealed class Guide
    {
        private readonly Mall m_mall;
        private readonly EventBus m_bus;
        private string m_area;
        private string m_thing;
        private string m_kind;
        private string m_param;

        public bool Active { get; private set; }
        public bool Walking { get; private set; }
        // 지금 곳에서 가리키는 것: 다른 곳으로 가는 다음 통로 또는 마지막 사물(곳에 들어가기만 하면 되는 걸음은 null)
        public Interactable Pointed { get; private set; }
        // 다음 통로 너머 마지막 곳 id(화면 끝 이름표). 지금 곳이 마지막 곳이면 null
        public string Toward { get; private set; }

        public Guide(Mall mall, EventBus bus)
        {
            m_mall = mall;
            m_bus = bus;
            bus.Subscribe<Events.AreaChanged>(_ => Bus_AreaChanged());
        }

        // 가기: 곳 · 사물 종류(thing이 null이면 곳에 들어가면 끝)로 길을 잡고 걷기 시작한다. kind · param은 그 종류 중 걸음에 맞는 것 고르기. 갈 곳이 없으면 false
        public bool TryStart(string area, string thing, string kind, string param)
        {
            m_area = area;
            m_thing = thing;
            m_kind = kind;
            m_param = param;
            Active = true;

            if (Plan(true))
            {
                return true;
            }

            Stop();
            return false;
        }

        // 걸음이 끝나거나 바뀌면 · 편집 모드에 들면 멈춘다. 걷던 웜뱃도 그 자리에 선다(다음 문을 지나가지 않게, 2026-10-09 리뷰)
        public void Stop()
        {
            if (!Active)
            {
                return;
            }

            if (Walking)
            {
                m_mall.Wombat.Stop();
            }

            Active = false;
            Walking = false;
            Pointed = null;
            Toward = null;
            m_bus.Publish(new Events.GuideChanged(this));
        }

        // 매 프레임: 시스템 이동이 끊기면(조이스틱 · 도착) 걷기만 멈춘다. 가리키던 사물이 없어졌으면(치운 똥 등) 다시 고른다
        public void Tick()
        {
            if (!Active)
            {
                return;
            }

            if (Pointed != null && !Pointed.Area.Things.Contains(Pointed))
            {
                if (!Plan(Walking))
                {
                    Stop();
                }

                return;
            }

            if (Walking && !m_mall.Wombat.Guided)
            {
                Walking = false;
                m_bus.Publish(new Events.GuideChanged(this));
            }
        }

        private void Bus_AreaChanged()
        {
            if (Active && !Plan(Walking))
            {
                Stop();
            }
        }

        // 지금 곳에서 가리킬 것을 정하고, walk면 그리로 걷게 한다
        private bool Plan(bool walk)
        {
            WombatArea here = m_mall.Active;
            List<PassageInteractable> route = Route(here, m_area);

            if (route == null)
            {
                return false;
            }

            if (route.Count > 0)
            {
                Pointed = route[0];
                Toward = m_area;
            }
            else
            {
                Pointed = m_thing == null ? null : Pick(here);
                Toward = null;

                if (m_thing != null && Pointed == null)
                {
                    return false;
                }
            }

            Walking = walk && Pointed != null && here.TryGuideTo(Pointed);
            m_bus.Publish(new Events.GuideChanged(this));
            return true;
        }

        // 그 곳의 그 종류 중 걸음에 알맞은 것(빈 밭 등), 웜뱃에서 가까운 것
        private Interactable Pick(WombatArea area)
        {
            return area.Things.Where(thing => thing.Table.Id == m_thing && GoalCounter.Suits(m_kind, m_param, thing))
                .OrderBy(thing => thing.DistanceTo(m_mall.Wombat.Mover.Position)).FirstOrDefault();
        }

        // 지금 곳에서 to까지 지나갈 통로들(같은 곳이면 빈 목록). 열린 문으로 못 가면 닫힌 문까지 넣어 찾고, 못 가면 null
        private List<PassageInteractable> Route(WombatArea from, string to)
        {
            return Search(from, to, true) ?? Search(from, to, false);
        }

        private List<PassageInteractable> Search(WombatArea from, string to, bool openOnly)
        {
            Dictionary<WombatArea, PassageInteractable> came = new Dictionary<WombatArea, PassageInteractable> { [from] = null };
            Queue<WombatArea> queue = new Queue<WombatArea>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                WombatArea area = queue.Dequeue();

                if (area.Id == to)
                {
                    List<PassageInteractable> route = new List<PassageInteractable>();

                    for (PassageInteractable passage = came[area]; passage != null; passage = came[passage.Area])
                    {
                        route.Insert(0, passage);
                    }

                    return route;
                }

                foreach (PassageInteractable passage in area.Things.OfType<PassageInteractable>())
                {
                    WombatArea next = m_mall.Areas.FirstOrDefault(a => a.Id == passage.To);

                    if (next == null || came.ContainsKey(next) || openOnly && !passage.IsOpen)
                    {
                        continue;
                    }

                    came[next] = passage;
                    queue.Enqueue(next);
                }
            }

            return null;
        }
    }
}
