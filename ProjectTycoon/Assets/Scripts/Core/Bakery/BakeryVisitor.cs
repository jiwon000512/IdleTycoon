using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08 v0.5 · 손님 동선 설계 v0.2 5·6·7장 · 리뷰 R2: 빵집 손님 한 명. 할 일은 자기 행동 트리(Brain)와 잎 행동으로 스스로 정한다.
    // 구멍에서 톡 나와 → 빵을 고르고 진열대로 → 집기(비었으면 기다림) → 줄 → 계산 → 구멍으로. 빵을 못 찾으면 그냥 나간다
    public sealed class BakeryVisitor : Visitor
    {
        private readonly BtNode<BakeryVisitor> m_brain;
        private readonly HashSet<string> m_tried = new HashSet<string>();
        private double m_patience;

        public BakeryArea Bakery { get; }
        // 지금 찾는 빵과 그 진열대
        public BreadTable Bread { get; private set; }
        public ShelfInteractable Shelf { get; private set; }
        public bool CarriesBread { get; private set; }
        public bool Angry { get; private set; }
        public bool Paid { get; private set; }
        // 진열대 앞 서는 자리를 잡고 있다(다른 손님이 같은 자리에 서지 않게 곳이 본다)
        public bool HasSpot { get; private set; }
        public Vector2 Spot { get; private set; }

        internal BakeryVisitor(int id, VisitorTable look, BakeryArea bakery) : base(id, look, bakery.Layout.HoleInside, bakery.Tables)
        {
            Bakery = bakery;
            m_patience = bakery.Config.PatienceSeconds;
            m_brain = BuildBrain();
        }

        // 배치가 바뀌었다: 걷는 중이면 같은 목적지로 새 길을 찾는다(줄에 선·서러 가는 손님은 계산대가 옮긴다)
        internal void Repath()
        {
            if (Moving && Phase != VisitorPhase.ToQueue && Phase != VisitorPhase.Queued)
            {
                Mover.WalkTo(Bakery.Layout.Nav, Mover.Destination, Mover.ArriveFacing);
            }
        }

        // 계산대가 값을 받았다
        internal void Pay()
        {
            Paid = true;
            CarriesBread = false;
        }

        protected override bool Act(double dt)
        {
            if (m_brain.Tick(this, dt) == BtStatus.Running)
            {
                return true;
            }

            HasSpot = false;
            return false;
        }

        // 5장 트리. 마디가 진행 상태를 가지므로 손님마다 새로 만든다
        private BtNode<BakeryVisitor> BuildBrain()
        {
            BtNode<BakeryVisitor> pick = new BtAction<BakeryVisitor>(v => v.StartPick(), (v, dt) => v.TickPick(dt));
            BtNode<BakeryVisitor> findOnce = new BtSequence<BakeryVisitor>(
                new BtAction<BakeryVisitor>(null, (v, dt) => v.ChooseBread()),
                new BtAction<BakeryVisitor>(v => v.StartWalkToShelf(), (v, dt) => v.TickWalk()),
                new BtSelector<BakeryVisitor>(
                    pick,
                    new BtSequence<BakeryVisitor>(
                        new BtAction<BakeryVisitor>(v => v.StartLook(), (v, dt) => v.TickLook(dt)),
                        new BtAction<BakeryVisitor>(v => v.StartPick(), (v, dt) => v.TickPick(dt)))));

            return new BtSelector<BakeryVisitor>(
                new BtSequence<BakeryVisitor>(
                    new BtAction<BakeryVisitor>(v => v.StartEnter(), (v, dt) => v.TickHopStatus(dt)),
                    new BtRepeat<BakeryVisitor>(v => v.m_patience > 0d, findOnce),
                    new BtAction<BakeryVisitor>(v => v.StartToQueue(), (v, dt) => v.TickToQueue()),
                    new BtAction<BakeryVisitor>(null, (v, dt) => v.TickWaitCheckout()),
                    new BtAction<BakeryVisitor>(v => v.StartLeave(), (v, dt) => v.TickWalk()),
                    new BtAction<BakeryVisitor>(v => v.StartExit(), (v, dt) => v.TickHopStatus(dt))),
                new BtSequence<BakeryVisitor>(
                    new BtAction<BakeryVisitor>(v => v.StartAngry(), (v, dt) => v.TickWalk()),
                    new BtAction<BakeryVisitor>(v => v.StartExit(), (v, dt) => v.TickHopStatus(dt))));
        }

        // ---------- 잎 행동 ----------

        // 구멍 안에서 톡 뛰어내려 구멍 아래 바닥에 선다
        private bool StartEnter()
        {
            StartHop(VisitorPhase.Entering, Bakery.Layout.HoleInside, Bakery.Layout.HoleFloor);
            return true;
        }

        // 나가기: 구멍 아래 바닥에서 톡 뛰어 구멍 안으로
        private bool StartExit()
        {
            StartHop(VisitorPhase.Exiting, Bakery.Layout.HoleFloor, Bakery.Layout.HoleInside);
            return true;
        }

        private BtStatus TickHopStatus(double dt)
        {
            return TickHop(dt) ? BtStatus.Success : BtStatus.Running;
        }

        // 안 가 본 빵 중 가중치 난수. 재고는 보지 않는다(가 봐야 안다). 다 가 봤으면 지금 빵 그대로.
        // 설계 17: 진열대는 그 빵 재고가 있는 가장 가까운 곳, 없으면 가장 가까운 진열대(거기서 기다린다)
        private BtStatus ChooseBread()
        {
            int weightSum = 0;

            foreach (BreadTable bread in Bakery.UnlockedBreads)
            {
                weightSum += m_tried.Contains(bread.Id) ? 0 : bread.Weight;
            }

            if (weightSum == 0)
            {
                if (Bread == null)
                {
                    return BtStatus.Failure;
                }

                Shelf = Bakery.ShelfFor(Bread, Position);
                return Shelf != null ? BtStatus.Success : BtStatus.Failure;
            }

            double roll = Bakery.Random.NextDouble() * weightSum;
            BreadTable chosen = null;

            foreach (BreadTable bread in Bakery.UnlockedBreads)
            {
                if (m_tried.Contains(bread.Id))
                {
                    continue;
                }

                chosen = bread;
                roll -= bread.Weight;

                if (roll < 0d)
                {
                    break;
                }
            }

            Bread = chosen;
            Shelf = Bakery.ShelfFor(chosen, Position);
            m_tried.Add(chosen.Id);
            // 진열대가 하나도 없으면(다 보관함) 빵을 못 찾은 것
            return Shelf != null ? BtStatus.Success : BtStatus.Failure;
        }

        // 그 진열대의 빈 서는 자리를 잡고 걷는다. 이미 그 진열대 자리에 서 있으면 그대로
        private bool StartWalkToShelf()
        {
            Phase = VisitorPhase.Walking;

            if (HasSpot && IsShelfSpot(Shelf, Spot))
            {
                return true;
            }

            HasSpot = false;
            Spot = Bakery.FreeSpot(Shelf);
            HasSpot = true;
            Mover.WalkTo(Bakery.Layout.Nav, Spot, Bakery.Layout.ShelfFacing(Shelf, Spot));
            return true;
        }

        private BtStatus TickWalk()
        {
            return Moving ? BtStatus.Running : BtStatus.Success;
        }

        // 내 빵의 재고가 있으면 하나 집는다(pickSeconds 동안 빵이 손으로). 기다리는 사이 다른 빵으로 바뀐 진열대에서는 집지 않는다
        private bool StartPick()
        {
            if (Shelf.Bread != Bread || !Shelf.TryPick())
            {
                return false;
            }

            Phase = VisitorPhase.Picking;
            Timer = Bakery.Config.PickSeconds;
            Bakery.Bus.Publish(new Events.BakeryVisitorPicked(this));
            return true;
        }

        private BtStatus TickPick(double dt)
        {
            Timer -= dt;

            if (Timer > 0d)
            {
                return BtStatus.Running;
            }

            CarriesBread = true;
            return BtStatus.Success;
        }

        // 빈 진열대 앞에서 최대 lookSeconds(남은 인내까지) 두리번. 그사이 재고가 생기면 바로 성공
        private bool StartLook()
        {
            if (m_patience <= 0d)
            {
                return false;
            }

            Phase = VisitorPhase.Looking;
            Timer = Math.Min(Bakery.Config.LookSeconds, m_patience);
            return true;
        }

        private BtStatus TickLook(double dt)
        {
            if (Shelf.Bread == Bread && Shelf.Stock > 0)
            {
                return BtStatus.Success;
            }

            Timer -= dt;
            m_patience -= dt;
            return Timer > 0d ? BtStatus.Running : BtStatus.Failure;
        }

        // 집은 순서대로 줄 번호를 받고 그 자리로 걷는다
        private bool StartToQueue()
        {
            HasSpot = false;
            Phase = VisitorPhase.ToQueue;
            Bakery.ShortestQueue(Position).Join(this);
            return true;
        }

        private BtStatus TickToQueue()
        {
            if (Moving)
            {
                return BtStatus.Running;
            }

            Phase = VisitorPhase.Queued;
            return BtStatus.Success;
        }

        // 앞사람이 빠지면 계산대가 한 칸씩 앞으로 걷게 한다. 계산이 끝나면 성공
        private BtStatus TickWaitCheckout()
        {
            return Paid ? BtStatus.Success : BtStatus.Running;
        }

        private bool StartLeave()
        {
            Phase = VisitorPhase.Leaving;
            Mover.WalkTo(Bakery.Layout.Nav, Bakery.Layout.HoleFloor, Facing.Up);
            return true;
        }

        // 빵을 못 찾았다: 구멍으로
        private bool StartAngry()
        {
            Angry = true;
            HasSpot = false;
            Phase = VisitorPhase.Leaving;
            Mover.WalkTo(Bakery.Layout.Nav, Bakery.Layout.HoleFloor, Facing.Up);
            Bakery.Bus.Publish(new Events.BakeryVisitorGaveUp(this));
            return true;
        }

        private bool IsShelfSpot(ShelfInteractable shelf, Vector2 p)
        {
            foreach (Vector2 spot in Bakery.Layout.ShelfSpots(shelf))
            {
                if (Vector2.DistanceSquared(spot, p) < 0.01f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
