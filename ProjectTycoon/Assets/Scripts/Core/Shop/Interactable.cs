using System;

namespace ZooTycoon.Core
{
    // 설계 09: 웜뱃이 상호작용 버튼으로 다루는 가게 사물(옛 World ShopTargetKind·UI SheetTargetKind 통합). 설계 11: 빵집 구멍 앞(Exit)·광장 빵집 문(Door)
    public enum InteractKind
    {
        Shelf,
        Oven,
        Counter,
        EmptySlot,
        Dig,
        Exit,
        Door,
    }

    // Cell = 그 사물의 칸(진열대·빈 자리·파기), Index = 오븐 번호
    public readonly struct Interactable : IEquatable<Interactable>
    {
        // interactables.json의 id(InteractKind 순서)
        public static readonly string[] k_KindIds = { "shelf", "oven", "counter", "slot", "dig", "exit", "door" };

        public InteractKind Kind { get; }
        public Cell Cell { get; }
        public int Index { get; }

        public Interactable(InteractKind kind, Cell cell, int index = 0)
        {
            Kind = kind;
            Cell = cell;
            Index = index;
        }

        public bool Equals(Interactable other)
        {
            return Kind == other.Kind && Cell.Equals(other.Cell) && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is Interactable other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Kind * 397 ^ Cell.GetHashCode()) * 397 ^ Index;
        }
    }
}
