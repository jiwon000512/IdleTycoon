using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 44: 둑의 말뚝 자리 하나. 잠겨 있으면 값을 치러 열고, 비었으면 소환하고, 꽂힌 대는 들고 옮기거나 셋을 합친다.
    // 대가 월척을 붙잡고 있으면(Hooked) 털썩, 대물을 감고 있으면 곁에 앉는다(행동은 ActionFactory.Fishing.cs)
    public sealed class StakeInteractable : Interactable
    {
        public const string k_Id = "stake";

        public FishingArea Fishing { get; }
        public int Index { get; }
        public Vector2 Position { get; }
        public double Cost { get; }
        public bool Open { get; private set; }
        public RodTable Rod { get; private set; }
        // 계열 대는 1~3, 특별한 대는 1
        public int Grade { get; private set; }
        // 붙잡은 월척(없으면 null)
        public Fish Hooked => Fishing.Sim.HookOf(this);

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

        internal void Put(RodTable rod, int grade)
        {
            Rod = rod;
            Grade = rod == null ? 0 : grade;
            OnChanged();
        }

        internal void RaiseGrade()
        {
            Grade++;
            OnChanged();
        }

        internal void Refresh()
        {
            OnChanged();
        }
    }
}
