using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 47: 횟집 손님 한 명. 구멍에서 톡 나와 → 수조 앞(물고기 · 빈 자리를 기다림) → 물고기 고르기(수조에서 그 물고기를 잡아 두고 주문이 도마 줄로, 탁자 자리를 잡는다) →
    // 자리로 걸어가 접시를 기다림 → 먹기 → 탁자에서 값 → 구멍으로. 기다리다 지치거나 똥에 길이 막히면(🤢) 화나서 나간다.
    // 손님 자신이 주문이다: 도마 줄과 웜뱃 손의 접시는 이 손님을 가리킨다(Dish · Table · Seat)
    public sealed class RestaurantVisitor : Visitor
    {
        private readonly BtNode<RestaurantVisitor> m_brain;
        private double m_patience;
        private bool m_blocked;

        public RestaurantArea Restaurant { get; }
        protected override WombatArea Area => Restaurant;
        public TankInteractable Tank { get; private set; }
        // 수조 앞 서는 자리를 잡고 있다(다른 손님이 같은 자리에 서지 않게 곳이 본다)
        public bool HasSpot { get; private set; }
        public Vector2 Spot { get; private set; }
        // 고른 회. 고르기 전에는 null
        public DishTable Dish { get; private set; }
        // 잡은 탁자 자리(고른 순간부터 나갈 때까지). 없으면 null · −1
        public DiningTableInteractable Table { get; private set; }
        public int Seat { get; private set; } = -1;
        // 웜뱃이 수조에서 이 손님 물고기를 꺼냈다 · 도마에서 회를 다 떴다(웜뱃 손의 것이 접시)
        public bool FishTaken { get; internal set; }
        public bool Cut { get; internal set; }
        public bool Served { get; private set; }
        public bool Paid { get; private set; }
        public bool Angry { get; private set; }
        // 접시를 받아 먹는 중(탁자 위 접시)
        public bool Eating => Served && !Paid;

        internal RestaurantVisitor(int id, VisitorTable look, RestaurantArea restaurant) : base(id, look, restaurant.Layout.HoleInside, restaurant.Tables)
        {
            Restaurant = restaurant;
            m_patience = restaurant.Config.PatienceSeconds;
            m_brain = BuildBrain();
        }

        // 배치가 바뀌었다: 자리로 가거나 앉은 손님은 (옮겨졌을지 모를) 자리로, 걷는 중이면 같은 목적지로 새 길을 찾는다
        internal void Relayout()
        {
            if (Table != null && (Phase == VisitorPhase.ToQueue || Phase == VisitorPhase.Queued))
            {
                RestaurantLayout.Seat seat = Restaurant.SeatOf(this);
                WalkTo(seat.Position, seat.Facing);
            }
            else if (Moving)
            {
                WalkTo(Mover.Destination, Mover.ArriveFacing);
            }
        }

        // 설계 24: 똥 둘레를 피해 걷는다. 길이 없으면 오가는 중엔 🤢(걷던 잎이 실패해 화나서 나감), 나가는 중엔 둘레를 무시하고 지나간다
        internal void WalkTo(Vector2 target, Facing arrive)
        {
            if (Mover.WalkTo(Restaurant.Layout.Nav, target, arrive, Phase == VisitorPhase.Leaving))
            {
                return;
            }

            m_blocked = true;
            Bubble.Show(BubbleTable.k_Yuck);
        }

        // 탁자에 접시가 왔다
        internal void Serve()
        {
            Served = true;
            Timer = Restaurant.Config.EatSeconds;
        }

        protected override bool Act(double dt)
        {
            return m_brain.Tick(this, dt) == BtStatus.Running;
        }

        private BtNode<RestaurantVisitor> BuildBrain()
        {
            return new BtSelector<RestaurantVisitor>(
                new BtSequence<RestaurantVisitor>(
                    new BtAction<RestaurantVisitor>(v => v.StartEnter(), (v, dt) => v.TickHopStatus(dt)),
                    new BtAction<RestaurantVisitor>(v => v.StartWalkToTank(), (v, dt) => v.TickWalk()),
                    new BtAction<RestaurantVisitor>(v => v.StartWait(), (v, dt) => v.TickWait(dt)),
                    new BtAction<RestaurantVisitor>(v => v.StartPick(), (v, dt) => v.TickPick(dt)),
                    new BtAction<RestaurantVisitor>(v => v.StartToSeat(), (v, dt) => v.TickToSeat()),
                    new BtAction<RestaurantVisitor>(null, (v, dt) => v.TickEat(dt)),
                    new BtAction<RestaurantVisitor>(v => v.StartLeave(), (v, dt) => v.TickWalk()),
                    new BtAction<RestaurantVisitor>(v => v.StartExit(), (v, dt) => v.TickHopStatus(dt))),
                new BtSequence<RestaurantVisitor>(
                    new BtAction<RestaurantVisitor>(v => v.StartAngry(), (v, dt) => v.TickWalk()),
                    new BtAction<RestaurantVisitor>(v => v.StartExit(), (v, dt) => v.TickHopStatus(dt))));
        }

        // ---------- 잎 행동 ----------

        private bool StartEnter()
        {
            StartHop(VisitorPhase.Entering, Restaurant.Layout.HoleInside, Restaurant.Layout.HoleFloor);
            return true;
        }

        private bool StartExit()
        {
            StartHop(VisitorPhase.Exiting, Restaurant.Layout.HoleFloor, Restaurant.Layout.HoleInside);
            return true;
        }

        private BtStatus TickHopStatus(double dt)
        {
            return TickHop(dt) ? BtStatus.Success : BtStatus.Running;
        }

        // 물고기가 있는 가장 가까운 수조(없으면 가장 가까운 수조)의 빈 서는 자리로
        private bool StartWalkToTank()
        {
            Tank = Restaurant.TankFor(Position);

            if (Tank == null)
            {
                return false;
            }

            Phase = VisitorPhase.Walking;
            Spot = Restaurant.FreeSpot(Tank);
            HasSpot = true;
            WalkTo(Spot, Facing.Up);
            return true;
        }

        private BtStatus TickWalk()
        {
            if (m_blocked)
            {
                return BtStatus.Failure;
            }

            return Moving ? BtStatus.Running : BtStatus.Success;
        }

        // 수조에 물고기가 있고 빈 탁자 자리가 날 때까지 「…」. 인내를 다 쓰면 실패(화나서 나감)
        private bool StartWait()
        {
            Phase = VisitorPhase.Looking;
            return true;
        }

        private BtStatus TickWait(double dt)
        {
            if (Tank.Free > 0 && Restaurant.HasFreeSeat)
            {
                Bubble.Clear();
                return BtStatus.Success;
            }

            Bubble.Show(BubbleTable.k_Wait);
            m_patience -= dt;

            if (m_patience > 0d)
            {
                return BtStatus.Running;
            }

            Bubble.Clear();
            return BtStatus.Failure;
        }

        // 물고기 하나를 잡아 두고(웜뱃이 꺼낼 때까지 수조에 있다) 주문하고(도마 줄) 탁자 자리를 잡는다. pickSeconds 동안 수조 앞에 선다
        private bool StartPick()
        {
            if (!Restaurant.FindSeat(out DiningTableInteractable table, out int seat) || !(Tank.TakeRandom(Restaurant.Random) is DishTable dish))
            {
                return false;
            }

            Dish = dish;
            Table = table;
            Seat = seat;
            Phase = VisitorPhase.Picking;
            Timer = Restaurant.Config.PickSeconds;
            Restaurant.ShortestBoard(Position).Add(this);
            Restaurant.Bus.Publish(new Events.DishOrdered(this));
            return true;
        }

        private BtStatus TickPick(double dt)
        {
            Timer -= dt;
            return Timer > 0d ? BtStatus.Running : BtStatus.Success;
        }

        private bool StartToSeat()
        {
            HasSpot = false;
            Phase = VisitorPhase.ToQueue;
            RestaurantLayout.Seat seat = Restaurant.SeatOf(this);
            WalkTo(seat.Position, seat.Facing);
            return true;
        }

        private BtStatus TickToSeat()
        {
            if (m_blocked)
            {
                return BtStatus.Failure;
            }

            if (Moving)
            {
                return BtStatus.Running;
            }

            Phase = VisitorPhase.Queued;
            return BtStatus.Success;
        }

        // 접시를 기다렸다가(접시는 걸어오는 중에도 놓일 수 있다) 다 먹으면 탁자에서 값을 낸다
        private BtStatus TickEat(double dt)
        {
            if (!Served)
            {
                return BtStatus.Running;
            }

            Timer -= dt;

            if (Timer > 0d)
            {
                return BtStatus.Running;
            }

            Paid = true;
            Restaurant.Pay(this);
            Bubble.Show(BubbleTable.k_Heart);
            return BtStatus.Success;
        }

        private bool StartLeave()
        {
            m_blocked = false;
            Table = null;
            Seat = -1;
            Phase = VisitorPhase.Leaving;
            WalkTo(Restaurant.Layout.HoleFloor, Facing.Up);
            return true;
        }

        // 기다리다 지쳤거나 똥에 막혔다: 주문 · 자리를 놓고(든 접시는 사라진다) 구멍으로
        private bool StartAngry()
        {
            Angry = true;
            HasSpot = false;
            m_blocked = false;
            Restaurant.Cancel(this);
            Table = null;
            Seat = -1;
            Phase = VisitorPhase.Leaving;
            WalkTo(Restaurant.Layout.HoleFloor, Facing.Up);
            return true;
        }
    }
}
