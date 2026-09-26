using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 21 점원 검증 1~6: 계산 점원·오븐 점원 한 바퀴·월급 해고·고용과 후보·협상·웜뱃 건너뛰기/보관/옮기기. 매 프레임 틱 0.02초, 실제 JSON 값
    public sealed class ClerkTests
    {
        private const double k_Dt = 0.02;

        // 같은 값만 돌려주는 난수(1.0 = 일머리 100·딴짓 없음, 0 = 일머리 1·매번 딴짓)
        private sealed class ConstantRandom : IRandom
        {
            private readonly double m_value;

            public ConstantRandom(double value)
            {
                m_value = value;
            }

            public double NextDouble()
            {
                return m_value;
            }
        }

        private EventBus m_bus;
        private ZooState m_state;
        private TableSet m_tables;
        private VisitorTable m_customer;
        private readonly List<Events.ClerkFired> m_fired = new List<Events.ClerkFired>();

        private BakeryArea Create(IRandom random = null)
        {
            m_tables = TestTables.Load();
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            m_fired.Clear();
            m_bus.Subscribe<Events.ClerkFired>(e => m_fired.Add(e));

            foreach (VisitorTable look in m_tables.GetAll<VisitorTable>())
            {
                if (look.Role == VisitorRole.Customer)
                {
                    m_customer = look;
                    break;
                }
            }

            // 딴짓 시간은 일머리로 정해지므로(설계 22) 테스트는 짧게, 외출은 끈다
            ClerkConfigTable config = m_tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            config.IdleSecondsMax = config.IdleSecondsMin;
            config.OutingChance = 0d;
            BakeryArea shop = new BakeryArea(m_state, m_tables, random ?? new ConstantRandom(1d), new Wombat(m_tables, m_state), m_bus);
            // 웜뱃은 계산대 자리에서 시작한다: 점원만 일하게 구멍 아래로 비킨다
            shop.Wombat.Mover.Place(shop.Layout.HoleFloor);
            return shop;
        }

        private static void Run(BakeryArea shop, double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                shop.Tick(k_Dt);
            }
        }

        private static bool RunUntil(BakeryArea shop, Func<bool> done, double maxSeconds = 60d)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return true;
                }

                shop.Tick(k_Dt);
            }

            return done();
        }

        private static Clerk Hire(BakeryArea shop, Interactable thing)
        {
            Candidate candidate = shop.Candidates[0];
            Assert.That(shop.TryHire(candidate, thing, shop.WageFor(candidate, thing)), Is.True);
            return shop.ClerkOf(thing);
        }

        private BreadTable Bread(string id)
        {
            return m_tables.Get<BreadTable>(id);
        }

        // 검증 1: 웜뱃이 자리에 없어도 계산 점원이 줄 머리를 계산해 코인이 는다
        [Test]
        public void CounterClerk_ServesQueueWithoutWombat()
        {
            BakeryArea shop = Create();
            shop.Shelves[0].Put(Bread("b01"), 4);
            Hire(shop, shop.Counter);
            double coins = m_state.Coins;
            shop.Admit(m_customer);

            Assert.That(RunUntil(shop, () => shop.Counter.Served > 0), Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins + Bread("b01").Price));
        }

        // 검증 1: 점원이 없으면 웜뱃이 자리에 없는 계산대는 계산하지 않는다
        [Test]
        public void Counter_WithoutClerkOrWombat_DoesNotServe()
        {
            BakeryArea shop = Create();
            shop.Shelves[0].Put(Bread("b01"), 4);
            shop.Admit(m_customer);

            Assert.That(RunUntil(shop, () => shop.Counter.HeadWaiting), Is.True);
            Run(shop, 5d);
            Assert.That(shop.Counter.Served, Is.EqualTo(0));
        }

        // 검증 2: 마지막 빵이 있는 빈 오븐에서 굽기 → 꺼내기 → 진열대 채우기 → 다시 굽기
        [Test]
        public void OvenClerk_BakesTakesOutFillsAndRepeats()
        {
            BakeryArea shop = Create();
            OvenInteractable oven = shop.Ovens[0];
            Assert.That(shop.TryChoose(ActionTable.k_Bake, oven, "b01"), Is.True);
            Clerk clerk = Hire(shop, oven);

            Assert.That(RunUntil(shop, () => shop.StockOf(Bread("b01")) >= Bread("b01").BatchSize), Is.True);
            Assert.That(clerk.Worker.Hands.Count, Is.EqualTo(0));
            Assert.That(RunUntil(shop, () => !oven.IsEmpty && oven.Ready == 0, 20d), Is.True, "다시 굽지 않았다");
        }

        // 2026-09-26 사용자 버그: 한 바퀴가 끝날 때마다 구멍에서 다시 나왔다. 들어온 뒤로는 톡 뛰기·구멍 안 위치가 다시 없어야 한다
        [Test]
        public void Clerk_AfterEntering_NeverHopsAgainWhileWorking()
        {
            BakeryArea shop = Create();
            OvenInteractable oven = shop.Ovens[0];
            Assert.That(shop.TryChoose(ActionTable.k_Bake, oven, "b01"), Is.True);
            Clerk clerk = Hire(shop, oven);
            Assert.That(RunUntil(shop, () => clerk.Working), Is.True);
            int cycles = 0;
            int stock = 0;

            for (double t = 0d; t < 60d; t += k_Dt)
            {
                shop.Tick(k_Dt);
                Assert.That(clerk.Hopping, Is.False, "다시 톡 뛰었다");
                Assert.That(Vector2.Distance(clerk.Position, shop.Layout.HoleInside), Is.GreaterThan(0.5f), "구멍 안으로 돌아갔다");

                if (shop.StockOf(Bread("b01")) > stock)
                {
                    cycles++;
                }

                stock = shop.StockOf(Bread("b01"));
                shop.Shelves[0].TryPick();
            }

            Assert.That(cycles, Is.GreaterThan(1), "바퀴가 두 번 이상 돌아야 한다");
        }

        // 2026-09-26 사용자: 오븐 자리로 갈 때 마지막 한 걸음(격자 점 → 격자 밖 자리, 0.09)에서 옆모습이 끼어들었다. 자리 가까이에서는 옆을 보지 않는다
        [Test]
        public void Clerk_ApproachingSpot_DoesNotTurnSidewaysOnLastStep()
        {
            BakeryArea shop = Create();
            Clerk clerk = Hire(shop, shop.Ovens[0]);
            bool sideways = false;

            for (double t = 0d; t < 30d && !clerk.Working; t += k_Dt)
            {
                shop.Tick(k_Dt);

                if (clerk.Moving && Vector2.Distance(clerk.Position, clerk.WorkerSpot) < 0.3f)
                {
                    sideways |= clerk.Facing == Facing.Left || clerk.Facing == Facing.Right;
                }
            }

            Assert.That(clerk.Working, Is.True);
            Assert.That(sideways, Is.False, "자리 앞에서 옆모습이 됐다");
        }

        // 2026-09-26 사용자 버그: 진열대가 가득 차면 자리에서 좌우로 떨렸다. 든 채로 자리에 서서 기다리다 자리가 나면 채운다(그동안 오븐은 계속 굽는다)
        [Test]
        public void OvenClerk_WhenShelvesFull_WaitsStillAndFillsWhenRoomAppears()
        {
            BakeryArea shop = Create();
            ShelfInteractable shelf = shop.Shelves[0];
            shelf.Put(Bread("b01"), shelf.Capacity);
            Clerk clerk = Hire(shop, shop.Ovens[0]);
            Assert.That(RunUntil(shop, () => clerk.Worker.Hands.Count > 0), Is.True);
            Assert.That(RunUntil(shop, () => !clerk.Moving && clerk.Working), Is.True);
            Vector2 at = clerk.Position;

            for (double t = 0d; t < 5d; t += k_Dt)
            {
                shop.Tick(k_Dt);
                Assert.That(Vector2.Distance(clerk.Position, at), Is.LessThan(1e-4f), "자리에서 움직였다");
            }

            Assert.That(clerk.Worker.Hands.Count, Is.GreaterThan(0));
            Assert.That(shop.Ovens[0].IsEmpty, Is.False, "기다리는 동안 오븐이 놀았다");
            shelf.TryPick();
            shelf.TryPick();
            Assert.That(RunUntil(shop, () => shelf.Stock == shelf.Capacity, 20d), Is.True);
        }

        // 2026-09-26 사용자: 아직 구운 적 없는 오븐의 점원은 해금된 첫 빵을 굽는다. 웜뱃이 다른 빵을 고르면 그 빵으로
        [Test]
        public void OvenClerk_WithoutLastBread_BakesFirstBread_ThenFollowsWombatChoice()
        {
            BakeryArea shop = Create();
            OvenInteractable oven = shop.Ovens[0];
            Clerk clerk = Hire(shop, oven);

            Assert.That(RunUntil(shop, () => !oven.IsEmpty), Is.True);
            Assert.That(oven.Bread, Is.EqualTo(Bread("b01")));
            Assert.That(RunUntil(shop, () => shop.StockOf(Bread("b01")) > 0), Is.True);

            // 웜뱃이 다음 빵을 열어 굽게 하면 그 뒤 바퀴는 그 빵
            Assert.That(RunUntil(shop, () => oven.IsEmpty), Is.True);
            m_state.AddCoins(shop.NextBread.UnlockCost);
            Assert.That(shop.TryChoose(ActionTable.k_Bake, oven, shop.NextBread.Id), Is.True);
            BreadTable second = oven.Bread;
            Assert.That(second, Is.Not.EqualTo(Bread("b01")));
            // 점원이 꺼내면(진열대에 식빵이 있어 든 채 기다린다) 그 자리에서 같은 빵을 다시 굽는다
            Assert.That(RunUntil(shop, () => clerk.Worker.Hands.Bread == second, 30d), Is.True);
            Assert.That(RunUntil(shop, () => !oven.IsEmpty && oven.Bread == second, 20d), Is.True, "다음 바퀴도 그 빵");
        }

        // 검증 2: 일머리 1이면 한 바퀴 뒤 딴짓(Looking), 일머리 100이면 없음
        [Test]
        public void Clerk_LowSkill_IdlesAfterCycle()
        {
            BakeryArea shop = Create(new ConstantRandom(0d));
            shop.Shelves[0].Put(Bread("b01"), 4);
            Clerk clerk = Hire(shop, shop.Counter);
            Assert.That(clerk.Skill, Is.EqualTo(1));
            shop.Admit(m_customer);

            Assert.That(RunUntil(shop, () => shop.Counter.Served > 0), Is.True);
            Assert.That(RunUntil(shop, () => clerk.Idling, 2d), Is.True);
            Assert.That(clerk.Working, Is.False);
        }

        // 검증 3: 주기마다 월급을 빼고, 모자라면 그 점원만 해고된다
        [Test]
        public void Payroll_FiresOnlyTheClerkItCannotPay()
        {
            BakeryArea shop = Create();
            double period = shop.ClerkConfig.WagePeriodSeconds;
            Clerk ovenClerk = Hire(shop, shop.Ovens[0]);
            Clerk counterClerk = Hire(shop, shop.Counter);
            Assert.That(m_state.TrySpendCoins(m_state.Coins), Is.True);
            m_state.AddCoins(counterClerk.Wage);

            Run(shop, period + k_Dt);

            Assert.That(shop.ClerkOf(shop.Counter), Is.EqualTo(counterClerk));
            Assert.That(shop.ClerkOf(shop.Ovens[0]), Is.Null);
            Assert.That(m_fired.Count, Is.EqualTo(1));
            Assert.That(m_fired[0].Clerk, Is.EqualTo(ovenClerk));
            Assert.That(m_fired[0].Reason, Is.EqualTo(FireReason.Unpaid));
            Assert.That(m_state.Coins, Is.EqualTo(0d));
        }

        // 검증 3: 월급이 빠지고 나면 다음 주기까지 다시 빠지지 않는다
        [Test]
        public void Payroll_ChargesOncePerPeriod()
        {
            BakeryArea shop = Create();
            Clerk clerk = Hire(shop, shop.Counter);
            double coins = m_state.Coins;

            Run(shop, shop.ClerkConfig.WagePeriodSeconds - 1d);
            Assert.That(m_state.Coins, Is.EqualTo(coins));
            Run(shop, 2d);
            Assert.That(m_state.Coins, Is.EqualTo(coins - clerk.Wage));
        }

        // 검증 4: 첫 월급을 내고 고용, 후보는 빠진 자리가 채워져 5명, 찬 자리·모자란 코인이면 실패
        [Test]
        public void Hire_SpendsFirstWage_RefillsCandidates_RejectsOccupiedOrPoor()
        {
            BakeryArea shop = Create();
            int count = shop.ClerkConfig.CandidateCount;
            Assert.That(shop.Candidates.Count, Is.EqualTo(count));
            Candidate first = shop.Candidates[0];
            int wage = shop.WageFor(first, shop.Counter);
            double coins = m_state.Coins;

            Assert.That(shop.TryHire(first, shop.Counter, wage), Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins - wage));
            Assert.That(shop.Candidates.Count, Is.EqualTo(count));
            Assert.That(shop.Clerks[0].Name, Is.EqualTo(first.Name));
            Assert.That(shop.Clerks[0].Wage, Is.EqualTo(wage));

            Assert.That(shop.TryHire(shop.Candidates[0], shop.Counter, 1), Is.False, "찬 자리");
            Assert.That(m_state.TrySpendCoins(m_state.Coins), Is.True);
            Assert.That(shop.TryHire(shop.Candidates[0], shop.Ovens[0], 1), Is.False, "코인 부족");
            Assert.That(shop.TryHire(shop.Candidates[0], shop.Shelves[0], 0), Is.False, "진열대에는 점원이 없다");
        }

        // 검증 4: 새 후보 보기는 refreshCost를 내고 후보를 갈아 준다
        [Test]
        public void RefreshCandidates_SpendsCostAndKeepsCount()
        {
            BakeryArea shop = Create();
            double coins = m_state.Coins;

            Assert.That(shop.TryRefreshCandidates(), Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins - shop.ClerkConfig.RefreshCost));
            Assert.That(shop.Candidates.Count, Is.EqualTo(shop.ClerkConfig.CandidateCount));
        }

        // 검증 4: 일머리 = 1 + ⌊99 × r²⌋ — r 0.5면 25, 0.9면 81. 기본 월급은 일머리에 비례
        [Test]
        public void Candidate_SkillFollowsSkew_WageFollowsSkill()
        {
            // 후보마다 (이름, 일머리, 외형) 순으로 난수 3개
            BakeryArea shop = Create(new SequenceRandom(0d, 0.5, 0d, 0d, 0.9, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d));
            IReadOnlyList<Candidate> candidates = shop.Candidates;

            Assert.That(candidates[0].Skill, Is.EqualTo(25));
            Assert.That(candidates[1].Skill, Is.EqualTo(81));
            Assert.That(candidates[2].Skill, Is.EqualTo(1));
            Assert.That(candidates[0].Name, Is.Not.EqualTo(candidates[1].Name));
            Assert.That(candidates[0].Look.Role, Is.EqualTo(VisitorRole.Clerk));
            double baseWage = m_tables.Get<ClerkTable>(OvenInteractable.k_Id).BaseWage;
            Assert.That(shop.WageFor(candidates[0], shop.Ovens[0]), Is.EqualTo((int)Math.Round(baseWage * (1d + 0.25 * shop.ClerkConfig.WagePerSkill))));
        }

        // 검증 5: 멈춘 색별 결과와 변동 금액, 시간 초과, 표시 속도
        [Test]
        public void Negotiation_CenterIsGreen_EdgeIsRed_Timeout()
        {
            ClerkConfigTable config = TestTables.Load().Get<ClerkConfigTable>(ClerkConfigTable.k_Main);

            // 시작 위치 0 = 빨강. 확률 난수 0.99 → 인상, 금액 난수 0.5 → 20 × (0.1 + 0.5 × 0.3) = 5
            Negotiation red = new Negotiation(config, new SequenceRandom(0.99, 0.5), 1, 20);
            red.Stop();
            Assert.That(red.Zone, Is.EqualTo(ZoneColor.Red));
            Assert.That(red.Outcome, Is.EqualTo(NegotiationOutcome.Up));
            Assert.That(red.Wage, Is.EqualTo(25));

            // 가운데 = 초록. 0.99 → 인하, 금액 난수 0 → 2
            Negotiation green = new Negotiation(config, new SequenceRandom(0.99, 0d), 1, 20);
            green.Tick(0.5 / config.MarkerSpeedMin);
            Assert.That(green.Marker, Is.EqualTo(0.5).Within(1e-6));
            green.Stop();
            Assert.That(green.Zone, Is.EqualTo(ZoneColor.Green));
            Assert.That(green.Outcome, Is.EqualTo(NegotiationOutcome.Down));
            Assert.That(green.Wage, Is.EqualTo(18));

            // 흰색(가운데에서 0.2): 0.9 → 유지 80 밖 → 인하
            Negotiation white = new Negotiation(config, new SequenceRandom(0.9, 0d), 1, 20);
            white.Tick(0.3 / config.MarkerSpeedMin);
            white.Stop();
            Assert.That(white.Zone, Is.EqualTo(ZoneColor.White));
            Assert.That(white.Outcome, Is.EqualTo(NegotiationOutcome.Down));

            // 시간 초과 = 빨강, 0 → 유지
            Negotiation late = new Negotiation(config, new SequenceRandom(0d, 0d), 1, 20);
            late.Tick(config.NegotiateSeconds + 0.1);
            Assert.That(late.Done, Is.True);
            Assert.That(late.Zone, Is.EqualTo(ZoneColor.Red));
            Assert.That(late.Wage, Is.EqualTo(20));

            // 일머리 100의 표시가 더 빠르다
            Negotiation slow = new Negotiation(config, new SequenceRandom(), 1, 20);
            Negotiation fast = new Negotiation(config, new SequenceRandom(), 100, 20);
            slow.Tick(0.3);
            fast.Tick(0.3);
            Assert.That(fast.Marker, Is.GreaterThan(slow.Marker));
        }

        // 검증 6: 점원 있는 오븐의 다 구운 빵은 웜뱃이 range 안에 있어도 저절로 꺼내지 않는다
        [Test]
        public void Wombat_SkipsAutoActionsOnStaffedThing()
        {
            BakeryArea shop = Create();
            OvenInteractable oven = shop.Ovens[0];
            Assert.That(shop.TryChoose(ActionTable.k_Bake, oven, "b01"), Is.True);
            Clerk clerk = Hire(shop, oven);
            // 점원이 자리에 닿기 전에 웜뱃이 오븐 앞에 선다(둘 다 range 안)
            Assert.That(RunUntil(shop, () => oven.Ready > 0), Is.True);
            shop.Wombat.Mover.Place(oven.Position + new Vector2(0.9f, 0.8f));
            shop.Tick(k_Dt);

            Assert.That(shop.Wombat.Worker.Hands.Count, Is.EqualTo(0));
            Assert.That(RunUntil(shop, () => clerk.Worker.Hands.Count > 0 || shop.StockOf(Bread("b01")) > 0, 20d), Is.True);
        }

        // 검증 6: 사물을 보관하면 점원은 그만두고, 옮기면 새 자리로 간다
        [Test]
        public void Store_FiresClerk_Move_RelocatesClerk()
        {
            BakeryArea shop = Create();
            OvenInteractable oven = shop.Ovens[0];
            Clerk clerk = Hire(shop, oven);
            Assert.That(RunUntil(shop, () => clerk.Working), Is.True);

            // 왼쪽으로(오른쪽은 계산대 바닥과 겹친다)
            Vector2 to = oven.Position + new Vector2(-0.4f, 0f);
            Assert.That(shop.TryMove(oven, to), Is.True);
            Assert.That(clerk.Working, Is.False);
            Assert.That(RunUntil(shop, () => clerk.Working, 10d), Is.True);
            Assert.That(Vector2.Distance(clerk.Position, clerk.WorkerSpot), Is.LessThan(0.06f));

            Assert.That(shop.TryStore(oven), Is.True);
            Assert.That(shop.ClerkOf(oven), Is.Null);
            Assert.That(shop.Clerks.Count, Is.EqualTo(0));
            Assert.That(m_fired[0].Reason, Is.EqualTo(FireReason.Stored));
            int left = 0;
            m_bus.Subscribe<Events.ClerkLeft>(_ => left++);
            Assert.That(RunUntil(shop, () => left == 1, 20d), Is.True, "구멍으로 나가 사라진다");
        }
    }
}
