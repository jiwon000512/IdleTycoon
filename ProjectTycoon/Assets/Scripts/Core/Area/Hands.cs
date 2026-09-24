using System;

namespace ZooTycoon.Core
{
    // 설계 13: 행동하는 쪽이 든 물건. 지금은 빵 한 종류 × 개수(설계 09: 웜뱃이 드는 빵 수 = carryCapacity)
    public sealed class Hands
    {
        public BreadTable Bread { get; private set; }
        public int Count { get; private set; }
        public int Capacity { get; }

        public event Action Changed;

        public Hands(int capacity)
        {
            Capacity = capacity;
        }

        // 더 들 수 있는 수. 다른 빵을 들고 있으면 0
        public int SpaceFor(BreadTable bread)
        {
            return Bread == null || Bread == bread ? Capacity - Count : 0;
        }

        public void Add(BreadTable bread, int count)
        {
            Bread = bread;
            Count += count;
            OnChanged();
        }

        // 다 내려놓으면 빈손
        public void Remove(int count)
        {
            Count -= count;

            if (Count == 0)
            {
                Bread = null;
            }

            OnChanged();
        }

        private void OnChanged()
        {
            Changed?.Invoke();
        }
    }
}
