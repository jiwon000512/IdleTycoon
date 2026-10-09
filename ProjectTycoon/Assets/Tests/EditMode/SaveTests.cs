using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 43 검증: 게임 전체(빵집 · 광장 · 농장 층)를 GameManager처럼 만든다. 난수는 씨앗 고정
    internal sealed class TestGame
    {
        private sealed class SeededRandom : IRandom
        {
            private readonly Random m_random;

            public SeededRandom(int seed)
            {
                m_random = new Random(seed);
            }

            public double NextDouble()
            {
                return m_random.NextDouble();
            }
        }

        public TableSet Tables;
        public EventBus Bus;
        public ZooState State;
        public Mall Mall;

        public BakeryArea Bakery => Mall.Bakery;

        // clock: 설계 54 오늘의 일 시계(없으면 기기 시각)
        public TestGame(TableSet tables = null, int seed = 1, Func<DateTime> clock = null)
        {
            Tables = tables ?? TestTables.Load();
            Bus = new EventBus();
            State = ZooState.CreateNew(Tables, Bus);
            SeededRandom random = new SeededRandom(seed);
            Wombat wombat = new Wombat(Tables, State);
            BakeryArea bakery = new BakeryArea(State, Tables, random, wombat, Bus);
            Mall = new Mall(bakery, new PlazaArea(Tables, bakery, random, wombat, Bus), new FarmArea(Tables, random, wombat, Bus), Bus, clock: clock);
        }

        public VisitorTable ClerkLook => Tables.GetAll<VisitorTable>().First(look => look.Role == VisitorRole.Clerk);

        // 일머리를 정한 점원을 들인다(첫 월급은 낸다)
        public Clerk Hire(WombatArea area, Interactable thing, int skill)
        {
            Candidate candidate = new Candidate("시험", skill, ClerkLook);
            Assert.That(area.TryHire(candidate, thing, area.WageFor(candidate, thing)), Is.True);
            return area.ClerkOf(thing);
        }

        public string Json()
        {
            return JsonConvert.SerializeObject(GameSave.Capture(State, Mall));
        }
    }

    public sealed class SaveTests
    {
        // 저장 모양으로만 만들 수 있는 진행(업그레이드 · 해금 · 유물 · 별 · 축복 · 아래층)을 덮은 게임
        private static TestGame Progressed()
        {
            TestGame game = new TestGame();
            SaveData data = GameSave.Capture(game.State, game.Mall);
            TableSet tables = game.Tables;
            data.Coins = 12345;
            data.Items["wheat"] = 7;
            data.Blessing = tables.GetAll<BlessingTable>()[0].Id;
            data.BlessingLeft = 100;
            data.BlessingCooldown = 400;
            string relic = tables.GetAll<RelicTable>()[0].Id;
            data.Relics[relic] = 2;
            data.RelicSlots = new[] { relic, null, null }.ToList();
            data.RelicSales = 2;
            data.Stars[BakeryArea.k_Id] = 3;
            data.UntilPayday = 25;

            AreaSave bakery = data.Areas.First(a => a.Id == BakeryArea.k_Id);
            Cell dug = game.Bakery.Grid.Frontier().First();
            bakery.Cells.Add(new[] { dug.Col, dug.Row });
            bakery.Upgrades["oven"] = 2;
            bakery.Upgrades["shelf"] = 1;
            bakery.Unlocked.Add("b02");
            bakery.Stored["shelf"] = 1;
            bakery.EvaluationCooldown = 50;
            ThingSave shelf = bakery.Things.First(t => t.Kind == ShelfInteractable.k_Id);
            shelf.Bread = "b01";
            shelf.Stock = 5;
            int oven = bakery.Things.FindIndex(t => t.Kind == OvenInteractable.k_Id);
            bakery.Things[oven].Bread = "b02";
            bakery.Things[oven].LastBread = "b02";
            bakery.Things[oven].Remaining = 4;
            bakery.Clerks.Add(new ClerkSave { Thing = oven, Name = "곰곰", Skill = 70, Wage = 20, Look = game.ClerkLook.Id, Product = "b02" });
            bakery.Candidates.Add(new CandidateSave { Name = "도토리", Skill = 33, Look = game.ClerkLook.Id });

            AreaSave farm = data.Areas.First(a => a.Id == FarmArea.k_Id);
            farm.Unlocked = tables.GetAll<CropTable>().Select(c => c.Id).ToList();
            PlotSave plot = farm.Plots.First(p => p.Tilled);
            plot.Crop = tables.GetAll<CropTable>()[0].Id;
            plot.Remaining = 5;
            plot.Fertilized = true;
            farm.Clerks.Add(new ClerkSave { Thing = -1, Name = "콩콩", Skill = 50, Wage = 15, Look = game.ClerkLook.Id, Product = plot.Crop });
            data.Areas.First(a => a.Id == game.Mall.Farms[1].Id).Open = true;
            data.Areas.First(a => a.Id == PlazaArea.k_Id).MerchantUntil = 123;

            GameSave.Apply(JsonConvert.DeserializeObject<SaveData>(JsonConvert.SerializeObject(data)), game.State, game.Mall, game.Tables);
            return game;
        }

        [Test]
        public void Apply_RestoresProgress()
        {
            TestGame game = Progressed();
            BakeryArea bakery = game.Bakery;
            FarmArea farm = game.Mall.Farm;

            Assert.That(game.State.Coins, Is.EqualTo(12345));
            Assert.That(game.State.Count("wheat"), Is.EqualTo(7));
            Assert.That(game.State.Blessing.Active, Is.Not.Null);
            Assert.That(game.State.Blessing.Remaining, Is.EqualTo(100));
            Assert.That(game.State.Relics.Slots[0], Is.Not.Null);
            Assert.That(game.State.Stars.Count(BakeryArea.k_Id), Is.EqualTo(3));
            Assert.That(bakery.UpgradeLevel("oven"), Is.EqualTo(2));
            Assert.That(bakery.UnlockedBreads.Select(b => b.Id), Is.EqualTo(new[] { "b01", "b02" }));
            Assert.That(bakery.StoredCount("shelf"), Is.EqualTo(1));
            Assert.That(bakery.Shelves[0].Stock, Is.EqualTo(5));
            Assert.That(bakery.Ovens[0].Bread.Id, Is.EqualTo("b02"));
            Assert.That(bakery.Ovens[0].Remaining, Is.EqualTo(4));
            Assert.That(bakery.ClerkOf(bakery.Ovens[0]).Product, Is.EqualTo("b02"));
            Assert.That(bakery.Evaluation.Cooldown, Is.EqualTo(50));
            Assert.That(farm.ClerkOf(farm.Barn).Skill, Is.EqualTo(50));
            Assert.That(farm.Plots.First(p => p.Crop != null).IsFertilized, Is.True);
            Assert.That(game.Mall.Farms[1].IsOpen, Is.True);
            Assert.That(game.Mall.Plaza.Merchant.UntilNext, Is.EqualTo(123));
            Assert.That(game.Mall.Payroll.UntilPayday, Is.EqualTo(25));
        }

        // 검증 2: Capture → JSON → 새 게임에 Apply → 다시 Capture가 같다
        [Test]
        public void CaptureApply_RoundTrips()
        {
            TestGame first = Progressed();
            string json = first.Json();
            TestGame second = new TestGame(seed: 2);

            GameSave.Apply(JsonConvert.DeserializeObject<SaveData>(json), second.State, second.Mall, second.Tables);

            Assert.That(second.Json(), Is.EqualTo(json));
        }

        // 놓고 옮기고 보관한 배치 · 판 칸이 그대로 돌아온다
        [Test]
        public void CaptureApply_KeepsPlacement()
        {
            TestGame first = new TestGame();
            BakeryArea bakery = first.Bakery;
            first.State.AddCoins(100000);
            bakery.Grid.Dig(bakery.Grid.Frontier().First());
            Assert.That(bakery.TryFindSpot(ShelfInteractable.k_Id, new Vector2(0f, -3f), out Vector2 spot), Is.True);
            Assert.That(bakery.TryBuy(ShelfInteractable.k_Id, spot), Is.True);
            Assert.That(bakery.TryStore(bakery.Shelves[0]), Is.True);
            string json = first.Json();
            TestGame second = new TestGame();

            GameSave.Apply(JsonConvert.DeserializeObject<SaveData>(json), second.State, second.Mall, second.Tables);

            Assert.That(second.Bakery.Shelves.Select(s => s.Position), Is.EqualTo(bakery.Shelves.Select(s => s.Position)));
            Assert.That(second.Bakery.StoredCount(ShelfInteractable.k_Id), Is.EqualTo(1));
            Assert.That(second.Bakery.Grid.Cells.Count, Is.EqualTo(bakery.Grid.Cells.Count));
            Assert.That(second.Json(), Is.EqualTo(json));
        }

        // 표에서 사라진 id는 건너뛴다(던지지 않는다)
        [Test]
        public void Apply_SkipsUnknownIds()
        {
            TestGame game = new TestGame();
            SaveData data = GameSave.Capture(game.State, game.Mall);
            AreaSave bakery = data.Areas.First(a => a.Id == BakeryArea.k_Id);
            bakery.Things.Add(new ThingSave { Kind = "gone", X = 0f, Y = -3f });
            bakery.Things[0].Bread = "gone";
            bakery.Unlocked.Add("gone");
            bakery.Clerks.Add(new ClerkSave { Thing = 0, Name = "누구", Skill = 10, Wage = 5, Look = "gone" });
            data.Relics["gone"] = 1;
            data.RelicSlots = new[] { "gone" }.ToList();
            data.Blessing = "gone";
            data.Areas.Add(new AreaSave { Id = "gone" });

            Assert.DoesNotThrow(() => GameSave.Apply(data, game.State, game.Mall, game.Tables));
            Assert.That(game.Bakery.Shelves.Count + game.Bakery.Ovens.Count + game.Bakery.Counters.Count, Is.EqualTo(3));
            Assert.That(game.Bakery.Clerks, Is.Empty);
            Assert.That(game.State.Blessing.Active, Is.Null);
        }
    }
}
