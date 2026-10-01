using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 24: 웜뱃 똥. 웜뱃이 빵집 안을 poopEvery 유닛 걸을 때마다 poopChance로 등 뒤에 하나(바닥에 poopMax까지, 다른 똥 poopGap 안이면 건너뜀).
    // 똥은 사물(치우기 버튼)이고, 손님 땅에만 둘레(poopAvoidRadius)를 건다. 점원·웜뱃은 다른 땅(WombatNav)을 걸어 밟고 지나간다
    public sealed partial class BakeryArea
    {
        // 등 뒤로 떨어지는 거리
        private const float k_PoopBehind = 0.25f;

        private readonly List<PoopInteractable> m_poops = new List<PoopInteractable>();
        private readonly List<Vector2> m_poopPoints = new List<Vector2>();
        private double m_walked;

        public IReadOnlyList<PoopInteractable> Poops => m_poops;

        // 그 자리에 똥 하나. 바닥에 poopMax개거나 다른 똥 poopGap 안이면 false
        public bool TryDropPoop(Vector2 at)
        {
            if (m_poops.Count >= m_config.PoopMax)
            {
                return false;
            }

            foreach (PoopInteractable poop in m_poops)
            {
                if (Vector2.Distance(poop.Position, at) < m_config.PoopGap)
                {
                    return false;
                }
            }

            PoopInteractable dropped = new PoopInteractable(Row(PoopInteractable.k_Id), at, this);
            m_poops.Add(dropped);
            OnPoopsChanged();
            Bus.Publish(new Events.PoopDropped(dropped));
            return true;
        }

        // 치우기 버튼: 웜뱃 range 안 똥 전부. 치운 만큼 거름(poopItem)이 창고로 간다(설계 28. 사물 밑에 깔려 사라진 똥은 주지 않는다).
        // 설계 31 거름 국자를 끼웠으면 똥 하나마다 코인
        internal void CleanAround()
        {
            int cleaned = 0;
            double coins = m_state.Relics.Value(RelicTable.k_Scoop);

            for (int i = m_poops.Count - 1; i >= 0; i--)
            {
                if (IsInRange(m_poops[i]))
                {
                    RemovePoop(i, coins);
                    cleaned++;
                }
            }

            if (coins > 0d && cleaned > 0)
            {
                m_state.AddCoins(coins * cleaned);
            }

            if (cleaned > 0)
            {
                m_state.AddItem(m_config.PoopItem, cleaned);
                OnPoopsChanged();
            }
        }

        // 서는 자리 고르기: 똥 둘레 안인가
        private bool NearPoop(Vector2 p)
        {
            foreach (PoopInteractable poop in m_poops)
            {
                if (Vector2.Distance(poop.Position, p) < m_config.PoopAvoidRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private void TickPoops()
        {
            m_walked += WombatPresent ? Wombat.Walked : 0d;

            if (m_walked < m_config.PoopEvery)
            {
                return;
            }

            m_walked -= m_config.PoopEvery;

            if (Random.NextDouble() < m_config.PoopChance)
            {
                TryDropPoop(Behind());
            }
        }

        // 웜뱃 등 뒤. 걷는 땅이 아니면 발밑
        private Vector2 Behind()
        {
            Vector2 feet = Wombat.Mover.Position;
            Vector2 behind = feet - Mover.Direction(Wombat.Mover.Facing) * k_PoopBehind;
            return Layout.WombatNav.IsWalkable(behind) ? behind : feet;
        }

        // 배치가 바뀌었다: 사물 밑에 깔린 똥은 치운 셈이고, 새 손님 땅에 둘레를 다시 건다
        private void ClearBuriedPoops()
        {
            for (int i = m_poops.Count - 1; i >= 0; i--)
            {
                foreach (IPlaced thing in PlacedThings)
                {
                    if (Placement.Rect(thing).Contains(m_poops[i].Position, 0f))
                    {
                        RemovePoop(i, 0d);
                        break;
                    }
                }
            }

            ApplyPoopObstacles();
        }

        // 사물 목록(대상 · 버튼)과 손님 땅의 둘레를 맞추고, 걷는 손님은 새 길을 찾는다(길이 없으면 포기)
        private void OnPoopsChanged()
        {
            SyncThings();
            ApplyPoopObstacles();
            RepathVisitors();
        }

        private void ApplyPoopObstacles()
        {
            m_poopPoints.Clear();

            foreach (PoopInteractable poop in m_poops)
            {
                m_poopPoints.Add(poop.Position);
            }

            Layout.Nav.SetObstacles(m_poopPoints, (float)m_config.PoopAvoidRadius);
        }

        private void RemovePoop(int index, double coins)
        {
            PoopInteractable poop = m_poops[index];
            m_poops.RemoveAt(index);
            Bus.Publish(new Events.PoopCleaned(poop, coins));
        }
    }
}
