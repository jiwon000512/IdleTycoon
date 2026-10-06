using System;
using System.Collections.Generic;
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

    // 설계 21: 점원 한 명 = 사물 하나(오븐·계산대·농장 작업대)에 붙은 일꾼. 외형·걷기·톡 뛰기는 손님과 같은 뼈대(Visitor), 할 일은 자기 행동 트리.
    // 설계 38: 일하는 곳(Home)은 빵집 · 농장 어디든, 만들 것(Product: 구울 빵 · 심을 작물)은 플레이어가 고른다.
    // 오븐 점원: 자리로 → 고른 빵 굽기 → 다 구울 때까지 → 꺼내기 → 진열대로 → 채우기 → 딴짓 판정 → (반복).
    // 계산 점원: 자리로 → 한 명 계산될 때까지 서 있기(계산은 계산대가 돌린다) → 딴짓 판정 → (반복).
    // 농장 점원: 작업대 앞으로 → 익은 밭 · 빈 밭이 생길 때까지 → 가까운 밭부터 돌며 거두고 고른 작물을 심기(웜뱃과 같은 메서드) → 작업대 앞에서 딴짓 판정 → (반복).
    // 딴짓 확률은 (100 − 일머리)/100, 시간은 일머리가 낮을수록 길다(2026-09-26 사용자: 1분도 넘을 수 있다).
    // 설계 22: 딴짓 중엔 곳이 ClerkInteractable로 감싸 웜뱃이 깨울 수 있다(WakeUp: 딴짓을 끊고 다음 판정 wakeSkips회 건너뜀). 외출(Away) 중엔 광장이 같은 점원을 손님 그림으로 대신 세우고 거기서 깨운다.
    // 해고되면 구멍으로 걸어가 톡 사라진다(외출 중이면 그 자리에서 사라지고 광장 그림이 계단으로 간다)
    public sealed class Clerk : Visitor
    {
        private const float k_AtSpot = 0.05f;
        // 진열대 앞 서는 자리(진열대 밑변 가운데 기준). 손님 앞자리(0·0.6, −0.7) 왼쪽
        private static readonly Vector2 k_ShelfStand = new Vector2(-0.6f, -0.7f);
        // 설계 44: 말뚝 앞에 서는 곳(말뚝 기준점 아래)
        private static readonly Vector2 k_StakeStand = new Vector2(0f, -0.5f);
        private const float k_StandSearch = 1f;
        // 딴짓 시간 흔들림: 일머리로 정한 시간 × 0.75~1.25
        private const double k_IdleJitter = 0.5;

        private enum Goal
        {
            Spot,
            Shelf,
            Plot,
            Stake,
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
        private PlotInteractable m_plot;
        private StakeInteractable m_stake;
        private int m_servedAtStart;
        private bool m_idling;
        private double m_idleLeft;
        // 외출: 구멍에서 톡 뛰는 중 · 돌아오는 중(깨웠거나 시간이 다 됐다. 자리에 돌아와 딴짓이 끝날 때까지)
        private bool m_hopping;
        private bool m_returning;
        // 수다: 상대 · 상대가 불러 세웠다(하던 바퀴를 멈추고 선다) · 멈출 때 걷던 중이었다 · 대화
        private Clerk m_partner;
        private bool m_pulled;
        private bool m_resumeWalk;
        private Dialogue m_chat;

        // 일하는 곳(빵집 · 농장)
        public WombatArea Home { get; }
        protected override WombatArea Area => Home;
        public Interactable Thing { get; }
        public ClerkTable Role { get; }
        public string Name { get; }
        public int Skill { get; }
        public int Wage { get; }
        public Worker Worker { get; }
        // 설계 38: 만들 것 id(오븐 점원은 구울 빵, 농장 점원은 심을 작물). 계산 점원은 null
        public string Product { get; private set; }
        public bool Leaving { get; private set; }
        // 딴짓 중이고 아직 돌아오는 길이 아니다(2026-09-26 버그: 광장에서 깨운 점원이 돌아가는 동안 「?」·「♪」와 깨우기 버튼이 다시 떴다)
        public bool Idling => m_idling && !m_returning;
        public IdleKind Idle { get; private set; }
        // 외출 중(구멍 안, 광장에 그림이 있다)
        public bool Away { get; private set; }
        // 오븐 점원: 재료가 모자라 못 굽고 기다리는 재료(굽기 시작하면 없음). 화면은 기다림 말풍선 동안 이 재료를 글로 띄운다
        public ItemTable Missing { get; private set; }
        // 딴짓 종류의 말풍선(광장이 외출 점원에게 띄운다)
        public string IdleBubbleId => IdleBubble();
        // 자리에 붙어 일하는 중(걷기·딴짓·퇴장 아님). 계산대는 이때만 계산을 돌린다
        // 수다 상대가 될 수 있다: 가게 안에 서 있고 수다·외출·퇴장 중이 아니다
        internal bool CanChat => m_entered && !Leaving && !Away && !m_hopping && !m_returning && m_partner == null && Idle != IdleKind.Outing;
        public bool Working => !Leaving && !m_idling && !Moving && Vector2.Distance(Position, WorkerSpot) <= k_AtSpot;

        public Vector2 WorkerSpot => Placement.SpotOf((IPlaced)Thing, SpotRole.Worker);

        internal Clerk(int id, Candidate candidate, Interactable thing, int wage, WombatArea home) : base(id, candidate.Look, home.HoleInside, home.Tables)
        {
            Home = home;
            Thing = thing;
            Role = home.Tables.Get<ClerkTable>(thing.Table.Id);
            Name = candidate.Name;
            Skill = candidate.Skill;
            Wage = wage;
            Worker = new Worker(new Hands((int)home.Tables.Get<ConfigTable>(ConfigTable.k_CarryCapacity).Value), home.Wombat.Worker.Wallet);
            m_config = home.Tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            m_questionRange = (float)home.Tables.Get<InteractableTable>(ClerkInteractable.k_Id).Range;
            m_enter = new BtAction<Clerk>(c => c.StartEnter(), (c, dt) => c.TickHopStatus(dt));

            // 처음 만들 것: 오븐은 그 오븐의 마지막 빵(없으면 첫 빵), 농장은 첫 작물
            switch (thing)
            {
                case OvenInteractable oven:
                    Product = (oven.LastBread ?? oven.Bakery.UnlockedBreads[0]).Id;
                    m_cycle = BuildOvenCycle();
                    break;
                case BarnInteractable barn:
                    Product = barn.Farm.UnlockedCrops[0].Id;
                    m_cycle = BuildFarmCycle();
                    break;
                case CounterInteractable _:
                    m_cycle = BuildCounterCycle();
                    break;
                case FishingHutInteractable _:
                    m_cycle = BuildFishingCycle();
                    break;
                default:
                    throw new InvalidOperationException($"점원 자리 '{thing.Table.Id}'의 일이 없다.");
            }

            m_leave = new BtSequence<Clerk>(
                new BtAction<Clerk>(c => c.StartLeave(), (c, dt) => c.TickWalk()),
                new BtAction<Clerk>(c => c.StartExit(), (c, dt) => c.TickHopStatus(dt)));
        }

        // 그만둔다: 하던 일을 놓고 구멍으로. 외출 중이면 다음 틱에 그대로 사라진다(광장 그림은 광장이 계단으로 보낸다)
        internal void Leave()
        {
            EndChat();
            Leaving = true;
            m_idling = false;
            Idle = IdleKind.None;
            Bubble.Show(BubbleTable.k_Angry);
        }

        // 설계 38: 고를 수 있는 만들 것(해금된 빵 · 작물 id, 표 순서). 계산 점원은 없다
        public IReadOnlyList<string> Products
        {
            get
            {
                List<string> ids = new List<string>();

                switch (Thing)
                {
                    case OvenInteractable oven:
                        foreach (BreadTable bread in oven.Bakery.UnlockedBreads)
                        {
                            ids.Add(bread.Id);
                        }

                        break;
                    case BarnInteractable barn:
                        foreach (CropTable crop in barn.Farm.UnlockedCrops)
                        {
                            ids.Add(crop.Id);
                        }

                        break;
                }

                return ids;
            }
        }

        // 다음 바퀴부터 고른 것을 만든다(굽는 중인 빵 · 자라는 작물은 그대로)
        public bool TrySetProduct(string id)
        {
            foreach (string product in Products)
            {
                if (product == id)
                {
                    Product = id;
                    return true;
                }
            }

            return false;
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
            Home.Bus.Publish(new Events.ClerkWoke(this));

            if (m_partner != null)
            {
                EndChat();
            }
            else if (Away || m_hopping)
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
            StartHop(VisitorPhase.Entering, Home.HoleInside, Home.HoleFloor);
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

            // 불려 선 동안은 하던 바퀴를 멈춘다(끝나면 멈춘 곳에서 잇는다)
            if (m_pulled)
            {
                ShowIdleBubble();
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

        private static BtNode<Clerk> BuildFarmCycle()
        {
            return new BtSequence<Clerk>(
                new BtAction<Clerk>(c => c.StartWalkToSpot(), (c, dt) => c.TickWalk()),
                new BtAction<Clerk>(null, (c, dt) => c.NextPlot() != null ? BtStatus.Success : BtStatus.Running),
                new BtAction<Clerk>(c => c.StartTend(), (c, dt) => c.TickTend()),
                new BtAction<Clerk>(c => c.StartWalkToSpot(), (c, dt) => c.TickWalk()),
                new BtAction<Clerk>(c => c.StartIdle(), (c, dt) => c.TickIdle(dt)));
        }

        // 설계 44 · 46: 오두막에서 기다리다 대가 월척을 붙잡으면 바로 그 말뚝으로 가서 건지고 돌아와 딴짓 판정(2026-10-06 사용자)
        private static BtNode<Clerk> BuildFishingCycle()
        {
            return new BtSequence<Clerk>(
                new BtAction<Clerk>(c => c.StartWalkToSpot(), (c, dt) => c.TickWalk()),
                new BtAction<Clerk>(null, (c, dt) => c.NextChore() != null ? BtStatus.Success : BtStatus.Running),
                new BtAction<Clerk>(c => c.StartChores(), (c, dt) => c.TickChores()),
                new BtAction<Clerk>(c => c.StartWalkToSpot(), (c, dt) => c.TickWalk()),
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
            StartHop(VisitorPhase.Entering, Home.HoleInside, Home.HoleFloor);
            return true;
        }

        private bool StartExit()
        {
            StartHop(VisitorPhase.Exiting, Home.HoleFloor, Home.HoleInside);
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

        // 빈 오븐이면 고른 빵을 굽는다(2026-09-26 사용자: 기다리지 말고 일해야 한다).
        // 설계 25: 재료가 모자라 못 구우면 「…」를 띄우고, 구우면 지운다
        private BtStatus Bake()
        {
            OvenInteractable oven = (OvenInteractable)Thing;

            if (!oven.IsEmpty)
            {
                return BtStatus.Success;
            }

            BreadTable bread = Home.Tables.Get<BreadTable>(Product);

            if (!oven.TryStart(bread))
            {
                IngredientData lacking = bread.Ingredients.Find(i => oven.Bakery.Wallet.Count(i.Item) < i.Count);
                Missing = lacking != null ? Home.Tables.Get<ItemTable>(lacking.Item) : null;
                Bubble.Show(BubbleTable.k_Wait);
                return BtStatus.Success;
            }

            Missing = null;

            if (Bubble.Id == BubbleTable.k_Wait)
            {
                Bubble.Clear();
            }

            return BtStatus.Success;
        }

        // 재료가 들어올 때까지 빈 오븐을 다시 굽는다
        private BtStatus TickWaitReady()
        {
            OvenInteractable oven = (OvenInteractable)Thing;

            if (oven.IsEmpty)
            {
                Bake();
            }

            return oven.Ready > 0 ? BtStatus.Success : BtStatus.Running;
        }

        private BtStatus TakeOut()
        {
            Home.TryDo(ActionTable.k_TakeOut, Worker, Thing);
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
                Home.TryDo(ActionTable.k_Fill, Worker, m_shelf);
            }

            return BtStatus.Success;
        }

        // 할 일이 있는 가장 가까운 밭(익었거나 갈아 둔 빈 밭). 없으면 null
        private PlotInteractable NextPlot()
        {
            PlotInteractable best = null;

            foreach (PlotInteractable plot in ((BarnInteractable)Thing).Farm.Plots)
            {
                if ((plot.IsRipe || plot.IsTilled && plot.IsEmpty) && (best == null || plot.DistanceTo(Position) < best.DistanceTo(Position)))
                {
                    best = plot;
                }
            }

            return best;
        }

        private bool StartTend()
        {
            WalkToPlot(NextPlot());
            return true;
        }

        // 밭에 닿으면 다시 보고(웜뱃이 먼저 거뒀을 수 있다) 익었으면 거두고 비었으면 고른 작물을 심는다. 다음 밭이 없으면 끝
        private BtStatus TickTend()
        {
            if (Moving)
            {
                return BtStatus.Running;
            }

            if (m_plot != null)
            {
                Tend(m_plot);
            }

            WalkToPlot(NextPlot());
            return m_plot != null ? BtStatus.Running : BtStatus.Success;
        }

        private void Tend(PlotInteractable plot)
        {
            if (plot.IsRipe)
            {
                plot.Harvest(Home.Wombat.Worker.Wallet, Home.Random);
            }

            if (plot.IsTilled && plot.IsEmpty)
            {
                Home.TryChoose(ActionTable.k_Plant, Worker, plot, Product);
            }
        }

        private void WalkToPlot(PlotInteractable plot)
        {
            m_plot = plot;

            if (plot != null)
            {
                WalkToGoal(Goal.Plot);
            }
        }

        // 할 일이 있는 말뚝: 월척을 붙잡은 대. 없으면 null
        private StakeInteractable NextChore()
        {
            return ((FishingHutInteractable)Thing).Fishing.HookedStake();
        }

        private bool StartChores()
        {
            WalkToStake(NextChore());
            return true;
        }

        // 말뚝에 닿으면 다시 보고(웜뱃이 먼저 털썩했을 수 있다) 건진다. 다음 할 일이 없으면 끝
        private BtStatus TickChores()
        {
            if (Moving)
            {
                return BtStatus.Running;
            }

            if (m_stake?.Hooked != null)
            {
                m_stake.Fishing.ClerkHaul(m_stake);
            }

            WalkToStake(NextChore());
            return m_stake != null ? BtStatus.Running : BtStatus.Success;
        }

        private void WalkToStake(StakeInteractable stake)
        {
            m_stake = stake;

            if (stake != null)
            {
                WalkToGoal(Goal.Stake);
            }
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

            if (Home.Random.NextDouble() >= (100 - Skill) / 100d)
            {
                return true;
            }

            m_idling = true;
            double bySkill = m_config.IdleSecondsMin + (m_config.IdleSecondsMax - m_config.IdleSecondsMin) * (100 - Skill) / 100d;
            m_idleLeft = bySkill * (1d - k_IdleJitter * 0.5 + k_IdleJitter * Home.Random.NextDouble());
            // 설계 31 알람 시계: 딴짓이 그 초를 넘지 않는다
            double alarm = Home.Wombat.Worker.Wallet.Relics.Value(RelicTable.k_Clock);
            m_idleLeft = alarm > 0d ? Math.Min(m_idleLeft, alarm) : m_idleLeft;
            BurrowNav nav = Home.ClerkNav;
            double roll = Home.Random.NextDouble();
            Clerk other = roll < m_config.ChatChance ? Home.ChatPartnerFor(this) : null;

            if (other != null)
            {
                StartChat(nav, other);
                return true;
            }

            // 수다가 아니면(상대가 없을 때 포함) 같은 난수를 나머지 딴짓 구간(외출 → 산책 → 멍, 합 1 − chatChance)에 다시 편다
            double rest = 1d - m_config.ChatChance;
            roll = roll < m_config.ChatChance ? roll / m_config.ChatChance : (roll - m_config.ChatChance) / rest;
            roll *= rest;


            // 설계 40: 가게를 지키는 동안(평가 중)은 외출 대신 산책
            if (roll < m_config.OutingChance && Home.CanLeave)
            {
                Idle = IdleKind.Outing;
                WalkToGoal(Goal.Hole);
            }
            else if (roll < m_config.OutingChance + m_config.StrollChance)
            {
                Idle = IdleKind.Stroll;
                float radius = (float)m_config.StrollRadius;
                Vector2 around = WorkerSpot + new Vector2((float)(Home.Random.NextDouble() * 2d - 1d) * radius, (float)(Home.Random.NextDouble() * 2d - 1d) * radius);
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

        // 수다 걸기: 상대를 불러 세우고 그 옆으로 걸어간다. 도착하면 대화(TickChat)
        private void StartChat(BurrowNav nav, Clerk other)
        {
            Idle = IdleKind.Chat;
            m_partner = other;
            other.JoinChat(this);
            float side = Position.X >= other.Position.X ? 1f : -1f;
            Vector2 beside = nav.Snap(other.Position + new Vector2(side * (float)m_config.ChatOffset, 0f));
            Mover.WalkTo(nav, nav.IsWalkable(beside) || !nav.TryNearestFree(beside, _ => false, k_StandSearch, out Vector2 free) ? beside : free, side > 0f ? Facing.Left : Facing.Right);
            Bubble.Clear();
        }

        // 수다에 불렸다: 하던 걸음·딴짓을 멈추고 그 자리에 선다
        private void JoinChat(Clerk from)
        {
            m_partner = from;
            m_pulled = true;
            m_resumeWalk = Moving;
            Mover.Place(Position);
            m_idling = true;
            Idle = IdleKind.Chat;
            Bubble.Clear();
        }

        // 수다 끝(대화 끝 · 둘 중 하나를 깨움 · 해고): 둘 다 딴짓을 끝내고 하던 일로
        private void EndChat()
        {
            Clerk partner = m_partner;

            if (partner == null)
            {
                return;
            }

            Home.StopDialogue(this);
            LeaveChat();
            partner.LeaveChat();
        }

        private void LeaveChat()
        {
            m_partner = null;
            m_chat = null;

            if (m_idling)
            {
                EndIdle();
            }

            if (!m_pulled)
            {
                return;
            }

            m_pulled = false;

            // 걷던 중이었거나 자리에서 일하던 중이면 그 목표로 다시 걷는다(TickWalk·TickServe가 이어진다)
            if (!Leaving && (m_resumeWalk || m_goal == Goal.Spot))
            {
                WalkToGoal(m_goal);
            }
        }

        // 수다 건 쪽: 옆에 닿으면 마주 보고 대화를 시작하고, 대화가 끝나면 둘 다 끝
        private BtStatus TickChat()
        {
            ShowIdleBubble();

            if (Moving)
            {
                return BtStatus.Running;
            }

            if (m_chat == null)
            {
                System.Collections.Generic.List<string> ids = m_config.ChatDialogues;
                string id = ids[Math.Min(ids.Count - 1, (int)(Home.Random.NextDouble() * ids.Count))];
                Mover.Facing = Mover.FacingOf(m_partner.Position - Position);
                m_partner.Mover.Facing = Mover.FacingOf(Position - m_partner.Position);
                m_chat = Home.StartDialogue(id, this, m_partner);
                return BtStatus.Running;
            }

            if (!m_chat.Done)
            {
                return BtStatus.Running;
            }

            EndChat();
            return BtStatus.Success;
        }

        // 딴짓 중: 웜뱃이 가까이 오면 그쪽을 보며 「?」, 멀어지면 딴짓 말풍선으로. 시간이 다 되면 끝. 외출·수다는 따로
        private BtStatus TickIdle(double dt)
        {
            if (!m_idling)
            {
                return BtStatus.Success;
            }

            m_idleLeft -= dt;

            if (m_partner != null)
            {
                return TickChat();
            }

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
                    StartHop(VisitorPhase.Entering, Home.HoleInside, Home.HoleFloor);
                    return BtStatus.Running;
                }

                if (Phase == VisitorPhase.Exiting)
                {
                    Away = true;
                    Home.Bus.Publish(new Events.ClerkWentOut(this));

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
            StartHop(VisitorPhase.Exiting, Home.HoleFloor, Home.HoleInside);
            return BtStatus.Running;
        }

        // 돌아오는 길엔 딴짓 말풍선을 지운다(깨웠으면 WakeUp이 「!」를 띄운다). 광장 그림은 ClerkReturning을 받고 문으로 간다
        private void RequestReturn()
        {
            m_returning = true;
            Bubble.Clear();

            if (Away)
            {
                Home.Bus.Publish(new Events.ClerkReturning(this));
            }
        }

        private void ShowIdleBubble()
        {
            Vector2 wombat = Home.Wombat.Mover.Position;

            if (Home.WombatPresent && Vector2.Distance(wombat, Position) <= m_questionRange)
            {
                // 「?」가 뜬 순간 잠깐 멈춰 웜뱃을 본다(깨우기 쉽게)
                if (Bubble.Id != BubbleTable.k_Question)
                {
                    Hold(m_config.QuestionHoldSeconds);
                }

                Bubble.Show(BubbleTable.k_Question);

                if (!Moving || Held)
                {
                    Mover.Facing = Mover.FacingOf(wombat - Position);
                }
            }
            // 수다는 이모지 없이 대사 말풍선만(말하지 않는 동안 비워 둔다)
            else if (Idle == IdleKind.Chat)
            {
                Bubble.Clear();
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
            BurrowNav nav = Home.ClerkNav;

            switch (goal)
            {
                case Goal.Spot:
                    Mover.WalkTo(nav, WorkerSpot, WorkerFacing());
                    break;
                case Goal.Shelf:
                    Mover.WalkTo(nav, ShelfStand(nav, m_shelf), Facing.Up);
                    break;
                case Goal.Plot:
                    Mover.WalkTo(nav, nav.Snap(((BarnInteractable)Thing).Farm.Layout.Cells.CellCenter(m_plot.Cell)), Facing.Down);
                    break;
                case Goal.Stake:
                    Vector2 stand = nav.Snap(m_stake.Position + k_StakeStand);
                    Mover.WalkTo(nav, nav.IsWalkable(stand) || !nav.TryNearestFree(stand, _ => false, k_StandSearch, out Vector2 free) ? stand : free, Facing.Up);
                    break;
                default:
                    Mover.WalkTo(nav, Home.HoleFloor, Facing.Up);
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

            foreach (ShelfInteractable shelf in ((OvenInteractable)Thing).Bakery.Shelves)
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
