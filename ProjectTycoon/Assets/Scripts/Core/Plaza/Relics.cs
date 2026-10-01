using System;
using System.Collections.Generic;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 31 → 설계 36: 유물(플레이어 상태라 ZooState가 든다). 광장 행상에게 반짝돌(relicItem)을 relicCost개 내고 뽑으면 별이 다 오르지 않은 유물 중
    // 하나가 비중으로 바로 들어온다(새 유물은 ★1, 가진 유물은 별 +1). 칸(relicSlots)에 끼운 유물만 효과가 난다(빈 칸이면 뽑을 때 저절로 끼운다)
    public sealed class Relics
    {
        private readonly ZooState m_wallet;
        private readonly EventBus m_bus;
        private readonly PlazaConfigTable m_config;
        private readonly Dictionary<string, int> m_stars = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly RelicTable[] m_slots;
        // 낡은 주판: 끼운 동안 센 계산 수
        private int m_sales;

        public IReadOnlyList<RelicTable> All { get; }
        // 칸(빈 칸은 null)
        public IReadOnlyList<RelicTable> Slots => m_slots;
        public string Item => m_config.RelicItem;
        public int Cost => m_config.RelicCost;
        // 모든 유물의 별이 다 올랐다(더 뽑을 것이 없다)
        public bool Complete => Candidates().Count == 0;

        public Relics(TableSet tables, ZooState wallet, EventBus bus)
        {
            m_wallet = wallet;
            m_bus = bus;
            m_config = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_slots = new RelicTable[m_config.RelicSlots];
            All = tables.GetAll<RelicTable>();
        }

        public int Stars(RelicTable relic)
        {
            m_stars.TryGetValue(relic.Id, out int stars);
            return stars;
        }

        public bool IsEquipped(RelicTable relic)
        {
            return Array.IndexOf(m_slots, relic) >= 0;
        }

        // 뽑은 유물 하나: 별 +1, 칸에 없고 빈 칸이 있으면 끼운다. 더 뽑을 것이 없거나 반짝돌이 모자라면 아무 일 없이 false
        public bool TryDraw(IRandom random)
        {
            List<RelicTable> pool = Candidates();

            if (pool.Count == 0 || !m_wallet.TrySpendItem(Item, Cost))
            {
                return false;
            }

            RelicTable relic = pool[Pick(pool, random)];
            m_stars[relic.Id] = Stars(relic) + 1;
            int free = Array.IndexOf(m_slots, null);

            if (!IsEquipped(relic) && free >= 0)
            {
                m_slots[free] = relic;
            }

            Publish(RelicChange.Drawn, relic);
            return true;
        }

        // 가진 유물을 빈 칸에. 없는 유물 · 이미 끼운 유물 · 빈 칸이 없으면 false
        public bool TryEquip(RelicTable relic)
        {
            int free = Array.IndexOf(m_slots, null);

            if (Stars(relic) == 0 || IsEquipped(relic) || free < 0)
            {
                return false;
            }

            m_slots[free] = relic;
            Publish(RelicChange.Equipped, relic);
            return true;
        }

        public void Unequip(RelicTable relic)
        {
            int slot = Array.IndexOf(m_slots, relic);

            if (slot < 0)
            {
                return;
            }

            m_slots[slot] = null;
            Publish(RelicChange.Equipped, relic);
        }

        // 칸에 끼운 유물 중 그 효과의 지금 별 값(없으면 0)
        public double Value(string effect)
        {
            foreach (RelicTable relic in m_slots)
            {
                if (relic != null && relic.Effect == effect)
                {
                    return ValueAt(relic, Stars(relic));
                }
            }

            return 0d;
        }

        // 별 stars(1부터)일 때 값
        public static double ValueAt(RelicTable relic, int stars)
        {
            return relic.Values[Math.Min(stars, relic.Values.Length) - 1];
        }

        // 낡은 주판: 끼운 동안 계산을 세어 N번째면 true(세기를 처음부터)
        public bool CountSale()
        {
            double every = Value(RelicTable.k_Abacus);

            if (every <= 0d)
            {
                return false;
            }

            m_sales++;

            if (m_sales < every)
            {
                return false;
            }

            m_sales = 0;
            return true;
        }

        private List<RelicTable> Candidates()
        {
            List<RelicTable> pool = new List<RelicTable>();

            foreach (RelicTable relic in All)
            {
                if (Stars(relic) < relic.Values.Length && relic.Weight > 0d)
                {
                    pool.Add(relic);
                }
            }

            return pool;
        }

        private static int Pick(List<RelicTable> pool, IRandom random)
        {
            double total = 0d;

            foreach (RelicTable relic in pool)
            {
                total += relic.Weight;
            }

            double roll = random.NextDouble() * total;

            for (int i = 0; i < pool.Count; i++)
            {
                roll -= pool[i].Weight;

                if (roll < 0d)
                {
                    return i;
                }
            }

            return pool.Count - 1;
        }

        private void Publish(RelicChange change, RelicTable relic)
        {
            m_bus.Publish(new Events.RelicsChanged(this, change, relic));
        }
    }

    // 뽑음(Relic이 들어왔다) · 끼움/뺌
    public enum RelicChange
    {
        Drawn,
        Equipped,
    }
}
