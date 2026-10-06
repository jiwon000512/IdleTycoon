using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 47: 수조(자유 배치, 밑변 가운데). 물고기를 종류 섞어 용량까지 담는다. 웜뱃이 곁에 서면 창고에서 채우고(가장 많은 종류부터 하나씩),
    // 손님이 앞에서 남은 물고기 중 마리 수에 비례해 하나를 골라 잡아 두면, 웜뱃이 곁에 와서 그 물고기를 꺼내 도마로 가져간다. 용량은 업그레이드
    public sealed class TankInteractable : Interactable, IPlaced
    {
        public const string k_Id = "tank";

        private readonly Dictionary<DishTable, int> m_fish = new Dictionary<DishTable, int>();
        private readonly Dictionary<DishTable, int> m_reserved = new Dictionary<DishTable, int>();

        public RestaurantArea Restaurant { get; }
        public Vector2 Position { get; private set; }
        public IPlacedKind Kind => Table;
        // 물에 든 마리 수(손님이 잡아 둔 것 포함) · 잡아 둔 수 · 손님이 고를 수 있는 수
        public int Stock { get; private set; }
        public int Reserved { get; private set; }
        public int Free => Stock - Reserved;
        public int Capacity => (int)UpgradeValue(UpgradeLevel);

        public TankInteractable(InteractableTable table, Vector2 position, RestaurantArea restaurant) : base(table, restaurant)
        {
            Restaurant = restaurant;
            Position = position;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }

        public void MoveTo(Vector2 position)
        {
            Position = position;
        }

        public override double UpgradeValue(int level)
        {
            return Restaurant.Config.TankCapacity + Table.Upgrade.EffectPerLevel * level;
        }

        // 그 회의 물고기 마리 수(잡아 둔 것 포함)
        public int CountOf(DishTable dish)
        {
            return m_fish.TryGetValue(dish, out int count) ? count : 0;
        }

        public int FreeOf(DishTable dish)
        {
            return CountOf(dish) - (m_reserved.TryGetValue(dish, out int reserved) ? reserved : 0);
        }

        internal bool CanFill(ZooState wallet)
        {
            return Stock < Capacity && MostStocked(wallet) != null;
        }

        // 자리만큼 창고에서 담는다(창고에 가장 많은 종류부터 하나씩). 담은 마리 수
        internal int Fill(ZooState wallet)
        {
            int added = 0;

            while (Stock < Capacity && MostStocked(wallet) is DishTable dish && wallet.TrySpendItem(dish.Fish, 1))
            {
                Add(dish, 1);
                added++;
            }

            if (added > 0)
            {
                OnChanged();
            }

            return added;
        }

        // 손님이 남은 물고기 중 마리 수에 비례해 하나를 잡아 둔다(회 순서대로 센다). 남은 게 없으면 null
        internal DishTable TakeRandom(IRandom random)
        {
            int roll = (int)(random.NextDouble() * Free);

            foreach (DishTable dish in Restaurant.Dishes)
            {
                roll -= FreeOf(dish);

                if (roll < 0)
                {
                    Reserve(dish, 1);
                    OnChanged();
                    return dish;
                }
            }

            return null;
        }

        // 웜뱃이 잡아 둔 물고기를 꺼낸다
        internal void TakeReserved(DishTable dish)
        {
            Reserve(dish, -1);
            Add(dish, -1);
            OnChanged();
        }

        // 손님이 꺼내기 전에 화나서 나갔다: 다시 고를 수 있는 물고기
        internal void Unreserve(DishTable dish)
        {
            Reserve(dish, -1);
            OnChanged();
        }

        private void Reserve(DishTable dish, int count)
        {
            m_reserved[dish] = (m_reserved.TryGetValue(dish, out int reserved) ? reserved : 0) + count;
            Reserved += count;
        }

        // 설계 43: 저장한 마리 수
        internal void Restore(DishTable dish, int count)
        {
            Add(dish, count);
        }

        private void Add(DishTable dish, int count)
        {
            m_fish[dish] = CountOf(dish) + count;
            Stock += count;
        }

        private DishTable MostStocked(ZooState wallet)
        {
            DishTable best = null;
            int bestCount = 0;

            foreach (DishTable dish in Restaurant.Dishes)
            {
                int count = wallet.Count(dish.Fish);

                if (count > bestCount)
                {
                    best = dish;
                    bestCount = count;
                }
            }

            return best;
        }
    }
}
