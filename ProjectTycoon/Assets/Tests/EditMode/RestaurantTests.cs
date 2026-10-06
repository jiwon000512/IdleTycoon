using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;
using ZooTycoon.Data;

namespace ZooTycoon.Tests
{
    // 설계 47 검증: 수조 횟집. 광장 문 열기(빵집 ★5 · 값) · 수조 채우기 · 손님 한 바퀴(고르기 → 웜뱃이 물고기를 꺼내 도마에 → 뜨기 → 접시 → 먹기 → 값) · 기다리다 화남 ·
    // 도마는 자리에 붙어야 · 광장 손님 · 저장. 웜뱃을 사물 곁에 세우는 실제 길로 다룬다. 횟집 난수는 늘 0.3
    public sealed class RestaurantTests
    {
        private const double k_Dt = 0.05;

        private sealed class FixedRandom : IRandom
        {
            private readonly double m_value;

            public FixedRandom(double value)
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
        private PlazaArea m_plaza;
        private RestaurantArea m_restaurant;
        private Mall m_mall;

        private Worker Worker => m_mall.Wombat.Worker;
        private TankInteractable Tank => m_restaurant.Tanks[0];
        private CuttingBoardInteractable Board => m_restaurant.Boards[0];

        // 빵집 손님은 받지 않는다(광장 손님이 횟집 차례만 남게)
        private void Create()
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            m_state.AddCoins(100000d);
            Wombat wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, new SequenceRandom(new double[4000]), wombat, m_bus);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 40000).ToArray()), wombat, m_bus);
            m_restaurant = new RestaurantArea(m_state, m_tables, new FixedRandom(0.3), wombat, m_bus);
            m_mall = new Mall(m_shop, m_plaza, new FarmArea(m_tables, new FixedRandom(0.99), wombat, m_bus), m_bus, null, m_restaurant);
        }

        private void Run(double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }
        }

        private bool RunUntil(Func<bool> done, double maxSeconds)
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

        private ShopGateInteractable Gate => m_plaza.Gates.Single();

        private PassageInteractable Door => m_plaza.Things.OfType<PassageInteractable>().Single(p => p.To == RestaurantArea.k_Id);

        // 빵집 별을 openStar까지 채우고 광장 문 앞 시트에서 연다
        private void Open()
        {
            while (m_restaurant.StarLocked)
            {
                m_shop.Evaluation.PassNow();
            }

            Assert.That(m_plaza.TryChoose(ActionTable.k_OpenShop, Gate, null), Is.True, "횟집 열기");
            Run(k_Dt);
        }

        private void GoIn()
        {
            m_bus.Publish(new Events.Passed(m_shop, PlazaArea.k_Id));
            m_bus.Publish(new Events.Passed(m_plaza, RestaurantArea.k_Id));
            Run(k_Dt);
        }

        // 창고 물고기를 정해진 수로
        private void SetFish(int minnow, int crucian, int catfish)
        {
            foreach ((string item, int count) in new[] { ("fish_minnow", minnow), ("fish_crucian", crucian), ("fish_catfish", catfish) })
            {
                m_state.TrySpendItem(item, m_state.Count(item));
                m_state.AddItem(item, count);
            }
        }

        private void Stand(Vector2 at)
        {
            m_mall.Wombat.Mover.Place(at);
            Run(k_Dt);
        }

        private RestaurantVisitor Admit()
        {
            m_restaurant.Admit(m_tables.GetAll<VisitorTable>().First(look => look.Role == VisitorRole.Customer));
            return m_restaurant.Visitors.Last();
        }

        [Test]
        public void Door_IsShut_UntilBakeryStarFive_ThenOpensForCoins()
        {
            Create();
            Assert.That(m_restaurant.IsOpen, Is.False);
            Assert.That(Door.IsOpen, Is.False, "닫힌 동안 문 통로가 막힌다");
            SheetOption locked = m_plaza.SheetActions(Gate).Single().Options(Worker, Gate)[0];
            Assert.That(locked.State, Is.EqualTo(SheetOptionState.Locked), "별이 모자라면 잠김");
            Assert.That(m_plaza.TryChoose(ActionTable.k_OpenShop, Gate, null), Is.False);

            int opened = 0;
            m_bus.Subscribe<Events.ShopOpened>(_ => opened++);
            double coins = m_state.Coins;
            Open();

            Assert.That(m_restaurant.IsOpen, Is.True);
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(coins - m_state.Coins, Is.EqualTo(m_restaurant.Config.OpenCost).Within(1e-6), "문 값(평가 보상은 반짝돌이라 코인과 따로)");
            Assert.That(m_plaza.Gates, Is.Empty, "열리면 문 앞 표식이 빠진다");
            Assert.That(Door.IsOpen, Is.True);
        }

        [Test]
        public void Plaza_SendsCustomers_OnlyAfterTheDoorOpens()
        {
            Create();
            Run(40d);
            Assert.That(m_restaurant.Visitors, Is.Empty, "닫힌 횟집에는 손님이 없다");

            Open();
            Assert.That(RunUntil(() => m_restaurant.Visitors.Count > 0, 60d), Is.True, "연 뒤에는 광장 손님이 온다");
        }

        [Test]
        public void Tank_FillsFromStorage_MostStockedFirst_UpToCapacity()
        {
            Create();
            Open();
            GoIn();
            SetFish(3, 4, 0);
            Stand(Tank.Position + new Vector2(0f, -0.6f));

            Assert.That(Tank.Stock, Is.EqualTo(Tank.Capacity), "용량까지");
            Assert.That(Tank.CountOf(m_tables.Get<DishTable>("dish_crucian")), Is.EqualTo(3), "가장 많은 종류부터 하나씩");
            Assert.That(Tank.CountOf(m_tables.Get<DishTable>("dish_minnow")), Is.EqualTo(3));
            Assert.That(m_state.Count("fish_crucian"), Is.EqualTo(1));
            Assert.That(m_state.Count("fish_minnow"), Is.EqualTo(0));
        }

        [Test]
        public void Customer_PicksFish_WombatCutsAndServes_PaysAtTable()
        {
            Create();
            Open();
            GoIn();
            SetFish(6, 0, 0);
            Stand(Tank.Position + new Vector2(0f, -0.6f));
            Stand(new Vector2(-2.6f, -4.0f));
            RestaurantVisitor visitor = Admit();

            Assert.That(RunUntil(() => Board.Orders.Count == 1, 20d), Is.True, "수조에서 골라 도마 줄에");
            Assert.That(visitor.Dish.Id, Is.EqualTo("dish_minnow"));
            Assert.That(visitor.Table, Is.Not.Null, "탁자 자리를 잡았다");
            Assert.That(Tank.Stock, Is.EqualTo(6), "고른 물고기는 웜뱃이 꺼낼 때까지 수조에");
            Assert.That(Tank.Free, Is.EqualTo(5));

            Stand(Tank.Position + new Vector2(0f, -0.6f));
            Assert.That(Worker.Hands.Order, Is.SameAs(visitor), "수조 곁에서 그 손님 물고기를 손에");
            Assert.That(visitor.FishTaken && !visitor.Cut, Is.True);
            Assert.That(Tank.Stock, Is.EqualTo(5), "꺼낸 물고기는 수조에서 빠진다(창고가 비어 다시 채우지 않음)");

            Stand(Board.WorkerSpot);
            Assert.That(Board.OnBoard, Is.SameAs(visitor), "도마에 놓았다");
            Assert.That(RunUntil(() => visitor.Cut && Worker.Hands.Order == visitor, 10d), Is.True, "도마 자리에서 뜨면 접시가 손에");
            Assert.That(Board.Orders, Is.Empty);
            Assert.That(Board.OnBoard, Is.Null);

            double coins = m_state.Coins;
            double paid = 0d;
            m_bus.Subscribe<Events.RestaurantVisitorPaid>(e => paid = e.Coins);
            Stand(visitor.Table.Position + new Vector2(0f, -0.5f));
            Assert.That(visitor.Served, Is.True, "그 손님 탁자 곁에서 놓는다");
            Assert.That(Worker.Hands.Order, Is.Null);

            Stand(new Vector2(-2.6f, -4.0f));
            Assert.That(RunUntil(() => visitor.Paid, 20d), Is.True, "다 먹으면 탁자에서 값");
            // 값 배수(축복 · 유물 · 별)는 빵집과 같다: 문을 열려고 쌓은 빵집 별 다섯이 붙는다
            Assert.That(paid, Is.EqualTo(Math.Round(m_tables.Get<DishTable>("dish_minnow").Price * m_state.Scale(BlessingTable.k_Price))).Within(1e-6));
            Assert.That(paid, Is.GreaterThan(m_tables.Get<DishTable>("dish_minnow").Price));
            Assert.That(m_state.Coins - coins, Is.EqualTo(paid).Within(1e-6));
            Assert.That(RunUntil(() => m_restaurant.Visitors.Count == 0, 20d), Is.True, "구멍으로 나간다");
            Assert.That(visitor.Angry, Is.False);
        }

        [Test]
        public void Customer_AtEmptyTank_WaitsThenLeavesAngry()
        {
            Create();
            Open();
            SetFish(0, 0, 0);
            RestaurantVisitor visitor = Admit();

            Assert.That(RunUntil(() => m_restaurant.Visitors.Count == 0, 30d), Is.True, "기다리다 나간다");
            Assert.That(visitor.Angry, Is.True);
            Assert.That(visitor.Dish, Is.Null);
        }

        [Test]
        public void Board_CutsOnlyWhileWombatStandsAtItsSpot()
        {
            Create();
            Open();
            GoIn();
            SetFish(6, 0, 0);
            Stand(Tank.Position + new Vector2(0f, -0.6f));
            Stand(new Vector2(-2.6f, -4.0f));
            Admit();
            Assert.That(RunUntil(() => Board.Orders.Count == 1, 20d), Is.True);

            Run(10d);
            Assert.That(Board.OnBoard, Is.Null, "웜뱃이 가져오기 전에는 도마가 빈다");
            Stand(Tank.Position + new Vector2(0f, -0.6f));
            Stand(Board.WorkerSpot);
            Assert.That(Board.OnBoard, Is.Not.Null, "가져온 물고기를 놓았다");
            Stand(new Vector2(-2.6f, -4.0f));
            double progress = Board.Progress;
            Run(10d);
            Assert.That(Board.Progress, Is.EqualTo(progress), "웜뱃이 자리에 없으면 뜨지 않는다");
            Assert.That(Worker.Hands.Order, Is.Null);

            Stand(Board.WorkerSpot);
            Assert.That(Board.Cutting, Is.True);
        }

        [Test]
        public void Save_RoundTrip_KeepsOpenDoorAndTankFish()
        {
            Create();
            Open();
            GoIn();
            SetFish(2, 2, 2);
            Stand(Tank.Position + new Vector2(0f, -0.6f));
            Dictionary<string, int> fish = m_restaurant.Dishes.ToDictionary(dish => dish.Id, Tank.CountOf);
            SaveData data = GameSave.Capture(m_state, m_mall);

            Create();
            GameSave.Apply(data, m_state, m_mall, m_tables);
            Run(k_Dt);

            Assert.That(m_restaurant.IsOpen, Is.True);
            Assert.That(m_plaza.Gates, Is.Empty, "열린 저장이면 문 앞 표식이 없다");
            Assert.That(m_restaurant.Dishes.ToDictionary(dish => dish.Id, Tank.CountOf), Is.EqualTo(fish));
        }

        [Test]
        public void Validate_WhenDishFishUnknown_ReportsError()
        {
            TableSet tables = TestTables.Load("DishTable", rows => rows[0]["fish"] = "nope");

            Assert.That(TableValidator.Validate(tables).Any(error => error.StartsWith("DishTable")), Is.True);
        }
    }
}
