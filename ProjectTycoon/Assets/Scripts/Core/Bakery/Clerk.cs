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

    // 설계 22: 딴짓 종류. 멍(「…」) · 산책(「♪」, 자리 근처 걷는 점으로) · 수다(「💬」, 딴짓 중인 다른 점원 옆으로) · 외출(「♪」, 구멍으로 나가 광장에서 놀다 옴)
    public enum IdleKind
    {
        None,
        Daze,
        Stroll,
        Chat,
        Outing,
    }

    // 설계 21: 점원 한 명 = 사물 하나(오븐·계산대)에 붙은 일꾼. 외형·걷기·톡 뛰기는 손님과 같은 뼈대(Visitor), 할 일은 자기 행동 트리.
    // 오븐 점원: 자리로 → 마지막 빵(없으면 첫 빵) 굽기 → 다 구울 때까지 → 꺼내기 → 진열대로 → 채우기 → 딴짓 판정 → (반복).
    // 계산 점원: 자리로 → 한 명 계산될 때까지 서 있기(계산은 계산대가 돌린다) → 딴짓 판정 → (반복).
    // 딴짓 확률은 (100 − 일머리)/100, 시간은 일머리가 낮을수록 길다(2026-09-26 사용자: 1분도 넘을 수 있다).
    // 설계 22: 딴짓 중엔 곳이 ClerkInteractable로 감싸 웜뱃이 깨울 수 있다(WakeUp: 딴짓을 끊고 다음 판정 wakeSkips회 건너뜀). 외출(Away) 중엔 광장이 같은 점원을 손님 그림으로 대신 세우고 거기서 깨운다.
    // 해고되면 구멍으로 걸어가 톡 사라진다(외출 중이면 그 자리에서 사라지고 광장 그림이 계단으로 간다)
    public sealed class Clerk : Visitor
    {
        private const float k_AtSpot = 0.05f;
        // 진열대 앞 서는 자리(진열대 밑변 가운데 기준). 손님 앞자리(0·0.6, −0.7) 왼쪽
        private static readonly Vector2 k_ShelfStand = new Vector2(-0.6f, -0.7f);
        private const float k_StandSearch = 1f;
        // 딴짓 시간 흔들림: 일머리로 정한 시간 × 0.75~1.25
        private const double k_IdleJitter = 0.5;

        private enum Goal
        {
            Spot,
            Shelf,
            Hole,
        }

        private readonly ClerkConfigTable m_config;
        // 웜뱃이 이 거리 안이면 딴짓 중 「?」(clerk 사물 range)
        private readonly float m_questionRange;
        private int m_skipIdle;
        // 들어오기(한 번) → 바퀴(끝나면 스스로 되돌아가 다시. 2026-09-26 버그: 순서 안에 넣으면 바퀴가 성공할 때마다 처음부터 다시 들어왔다) → 퇴장
        private readonly BtNode<Clerk> m_enter;
        private readonly BtNode<Clerk> m_cycle;
        private readonly BtNode<Clerk> m_leave;
        private bool m_entered;
        private Goal m_goal;
        private ShelfInteractable m_shelf;
        private int m_servedAtStart;
        private bool m_idling;
        private double m_idleLeft;
        // 외출: 구멍에서 톡 뛰는 중 · 돌아오는 중(깨웠거나 시간이 다 됐다. 자리에 돌아와 딴짓이 끝날 때까지)
        private bool m_hopping;
        private bool m_returning;

        public BakeryArea Bakery { get; }
        public Interactable Thing { get; }
        public ClerkTable Role { get; }
        public string Name { get; }
        public int Skill { get; }
        public int Wage { get; }
        public Worker Worker { get; }
        public bool Leaving { get; private set; }
        // 딴짓 중이고 아직 돌아오는 길이 아니다(2026-09-26 버그: 광장에서 깨운 점원이 돌아가는 동안 「?」·「♪」와 깨우기 버튼이 다시 떴다)
        public bool Idling => m_idling && !m_returning;
        public IdleKind Idle { get; private set; }
        // 외출 중(구멍 안, 광장에 그림이 있다)
        public bool Away { get; private set; }
        // 딴짓 종류의 말풍선(광장이 외출 점원에게 띄운다)
        public string IdleBubbleId => IdleBubble();
        // 다음 월급까지 남은 초(곳이 센다)
        internal double UntilPay { get; set; }

        // 자리에 붙어 일하는 중(걷기·딴짓·퇴장 아님). 계산대는 이때만 계산을 돌린다
        public bool Working => !Leaving && !m_idling && !Moving && Vector2.Distance(Position, WorkerSpot) <= k_AtSpot;

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
            m_questionRange = (float)bakery.Tables.Get<InteractableTable>(ClerkInteractable.k_Id).Range;
            UntilPay = m_config.WagePeriodSeconds;
            m_enter = new BtAction<Clerk>(c => c.StartEnter(), (c, dt) => c.TickHopStatus(dt));
            m_cycle = thing is CounterInteractable ? BuildCounterCycle() : BuildOvenCycle();
            m_leave = new BtSequence<Clerk>(
                new BtAction<Clerk>(c => c.StartLeave(), (c, dt) => c.TickWalk()),
                new BtAction<Clerk>(c => c.StartExit(), (c, dt) => c.TickHopStatus(dt)));
        }

        // 그만둔다: 하던 일을 놓고 구멍으로. 외출 중이면 다음 틱에 그대로 사라진다(광장 그림은 광장이 계단으로 보낸다)
        internal void Leave()
        {
            Leaving = true;
            m_idling = false;
            Idle = IdleKind.None;
            Bubble.Show(BubbleTable.k_Angry);
        }

        // 설계 22: 웜뱃이 깨웠다. 딴짓을 끊고(바퀴가 자리로 보낸다) 다음 딴짓 판정을 건너뛴다.
        // 외출 중이면 돌아오라고 하고, 구멍으로 뛰어드는 중이면 광장에 가지 않고 바로 다시 나온다. 돌아오는 동안은 딴짓으로 치지 않는다
        public void WakeUp()
        {
            if (!Idling)
            {
                return;
            }

            m_skipIdle = m_config.WakeSkips;
            Bakery.Bus.Publish(new Events.ClerkWoke(this));

            if (Away || m_hopping)
            {
                RequestReturn();
            }
            else
            {
                EndIdle();
            }

            Bubble.Show(BubbleTable.k_Alert);
        }

        // 광장 그림이 문으로 들어왔다: 구멍에서 톡 나와 딴짓을 끝낸다
        internal void ArriveBack()
        {
            if (!Away)
            {
                return;
            }

            Away = false;
            m_hopping = true;
            StartHop(VisitorPhase.Entering, Bakery.Layout.HoleInside, Bakery.Layout.HoleFloor);
        }

        // 배치가 바뀌었다: 걷는 중이면 같은 목표로 새 길, 자리에 서 있었으면 옮겨진 자리로(하던 기다림은 걸으면서 이어진다)
        internal void Repath()
        {
            if (Away || m_hopping)
            {
                return;
            }

            if (Moving || m_goal == Goal.Spot && !Leaving)
            {
                WalkToGoal(m_goal);
            }
        }

        protected override bool Act(double dt)
        {
            if (Leaving)
            {
                return !Away && m_leave.Tick(this, dt) == BtStatus.Running;
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

        // 확률 (100 − 일머리)/100로 딴짓. 깨운 뒤에는 판정을 건너뛴다. 시간 = (min + (max − min) × (100 − 일머리)/100) × 0.75~1.25.
        // 종류: 딴짓 중인 다른 점원이 있으면 수다, 아니면 한 난수로 외출(outingChance) → 산책(strollChance) → 멍
        private bool StartIdle()
        {
            if (m_skipIdle > 0)
            {
                m_skipIdle--;
                return true;
            }

            if (Bakery.Random.NextDouble() >= (100 - Skill) / 100d)
            {
                return true;
            }

            m_idling = true;
            double bySkill = m_config.IdleSecondsMin + (m_config.IdleSecondsMax - m_config.IdleSecondsMin) * (100 - Skill) / 100d;
            m_idleLeft = bySkill * (1d - k_IdleJitter * 0.5 + k_IdleJitter * Bakery.Random.NextDouble());
            BurrowNav nav = Bakery.Layout.WombatNav;
            Clerk other = Bakery.IdlingClerkOther(this);

            if (other != null)
            {
                Idle = IdleKind.Chat;
                float side = Position.X >= other.Position.X ? 1f : -1f;
                Vector2 beside = nav.Snap(other.Position + new Vector2(side * (float)m_config.ChatOffset, 0f));
                Mover.WalkTo(nav, nav.IsWalkable(beside) || !nav.TryNearestFree(beside, _ => false, k_StandSearch, out Vector2 free) ? beside : free, side > 0f ? Facing.Left : Facing.Right);
                Bubble.Show(IdleBubble());
                return true;
            }

            double roll = Bakery.Random.NextDouble();

            if (roll < m_config.OutingChance)
            {
                Idle = IdleKind.Outing;
                WalkToGoal(Goal.Hole);
            }
            else if (roll < m_config.OutingChance + m_config.StrollChance)
            {
                Idle = IdleKind.Stroll;
                float radius = (float)m_config.StrollRadius;
                Vector2 around = WorkerSpot + new Vector2((float)(Bakery.Random.NextDouble() * 2d - 1d) * radius, (float)(Bakery.Random.NextDouble() * 2d - 1d) * radius);
                Vector2 to = nav.Snap(around);

                if (nav.IsWalkable(to) || nav.TryNearestFree(to, _ => false, radius, out to))
                {
                    Mover.WalkTo(nav, to, Facing.Down);
                }
            }
            else
            {
                Idle = IdleKind.Daze;
            }

            Bubble.Show(IdleBubble());
            return true;
        }

        // 딴짓 중: 웜뱃이 가까이 오면 그쪽을 보며 「?」, 멀어지면 딴짓 말풍선으로. 시간이 다 되면 끝. 외출은 따로
        private BtStatus TickIdle(double dt)
        {
            if (!m_idling)
            {
                return BtStatus.Success;
            }

            m_idleLeft -= dt;

            if (Idle == IdleKind.Outing)
            {
                return TickOuting(dt);
            }

            ShowIdleBubble();

            if (m_idleLeft > 0d)
            {
                return BtStatus.Running;
            }

            EndIdle();
            return BtStatus.Success;
        }

        // 외출: 구멍으로 걸어가 톡 뛰어들어 Away(광장이 그림을 세운다) → 시간이 다 되거나 깨우면 돌아오라고 → 광장 그림이 문으로 들어오면 ArriveBack → 구멍에서 톡 나와 끝
        private BtStatus TickOuting(double dt)
        {
            if (m_hopping)
            {
                if (!TickHop(dt))
                {
                    return BtStatus.Running;
                }

                m_hopping = false;

                if (Phase == VisitorPhase.Exiting && m_returning)
                {
                    // 뛰어드는 중에 깨웠다: 광장에 가지 않고 바로 다시 나온다
                    m_hopping = true;
                    StartHop(VisitorPhase.Entering, Bakery.Layout.HoleInside, Bakery.Layout.HoleFloor);
                    return BtStatus.Running;
                }

                if (Phase == VisitorPhase.Exiting)
                {
                    Away = true;
                    Bakery.Bus.Publish(new Events.ClerkWentOut(this));

                    if (m_idleLeft <= 0d)
                    {
                        RequestReturn();
                    }

                    return BtStatus.Running;
                }

                EndIdle();
                return BtStatus.Success;
            }

            if (Away)
            {
                if (!m_returning && m_idleLeft <= 0d)
                {
                    RequestReturn();
                }

                return BtStatus.Running;
            }

            if (Moving)
            {
                ShowIdleBubble();
                return BtStatus.Running;
            }

            m_hopping = true;
            StartHop(VisitorPhase.Exiting, Bakery.Layout.HoleFloor, Bakery.Layout.HoleInside);
            return BtStatus.Running;
        }

        // 돌아오는 길엔 딴짓 말풍선을 지운다(깨웠으면 WakeUp이 「!」를 띄운다). 광장 그림은 ClerkReturning을 받고 문으로 간다
        private void RequestReturn()
        {
            m_returning = true;
            Bubble.Clear();

            if (Away)
            {
                Bakery.Bus.Publish(new Events.ClerkReturning(this));
            }
        }

        private void ShowIdleBubble()
        {
            Vector2 wombat = Bakery.Wombat.Mover.Position;

            if (Bakery.WombatPresent && Vector2.Distance(wombat, Position) <= m_questionRange)
            {
                Bubble.Show(BubbleTable.k_Question);

                if (!Moving)
                {
                    Mover.Facing = Mover.FacingOf(wombat - Position);
                }
            }
            else
            {
                Bubble.Show(IdleBubble());
            }
        }

        private string IdleBubble()
        {
            switch (Idle)
            {
                case IdleKind.Stroll:
                case IdleKind.Outing:
                    return BubbleTable.k_Note;
                case IdleKind.Chat:
                    return BubbleTable.k_Chat;
                default:
                    return BubbleTable.k_Wait;
            }
        }

        private void EndIdle()
        {
            m_idling = false;
            m_returning = false;
            Phase = VisitorPhase.Queued;
            Idle = IdleKind.None;
            Bubble.Clear();
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
