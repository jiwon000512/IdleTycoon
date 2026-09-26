using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08·09·13 · 설계 18: 오븐 한 대(자유 배치, 밑변 가운데 좌표). 빈 오븐 → 굽는 중 → 다 구움(웜뱃이 꺼낼 때까지 대기) → 다 꺼내면 빈 오븐. 굽기 속도는 업그레이드
    public sealed class OvenInteractable : Interactable, IPlaced
    {
        public const string k_Id = "oven";

        public BakeryArea Bakery { get; }
        public Vector2 Position { get; private set; }
        public BreadTable Bread { get; private set; }
        // 설계 21: 이 오븐에서 마지막으로 구운 빵. 오븐 점원이 이 빵을 반복해 굽는다(아직 없으면 해금된 첫 빵)
        public BreadTable LastBread { get; private set; }
        public double Remaining { get; private set; }
        public int Ready { get; private set; }
        public bool IsEmpty => Bread == null;
        // 0~1(화면 타이머)
        public double Progress => IsEmpty ? 0d : 1d - Remaining / Bread.BakeSeconds;
        // 외형 2단계(굽기 속도 lookLevel 이상)
        public bool LookUpgraded => Table.Upgrade.LookLevel > 0 && UpgradeLevel >= Table.Upgrade.LookLevel;
        public IPlacedKind Kind => Table;

        public OvenInteractable(InteractableTable table, Vector2 position, BakeryArea bakery) : base(table, bakery)
        {
            Bakery = bakery;
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

        public bool TryStart(BreadTable bread)
        {
            if (!IsEmpty)
            {
                return false;
            }

            Bread = bread;
            LastBread = bread;
            Remaining = bread.BakeSeconds;
            OnChanged();
            return true;
        }

        // 남은 초(올림)가 바뀔 때만 알린다. 진행 막대는 화면이 매 프레임 Progress를 읽는다
        public override void Tick(double dt)
        {
            if (IsEmpty || Remaining <= 0d)
            {
                return;
            }

            double before = Math.Ceiling(Remaining);
            double speed = UpgradeValue(UpgradeLevel);
            Remaining = Math.Max(0d, Remaining - dt * speed);

            if (Remaining == 0d)
            {
                Ready = Bread.BatchSize;
            }

            if (Math.Ceiling(Remaining) != before)
            {
                OnChanged();
            }
        }

        // 다 구운 빵을 최대 max개 꺼낸다. 다 꺼내면 빈 오븐
        public int Take(int max)
        {
            int count = Math.Min(Ready, max);
            Ready -= count;

            if (Ready == 0)
            {
                Bread = null;
            }

            OnChanged();
            return count;
        }
    }
}
