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
    // 설계 25 · 27 검증: 광장 농장 문 ↔ 농장 굴, 파기 → 갈기 → 심기 → 자람 → 밟고 거두기(창고로), 밭은 길을 막지 않음, 층 상한, 레시피만큼 재료를 꺼내 굽기. 실제 JSON 값
    public sealed class FarmTests
    {
        private const double k_Dt = 0.02;
        private const string k_Wheat = "wheat";

        private TableSet m_tables;
        private EventBus m_bus;
        private ZooState m_state;
        private BakeryArea m_shop;
        private PlazaArea m_plaza;
        private FarmArea m_farm;
        private Mall m_mall;

        private FarmConfigTable Config => m_tables.Get<FarmConfigTable>(FarmConfigTable.k_Main);

        private void Create()
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            Wombat wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, new SequenceRandom(new double[2000]), wombat, m_bus);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 4000).ToArray()), wombat, m_bus);
            m_farm = new FarmArea(m_tables, wombat, m_bus);
            m_mall = new Mall(m_shop, m_plaza, m_farm, m_bus);
        }

        private void Run(double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }
        }

        private void RunUntil(Func<bool> done, double maxSeconds)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return;
                }

                m_mall.Tick(k_Dt);
            }

            Assert.Fail("시간 안에 조건이 참이 되지 않았다.");
        }

        // 조이스틱으로 지금 있는 곳의 웜뱃을 점마다 끈다
        private void Steer(params Vector2[] points)
        {
            float stepLength = (float)(m_tables.Get<ConfigTable>(ConfigTable.k_WombatSpeed).Value * k_Dt);

            foreach (Vector2 point in points)
            {
                RunUntil(() =>
                {
                    Vector2 delta = point - m_mall.Wombat.Mover.Position;
                    float distance = delta.Length();
                    m_mall.Wombat.SetInput(distance < 1e-3f ? Vector2.Zero : delta / distance * Math.Min(1f, distance / stepLength));
                    return distance < 0.05f;
                }, 20d);
            }

            Release();
        }

        private void PushUntil(Vector2 direction, WombatArea to)
        {
            RunUntil(() =>
            {
                m_mall.Wombat.SetInput(direction);
                return m_mall.Active == to;
            }, 5d);
            Release();
        }

        // 들어온 뒤에는 조이스틱을 놓아야 걷는다
        private void Release()
        {
            m_mall.Wombat.SetInput(Vector2.Zero);
            Run(0.1d);
        }

        // 빵집 → 광장 → 농장(통로 사건으로 바로)
        private void GoToFarm()
        {
            m_bus.Publish(new Events.Passed(m_shop, PlazaArea.k_Id));
            m_bus.Publish(new Events.Passed(m_plaza, FarmArea.k_Id));
            Release();
        }

        private void SetWheat(int count)
        {
            m_state.TrySpendItems(new List<IngredientData> { new IngredientData { Item = k_Wheat, Count = m_state.Count(k_Wheat) } });
            m_state.AddItem(k_Wheat, count);
        }

        private SheetOption BakeOption(string breadId)
        {
            SheetAction bake = m_shop.SheetActions(m_shop.Ovens[0]).First(action => action.Table.Id == ActionTable.k_Bake);
            return bake.Options(m_shop.Wombat.Worker, m_shop.Ovens[0]).First(option => option.Option == breadId);
        }

        private Vector2 CenterOf(Cell cell)
        {
            return m_farm.Layout.Cells.CellCenter(cell);
        }

        private DigInteractable DigAt(Cell cell)
        {
            return m_farm.Things.OfType<DigInteractable>().Single(dig => dig.Cell.Equals(cell));
        }

        private PlotInteractable PlotAt(Cell cell)
        {
            return m_farm.Plots.Single(plot => plot.Cell.Equals(cell));
        }

        // 시작: 가운데 두 열 × startRows줄이 판 채, 입구 줄 아래 칸마다 밭 사물, 앞의 startFields개는 갈아져 있고 비어 있다. 놓는 사물은 없다
        [Test]
        public void Farm_StartsWithDugColumnsAndTilledFields()
        {
            Create();
            FarmConfigTable config = Config;

            Assert.That(m_farm.Grid.Cells.Count, Is.EqualTo(2 * config.StartRows));
            Assert.That(m_farm.Plots.Count, Is.EqualTo(2 * (config.StartRows - 1)));
            Assert.That(m_farm.Plots.Take(config.StartFields).All(plot => plot.IsTilled && plot.IsEmpty), Is.True);
            Assert.That(m_farm.Plots.Skip(config.StartFields).All(plot => !plot.IsTilled), Is.True);
            Assert.That(m_farm.Plots.All(plot => plot.Cell.Row > BurrowGrid.k_EntranceRow), Is.True);
            Assert.That(m_farm.Things.OfType<DigInteractable>().Count(), Is.EqualTo(m_farm.Grid.Frontier().Count()));
            Assert.That(m_farm.Things.OfType<PassageInteractable>().Single().To, Is.EqualTo(PlazaArea.k_Id));
            Assert.That(m_farm.ShopKinds, Is.Empty);
            Assert.That(m_farm.WombatPresent, Is.False);
        }

        // 광장 계단 오른쪽 문 앞 띠에 들면 농장 구멍 아래로, 농장 구멍 앞 띠에 들면 광장 농장 문 아래로 돌아온다
        [Test]
        public void FarmDoor_MovesWombatBetweenPlazaAndFarm()
        {
            Create();
            m_bus.Publish(new Events.Passed(m_shop, PlazaArea.k_Id));
            Release();
            PlazaLayout plaza = m_plaza.Layout;
            Vector2 below = new Vector2(0f, -0.8f);

            Steer(plaza.DoorFloor + below, plaza.FarmDoorFloor + below, plaza.FarmDoorFloor);
            Assert.That(m_mall.Active, Is.SameAs(m_plaza));
            PushUntil(new Vector2(0f, 1f), m_farm);

            Assert.That(m_farm.WombatPresent, Is.True);
            Assert.That(m_plaza.WombatPresent, Is.False);
            Assert.That(m_farm.Wombat.Mover.Position, Is.EqualTo(m_farm.Layout.HoleFloor));

            PushUntil(new Vector2(0f, 1f), m_plaza);

            Assert.That(m_plaza.Wombat.Mover.Position, Is.EqualTo(plaza.FarmDoorFloor));
        }

        // 밭 칸에 서면 버튼 = 심기. 자라는 동안은 밭에 서 있어도 거두지 않고, 익은 밭 칸에 발이 들면 저절로 거둬 창고로(들고 다니지 않는다). 옆 칸에서는 거두지 않는다
        [Test]
        public void Plant_GrowsThenHarvestsWhenSteppingOnField()
        {
            Create();
            GoToFarm();
            PlotInteractable plot = m_farm.Plots[0];
            CropTable crop = m_farm.Crop;
            Vector2 inside = CenterOf(plot.Cell);
            Vector2 beside = CenterOf(plot.Cell.Offset(1, 0));
            int wheat = m_state.Count(k_Wheat);
            List<Events.Harvested> harvested = new List<Events.Harvested>();
            m_bus.Subscribe<Events.Harvested>(e => harvested.Add(e));

            m_farm.Wombat.Mover.Place(inside);
            Run(k_Dt);
            Assert.That(m_farm.Target, Is.SameAs(plot));
            Assert.That(m_farm.TargetAction.Id, Is.EqualTo(ActionTable.k_Plant));
            Assert.That(m_farm.TryInteract(), Is.True);
            Assert.That(plot.Crop, Is.SameAs(crop));
            Assert.That(plot.Stage, Is.EqualTo(0));

            Run(crop.GrowSeconds * 0.9);
            Assert.That(plot.IsRipe, Is.False);
            Assert.That(m_farm.TargetAction, Is.Null);

            m_farm.Wombat.Mover.Place(beside);
            Run(crop.GrowSeconds * 0.2);
            Assert.That(plot.IsRipe, Is.True);
            Assert.That(plot.Stage, Is.EqualTo(crop.Stages - 1));
            Assert.That(m_state.Count(k_Wheat), Is.EqualTo(wheat));

            m_farm.Wombat.Mover.Place(inside);
            Run(k_Dt);
            Assert.That(plot.IsEmpty, Is.True);
            Assert.That(m_state.Count(k_Wheat), Is.EqualTo(wheat + crop.Yield));
            Assert.That(harvested.Count, Is.EqualTo(1));
            Assert.That(harvested[0].Count, Is.EqualTo(crop.Yield));
            Assert.That(m_mall.Wombat.Worker.Hands.Count, Is.EqualTo(0));
        }

        // 파기: 붙은 흙 칸의 시트 행동으로 코인을 치르면 굴이 넓어지고 그 칸에 흙 밭 사물이 생긴다(파기 대상에서는 빠진다).
        // 갈기: 흙 칸에 서면 버튼 = 갈기, 누르면 코인을 치르고 밭이 되어 버튼 = 심기. 코인이 모자라면 버튼이 없다
        [Test]
        public void Dig_ThenTill_MakesField()
        {
            Create();
            GoToFarm();
            FarmConfigTable config = Config;
            Cell cell = new Cell(-1, config.StartRows);
            double coins = m_state.Coins;
            DigInteractable dig = DigAt(cell);
            SheetAction digAction = m_farm.SheetActions(dig).Single();
            List<Events.Tilled> tilled = new List<Events.Tilled>();
            m_bus.Subscribe<Events.Tilled>(e => tilled.Add(e));

            Assert.That(digAction.Options(m_farm.Wombat.Worker, dig)[0].Cost, Is.EqualTo(config.DigBaseCost));
            Assert.That(m_farm.TryChoose(ActionTable.k_Dig, dig, null), Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins - config.DigBaseCost));
            Assert.That(m_farm.Grid.Contains(cell), Is.True);
            Assert.That(m_farm.Grid.DigCost, Is.EqualTo(config.DigBaseCost * config.DigCostGrowth).Within(1e-9));
            Assert.That(m_farm.Things.OfType<DigInteractable>().Any(thing => thing.Cell.Equals(cell)), Is.False);
            PlotInteractable plot = PlotAt(cell);
            Assert.That(plot.IsTilled, Is.False);

            m_farm.Wombat.Mover.Place(CenterOf(cell));
            Run(k_Dt);
            Assert.That(m_farm.Target, Is.SameAs(plot));
            Assert.That(m_farm.TargetAction.Id, Is.EqualTo(ActionTable.k_Till));

            Assert.That(m_state.TrySpendCoins(m_state.Coins - config.TillCost + 1d), Is.True);
            Run(k_Dt);
            Assert.That(m_farm.TargetAction, Is.Null);
            Assert.That(m_farm.TryInteract(), Is.False);
            Assert.That(plot.IsTilled, Is.False);
            Assert.That(tilled, Is.Empty);

            m_state.AddCoins(1d);
            Run(k_Dt);
            Assert.That(m_farm.TargetAction.Id, Is.EqualTo(ActionTable.k_Till));
            Assert.That(m_farm.TryInteract(), Is.True);
            Assert.That(plot.IsTilled, Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(0d));
            Assert.That(tilled.Count, Is.EqualTo(1));

            Run(k_Dt);
            Assert.That(m_farm.TargetAction.Id, Is.EqualTo(ActionTable.k_Plant));
        }

        // 밭은 걷는 바닥: 구멍 아래에서 밭 칸 가운데까지 길이 있고, 조이스틱으로 밭을 가로질러 간다
        [Test]
        public void Fields_DoNotBlockWalking()
        {
            Create();
            GoToFarm();
            BurrowNav nav = m_farm.Layout.Nav;
            Vector2 center = nav.Snap(CenterOf(m_farm.Plots[0].Cell));

            Assert.That(nav.IsWalkable(center), Is.True);
            Assert.That(nav.FindPath(m_farm.Layout.HoleFloor, center).Count, Is.GreaterThan(0));

            Steer(center);

            Assert.That(Vector2.Distance(m_farm.Wombat.Mover.Position, center), Is.LessThan(0.1f));
        }

        // 편집 모드: 카드가 없고, 흙을 누르면 그 칸의 파기 사물을 준다
        [Test]
        public void EditMode_HasNoCardsAndFindsSoilByPoint()
        {
            Create();
            Cell cell = new Cell(-1, Config.StartRows);

            Assert.That(m_farm.ShopKinds, Is.Empty);
            Assert.That(m_farm.DigAt(CenterOf(cell))?.Cell, Is.EqualTo(cell));
            Assert.That(m_farm.DigAt(m_farm.Layout.HoleFloor), Is.Null);
        }

        // 층 상한: 층 안을 다 파면 팔 칸이 없고, 입구 줄을 뺀 칸마다 밭이 있다
        [Test]
        public void Floor_StopsDiggingWhenFull()
        {
            Create();
            FarmConfigTable config = Config;

            while (m_farm.Grid.Frontier().Any())
            {
                m_farm.Grid.Dig(m_farm.Grid.Frontier().First());
            }

            Assert.That(m_farm.Grid.Cells.Count, Is.EqualTo(2 + config.FloorCols * (config.FloorRows - 1)));
            Assert.That(m_farm.Plots.Count, Is.EqualTo(config.FloorCols * (config.FloorRows - 1)));
            Assert.That(m_farm.Things.OfType<DigInteractable>(), Is.Empty);
        }

        // 광장 장식은 농장 문 아래 바닥도 막지 못한다
        [Test]
        public void PlazaDecor_CannotBlockFarmDoor()
        {
            Create();
            Vector2 at = m_plaza.Snap(m_plaza.Layout.FarmDoorFloor + new Vector2(0f, -0.2f));

            Assert.That(m_plaza.CanPlace("bench_log", at), Is.EqualTo(PlacementCheck.Overlaps));
        }

        // 레시피 재료가 창고에 모자라면 칩이 잠기고 굽지 못한다. 채우면 구우면서 레시피만큼 꺼낸다
        [Test]
        public void Bake_NeedsRecipeAndSpendsIt()
        {
            Create();
            BreadTable bread = m_tables.Get<BreadTable>("b01");
            int need = bread.Ingredients.Single(ingredient => ingredient.Item == k_Wheat).Count;
            SetWheat(need - 1);

            Assert.That(BakeOption("b01").State, Is.EqualTo(SheetOptionState.Lacking));
            Assert.That(m_shop.TryChoose(ActionTable.k_Bake, m_shop.Ovens[0], "b01"), Is.False);
            Assert.That(m_shop.Ovens[0].IsEmpty, Is.True);
            Assert.That(m_state.Count(k_Wheat), Is.EqualTo(need - 1));

            m_state.AddItem(k_Wheat, 1);

            Assert.That(BakeOption("b01").State, Is.EqualTo(SheetOptionState.Enabled));
            Assert.That(m_shop.TryChoose(ActionTable.k_Bake, m_shop.Ovens[0], "b01"), Is.True);
            Assert.That(m_state.Count(k_Wheat), Is.EqualTo(0));
        }

        // 창고: 레시피는 다 있어야 한 번에 빼고, 하나라도 모자라면 아무것도 빼지 않는다. 바뀌면 ItemsChanged
        [Test]
        public void TrySpendItems_IsAllOrNothing()
        {
            Create();
            SetWheat(3);
            int changes = 0;
            m_bus.Subscribe<Events.ItemsChanged>(_ => changes++);
            List<IngredientData> recipe = new List<IngredientData>
            {
                new IngredientData { Item = k_Wheat, Count = 2 },
                new IngredientData { Item = k_Wheat + "_none", Count = 1 },
            };

            Assert.That(m_state.TrySpendItems(recipe), Is.False);
            Assert.That(m_state.Count(k_Wheat), Is.EqualTo(3));
            Assert.That(changes, Is.EqualTo(0));

            recipe.RemoveAt(1);

            Assert.That(m_state.TrySpendItems(recipe), Is.True);
            Assert.That(m_state.Count(k_Wheat), Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
        }
    }
}
