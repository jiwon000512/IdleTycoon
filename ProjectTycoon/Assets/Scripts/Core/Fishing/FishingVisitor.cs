using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 53: 좌대 손님 한 명. 광장 낚시터 문에서 들어와 구멍에서 톡 나와 → 잡아 둔 좌대 뒤로 걸어가 → 깡충 올라앉아 물 쪽을 보고 낚는다(간격마다 한 마리 · 코인) →
    // 다 낚으면 깡충 내려와 구멍으로. 길이 없으면(🤢) 화나서 곧장 나간다. 손님이 낚은 물고기는 창고에 들지 않는다.
    // ponytail: 낚시터는 웜뱃과 손님이 길(Layout.Nav)을 같이 써서 똥을 피하지 않는다. 손님 길을 따로 두면 ApplyPoopObstacles로 똥 둘레를 막는다
    public sealed class FishingVisitor : Visitor
    {
        // 좌대에 오르내리는 깡충의 포물선 높이(구멍 깡충 0.15보다 높게)
        private const float k_SeatHopArc = 0.25f;

        private readonly BtNode<FishingVisitor> m_brain;
        private double m_catchIn;
        private bool m_blocked;

        public FishingArea Fishing { get; }
        protected override WombatArea Area => Fishing;
        // 잡아 둔 좌대(들어올 때부터 나갈 때까지)
        public SeatInteractable Seat { get; private set; }
        // 좌대에 앉아 낚는 중
        public bool Seated => Phase == VisitorPhase.Queued;
        public bool Angry { get; private set; }
        // 좌대 위(뛰어오르기 시작 ~ 내려와 서기까지). 그동안 좌대 그림은 손님 뒤에 그린다
        public bool OnSeat { get; private set; }

        internal FishingVisitor(int id, VisitorTable look, FishingArea fishing, SeatInteractable seat) : base(id, look, fishing.Layout.HoleInside, fishing.Tables)
        {
            Fishing = fishing;
            Seat = seat;
            m_brain = BuildBrain();
        }

        // 배치가 바뀌었다(구간이 열림 · 물이 참): 걷는 중이면 같은 목적지로 새 길을 찾는다
        internal void Relayout()
        {
            if (Moving)
            {
                WalkTo(Mover.Destination, Mover.ArriveFacing);
            }
        }

        protected override bool Act(double dt)
        {
            return m_brain.Tick(this, dt) == BtStatus.Running;
        }

        private BtNode<FishingVisitor> BuildBrain()
        {
            return new BtSelector<FishingVisitor>(
                new BtSequence<FishingVisitor>(
                    new BtAction<FishingVisitor>(v => v.StartEnter(), (v, dt) => v.TickHopStatus(dt)),
                    new BtAction<FishingVisitor>(v => v.StartToSeat(), (v, dt) => v.TickWalk()),
                    new BtAction<FishingVisitor>(v => v.StartSit(), (v, dt) => v.TickSeatHop(dt, true)),
                    new BtAction<FishingVisitor>(v => v.StartFish(), (v, dt) => v.TickFish(dt)),
                    new BtAction<FishingVisitor>(v => v.StartStand(), (v, dt) => v.TickSeatHop(dt, false)),
                    new BtAction<FishingVisitor>(v => v.StartLeave(), (v, dt) => v.TickWalk()),
                    new BtAction<FishingVisitor>(v => v.StartExit(), (v, dt) => v.TickHopStatus(dt))),
                new BtSequence<FishingVisitor>(
                    new BtAction<FishingVisitor>(v => v.StartAngry(), (v, dt) => v.TickWalk()),
                    new BtAction<FishingVisitor>(v => v.StartExit(), (v, dt) => v.TickHopStatus(dt))));
        }

        // 길이 없으면 오가는 중엔 🤢(화나서 나감), 나가는 중엔 그냥 지나간다
        private void WalkTo(Vector2 target, Facing arrive)
        {
            if (Mover.WalkTo(Fishing.Layout.Nav, target, arrive, Phase == VisitorPhase.Leaving))
            {
                return;
            }

            m_blocked = true;
            Bubble.Show(BubbleTable.k_Yuck);
        }

        // ---------- 잎 행동 ----------

        private bool StartEnter()
        {
            StartHop(VisitorPhase.Entering, Fishing.Layout.HoleInside, Fishing.Layout.HoleFloor);
            return true;
        }

        private bool StartExit()
        {
            StartHop(VisitorPhase.Exiting, Fishing.Layout.HoleFloor, Fishing.Layout.HoleInside);
            return true;
        }

        private BtStatus TickHopStatus(double dt)
        {
            return TickHop(dt) ? BtStatus.Success : BtStatus.Running;
        }

        private bool StartToSeat()
        {
            Phase = VisitorPhase.Walking;
            WalkTo(Seat.Behind, Facing.Down);
            return true;
        }

        // 좌대 뒤에서 깡충 뛰어 윗판에 앉는다(사용자 2026-10-07). 발끝은 좌대 자리로 가고 화면은 윗판 높이만큼 올라간다
        private bool StartSit()
        {
            OnSeat = true;
            StartHop(VisitorPhase.Walking, Position, Seat.Position);
            return true;
        }

        // 다 낚으면 좌대 뒤로 깡충 내려온다
        private bool StartStand()
        {
            StartHop(VisitorPhase.Leaving, Seat.Position, Seat.Behind);
            return true;
        }

        private BtStatus TickSeatHop(double dt, bool up)
        {
            bool done = TickHop(dt);
            double t = HopProgress;
            Lift = (float)(SeatInteractable.k_Top * (up ? t : 1d - t) + Math.Sin(t * Math.PI) * k_SeatHopArc);

            if (!done)
            {
                return BtStatus.Running;
            }

            OnSeat = up;
            return BtStatus.Success;
        }

        private BtStatus TickWalk()
        {
            if (m_blocked)
            {
                return BtStatus.Failure;
            }

            return Moving ? BtStatus.Running : BtStatus.Success;
        }

        // 앉아 customerSecondsMin~Max초 낚는다. customerCatchEvery초마다 한 마리(코인)
        private bool StartFish()
        {
            FishingConfigTable config = Fishing.Config;
            Phase = VisitorPhase.Queued;
            Mover.Facing = Facing.Down;
            Timer = config.CustomerSecondsMin + Fishing.Random.NextDouble() * (config.CustomerSecondsMax - config.CustomerSecondsMin);
            m_catchIn = config.CustomerCatchEvery;
            return true;
        }

        private BtStatus TickFish(double dt)
        {
            Timer -= dt;
            m_catchIn -= dt;

            if (m_catchIn <= 0d)
            {
                m_catchIn += Fishing.Config.CustomerCatchEvery;
                Fishing.CustomerCatch(this);
            }

            if (Timer > 0d)
            {
                return BtStatus.Running;
            }

            Bubble.Show(BubbleTable.k_Heart);
            return BtStatus.Success;
        }

        private bool StartLeave()
        {
            m_blocked = false;
            Seat = null;
            Phase = VisitorPhase.Leaving;
            WalkTo(Fishing.Layout.HoleFloor, Facing.Up);
            return true;
        }

        private bool StartAngry()
        {
            Angry = true;
            m_blocked = false;
            Seat = null;
            Phase = VisitorPhase.Leaving;
            WalkTo(Fishing.Layout.HoleFloor, Facing.Up);
            return true;
        }
    }
}
