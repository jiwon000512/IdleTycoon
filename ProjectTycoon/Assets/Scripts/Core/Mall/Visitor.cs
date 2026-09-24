using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11: 광장 손님 한 명. 들를 곳 몇 곳 → 빵집 문 또는 지상 계단. 외형은 빵집 안까지 그대로 간다
    public sealed class Visitor : IWalker
    {
        internal enum Goal
        {
            Spot,
            Door,
            Stairs,
        }

        public int Id { get; }
        public VisitorRecord Look { get; }
        public CustomerPhase Phase { get; internal set; }
        public double HopProgress { get; internal set; }
        public Vector2 Position => Mover.Position;
        public Vector2 Sidestep => Vector2.Zero;
        public Facing Facing => Mover.Facing;
        public bool Moving => Mover.Moving;

        internal Mover Mover { get; }
        internal int VisitsLeft { get; set; }
        internal bool WantsShop { get; set; }
        // 빵집이 꽉 차서 한 곳 더 들렀다(다음에도 꽉 차면 떠난다)
        internal bool Waited { get; set; }
        internal Goal Heading { get; set; }
        // 들를 곳 번호(PlazaLayout.Spots). 없으면 −1
        internal int Spot { get; set; } = -1;
        // 머물기 남은 초(0보다 크면 들를 곳에서 쉬는 중)
        internal double Timer { get; set; }
        // 톡 뛰기 시작·끝
        internal Vector2 HopFrom { get; set; }
        internal Vector2 HopTo { get; set; }

        internal Visitor(int id, VisitorRecord look, Vector2 position)
        {
            Id = id;
            Look = look;
            Mover = new Mover(position, Facing.Down);
        }
    }
}
