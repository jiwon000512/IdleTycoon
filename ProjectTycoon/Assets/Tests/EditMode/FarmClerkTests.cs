using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 38 검증: 농장 점원(작업대 자리 · 익은 밭 거두기 · 빈 밭에 고른 작물 · 웜뱃이 먼저 거둔 밭), 모든 곳 공통 월급날, 농장 문 외출. 실제 JSON 값
    public sealed class FarmClerkTests
    {
        private const double k_Dt = 0.02;
        private const string k_Wheat = "wheat";
        private const string k_Strawberry = "strawberry";

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

        private TableSet m_tables;
        private EventBus m_bus;
        private ZooState m_state;
        private BakeryArea m_shop;
        private FarmArea m_farm;
        private Mall m_mall;

        private FarmConfigTable Config => m_tables.Get<FarmConfigTable>(FarmConfigTable.k_Main);

        // 난수 1: 일머리 100(딴짓 없음), 덤 · 똥 없음. 난수 0: 일머리 1(매 바퀴 딴짓, 종류는 외출). 웜뱃은 빵집에 있다(농장 밭을 밟지 않는다)
        private void Create(double roll, Action<TableSet> tweak = null)
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            tweak?.Invoke(m_tables);
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            m_state.TrySpendItem(Config.ManureItem, m_state.Count(Config.ManureItem));
            IRandom random = new ConstantRandom(roll);
            Wombat wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, random, wombat, m_bus);
            PlazaArea plaza = new PlazaArea(m_tables, m_shop, random, wombat, m_bus);
            m_farm = new FarmArea(m_tables, random, wombat, m_bus);
            m_mall = new Mall(m_shop, plaza, m_farm, m_bus);
        }

        private bool RunUntil(Func<bool> done, double maxSeconds = 60d)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return true;
                }

                m_mall.Tick(k_Dt);
            }

            return done();
        }

        private void Run(double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }
        }

        private static Clerk Hire(WombatArea area, Interactable thing)
        {
            Candidate candidate = area.Candidates[0];
            Assert.That(area.TryHire(candidate, thing, area.WageFor(candidate, thing)), Is.True);
            return area.ClerkOf(thing);
        }

        private IEnumerable<PlotInteractable> Tilled => m_farm.Plots.Where(plot => plot.IsTilled);

        // 작업대 자리는 농장 걷는 땅이고, 점원 자리 목록은 빵집 오븐 · 계산대, 농장 작업대
        [Test]
        public void Barn_IsTheFarmSlot_AndItsSpotIsWalkable()
        {
            Create(1d);
            Vector2 spot = Placement.SpotOf(m_farm.Barn, SpotRole.Worker);

            Assert.That(m_farm.Layout.Nav.IsWalkable(spot), Is.True);
            Assert.That(m_farm.ClerkSlots, Is.EqualTo(new Interactable[] { m_farm.Barn }));
            Assert.That(m_shop.ClerkSlots, Is.EqualTo(m_shop.Ovens.Cast<Interactable>().Concat(m_shop.Counters)));
            Assert.That(m_mall.Plaza.ClerkSlots, Is.Empty);
        }

        // 빈 밭에 고른 작물을 심고(처음은 첫 작물, 해금한 작물로 바꿀 수 있다), 익으면 거둬 창고로, 거둔 밭에 다시 심고, 할 일이 없으면 작업대 앞에 선다
        [Test]
        public void FarmClerk_PlantsChosenCrop_HarvestsRipe_AndWaitsAtBarn()
        {
            Create(1d);
            // 웜뱃이 밭 시트로 딸기를 열며 첫 밭에 심는다
            PlotInteractable first = Tilled.First();
            m_state.AddCoins(m_tables.Get<CropTable>(k_Strawberry).UnlockCost);
            Assert.That(m_farm.TryChoose(ActionTable.k_Plant, first, k_Strawberry), Is.True);

            Clerk clerk = Hire(m_farm, m_farm.Barn);
            Assert.That(clerk.Product, Is.EqualTo(k_Wheat), "처음은 첫 작물");
            Assert.That(clerk.Products, Is.EqualTo(new[] { k_Wheat, k_Strawberry }));
            Assert.That(clerk.TrySetProduct(k_Strawberry), Is.True);

            Assert.That(RunUntil(() => Tilled.All(plot => !plot.IsEmpty)), Is.True, "빈 밭을 다 심는다");
            Assert.That(Tilled.All(plot => plot.Crop.Id == k_Strawberry), Is.True, "고른 작물");
            Assert.That(RunUntil(() => clerk.Working), Is.True, "할 일이 없으면 작업대 앞");
            Assert.That(Vector2.Distance(clerk.Position, Placement.SpotOf(m_farm.Barn, SpotRole.Worker)), Is.LessThan(0.1f));

            int before = m_state.Count(k_Strawberry);
            CropTable strawberry = m_tables.Get<CropTable>(k_Strawberry);
            Assert.That(RunUntil(() => m_state.Count(k_Strawberry) >= before + strawberry.Yield * Tilled.Count(), strawberry.GrowSeconds + 60d), Is.True, "익은 밭을 거둔다");
            Assert.That(RunUntil(() => Tilled.All(plot => !plot.IsEmpty)), Is.True, "거둔 밭에 다시 심는다");
        }

        // 점원이 가는 동안 웜뱃이 먼저 거두면 닿아서 다시 본다: 두 번 거두지 않고 빈 밭에 심는다
        [Test]
        public void FarmClerk_RechecksPlotTheWombatHarvestedFirst()
        {
            Create(1d, tables => tables.Get<FarmFloorTable>(FarmArea.k_Id).StartFields = 1);
            PlotInteractable plot = Tilled.Single();
            Clerk clerk = Hire(m_farm, m_farm.Barn);
            Assert.That(RunUntil(() => !plot.IsEmpty && clerk.Working), Is.True);

            Assert.That(RunUntil(() => plot.IsRipe && clerk.Moving, m_tables.Get<CropTable>(k_Wheat).GrowSeconds + 10d), Is.True, "익자 밭으로 간다");
            int before = m_state.Count(k_Wheat);
            plot.Harvest(m_state, new ConstantRandom(1d));
            int afterWombat = m_state.Count(k_Wheat);

            Assert.That(RunUntil(() => !plot.IsEmpty), Is.True, "빈 밭에 다시 심는다");
            Assert.That(m_state.Count(k_Wheat), Is.EqualTo(afterWombat), "두 번 거두지 않는다");
            Assert.That(afterWombat, Is.GreaterThan(before));
        }

        // 월급날은 모든 곳 공통: 빵집 · 농장 점원이 같은 날 받고(월급날 사건 한 번), 모자라면 그 점원만 해고
        [Test]
        public void Payroll_PaysBakeryAndFarmTogether_AndFiresWhoItCannotPay()
        {
            Create(1d);
            double period = m_shop.ClerkConfig.WagePeriodSeconds;
            Clerk counter = Hire(m_shop, m_shop.Counter);
            Clerk farmer = Hire(m_farm, m_farm.Barn);
            int paid = 0;
            int paydays = 0;
            List<Clerk> fired = new List<Clerk>();
            m_bus.Subscribe<Events.ClerkPaid>(_ => paid++);
            m_bus.Subscribe<Events.Payday>(_ => paydays++);
            m_bus.Subscribe<Events.ClerkFired>(e => fired.Add(e.Clerk));

            Assert.That(m_mall.Payroll.Clerks, Is.EquivalentTo(new[] { counter, farmer }));
            Run(period + k_Dt);
            Assert.That(paid, Is.EqualTo(2));
            Assert.That(paydays, Is.EqualTo(1));

            // 한 명 월급만 남긴다
            Assert.That(m_state.TrySpendCoins(m_state.Coins), Is.True);
            m_state.AddCoins(Math.Max(counter.Wage, farmer.Wage));
            Assert.That(m_mall.Payroll.Short, Is.True);
            Run(period);
            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(m_mall.Payroll.Clerks.Count(), Is.EqualTo(1));
        }

        // D3: 농장 점원은 광장 농장 문으로 나가 놀다 그 문으로 돌아온다
        [Test]
        public void FarmClerk_Outing_UsesTheFarmDoor()
        {
            Create(0d, tables =>
            {
                ClerkConfigTable config = tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
                config.IdleSecondsMin = 6d;
                config.IdleSecondsMax = 6d;
            });
            Clerk clerk = Hire(m_farm, m_farm.Barn);

            Assert.That(RunUntil(() => clerk.Away), Is.True, "외출");
            PlazaVisitor figure = m_mall.Plaza.Visitors.Single(visitor => visitor.Clerk == clerk);
            Assert.That(RunUntil(() => !figure.Hopping, 2d), Is.True);
            Assert.That(Vector2.Distance(figure.Position, m_mall.Plaza.Layout.DoorTo(FarmArea.k_Id).Floor), Is.LessThan(0.1f), "농장 문에서 나온다");

            Assert.That(RunUntil(() => !clerk.Away && !clerk.Idling), Is.True, "농장 문으로 돌아와 다시 일한다");
            Assert.That(m_mall.Plaza.Visitors.Any(visitor => visitor.Clerk == clerk), Is.False);
        }
    }
}
