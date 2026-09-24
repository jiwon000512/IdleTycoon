using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2: 화면이 그림을 고르는 상태
    public enum CustomerPhase
    {
        Entering,
        Walking,
        Looking,
        Picking,
        ToQueue,
        Queued,
        Leaving,
        Exiting,
    }

    // 설계 08 v0.5 · 손님 동선 설계 v0.2: 빵집 손님 한 명. 위치·보는 방향은 Mover가, 할 일은 행동 트리(Brain)가 정한다
    // 설계 11: 외형(Look)은 광장에서 정해져 빵집 안까지, 나갈 때 다시 광장으로 그대로 간다
    public sealed class Customer : IWalker
    {
        public int Id { get; }
        public VisitorRecord Look { get; }
        // 지금 찾는 빵과 그 진열대 칸
        public BreadRecord Bread { get; internal set; }
        public Cell Cell { get; internal set; }
        public CustomerPhase Phase { get; internal set; }
        public bool CarriesBread { get; internal set; }
        public bool Angry { get; internal set; }
        // 나오기·나가기 톡 뛰기 진행(0~1)
        public double HopProgress { get; internal set; }
        public Vector2 Position => Mover.Position;
        // 비켜 걷기: 가까운 손님과 겹치지 않게 옆으로 비킨 만큼. 길·자리는 그대로이고 화면만 Position에 더한다
        public Vector2 Sidestep { get; internal set; }
        public Facing Facing => Mover.Facing;
        public bool Moving => Mover.Moving;
        internal bool Hopping => Phase == CustomerPhase.Entering || Phase == CustomerPhase.Exiting;

        internal Mover Mover { get; }
        internal BtNode<Customer> Brain { get; set; }
        internal HashSet<string> Tried { get; } = new HashSet<string>();
        internal double Patience { get; set; }
        internal double Timer { get; set; }
        internal bool HasSpot { get; set; }
        internal Vector2 Spot { get; set; }
        internal bool Paid { get; set; }

        internal Customer(int id, VisitorRecord look, Vector2 position, double patience)
        {
            Id = id;
            Look = look;
            Mover = new Mover(position, Facing.Down);
            Patience = patience;
        }
    }
}
