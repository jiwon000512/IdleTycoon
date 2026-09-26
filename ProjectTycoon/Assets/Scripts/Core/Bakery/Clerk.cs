using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    public enum FireReason
    {
        // 팝업의 해고 버튼
        Fired,
        // 월급을 못 냈다
        Unpaid,
        // 붙어 있던 사물을 보관했다
        Stored,
    }

    // 설계 21: 점원 한 명 = 사물 하나(오븐·계산대)에 붙은 일꾼. 외형·걷기·톡 뛰기는 손님과 같은 뼈대(Visitor), 할 일은 자기 행동 트리.
    // 오븐 점원: 자리로 → 마지막 빵(없으면 첫 빵) 굽기 → 다 구울 때까지 → 꺼내기 → 진열대로 → 채우기 → 딴짓 판정 → (반복).
    // 계산 점원: 자리로 → 한 명 계산될 때까지 서 있기(계산은 계산대가 돌린다) → 딴짓 판정 → (반복).
    // 딴짓(멍 때리기)은 확률 (100 − 일머리)/100, Phase Looking(머리 위 점 셋). 해고되면 구멍으로 걸어가 톡 사라진다
    public sealed class Clerk : Visitor
    {
        private const float k_AtSpot = 0.05f;
        // 진열대 앞 서는 자리(진열대 밑변 가운데 기준). 손님 앞자리(0·0.6, −0.7) 왼쪽
        private static readonly Vector2 k_ShelfStand = new Vector2(-0.6f, -0.7f);
        private const float k_StandSearch = 1f;

        private enum Goal
        {
            Spot,
            Shelf,
            Hole,
        }

        private readonly ClerkConfigTable m_config;
        // 들어오기(한 번) → 바퀴(끝나면 스스로 되돌아가 다시. 2026-09-26 버그: 순서 안에 넣으면 바퀴가 성공할 때마다 처음부터 다시 들어왔다) → 퇴장
        private readonly BtNode<Clerk> m_enter;
        private readonly BtNode<Clerk> m_cycle;
        private readonly BtNode<Clerk> m_leave;
        private bool m_entered;
        private Goal m_goal;
        private ShelfInteractable m_shelf;
        private int m_servedAtStart;

        public BakeryArea Bakery { get; }
        public Interactable Thing { get; }
        public ClerkTable Role { get; }
        public string Name { get; }
        public int Skill { get; }
        public int Wage { get; }
        public Worker Worker { get; }
        public bool Leaving { get; private set; }
        public bool Idling => Phase == VisitorPhase.Looking;
        // 다음 월급까지 남은 초(곳이 센다)
        internal double UntilPay { get; set; }

        // 자리에 붙어 일하는 중(걷기·딴짓·퇴장 아님). 계산대는 이때만 계산을 돌린다
        public bool Working => !Leaving && !Idling && !Moving && Vector2.Distance(Position, WorkerSpot) <= k_AtSpot;

        public Vector2 WorkerSpot => Placement.SpotOf((IPlaced)Thing, SpotRole.Worker);

        internal Clerk(int id, Candidate candidate, Interactable thing, int wage, BakeryArea bakery) : base(id, candidate.Look, bakery.Layout.HoleInside, bakery.Tables)
        {
            Bakery = bakery;
            Thing = thing;
            Role = bakery.Tables.Get<ClerkTable>(thing.Table.Id);
            Name = candidate.Name;
            Skill = candidate.Skill;
            Wage = wage;
            Worker = new Worker(new Hands((int)bakery.Tables.Get<ConfigTable>(ConfigTable.k_CarryCapacity).Value), bakery.Wombat.Worker.Wallet);
            m_config = bakery.Tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            UntilPay = m_config.WagePeriodSeconds;
            m_enter = new BtAction<Clerk>(c => c.StartEnter(), (c, dt) => c.TickHopStatus(dt));
            m_cycle = thing is CounterInteractable ? BuildCounterCycle() : BuildOvenCycle();
            m_leave = new BtSequence<Clerk>(
                new BtAction<Clerk>(c => c.StartLeave(), (c, dt) => c.TickWalk()),
                new BtAction<Clerk>(c => c.StartExit(), (c, dt) => c.TickHopStatus(dt)));
        }

        // 그만둔다: 하던 일을 놓고 구멍으로
        internal void Leave()
        {
            Leaving = true;
        }

        // 배치가 바뀌었다: 걷는 중이면 같은 목표로 새 길, 자리에 서 있었으면 옮겨진 자리로(하던 기다림은 걸으면서 이어진다)
        internal void Repath()
        {
            if (Moving || m_goal == Goal.Spot && !Leaving)
            {
                WalkToGoal(m_goal);
            }
        }

        protected override bool Act(double dt)
        {
            if (Leaving)
            {
                return m_leave.Tick(this, dt) == BtStatus.Running;
            }

            if (!m_entered)
            {
                m_entered = m_enter.Tick(this, dt) == BtStatus.Success;
                return true;
            }

            m_cycle.Tick(this, dt);
            return true;
        }

        private static BtNode<Clerk> BuildOvenCycle()
        {
            return new BtSequence<Clerk>(
                new BtAction<Clerk>(c => c.StartWalkToSpot(), (c, dt) => c.TickWalk()),
                new BtAction<Clerk>(null, (c, dt) => c.Bake()),
                new BtAction<Clerk>(null, (c, dt) => c.TickWaitReady()),
                new BtAction<Clerk>(null, (c, dt) => c.TakeOut()),
                new BtAction<Clerk>(null, (c, dt) => c.TickWaitShelf()),
                new BtAction<Clerk>(c => c.StartWalkToShelf(), (c, dt) => c.TickWalk()),
                new BtAction<Clerk>(null, (c, dt) => c.Fill()),
                new BtAction<Clerk>(c => c.StartIdle(), (c, dt) => c.TickIdle(dt)));
        }

        private static BtNode<Clerk> BuildCounterCycle()
        {
            return new BtSequence<Clerk>(
                new BtAction<Clerk>(c => c.StartWalkToSpot(), (c, dt) => c.TickWalk()),
                new BtAction<Clerk>(c => c.StartServe(), (c, dt) => c.TickServe()),
                new BtAction<Clerk>(c => c.StartIdle(), (c, dt) => c.TickIdle(dt)));
        }

        // ---------- 잎 행동 ----------

        private bool StartEnter()
        {
            StartHop(VisitorPhase.Entering, Bakery.Layout.HoleInside, Bakery.Layout.HoleFloor);
            return true;
        }

        private bool StartExit()
        {
            StartHop(VisitorPhase.Exiting, Bakery.Layout.HoleFloor, Bakery.Layout.HoleInside);
            return true;
        }

        private BtStatus TickHopStatus(double dt)
        {
            return TickHop(dt) ? BtStatus.Success : BtStatus.Running;
        }

        private bool StartWalkToSpot()
        {
            WalkToGoal(Goal.Spot);
            return true;
        }

        private BtStatus TickWalk()
        {
            if (Moving)
            {
                return BtStatus.Running;
            }

            Phase = VisitorPhase.Queued;
            return BtStatus.Success;
        }

        // 빈 오븐이면 마지막 빵을 굽는다. 아직 이 오븐에서 구운 적이 없으면 해금된 첫 빵(2026-09-26 사용자: 기다리지 말고 일해야 한다)
        private BtStatus Bake()
        {
            OvenInteractable oven = (OvenInteractable)Thing;

            if (oven.IsEmpty)
            {
                oven.TryStart(oven.LastBread ?? Bakery.UnlockedBreads[0]);
            }

            return BtStatus.Success;
        }

        private BtStatus TickWaitReady()
        {
            return ((OvenInteractable)Thing).Ready > 0 ? BtStatus.Success : BtStatus.Running;
        }

        private BtStatus TakeOut()
        {
            Bakery.TryDo(ActionTable.k_TakeOut, Worker, Thing);
            return BtStatus.Success;
        }

        // 든 빵을 받을 진열대(채우기 규칙과 같다: 그 빵 진열대에 자리, 없으면 빈 진열대)가 생길 때까지 자리에서 기다린다
        // (2026-09-26: 진열대가 가득 찰 때 바퀴가 매 프레임 되돌아 좌우로 떨렸다). 빈손이면 건너뛴다
        private BtStatus TickWaitShelf()
        {
            if (Worker.Hands.Count == 0)
            {
                m_shelf = null;
                return BtStatus.Success;
            }

            m_shelf = ShelfFor(Worker.Hands.Bread);

            // 기다리는 동안 오븐은 계속 돌린다
            if (m_shelf == null)
            {
                Bake();
            }

            return m_shelf != null ? BtStatus.Success : BtStatus.Running;
        }

        private bool StartWalkToShelf()
        {
            if (m_shelf == null)
            {
                return true;
            }

            WalkToGoal(Goal.Shelf);
            return true;
        }

        private BtStatus Fill()
        {
            if (m_shelf != null)
            {
                Bakery.TryDo(ActionTable.k_Fill, Worker, m_shelf);
            }

            return BtStatus.Success;
        }

        private bool StartServe()
        {
            m_servedAtStart = ((CounterInteractable)Thing).Served;
            return true;
        }

        private BtStatus TickServe()
        {
            return ((CounterInteractable)Thing).Served > m_servedAtStart ? BtStatus.Success : BtStatus.Running;
        }

        // 확률 (100 − 일머리)/100로 멍 때리기
        private bool StartIdle()
        {
            if (Bakery.Random.NextDouble() < (100 - Skill) / 100d)
            {
                Phase = VisitorPhase.Looking;
                Timer = m_config.IdleSecondsMin + Bakery.Random.NextDouble() * (m_config.IdleSecondsMax - m_config.IdleSecondsMin);
            }

            return true;
        }

        private BtStatus TickIdle(double dt)
        {
            if (!Idling)
            {
                return BtStatus.Success;
            }

            Timer -= dt;

            if (Timer > 0d)
            {
                return BtStatus.Running;
            }

            Phase = VisitorPhase.Queued;
            return BtStatus.Success;
        }

        private bool StartLeave()
        {
            WalkToGoal(Goal.Hole);
            return true;
        }

        private void WalkToGoal(Goal goal)
        {
            m_goal = goal;
            Phase = VisitorPhase.Walking;
            BurrowNav nav = Bakery.Layout.WombatNav;

            switch (goal)
            {
                case Goal.Spot:
                    Mover.WalkTo(nav, WorkerSpot, WorkerFacing());
                    break;
                case Goal.Shelf:
                    Mover.WalkTo(nav, ShelfStand(nav, m_shelf), Facing.Up);
                    break;
                default:
                    Mover.WalkTo(nav, Bakery.Layout.HoleFloor, Facing.Up);
                    break;
            }
        }

        private Facing WorkerFacing()
        {
            foreach (SpotOffset spot in ((IPlaced)Thing).Kind.Spots)
            {
                if (spot.Role == SpotRole.Worker)
                {
                    return spot.Face;
                }
            }

            return Facing.Down;
        }

        private static Vector2 ShelfStand(BurrowNav nav, ShelfInteractable shelf)
        {
            Vector2 stand = nav.Snap(shelf.Position + k_ShelfStand);
            return nav.IsWalkable(stand) || !nav.TryNearestFree(stand, _ => false, k_StandSearch, out Vector2 free) ? stand : free;
        }

        private ShelfInteractable ShelfFor(BreadTable bread)
        {
            if (bread == null)
            {
                return null;
            }

            ShelfInteractable empty = null;

            foreach (ShelfInteractable shelf in Bakery.Shelves)
            {
                if (shelf.HasRoomFor(bread))
                {
                    return shelf;
                }

                if (shelf.Bread == null && empty == null)
                {
                    empty = shelf;
                }
            }

            return empty;
        }
    }
}
