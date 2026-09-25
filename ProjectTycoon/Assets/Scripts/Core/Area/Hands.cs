using System;

namespace ZooTycoon.Core
{
    // 설계 13: 행동하는 쪽이 든 물건. 지금은 빵 한 종류 × 개수(설계 09: 웜뱃이 드는 빵 수 = ConfigTable carryCapacity)
    public sealed class Hands
    {
        public BreadTable Bread { get; private set; }
        public int Count { get; private set; }
        public int Capacity { get; }

        // 든 것이 바뀌었다. 인자는 들거나 내려놓은 사물(오븐·진열대). 화면이 그 사물과 손 사이에 빵을 날린다
        public event Action<Interactable> Changed;

        public Hands(int capacity)
        {
            Capacity = capacity;
        }

        // 더 들 수 있는 수. 다른 빵을 들고 있으면 0
        public int SpaceFor(BreadTable bread)
        {
            return Bread == null || Bread == bread ? Capacity - Count : 0;
        }

        public void Add(BreadTable bread, int count, Interactable from)
        {
            Bread = bread;
            Count += count;
            OnChanged(from);
        }

        // 다 내려놓으면 빈손
        public void Remove(int count, Interactable to)
        {
            Count -= count;

            if (Count == 0)
            {
                Bread = null;
            }

            OnChanged(to);
        }

        private void OnChanged(Interactable at)
        {
            Changed?.Invoke(at);
        }
    }
}
