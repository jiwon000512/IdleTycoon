using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 24 → 설계 37: 웜뱃 똥(곳 공용). 웜뱃이 있는 곳을 poopEvery 유닛 걸을 때마다 poopChance로 등 뒤에 하나(곳마다 바닥에 poopMax까지, 다른 똥 poopGap 안이면 건너뜀).
    // 똥은 사물(치우기 버튼)이고, 치운 만큼 거름(FarmConfigTable manureItem)이 창고로 간다. 손님 땅에 둘레(poopAvoidRadius)를 거는 일 · 손님 다시 길 찾기는 곳이 OnPoopsChanged로 한다
    // (점원 · 웜뱃은 다른 땅 WombatNav를 걸어 밟고 지나간다). 숫자는 웜뱃의 값이라 ConfigTable
    public abstract partial class WombatArea
    {
        // 등 뒤로 떨어지는 거리
        private const float k_PoopBehind = 0.25f;

        private readonly List<PoopInteractable> m_poops = new List<PoopInteractable>();
        private readonly List<Vector2> m_poopPoints = new List<Vector2>();
        private double m_poopEvery;
        private double m_poopChance;
        private int m_poopMax;
        private double m_poopGap;
        private string m_manureItem;
        private double m_walked;

        public IReadOnlyList<PoopInteractable> Poops => m_poops;
        // 손님이 걸어 들어가지 않는 똥 둘레(막힘의 세기)
        public float PoopAvoidRadius { get; private set; }

        // 그 자리에 똥 하나. 바닥에 poopMax개거나 다른 똥 poopGap 안이면 false
        public bool TryDropPoop(Vector2 at)
        {
            if (m_poops.Count >= m_poopMax)
            {
                return false;
            }

            foreach (PoopInteractable poop in m_poops)
            {
                if (Vector2.Distance(poop.Position, at) < m_poopGap)
                {
                    return false;
                }
            }

            PoopInteractable dropped = new PoopInteractable(Tables.Get<InteractableTable>(PoopInteractable.k_Id), at, this);
            m_poops.Add(dropped);
            OnPoopsChanged();
            Bus.Publish(new Events.PoopDropped(dropped));
            return true;
        }

        // 치우기 버튼: 웜뱃 range 안 똥 전부. 치운 만큼 거름이 창고로 간다(설계 28. 사물 밑에 깔려 사라진 똥은 주지 않는다).
        // 설계 31 거름 국자를 끼웠으면 똥 하나마다 코인
        internal void CleanAround()
        {
            ZooState wallet = Wombat.Worker.Wallet;
            int cleaned = 0;
            double coins = wallet.Relics.Value(RelicTable.k_Scoop);

            for (int i = m_poops.Count - 1; i >= 0; i--)
            {
                if (IsInRange(m_poops[i]))
                {
                    RemovePoop(i, coins);
                    cleaned++;
                }
            }

            if (cleaned == 0)
            {
                return;
            }

            if (coins > 0d)
            {
                wallet.AddCoins(coins * cleaned);
            }

            wallet.AddItem(m_manureItem, cleaned);
            OnPoopsChanged();
            Bus.Publish(new Events.PoopsCleaned(this, m_manureItem, cleaned));
        }

        // 서는 자리 고르기: 똥 둘레 안인가
        protected bool NearPoop(Vector2 p)
        {
            foreach (PoopInteractable poop in m_poops)
            {
                if (Vector2.Distance(poop.Position, p) < PoopAvoidRadius)
                {
                    return true;
                }
            }

            return false;
        }

        // 똥이 생기거나 치워졌다: 곳이 사물 목록 · 손님 땅의 둘레를 맞추고 걷는 손님에게 새 길을 찾게 한다
        protected abstract void OnPoopsChanged();

        // 손님 땅에 지금 똥 둘레를 건다(땅을 새로 만들면 다시 건다)
        protected void ApplyPoopObstacles(BurrowNav nav)
        {
            m_poopPoints.Clear();

            foreach (PoopInteractable poop in m_poops)
            {
                m_poopPoints.Add(poop.Position);
            }

            nav.SetObstacles(m_poopPoints, PoopAvoidRadius);
        }

        // 배치가 바뀌었다: 사물 밑에 깔린 똥은 치운 셈(거름 없음)
        protected void ClearBuriedPoops()
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
        }

        private void InitPoops(TableSet tables)
        {
            m_poopEvery = tables.Get<ConfigTable>(ConfigTable.k_PoopEvery).Value;
            m_poopChance = tables.Get<ConfigTable>(ConfigTable.k_PoopChance).Value;
            m_poopMax = (int)tables.Get<ConfigTable>(ConfigTable.k_PoopMax).Value;
            m_poopGap = tables.Get<ConfigTable>(ConfigTable.k_PoopGap).Value;
            PoopAvoidRadius = (float)tables.Get<ConfigTable>(ConfigTable.k_PoopAvoidRadius).Value;
            m_manureItem = tables.Get<FarmConfigTable>(FarmConfigTable.k_Main).ManureItem;
        }

        // 웜뱃이 있는 곳에서만 걸은 거리를 센다
        private void TickPoops()
        {
            if (!WombatPresent)
            {
                return;
            }

            m_walked += Wombat.Walked;

            if (m_walked < m_poopEvery)
            {
                return;
            }

            m_walked -= m_poopEvery;

            if (Random.NextDouble() < m_poopChance)
            {
                TryDropPoop(Behind());
            }
        }

        // 웜뱃 등 뒤. 걷는 땅이 아니면 발밑
        private Vector2 Behind()
        {
            Vector2 feet = Wombat.Mover.Position;
            Vector2 behind = feet - Mover.Direction(Wombat.Mover.Facing) * k_PoopBehind;
            return WombatNav.IsWalkable(behind) ? behind : feet;
        }

        private void RemovePoop(int index, double coins)
        {
            PoopInteractable poop = m_poops[index];
            m_poops.RemoveAt(index);
            Bus.Publish(new Events.PoopCleaned(poop, coins));
        }
    }
}
