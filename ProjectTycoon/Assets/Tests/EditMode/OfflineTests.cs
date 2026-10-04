using System.Linq;
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
        [TestCase(100)]
        [TestCase(60)]
        [TestCase(30)]
        public void Settle_MatchesSimulation(int skill)
        {
            const double seconds = 1200d;
            const double dt = 0.05;
            const int seeds = 6;
            double sales = 0d;

            for (int seed = 1; seed <= seeds; seed++)
            {
                TestGame sim = Staffed(skill, seed);
                sim.Bus.Subscribe<Events.BakeryVisitorPaid>(e => sales += e.Coins);
                sim.Bus.Subscribe<Events.Tipped>(e => sales += e.Coins);

                for (double t = 0d; t < seconds; t += dt)
                {
                    sim.Mall.Tick(dt);
                }
            }

            TestGame model = Staffed(skill);
            OfflineReport report = Offline.Settle(model.State, model.Mall, model.Tables, seconds);

            Assert.That(report.Sales, Is.EqualTo(sales / seeds).Within(10).Percent);
        }
    }
}
