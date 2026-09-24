using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 08 v0.5 · 손님 동선 설계 v0.2 검증 3 · 설계 09 검증 1~4: 매 프레임 틱(0.02초)으로 돌린다. 실제 JSON 값(도착 4 · 걷기 2.2/초 · 톡 0.3 · 집기 0.4 ·
    // 두리번 2 · 인내 6 · 계산 1.5 · 식빵 8초 6개 10코인 가중치 3 · 크루아상 15초 4개 25코인 가중치 2). 걷는 시간은 A* 길 길이 ÷ 2.2
    public sealed class ShopSimTests
    {
        private const double k_Dt = 0.02;
        private const double k_Tolerance = 0.06;

        private ZooState m_state;
        private TableSet m_tables;
        private BakeryConfigTable m_config;
        private double m_time;
        // 설계 11: 손님은 광장에서 오지만, 빵집 테스트는 옛 도착 타이머(첫 틱, 그다음 m_arrivalSeconds마다)로 구멍에 들인다
        private double m_arrivalSeconds;
        private double m_arrivalElapsed;
        private VisitorTable m_look;

        // edit: 행동 mode·사물 range 같은 다른 표 바꾸기(설계 09 v0.4)
        private ShopSim Create(Action<BakeryConfigTable> tweak = null, Action<TableSet> edit = null)
        {
            m_tables = TestTables.Load();
            m_config = m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery);
            m_arrivalSeconds = m_tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main).ArrivalSeconds;
            tweak?.Invoke(m_config);
            edit?.Invoke(m_tables);
            m_arrivalElapsed = m_arrivalSeconds;
            m_state = ZooState.CreateNew(m_tables);
            m_time = 0d;
            m_look = m_tables.GetAll<VisitorTable>()[0];
            return new ShopSim(m_state, m_tables, new SequenceRandom(new double[200]));
        }

        private double Config(string id)
        {
            return m_tables.Get<ConfigTable>(id).Value;
        }

        private void Arrive(ShopSim shop)
        {
            m_arrivalElapsed = Math.Min(m_arrivalElapsed + k_Dt, m_arrivalSeconds);

            if (m_arrivalElapsed < m_arrivalSeconds || !shop.CanAdmit)
            {
                return;
            }

            m_arrivalElapsed = 0d;
            shop.Admit(m_look);
        }

        private void Run(ShopSim shop, double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                shop.Tick(k_Dt);
                Arrive(shop);
                m_time += k_Dt;
            }
        }

        // 조건이 참이 될 때까지 돌리고 그때 시각을 돌려준다
        private double RunUntil(ShopSim shop, Func<bool> done, double maxSeconds = 60d)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return m_time;
                }

                shop.Tick(k_Dt);

                Arrive(shop);
                m_time += k_Dt;
            }

            Assert.Fail("시간 안에 조건이 참이 되지 않았다.");
            return m_time;
        }

        // 조이스틱으로 웜뱃을 끈다: 웜뱃 자리 높이까지 세로 → 가로 → 세로(계산대를 돌아가는 길). 닿으면 손을 뗀다
        private void WalkTo(ShopSim shop, Vector2 target)
        {
            float homeY = shop.Layout.WombatHome.Y;
            Steer(shop, new Vector2(shop.WombatPosition.X, homeY));
            Steer(shop, new Vector2(target.X, homeY));
            Steer(shop, target);
            shop.SetWombatInput(Vector2.Zero);
        }

        private void Steer(ShopSim shop, Vector2 point)
        {
            float stepLength = (float)(Config(ConfigTable.k_WombatSpeed) * k_Dt);

            RunUntil(shop, () =>
            {
                Vector2 delta = point - shop.WombatPosition;
                float distance = delta.Length();
                shop.SetWombatInput(distance < 1e-3f ? Vector2.Zero : delta / distance * Math.Min(1f, distance / stepLength));
                return distance < 0.05f;
            }, 20d);
        }

        // 진열대 앞(아래)·오븐 위: 설계 09 대상 거리(1.3) 안에서 그 사물이 가장 가까운 곳
        private static Vector2 ShelfStand(ShopSim shop, Cell cell)
        {
            return shop.Layout.ShelfBase(cell) - new Vector2(0f, 0.55f);
        }

        private static Vector2 OvenStand(ShopSim shop, Cell cell)
        {
            return shop.Layout.OvenBase(cell) + new Vector2(0f, 1.05f);
        }

        // 손님을 막아 두고 굽기 → 꺼내기 → 채우기 → 계산대 자리로 돌아오기
        private void Stock(ShopSim shop, string breadId, int count)
        {
            int before = m_config.MaxCustomers;
            m_config.MaxCustomers = 0;
            Cell ovenCell = new Cell(-1, 3);
            int oven = shop.OvenAt(ovenCell);
            Assert.That(shop.TryBake(oven, breadId), Is.True);
            RunUntil(shop, () => shop.Ovens[oven].Ready > 0);
            CarryToShelf(shop, ovenCell, breadId);
            WalkTo(shop, shop.Layout.WombatHome);
            Assert.That(shop.Stock(breadId), Is.GreaterThanOrEqualTo(count));
            m_config.MaxCustomers = before;
        }

        // 꺼내기는 오븐 range에, 채우기는 진열대 range에 들어가면 저절로(설계 09 v0.4, 2026-09-23 꺼내기도 auto)
        private void CarryToShelf(ShopSim shop, Cell ovenCell, string breadId)
        {
            WalkTo(shop, OvenStand(shop, ovenCell));
            Assert.That(shop.CarriedCount, Is.GreaterThan(0));
            WalkTo(shop, ShelfStand(shop, shop.ShelfCell(breadId)));
        }

        private static float PathSeconds(ShopSim shop, Vector2 from, Vector2 to, double speed)
        {
            return (float)(BurrowNav.Length(from, shop.Layout.Nav.FindPath(from, to)) / speed);
        }

        [Test]
        public void UpgradeValue_ByUpgradeAndLevel_MatchesConfigPlusEffect()
        {
            ShopSim shop = Create();

            Assert.That(shop.UpgradeValue(ShopSim.k_ShelfCapacity, 0), Is.EqualTo(8d));
            Assert.That(shop.UpgradeValue(ShopSim.k_ShelfCapacity, 1), Is.EqualTo(12d));
            Assert.That(shop.UpgradeValue(ShopSim.k_OvenCount, 2), Is.EqualTo(3d));
            Assert.That(shop.UpgradeValue(ShopSim.k_OvenSpeed, 2), Is.EqualTo(1.4d).Within(1e-9));
        }

        [Test]
        public void Admit_StopsAtMaxCustomers()
        {
            ShopSim shop = Create(c =>
            {
                c.PatienceSeconds = 1000d;
                m_arrivalSeconds = 1d;
            });

            Run(shop, 20d);

            Assert.That(shop.Customers.Count, Is.EqualTo(8));
        }

        // 구멍에서 톡(0.3초) → A* 길을 2.2/초로 → 도착하자마자 집기
        [Test]
        public void Customer_WithStock_PicksAfterHopAndPathWalk()
        {
            ShopSim shop = Create();
            Stock(shop, "b01", 6);
            double arrivedAt = -1d, pickedAt = -1d;
            Customer first = null;
            shop.CustomerArrived += c =>
            {
                first ??= c;
                arrivedAt = m_time;
            };
            shop.CustomerPicked += c => pickedAt = m_time;

            RunUntil(shop, () => pickedAt >= 0d);

            Vector2 spot = shop.Layout.ShelfSpots(new Cell(-1, 1))[0];
            double expected = Config(ConfigTable.k_HopSeconds) + PathSeconds(shop, shop.Layout.HoleFloor, spot, Config(ConfigTable.k_WalkSpeed));
            Assert.That(pickedAt - arrivedAt, Is.EqualTo(expected).Within(k_Tolerance * 2));
            Assert.That(Vector2.Distance(first.Position, spot), Is.LessThan(0.01f));
            Assert.That(shop.Stock("b01"), Is.EqualTo(5));
        }

        [Test]
        public void Customer_Paid_WalksOutThroughHoleAndLeaves()
        {
            ShopSim shop = Create(c => m_arrivalSeconds = 1000d);
            Stock(shop, "b01", 6);
            double coins = m_state.Coins;
            int exited = 0;
            bool paid = false;
            shop.CustomerPaid += (c, amount) => paid = true;
            shop.CustomerExited += c => exited++;

            RunUntil(shop, () => exited == 1);

            Assert.That(paid, Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins + 10d));
            Assert.That(shop.Customers.Count, Is.EqualTo(0));
        }

        // 빵을 집은 뒤 줄 머리 자리까지 걸어가 선 다음에야 계산(1.5초)이 시작된다
        [Test]
        public void Checkout_StartsOnlyAfterHeadStandsAtHeadSlot()
        {
            ShopSim shop = Create(c => m_arrivalSeconds = 1000d);
            Stock(shop, "b01", 6);
            double coins = m_state.Coins;
            Customer customer = null;
            double paidAt = -1d;
            shop.CustomerArrived += c => customer = c;
            shop.CustomerPaid += (c, amount) => paidAt = m_time;

            double queuedAt = RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Queued);
            Assert.That(m_state.Coins, Is.EqualTo(coins));
            Assert.That(Vector2.Distance(customer.Position, shop.Layout.QueueSlots[0]), Is.LessThan(0.01f));

            RunUntil(shop, () => paidAt >= 0d);
            Assert.That(paidAt - queuedAt, Is.EqualTo(m_config.CheckoutSeconds).Within(k_Tolerance));
        }

        // 원하는 빵(식빵)이 없으면 2초 두리번 → 다른 빵(크루아상) 진열대로 가서 집는다
        [Test]
        public void Customer_EmptyFirstShelf_LooksThenGoesToNextBread()
        {
            ShopSim shop = Create(c => m_arrivalSeconds = 1000d);
            m_state.AddCoins(10000d);
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 1)), Is.True);
            Stock(shop, "b02", 4);
            Customer customer = null;
            double picked = -1d;
            shop.CustomerArrived += c => customer = c;
            shop.CustomerPicked += c => picked = m_time;

            double lookStart = RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Looking);
            Assert.That(customer.Bread.Id, Is.EqualTo("b01"));

            double leftAt = RunUntil(shop, () => customer.Phase == CustomerPhase.Walking);
            Assert.That(leftAt - lookStart, Is.EqualTo(m_config.LookSeconds).Within(k_Tolerance));
            Assert.That(customer.Bread.Id, Is.EqualTo("b02"));
            Assert.That(customer.Cell, Is.EqualTo(new Cell(0, 1)));

            RunUntil(shop, () => picked >= 0d);
            Assert.That(shop.Stock("b02"), Is.EqualTo(3));
        }

        // 빵이 하나뿐이면 그 진열대에서 인내(6초)가 다 할 때까지 두리번하고 「!!」로 떠난다
        [Test]
        public void Customer_OnlyBreadEmpty_LooksForPatienceThenGivesUp()
        {
            ShopSim shop = Create(c => m_arrivalSeconds = 1000d);
            Customer customer = null;
            double gaveUpAt = -1d;
            int exited = 0;
            shop.CustomerArrived += c => customer = c;
            shop.CustomerGaveUp += c => gaveUpAt = m_time;
            shop.CustomerExited += c => exited++;

            double lookStart = RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Looking);
            RunUntil(shop, () => gaveUpAt >= 0d);

            Assert.That(gaveUpAt - lookStart, Is.EqualTo(m_config.PatienceSeconds).Within(k_Tolerance * 3));
            Assert.That(customer.Angry, Is.True);
            RunUntil(shop, () => exited == 1);
        }

        // 두리번하는 사이 빵이 채워지면 바로 집는다
        [Test]
        public void Customer_StockAppearsWhileLooking_PicksImmediately()
        {
            ShopSim shop = Create(c =>
            {
                m_arrivalSeconds = 1000d;
                c.PatienceSeconds = 100d;
            });
            Customer customer = null;
            int gaveUp = 0;
            double picked = -1d;
            shop.CustomerArrived += c => customer = c;
            shop.CustomerGaveUp += c => gaveUp++;
            shop.CustomerPicked += c => picked = m_time;

            RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Looking);
            Assert.That(shop.TryBake(0, "b01"), Is.True);
            RunUntil(shop, () => shop.Ovens[0].Ready > 0);
            double stockedAt = -1d;
            shop.StockChanged += _ => stockedAt = stockedAt < 0d ? m_time : stockedAt;
            CarryToShelf(shop, new Cell(-1, 3), "b01");
            RunUntil(shop, () => picked >= 0d);

            Assert.That(picked - stockedAt, Is.LessThan(k_Tolerance));
            Assert.That(gaveUp, Is.EqualTo(0));
        }

        // 줄 머리가 계산을 마치면 뒤 손님이 머리 자리로 걸어간다
        [Test]
        public void Queue_ShiftsForwardAfterPayment()
        {
            ShopSim shop = Create(c =>
            {
                m_arrivalSeconds = 0.5d;
                c.CheckoutSeconds = 3d;
            });
            Stock(shop, "b01", 6);
            m_config.MaxCustomers = 2;

            RunUntil(shop, () => shop.Queue.Count == 2 && shop.Queue[1].Phase == CustomerPhase.Queued);
            Customer second = shop.Queue[1];
            Assert.That(Vector2.Distance(second.Position, shop.Layout.QueueSlots[1]), Is.LessThan(0.01f));

            RunUntil(shop, () => shop.Queue.Count == 1);
            Assert.That(shop.Queue[0], Is.SameAs(second));
            RunUntil(shop, () => !second.Moving);
            Assert.That(Vector2.Distance(second.Position, shop.Layout.QueueSlots[0]), Is.LessThan(0.01f));
        }

        // 비켜 걷기: 붐비는 가게에서 그린 자리(위치 + 비킴)가 길 위치보다 덜 겹치고, 비킴은 0.25를 넘지 않는다
        [Test]
        public void Sidestep_KeepsCrowdedCustomersApart()
        {
            ShopSim shop = Create(c => m_arrivalSeconds = 0.5d);
            Stock(shop, "b01", 6);
            int pathClose = 0;
            int drawnClose = 0;

            for (double t = 0d; t < 20d; t += k_Dt)
            {
                shop.Tick(k_Dt);
                Arrive(shop);
                m_time += k_Dt;
                IReadOnlyList<Customer> customers = shop.Customers;

                for (int i = 0; i < customers.Count; i++)
                {
                    Customer a = customers[i];
                    Assert.That(a.Sidestep.Length(), Is.LessThanOrEqualTo(0.25f + 1e-4f));

                    for (int j = i + 1; j < customers.Count; j++)
                    {
                        Customer b = customers[j];

                        if (a.Phase == CustomerPhase.Entering || a.Phase == CustomerPhase.Exiting || b.Phase == CustomerPhase.Entering || b.Phase == CustomerPhase.Exiting)
                        {
                            continue;
                        }

                        pathClose += Vector2.Distance(a.Position, b.Position) < 0.3f ? 1 : 0;
                        drawnClose += Vector2.Distance(a.Position + a.Sidestep, b.Position + b.Sidestep) < 0.3f ? 1 : 0;
                    }
                }
            }

            Assert.That(pathClose, Is.GreaterThan(0));
            Assert.That(drawnClose, Is.LessThan(pathClose / 2));
        }

        // 설계 09: 다 구운 빵은 저절로 진열되지 않고, 오븐 앞에 가면 꺼내져 오븐이 비어 다시 구울 수 있다
        [Test]
        public void TakeOut_EmptiesOvenAndAllowsBakeAgain()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0);
            Assert.That(shop.TryBake(0, "b01"), Is.True);
            Assert.That(shop.TryBake(0, "b01"), Is.False);

            RunUntil(shop, () => shop.Ovens[0].Ready == 6);
            Run(shop, 1d);
            Assert.That(shop.Stock("b01"), Is.EqualTo(0));
            Assert.That(shop.TryBake(0, "b01"), Is.False);

            WalkTo(shop, OvenStand(shop, new Cell(-1, 3)));
            Assert.That(shop.CarriedCount, Is.EqualTo(6));
            Assert.That(shop.Carried.Id, Is.EqualTo("b01"));
            Assert.That(shop.Ovens[0].IsEmpty, Is.True);
            Assert.That(shop.TargetAction.Id, Is.EqualTo(ShopSim.k_ActionOpen));
            Assert.That(shop.TryBake(0, "b01"), Is.True);
        }

        // 설계 09 v0.4: 진열대 range에 들어가면 버튼 없이 자리만큼 내려놓고 남은 건 계속 든다. 진열대 버튼은 시트 열기
        [Test]
        public void Fill_AutoOnEnteringRange_PutsShelfSpaceAndKeepsRest()
        {
            ShopSim shop = Create(c =>
            {
                c.MaxCustomers = 0;
                c.ShelfCapacity = 4;
            });
            int carryChanged = 0;
            shop.CarryChanged += () => carryChanged++;
            shop.TryBake(0, "b01");
            RunUntil(shop, () => shop.Ovens[0].Ready > 0);

            CarryToShelf(shop, new Cell(-1, 3), "b01");

            Assert.That(shop.Stock("b01"), Is.EqualTo(4));
            Assert.That(shop.CarriedCount, Is.EqualTo(2));
            Assert.That(shop.TargetAction.Id, Is.EqualTo(ShopSim.k_ActionOpen));
            Assert.That(shop.TryInteract(), Is.False);
            Assert.That(carryChanged, Is.EqualTo(2));
        }

        [Test]
        public void Bake_LockedBread_Fails()
        {
            ShopSim shop = Create();

            Assert.That(shop.TryBake(0, "b02"), Is.False);
        }

        [Test]
        public void Unlock_NeedsEmptySlot()
        {
            ShopSim shop = Create();
            m_state.AddCoins(10000d);

            Assert.That(shop.TryUnlockNextBread(new Cell(-1, 1)), Is.False);
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 2)), Is.False);
            Assert.That(shop.TryUnlockNextBread(new Cell(1, 1)), Is.False);
            Assert.That(shop.Grid.TryDig(new Cell(1, 1)), Is.True);
            Assert.That(shop.TryUnlockNextBread(new Cell(1, 1)), Is.True);
            Assert.That(shop.Shelves[new Cell(1, 1)].Id, Is.EqualTo("b02"));
        }

        [Test]
        public void Unlock_FollowsTableOrderAndFillsSlots()
        {
            ShopSim shop = Create();
            int layoutChanged = 0;
            shop.LayoutChanged += () => layoutChanged++;

            Assert.That(shop.TryUnlockNextBread(new Cell(0, 1)), Is.False);

            m_state.AddCoins(10000d);
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 1)), Is.True);
            Assert.That(shop.UnlockedBreads[1].Id, Is.EqualTo("b02"));
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 1)), Is.False);

            Assert.That(shop.TryUnlockNextBread(new Cell(0, 3)), Is.True);
            Assert.That(shop.Shelves[new Cell(0, 3)].Id, Is.EqualTo("b03"));
            Assert.That(shop.NextBread, Is.Null);
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 3)), Is.False);
            Assert.That(layoutChanged, Is.EqualTo(2));
            Assert.That(shop.EmptySlots, Is.Empty);
        }

        [Test]
        public void AddOven_NeedsEmptySlotAndChargesGrowingCost()
        {
            ShopSim shop = Create();

            Assert.That(shop.TryAddOven(new Cell(-1, 3)), Is.False);
            Assert.That(shop.TryAddOven(new Cell(0, 3)), Is.True);
            Assert.That(shop.Ovens.Count, Is.EqualTo(2));
            Assert.That(shop.Ovens[1].Cell, Is.EqualTo(new Cell(0, 3)));
            Assert.That(shop.OvenAt(new Cell(0, 3)), Is.EqualTo(1));
            Assert.That(m_state.Coins, Is.EqualTo(50d));
            Assert.That(shop.UpgradeCost(ShopSim.k_OvenCount), Is.EqualTo(900d));
            Assert.That(shop.TryAddOven(new Cell(0, 1)), Is.False);

            m_state.AddCoins(100000d);
            Assert.That(shop.TryAddOven(new Cell(0, 1)), Is.True);
            shop.Grid.TryDig(new Cell(1, 3));
            Assert.That(shop.TryAddOven(new Cell(1, 3)), Is.True);
            Assert.That(shop.IsMaxed(ShopSim.k_OvenCount), Is.True);
            shop.Grid.TryDig(new Cell(1, 1));
            Assert.That(shop.TryAddOven(new Cell(1, 1)), Is.False);
        }

        [Test]
        public void Upgrade_OvenSpeedAndShelfCapacity_Apply()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0);
            shop.TryUpgrade(ShopSim.k_OvenSpeed);
            shop.TryUpgrade(ShopSim.k_ShelfCapacity);
            shop.TryBake(0, "b01");
            double done = RunUntil(shop, () => shop.Ovens[0].Ready > 0);

            // 굽기 8초 ÷ 1.2배. 시트에서 고르는 즉시 시작한다
            Assert.That(done, Is.EqualTo(8d / 1.2d).Within(k_Tolerance));
            Assert.That(shop.ShelfCapacity, Is.EqualTo(12));
        }

        // 설계 09 검증 1: 조이스틱 방향으로 wombatSpeed만큼 걷고, 벽을 대각선으로 밀면 벽을 따라 미끄러진다.
        // 미끄러지는 동안에도 조이스틱 쪽(오른쪽 위 → 가로가 큰 쪽 = 오른쪽)을 본다(2026-09-24)
        [Test]
        public void Wombat_Input_MovesAtSpeedAndSlidesAlongWall()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0);
            Vector2 home = shop.Layout.WombatHome;

            shop.SetWombatInput(new Vector2(1f, 0f));
            Run(shop, 0.5d);
            Assert.That(shop.WombatPosition.X - home.X, Is.EqualTo((float)(Config(ConfigTable.k_WombatSpeed) * 0.5d)).Within(0.25f));
            Assert.That(shop.WombatFacing, Is.EqualTo(Facing.Right));

            Run(shop, 3d);
            float wallX = shop.WombatPosition.X;
            Assert.That(shop.WombatMoving, Is.False);

            shop.SetWombatInput(new Vector2(1f, 1f));
            Run(shop, 0.5d);
            Assert.That(shop.WombatPosition.X, Is.EqualTo(wallX).Within(0.05f));
            Assert.That(shop.WombatPosition.Y, Is.GreaterThan(home.Y + 0.5f));
            Assert.That(shop.WombatMoving, Is.True);
            Assert.That(shop.WombatFacing, Is.EqualTo(Facing.Right));

            // 벽에 막혀 멈춰도 민 쪽을 본다
            shop.SetWombatInput(new Vector2(0f, -1f));
            Run(shop, 0.1d);
            shop.SetWombatInput(new Vector2(1f, 0f));
            Run(shop, 0.1d);
            Assert.That(shop.WombatMoving, Is.False);
            Assert.That(shop.WombatFacing, Is.EqualTo(Facing.Right));
        }

        // 설계 09 검증 2: 거리 안의 가장 가까운 사물이 대상이고, 바뀔 때만 알린다. 벽 옆이면 그 흙 칸
        [Test]
        public void Target_NearestInRange_ChangesOnlyWhenDifferent()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0);
            Assert.That(shop.Target.Value.Kind, Is.EqualTo(InteractKind.Counter));
            int changed = 0;
            shop.TargetChanged += () => changed++;

            Run(shop, 1d);
            Assert.That(changed, Is.EqualTo(0));

            WalkTo(shop, OvenStand(shop, new Cell(-1, 3)));
            Assert.That(shop.Target, Is.EqualTo(new Interactable(InteractKind.Oven, new Cell(-1, 3), 0)));
            Assert.That(shop.TargetAction.Id, Is.EqualTo(ShopSim.k_ActionOpen));

            WalkTo(shop, new Vector2(1.69f, shop.Layout.WombatHome.Y));
            Assert.That(shop.Target, Is.Null);
            Assert.That(shop.TargetAction, Is.Null);
            Assert.That(shop.TryInteract(), Is.False);

            WalkTo(shop, new Vector2(3f, shop.Layout.CellCenter(new Cell(0, 3)).Y));
            Assert.That(shop.Target, Is.EqualTo(new Interactable(InteractKind.Dig, new Cell(1, 3))));
            Assert.That(changed, Is.GreaterThanOrEqualTo(3));
        }

        // 설계 09 검증 4: 웜뱃이 계산대 자리를 비우면 줄 머리 계산이 멈추고, 돌아오면 이어서 계산한다
        [Test]
        public void Checkout_PausesWhileWombatAwayFromCounter()
        {
            ShopSim shop = Create(c => m_arrivalSeconds = 1000d);
            Stock(shop, "b01", 6);
            Customer customer = null;
            shop.CustomerArrived += c => customer = c;
            RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Queued);

            double coins = m_state.Coins;
            WalkTo(shop, new Vector2(1.69f, shop.Layout.WombatHome.Y));
            Assert.That(shop.WombatAtCounter, Is.False);
            Run(shop, m_config.CheckoutSeconds + 0.1d);
            Assert.That(m_state.Coins, Is.EqualTo(coins));

            WalkTo(shop, shop.Layout.WombatHome);
            double back = m_time;
            double paid = RunUntil(shop, () => m_state.Coins > coins);
            Assert.That(paid - back, Is.LessThanOrEqualTo(m_config.CheckoutSeconds + k_Tolerance));
        }

        // 설계 09 v0.4 검증 1: 진열대 앞에 서 있는 동안 손님이 집어 자리가 나면 든 빵으로 또 채운다
        [Test]
        public void Fill_WhileStanding_RefillsWhenCustomerPicks()
        {
            ShopSim shop = Create(c =>
            {
                c.ShelfCapacity = 4;
                m_arrivalSeconds = 1000d;
                c.PatienceSeconds = 100d;
            });
            double picked = -1d;
            shop.CustomerPicked += c => picked = m_time;
            shop.TryBake(0, "b01");
            RunUntil(shop, () => shop.Ovens[0].Ready > 0);

            CarryToShelf(shop, new Cell(-1, 3), "b01");
            RunUntil(shop, () => picked >= 0d);
            Run(shop, 0.1d);

            Assert.That(shop.Stock("b01"), Is.EqualTo(4));
            Assert.That(shop.CarriedCount, Is.EqualTo(1));
        }

        // 설계 09 v0.4 검증 2: 버튼 대상이 더 가까운 오븐이어도 range 안 진열대는 저절로 채운다
        [Test]
        public void Fill_AutoEvenWhenOvenIsTheTarget()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0, edit: t => t.Get<InteractableTable>("shelf").Range = 6d);
            shop.TryBake(0, "b01");
            RunUntil(shop, () => shop.Ovens[0].Ready > 0);

            WalkTo(shop, OvenStand(shop, new Cell(-1, 3)));
            Run(shop, 0.1d);

            Assert.That(shop.Target.Value.Kind, Is.EqualTo(InteractKind.Oven));
            Assert.That(shop.Stock("b01"), Is.EqualTo(6));
            Assert.That(shop.CarriedCount, Is.EqualTo(0));
        }

        // 설계 09 v0.4 검증 3: serve를 manual로 바꾸면 타이머가 흐르지 않고 버튼 한 번에 계산된다
        [Test]
        public void Serve_Manual_PaysHeadOnlyOnButton()
        {
            ShopSim shop = Create(c => m_arrivalSeconds = 1000d, t => t.Get<ActionTable>("serve").Mode = ActionMode.Manual);
            Stock(shop, "b01", 6);
            Customer customer = null;
            shop.CustomerArrived += c => customer = c;
            RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Queued && !customer.Moving);

            double coins = m_state.Coins;
            Run(shop, m_config.CheckoutSeconds + 0.5d);
            Assert.That(m_state.Coins, Is.EqualTo(coins));
            Assert.That(shop.TargetAction.Id, Is.EqualTo(ShopSim.k_ActionServe));

            Assert.That(shop.TryInteract(), Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins + 10d));
            Assert.That(shop.TargetAction.Id, Is.EqualTo(ShopSim.k_ActionOpen));
        }

        // 설계 09 v0.4 검증 4(2026-09-23 기본이 auto로 바뀜): take_out을 manual로 바꾸면 오븐 앞에 서도 버튼을 눌러야 꺼낸다
        [Test]
        public void TakeOut_Manual_OnlyOnButton()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0, t => t.Get<ActionTable>("take_out").Mode = ActionMode.Manual);
            shop.TryBake(0, "b01");
            RunUntil(shop, () => shop.Ovens[0].Ready > 0);

            WalkTo(shop, OvenStand(shop, new Cell(-1, 3)));
            Assert.That(shop.CarriedCount, Is.EqualTo(0));
            Assert.That(shop.TargetAction.Id, Is.EqualTo(ShopSim.k_ActionTakeOut));

            Assert.That(shop.TryInteract(), Is.True);
            Assert.That(shop.CarriedCount, Is.EqualTo(6));
            Assert.That(shop.Ovens[0].IsEmpty, Is.True);
            Assert.That(shop.TargetAction.Id, Is.EqualTo(ShopSim.k_ActionOpen));
        }

        // 빈 자리 앞에서 오븐을 놓으면 화면이 새 오븐을 만든(LayoutChanged) 뒤에야 대상이 그 오븐으로 바뀐다(2026-09-23 오븐 추가 예외)
        [Test]
        public void AddOven_AtTargetSlot_LayoutChangedBeforeTargetChanged()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0);
            m_state.AddCoins(10000d);
            Cell slot = new Cell(0, 3);
            WalkTo(shop, shop.Layout.SlotBase(slot));
            Assert.That(shop.Target, Is.EqualTo(new Interactable(InteractKind.EmptySlot, slot)));
            List<string> events = new List<string>();
            shop.LayoutChanged += () => events.Add("layout");
            shop.TargetChanged += () => events.Add("target");

            Assert.That(shop.TryAddOven(slot), Is.True);
            Assert.That(events, Is.EqualTo(new[] { "layout" }));

            Run(shop, k_Dt);
            Assert.That(events, Is.EqualTo(new[] { "layout", "target" }));
            Assert.That(shop.Target, Is.EqualTo(new Interactable(InteractKind.Oven, slot, 1)));
        }
    }
}
