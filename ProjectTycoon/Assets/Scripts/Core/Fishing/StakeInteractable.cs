using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 44 · 46 · 49: 둑의 말뚝 자리 하나. 잠겨 있으면 값을 치러 열고, 열리면 시트에서 대를 산다(꽂혀 있으면 바꾼다). 꽂힌 대(Rod)가 곧 종류다.
    // 대가 월척을 붙잡고 있으면(Hooked) 털썩(행동은 ActionFactory.Fishing.cs)
    public sealed class StakeInteractable : Interactable
    {
        public const string k_Id = "stake";

        public FishingArea Fishing { get; }
        public int Index { get; }
        public Vector2 Position { get; }
        public double Cost { get; }
        public bool Open { get; private set; }
        public RodTable Rod { get; private set; }
        // 붙잡은 월척(없으면 null)
        public Fish Hooked => Fishing.Sim.HookOf(this);
        // 지금 감는 물고기(맨 앞 하나, 없으면 null) · 전부(화면의 낚싯줄: 여러 마리 감는 대는 여럿)
        public Fish Reeling => Rod == null ? null : Fishing.Sim.TargetOf(this);
        public System.Collections.Generic.IReadOnlyList<Fish> Targets => Fishing.Sim.TargetsOf(this);

        internal StakeInteractable(InteractableTable table, FishingArea fishing, int index, Vector2 position, double cost) : base(table, fishing)
        {
            Fishing = fishing;
            Index = index;
            Position = position;
            Cost = cost;
            Open = cost <= 0d;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }

        internal void Unlock()
        {
            Open = true;
            OnChanged();
        }

        internal void Put(RodTable rod)
        {
            Rod = rod;
            Fishing.Sim.Forget(this);
            OnChanged();
        }

        internal void Refresh()
        {
            OnChanged();
        }
    }
}
