using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 40: 가게 별(플레이어 상태라 ZooState가 든다). 별 하나 = 평가 통과 한 번이고 끝없이 센다.
    // 별마다 능력(빵 값 · 손님)이 쌓이고(StarConfigTable), 정해진 별의 마일스톤이 가게 상한(사물 수 · 업그레이드 단계)을 연다(StarMilestoneTable).
    // 표에 없는 가게는 별이 없고 상한도 없다
    public sealed class Stars
    {
        // 상한 종류: 사물 수는 InteractableTable id, 업그레이드 단계는 이것
        public const string k_Upgrade = "upgrade";

        private readonly EventBus m_bus;
        private readonly IReadOnlyList<StarConfigTable> m_configs;
        private readonly IReadOnlyList<StarMilestoneTable> m_milestones;
        private readonly Dictionary<string, int> m_counts = new Dictionary<string, int>(StringComparer.Ordinal);

        public Stars(TableSet tables, EventBus bus)
        {
            m_bus = bus;
            m_configs = tables.GetAll<StarConfigTable>();
            m_milestones = tables.GetAll<StarMilestoneTable>();
        }

        public int Count(string shop)
        {
            m_counts.TryGetValue(shop, out int count);
            return count;
        }

        // 평가를 통과했다
        internal void Add(string shop)
        {
            m_counts[shop] = Count(shop) + 1;
            m_bus.Publish(new Events.StarsChanged(shop, m_counts[shop]));
        }

        // 효과 배수에 더할 값: 가게마다 별 수 × 그 가게의 별마다 능력(빵 값 · 손님)
        public double Value(string effect)
        {
            double value = 0d;

            foreach (StarConfigTable config in m_configs)
            {
                double each = effect == BlessingTable.k_Price ? config.PriceBonus : effect == BlessingTable.k_Visitors ? config.VisitorsBonus : 0d;
                value += Count(config.Id) * each;
            }

            return value;
        }

        // 지금 별까지 마일스톤 중 그 칸이 0이 아닌 마지막 값. 표에 그 가게가 없으면 상한 없음
        public int Cap(string shop, string kind)
        {
            return CapAt(shop, kind, Count(shop));
        }

        // 별 star개일 때의 상한(설계 41 평가판 · 소식지의 「2→3」)
        public int CapAt(string shop, string kind, int star)
        {
            int cap = int.MaxValue;

            foreach (StarMilestoneTable milestone in Milestones(shop).Where(m => m.Star <= star))
            {
                int value = CapOf(milestone, kind);
                cap = value > 0 ? value : cap;
            }

            return cap;
        }

        // 그 상한이 처음으로 value 이상이 되는 별(없으면 −1). 편집 카드 · 업그레이드 줄의 「★n 필요」
        public int StarFor(string shop, string kind, int value)
        {
            int cap = int.MaxValue;

            foreach (StarMilestoneTable milestone in Milestones(shop))
            {
                int each = CapOf(milestone, kind);
                cap = each > 0 ? each : cap;

                if (cap >= value)
                {
                    return milestone.Star;
                }
            }

            return -1;
        }

        // 다음 별의 마일스톤(없으면 null)
        public StarMilestoneTable Milestone(string shop, int star)
        {
            return Milestones(shop).FirstOrDefault(m => m.Star == star);
        }

        // 계산 한 번에 팁이 나올 확률
        public double TipChance(string shop)
        {
            StarConfigTable config = Config(shop);
            int count = Count(shop);

            if (config == null || count < config.TipFrom)
            {
                return 0d;
            }

            return Math.Min(config.TipMax, config.TipBase + config.TipPerStar * (count - config.TipFrom));
        }

        public StarConfigTable Config(string shop)
        {
            return m_configs.FirstOrDefault(config => config.Id == shop);
        }

        private IEnumerable<StarMilestoneTable> Milestones(string shop)
        {
            return m_milestones.Where(m => m.Shop == shop).OrderBy(m => m.Star);
        }

        private static int CapOf(StarMilestoneTable milestone, string kind)
        {
            switch (kind)
            {
                case ShelfInteractable.k_Id: return milestone.ShelfMax;
                case OvenInteractable.k_Id: return milestone.OvenMax;
                case CounterInteractable.k_Id: return milestone.CounterMax;
                case k_Upgrade: return milestone.UpgradeMax;
                default: return 0;
            }
        }
    }
}
