using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08·09·13: 오븐 한 대. 빈 오븐 → 굽는 중 → 다 구움(웜뱃이 꺼낼 때까지 대기) → 다 꺼내면 빈 오븐. 굽기 속도는 업그레이드
    public sealed class OvenInteractable : Interactable
    {
        public const string k_Id = "oven";

        private readonly Vector2 m_base;
        private readonly Upgrades m_upgrades;

        // 굴 격자 설계 v0.5: 놓인 칸
        public Cell Cell { get; }
        public BreadTable Bread { get; private set; }
        public double Remaining { get; private set; }
        public int Ready { get; private set; }
        public bool IsEmpty => Bread == null;
        // 0~1(화면 타이머)
        public double Progress => IsEmpty ? 0d : 1d - Remaining / Bread.BakeSeconds;

        public OvenInteractable(InteractableTable table, Cell cell, BakeryLayout layout, Upgrades upgrades) : base(table)
        {
            Cell = cell;
            m_base = layout.OvenBase(cell);
            m_upgrades = upgrades;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_base);
        }

        public bool TryStart(BreadTable bread)
        {
            if (!IsEmpty)
            {
                return false;
            }

            Bread = bread;
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
            Remaining = Math.Max(0d, Remaining - dt * (1d + m_upgrades.Effect(ShopUpgradeTable.k_OvenSpeed)));

            if (Remaining == 0d)
            {
                Ready = Bread.BatchSize;
            }

            if (Math.Ceiling(Remaining) != before)
            {
                OnChanged();
            }
        }

        public override bool CanDo(string actionId, Hands hands)
        {
            return actionId != ActionTable.k_TakeOut || (Ready > 0 && hands.SpaceFor(Bread) > 0);
        }

        // 꺼내기: 들 수 있는 만큼 손에. 다 꺼내면 빈 오븐
        public override void Do(string actionId, Hands hands)
        {
            if (actionId != ActionTable.k_TakeOut)
            {
                return;
            }

            BreadTable bread = Bread;
            int count = Math.Min(Ready, hands.SpaceFor(bread));
            Ready -= count;

            if (Ready == 0)
            {
                Bread = null;
            }

            OnChanged();
            hands.Add(bread, count);
        }
    }
}
