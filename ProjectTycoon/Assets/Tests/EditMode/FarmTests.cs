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

        // roll: 거둘 때마다 굴리는 덤 난수(기본은 안 나오는 값)
        private void Create(double roll = 0.99)
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);

            // 테스트 표는 재료를 넉넉히 준다(TestTables.k_PlentyItems). 거름 · 덤은 0에서 시작해야 규칙이 보인다
            foreach (string item in new[] { Config.ManureItem, Config.BonusItem })
            {
                m_state.TrySpendItem(item, m_state.Count(item));
            }

            Wombat wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, new SequenceRandom(new double[2000]), wombat, m_bus);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 4000).ToArray()), wombat, m_bus);
            m_farm = new FarmArea(m_tables, new SequenceRandom(Enumerable.Repeat(roll, 200).ToArray()), wombat, m_bus);
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

        // 밭 칸에 서면 버튼 = 심기(밭 시트 → 밀 칩). 자라는 동안은 밭에 서 있어도 거두지 않고, 익은 밭 칸에 발이 들면 저절로 거둬 창고로(들고 다니지 않는다). 옆 칸에서는 거두지 않는다
        [Test]
        public void Plant_GrowsThenHarvestsWhenSteppingOnField()
        {
            Create();
            GoToFarm();
            PlotInteractable plot = m_farm.Plots[0];
            CropTable crop = m_farm.UnlockedCrops[0];
            Vector2 inside = CenterOf(plot.Cell);
            Vector2 beside = CenterOf(plot.Cell.Offset(1, 0));
            int wheat = m_state.Count(k_Wheat);
            List<Events.Harvested> harvested = new List<Events.Harvested>();
            m_bus.Subscribe<Events.Harvested>(e => harvested.Add(e));

            m_farm.Wombat.Mover.Place(inside);
            Run(k_Dt);
            Assert.That(m_farm.Target, Is.SameAs(plot));
            Sow();
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

        // 설계 35: 밭 시트는 연 작물 칩(처음엔 밀) + 다음 작물 해금 칩(값 unlockCost). 코인이 모자라면 해금 칩이 Poor이고 골라도 아무것도 바뀌지 않는다.
        // 사면 열리고 그 밭에 바로 심으며, 거두면 그 작물의 재료가 창고로. 심은 밭에는 더 심지 못한다
        [Test]
        public void PlantSheet_UnlocksNextCrop_AndPlantsItRightAway()
        {
            Create();
            GoToFarm();
            PlotInteractable plot = m_farm.Plots[0];
            CropTable wheat = m_farm.UnlockedCrops[0];
            CropTable next = m_farm.NextCrop;
            SheetAction plant = m_farm.SheetActions(plot).Single();
            List<Events.Harvested> harvested = new List<Events.Harvested>();
            m_bus.Subscribe<Events.Harvested>(e => harvested.Add(e));
            m_farm.Wombat.Mover.Place(CenterOf(plot.Cell));
            Run(k_Dt);
            m_state.AddCoins(next.UnlockCost);
            Assert.That(m_state.TrySpendCoins(m_state.Coins - next.UnlockCost + 1d), Is.True);

            IReadOnlyList<SheetOption> options = plant.Options(m_farm.Wombat.Worker, plot);
            Assert.That(options.Select(o => o.Option), Is.EqualTo(new[] { wheat.Id, next.Id }));
            Assert.That(options[0].State, Is.EqualTo(SheetOptionState.Enabled));
            Assert.That(options[1].State, Is.EqualTo(SheetOptionState.Poor));
            Assert.That(options[1].Cost, Is.EqualTo(next.UnlockCost));
            Assert.That(m_farm.TryChoose(ActionTable.k_Plant, plot, next.Id), Is.False);
            Assert.That(m_farm.UnlockedCrops.Count, Is.EqualTo(1));
            Assert.That(plot.IsEmpty, Is.True);

            m_state.AddCoins(1d);
            Assert.That(m_farm.TryChoose(ActionTable.k_Plant, plot, next.Id), Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(0d));
            Assert.That(m_farm.UnlockedCrops, Is.EqualTo(new[] { wheat, next }));
            Assert.That(m_farm.NextCrop, Is.Null);
            Assert.That(plot.Crop, Is.SameAs(next));
            Assert.That(m_farm.TryChoose(ActionTable.k_Plant, plot, wheat.Id), Is.False);
            Assert.That(plant.Options(m_farm.Wombat.Worker, plot).Select(o => o.Option), Is.EqualTo(new[] { wheat.Id, next.Id }));

            int before = m_state.Count(next.Item);
            m_farm.Wombat.Mover.Place(MarginOf(plot.Cell));
            Run(next.GrowSeconds + k_Dt);
            m_farm.Wombat.Mover.Place(CenterOf(plot.Cell));
            Run(k_Dt);
            Assert.That(m_state.Count(next.Item), Is.EqualTo(before + next.Yield));
            Assert.That(harvested.Single().Crop, Is.SameAs(next));
        }

        // 2026-09-30 안 A: 익은 밭은 발이 밭 몸통(칸 둘레 여백 fieldInset 안쪽)에 들 때만 거둔다. 여백에 서면 대상이어도 그대로
        [Test]
        public void Harvest_NeedsFeetOnFieldBody()
        {
            Create();
            GoToFarm();
            PlotInteractable plot = m_farm.Plots[0];
            CellMetrics cells = m_farm.Layout.Cells;
            Vector2 center = cells.CellCenter(plot.Cell);
            Vector2 margin = new Vector2(center.X, center.Y + cells.CellHeight * 0.5f - (float)Config.FieldInset * 0.5f);
            int wheat = m_state.Count(k_Wheat);

            m_farm.Wombat.Mover.Place(center);
            Run(k_Dt);
            Sow();
            m_farm.Wombat.Mover.Place(margin);
            Run(plot.Crop.GrowSeconds + k_Dt);
            Assert.That(plot.IsRipe, Is.True);
            Assert.That(m_farm.Target, Is.SameAs(plot));
            Assert.That(m_state.Count(k_Wheat), Is.EqualTo(wheat));

            m_farm.Wombat.Mover.Place(center);
            Run(k_Dt);
            Assert.That(plot.IsEmpty, Is.True);
            Assert.That(m_state.Count(k_Wheat), Is.EqualTo(wheat + plot.Farm.UnlockedCrops[0].Yield));
        }

        // 설계 35: 빈 밭의 버튼은 밭 시트를 열고(TryInteract false), 시트의 작물 칩이 웜뱃이 선 밭에 심는다
        private void Sow(string crop = k_Wheat)
        {
            Assert.That(m_farm.TargetAction.Id, Is.EqualTo(ActionTable.k_OpenPlant));
            Assert.That(m_farm.TryInteract(), Is.False);
            Assert.That(m_farm.TryChoose(ActionTable.k_Plant, m_farm.Target, crop), Is.True);
        }

        // 칸 윗변 여백(칸 안 · 밭 몸통 밖): 기다리는 동안 거두지 않는 자리
        private Vector2 MarginOf(Cell cell)
        {
            CellMetrics cells = m_farm.Layout.Cells;
            Vector2 center = cells.CellCenter(cell);
            return new Vector2(center.X, center.Y + cells.CellHeight * 0.5f - (float)Config.FieldInset * 0.5f);
        }

        // 설계 28: 창고에 거름이 있으면 심을 때 하나 쓰고 그 밭은 manureGrowScale만큼 빨리 익는다. 거름이 없으면 그대로
        [Test]
        public void Plant_WithManure_SpendsOneAndGrowsFaster()
        {
            Create();
            GoToFarm();
            PlotInteractable fertilized = m_farm.Plots[0];
            PlotInteractable plain = m_farm.Plots[1];
            CropTable crop = m_farm.UnlockedCrops[0];
            m_state.AddItem(Config.ManureItem, 1);

            m_farm.Wombat.Mover.Place(CenterOf(fertilized.Cell));
            Run(k_Dt);
            Sow();
            Assert.That(fertilized.IsFertilized, Is.True);
            Assert.That(m_state.Count(Config.ManureItem), Is.EqualTo(0));

            m_farm.Wombat.Mover.Place(CenterOf(plain.Cell));
            Run(k_Dt);
            Sow();
            Assert.That(plain.IsFertilized, Is.False);

            m_farm.Wombat.Mover.Place(MarginOf(plain.Cell));
            Run(crop.GrowSeconds * Config.ManureGrowScale + 0.1);
            Assert.That(fertilized.IsRipe, Is.True);
            Assert.That(plain.IsRipe, Is.False);

            Run(crop.GrowSeconds * (1d - Config.ManureGrowScale));
            Assert.That(plain.IsRipe, Is.True);
        }

        // 설계 28: 덤은 bonusChance로, 거름 준 밭은 × manureBonusScale. 난수 0.3이면 보통 밭은 없고(0.3 ≥ 0.2) 거름 밭은 하나 나온다(0.3 < 0.4). 거두면 거름은 꺼진다
        [Test]
        public void Harvest_BonusChance_DoublesOnFertilizedField()
        {
            Create(0.3);
            GoToFarm();
            PlotInteractable plot = m_farm.Plots[0];
            List<Events.BonusFound> found = new List<Events.BonusFound>();
            m_bus.Subscribe<Events.BonusFound>(e => found.Add(e));

            PlantGrowHarvest(plot);
            Assert.That(m_state.Count(Config.BonusItem), Is.EqualTo(0));
            Assert.That(found, Is.Empty);

            m_state.AddItem(Config.ManureItem, 1);
            PlantGrowHarvest(plot);
            Assert.That(m_state.Count(Config.BonusItem), Is.EqualTo(1));
            Assert.That(found.Count, Is.EqualTo(1));
            Assert.That(found[0].Item, Is.EqualTo(Config.BonusItem));
            Assert.That(plot.IsFertilized, Is.False);
        }

        // 몸통에서 심고 → 여백에서 익히고 → 몸통을 밟아 거둔다
        private void PlantGrowHarvest(PlotInteractable plot)
        {
            m_farm.Wombat.Mover.Place(CenterOf(plot.Cell));
            Run(k_Dt);
            Sow();
            m_farm.Wombat.Mover.Place(MarginOf(plot.Cell));
            Run(plot.Crop.GrowSeconds + k_Dt);
            Assert.That(plot.IsRipe, Is.True);
            m_farm.Wombat.Mover.Place(CenterOf(plot.Cell));
            Run(k_Dt);
            Assert.That(plot.IsEmpty, Is.True);
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
            Assert.That(m_farm.TargetAction.Id, Is.EqualTo(ActionTable.k_OpenPlant));
        }

        // QA A: 밭 위에 서도 곁에 흙 칸(파기)이 있으면 흙 칸이 대상(삽 버튼). 흙 칸에서 멀면 밭이 대상
        [Test]
        public void Target_OnField_YieldsToAdjacentSoil()
        {
            Create();
            GoToFarm();
            PlotInteractable plot = m_farm.Plots[0];
            CellMetrics cells = m_farm.Layout.Cells;
            Vector2 center = cells.CellCenter(plot.Cell);
            Vector2 edge = new Vector2(center.X, center.Y - cells.CellHeight * 0.5f + 0.2f);

            m_farm.Wombat.Mover.Place(edge);
            Run(k_Dt);
            Assert.That(m_farm.Target, Is.InstanceOf<DigInteractable>());
            Assert.That(((DigInteractable)m_farm.Target).Cell, Is.EqualTo(plot.Cell.Offset(0, 1)));
            Assert.That(m_farm.TargetAction.Id, Is.EqualTo(ActionTable.k_OpenDig));

            m_farm.Wombat.Mover.Place(center);
            Run(k_Dt);
            Assert.That(m_farm.Target, Is.SameAs(plot));
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
