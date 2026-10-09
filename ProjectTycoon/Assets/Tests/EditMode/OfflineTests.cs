using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 43 검증 3 · 4: 오프라인 정산(공식 근사)과 실제 시뮬 대조
    public sealed class OfflineTests
    {
        // 계산대 · 오븐에 점원, 웜뱃은 농장으로(가게는 점원만 돈다)
        private static TestGame Staffed(int skill, int seed = 1)
        {
            TestGame game = new TestGame(seed: seed);
            game.Hire(game.Bakery, game.Bakery.Counter, skill);
            game.Hire(game.Bakery, game.Bakery.Ovens[0], skill);
            game.Bus.Publish(new Events.Passed(game.Bakery, FarmArea.k_Id));
            return game;
        }

        // 빵 셋: 별 1(오븐 · 진열대 셋까지), 오븐마다 다른 빵, 재료는 넉넉히
        private static TestGame ThreeBreads(int skill, int seed = 1)
        {
            TestGame game = new TestGame(seed: seed);
            BakeryArea bakery = game.Bakery;
            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.Stars[bakery.Id] = 1;
            GameSave.Apply(data, game.State, game.Mall, game.Tables);
            game.State.AddCoins(1000000d);
            game.State.AddItem("wheat", 100000);
            game.State.AddItem("strawberry", 100000);
            game.Bus.Publish(new Events.Passed(bakery, FarmArea.k_Id));

            // 입구 구멍 곁은 피해 안쪽 줄부터 놓고, 자리가 없으면 옆 칸을 판다
            foreach (string kind in new[] { OvenInteractable.k_Id, OvenInteractable.k_Id, ShelfInteractable.k_Id, ShelfInteractable.k_Id })
            {
                while (!bakery.Grid.Cells.Where(c => c.Row > 0).OrderByDescending(c => c.Row).ThenBy(c => Math.Abs(c.Col))
                    .Any(c => bakery.TryFindSpot(kind, bakery.Layout.Cells.CellCenter(c), out Vector2 spot) && Vector2.Distance(spot, bakery.Layout.HoleFloor) > 1.8f && bakery.TryBuy(kind, spot)))
                {
                    bakery.Grid.Dig(bakery.Grid.Frontier().OrderBy(c => c.Row).ThenBy(c => Math.Abs(c.Col)).First());
                }
            }

            game.Hire(bakery, bakery.Counter, skill);

            // 크루아상 · 케이크는 그 빵을 구울 오븐에서 연다(굽기 시트의 해금 칩은 그 오븐에 바로 굽는다)
            for (int i = 0; i < 3; i++)
            {
                string bread = "b0" + (i + 1);
                Assert.That(i == 0 || bakery.TryChoose(ActionTable.k_Bake, bakery.Ovens[i], bread), Is.True);
                Assert.That(game.Hire(bakery, bakery.Ovens[i], skill).TrySetProduct(bread), Is.True);
            }

            return game;
        }

        [Test]
        public void Settle_WithoutClerks_EarnsNothing()
        {
            TestGame game = new TestGame();
            double coins = game.State.Coins;

            OfflineReport report = Offline.Settle(game.State, game.Mall, game.Tables, 3600d);

            Assert.That(report.Sales, Is.EqualTo(0d));
            Assert.That(report.Items, Is.Empty);
            Assert.That(game.State.Coins, Is.EqualTo(coins));
        }

        // 오븐 점원이 없으면 진열 재고까지만 팔고, 월급은 판 돈 안에서만 낸다(D3)
        [Test]
        public void Settle_CounterClerkOnly_SellsShelfStockAndPaysWithinSales()
        {
            TestGame game = new TestGame();
            BakeryArea bakery = game.Bakery;
            bakery.Shelves[0].Put(game.Tables.Get<BreadTable>("b01"), 6);
            game.Hire(bakery, bakery.Counter, 100);
            double coins = game.State.Coins;

            OfflineReport report = Offline.Settle(game.State, game.Mall, game.Tables, 3600d);

            Assert.That(report.Sales, Is.EqualTo(60d));
            Assert.That(report.Wages, Is.EqualTo(60d));
            Assert.That(bakery.Shelves[0].Stock, Is.EqualTo(0));
            Assert.That(game.State.Coins, Is.EqualTo(coins));
        }

        // 밀이 바닥나면 거기서 굽기가 멈춘다(밀 10개 = 식빵 30개)
        [Test]
        public void Settle_RunsOutOfWheat_StopsBaking()
        {
            TestGame game = Staffed(100);
            game.State.TrySpendItem("wheat", game.State.Count("wheat") - 10);

            OfflineReport report = Offline.Settle(game.State, game.Mall, game.Tables, 28800d);

            Assert.That(game.State.Count("wheat"), Is.EqualTo(0));
            Assert.That(report.Items["wheat"], Is.EqualTo(-10));
            Assert.That(report.Sales, Is.EqualTo(300d).Within(10d));
        }

        // 2026-10-08 밸런스방: 딸기가 0이면 케이크 오븐만 멈추고 식빵 오븐은 그대로 판다(전에는 재료 하나가 모든 빵을 0으로 만들었다)
        [Test]
        public void Settle_LackingIngredient_StopsOnlyItsBread()
        {
            double Sales(bool cake)
            {
                TestGame game = Staffed(100);
                BakeryArea bakery = game.Bakery;
                game.State.AddCoins(10000d);
                game.State.TrySpendItem("strawberry", game.State.Count("strawberry"));
                Assert.That(bakery.TryBuy(OvenInteractable.k_Id, bakery.Layout.OvenBase(new Cell(-1, 3))), Is.True);
                OvenInteractable second = bakery.Ovens[1];
                // 크루아상 · 케이크를 연다(굽기 시트의 해금 칩)
                Assert.That(bakery.TryChoose(ActionTable.k_Bake, second, "b02"), Is.True);
                Assert.That(bakery.TryChoose(ActionTable.k_Bake, second, "b03"), Is.True);

                if (cake)
                {
                    Assert.That(game.Hire(bakery, second, 100).TrySetProduct("b03"), Is.True);
                }

                Assert.That(game.State.Count("strawberry"), Is.EqualTo(0));
                return Offline.Settle(game.State, game.Mall, game.Tables, 3600d).Sales;
            }

            double bread = Sales(false);
            double withCake = Sales(true);

            Assert.That(bread, Is.GreaterThan(1000d));
            Assert.That(withCake, Is.GreaterThan(bread * 0.5));
        }

        [Test]
        public void Settle_ClampsToMaxAndIgnoresNegative()
        {
            TestGame game = Staffed(100);

            Assert.That(Offline.Settle(game.State, game.Mall, game.Tables, -50d).Seconds, Is.EqualTo(0d));
            Assert.That(Offline.Settle(game.State, game.Mall, game.Tables, 100000d).Seconds, Is.EqualTo(28800d));
        }

        // 점원 없는 오븐 · 밭은 시간만큼 흐르고, 축복 · 행상 · 월급날도 흐른다
        [Test]
        public void Settle_AdvancesTimers()
        {
            TestGame game = new TestGame();
            BakeryArea bakery = game.Bakery;
            BreadTable bread = game.Tables.Get<BreadTable>("b01");
            Assert.That(bakery.Ovens[0].TryStart(bread), Is.True);
            PlotInteractable plot = game.Mall.Farm.Plots.First(p => p.IsTilled);
            plot.Plant(game.Tables.GetAll<CropTable>()[0], false);
            Assert.That(game.Mall.Plaza.Statue.TryPray(), Is.True);
            double until = game.Mall.Plaza.Merchant.UntilNext;

            Offline.Settle(game.State, game.Mall, game.Tables, 1000d);

            Assert.That(bakery.Ovens[0].Ready, Is.EqualTo(bread.BatchSize));
            Assert.That(plot.IsRipe, Is.True);
            Assert.That(game.State.Blessing.Active, Is.Null);
            Assert.That(game.Mall.Plaza.Merchant.UntilNext, Is.EqualTo(900d - (1000d - until) % 900d).Within(1e-6));
        }

        // 검증 4: 같은 배치로 실제 시뮬 20분(씨앗 여섯의 평균)과 Settle(20분)의 판 돈이 ±10% 안.
        // 씨앗 하나는 딴짓 난수로 ±13%쯤 흔들린다(일머리 60, 2026-10-04: 시뮬 720~1030 · 평균 877, 공식 885)
        // 빵 셋: 2026-10-09 밸런스방 — 굽는 몫대로 나누던 공식은 시뮬보다 24% 적었다(손님은 빵을 비중으로 고른다)
        [TestCase(100, false)]
        [TestCase(60, false)]
        [TestCase(30, false)]
        [TestCase(100, true)]
        public void Settle_MatchesSimulation(int skill, bool threeBreads)
        {
            const double seconds = 1200d;
            const double dt = 0.05;
            const int seeds = 6;
            double sales = 0d;

            for (int seed = 1; seed <= seeds; seed++)
            {
                TestGame sim = threeBreads ? ThreeBreads(skill, seed) : Staffed(skill, seed);
                sim.Bus.Subscribe<Events.BakeryVisitorPaid>(e => sales += e.Coins);
                sim.Bus.Subscribe<Events.Tipped>(e => sales += e.Coins);

                for (double t = 0d; t < seconds; t += dt)
                {
                    sim.Mall.Tick(dt);
                }
            }

            TestGame model = threeBreads ? ThreeBreads(skill) : Staffed(skill);
            OfflineReport report = Offline.Settle(model.State, model.Mall, model.Tables, seconds);

            Assert.That(report.Sales, Is.EqualTo(sales / seeds).Within(10).Percent);
        }
    }
}
