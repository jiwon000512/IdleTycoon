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

        // 설계 25: 레시피 재료를 창고에서 꺼내야 굽는다
        public bool TryStart(BreadTable bread)
        {
            if (!IsEmpty || !Bakery.Wallet.TrySpendItems(bread.Ingredients))
            {
                return false;
            }

            Bread = bread;
            LastBread = bread;
            Remaining = bread.BakeSeconds;
            OnChanged();
            return true;
        }

        // 설계 43: 저장한 굽는 빵(없으면 빈 오븐) · 마지막 빵 · 남은 초 · 다 구운 수
        internal void Restore(BreadTable bread, BreadTable last, double remaining, int ready)
        {
            Bread = bread;
            LastBread = last;
            Remaining = bread != null ? remaining : 0d;
            Ready = bread != null ? ready : 0;
        }

        // 남은 초(올림)가 바뀔 때만 알린다. 진행 막대는 화면이 매 프레임 Progress를 읽는다
        public override void Tick(double dt)
        {
            if (IsEmpty || Remaining <= 0d)
            {
                return;
            }

            double before = Math.Ceiling(Remaining);
            double speed = UpgradeValue(UpgradeLevel) * Bakery.Wallet.Scale(BlessingTable.k_Bake);
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
