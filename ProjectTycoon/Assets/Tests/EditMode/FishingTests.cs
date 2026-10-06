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
    // 설계 44 · 46 · 49 검증: 낚시 디펜스. 물길을 따라 오는 물고기 · 대의 감기 · 대 사기(단 뽑기 · 바꾸기) · 계열 문턱 · 쿵 · 월척 · 미끼 노점(업그레이드 둘) · 광장 문 · 점원 · 저장 · 오프라인.
    // 설계 49: 물때 마지막의 대물(낚으면 단계 · 놓치면 그대로) · 단계 보람(재료) · 종류(옮기기 · 바꾸기 · 합치기 · 특징 다섯) · 대물로 열리는 계열.
    // 웜뱃을 말뚝 · 노점 앞에 세우고 버튼(TryInteract) · 시트 줄(TryChoose)을 누르는 실제 길로 다룬다. 낚시터 난수는 늘 같은 값(0.3: 첫 종류 · 피라미 · 작은 크기 · 월척 아님)
    public sealed class FishingTests
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
        private FishingArea m_fishing;
        private Mall m_mall;

        private FishingConfigTable Config => m_tables.Get<FishingConfigTable>(FishingConfigTable.k_Main);

        private void Create(double roll = 0.3, Action<TableSet> tweak = null)
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            tweak?.Invoke(m_tables);
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            m_state.AddCoins(100000d);
            Wombat wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, new SequenceRandom(new double[4000]), wombat, m_bus);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 40000).ToArray()), wombat, m_bus);
            m_fishing = new FishingArea(m_tables, new FixedRandom(roll), wombat, m_bus);
            m_mall = new Mall(m_shop, m_plaza, new FarmArea(m_tables, new FixedRandom(0.99), wombat, m_bus), m_bus, m_fishing);
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

        // 빵집 → 광장 → 낚시터(통로 사건으로 바로)
        private void GoFishing()
        {
            m_bus.Publish(new Events.Passed(m_shop, PlazaArea.k_Id));
            m_bus.Publish(new Events.Passed(m_plaza, FishingArea.k_Id));
            Run(k_Dt);
        }

        // 말뚝 바로 아래에 서서 버튼
        private void Press(StakeInteractable stake)
        {
            m_mall.Wombat.Mover.Place(stake.Position + new Vector2(0f, -0.3f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(stake), "말뚝이 대상");
            Assert.That(m_fishing.TryInteract(), Is.True, "버튼");
        }

        private string ActionAt(StakeInteractable stake)
        {
            m_mall.Wombat.Mover.Place(stake.Position + new Vector2(0f, -0.3f));
            Run(k_Dt);
            return m_fishing.TargetAction?.Id;
        }

        private StakeInteractable Stake(int i)
        {
            return m_fishing.Stakes[i];
        }

        // 말뚝 앞에서 대 사기 버튼(시트를 연다) → 시트 칩에서 계열을 고른다
        private void Buy(StakeInteractable stake, string rod = RodTable.k_Bamboo)
        {
            Assert.That(ActionAt(stake), Is.EqualTo(ActionTable.k_OpenSummon), "열린 말뚝은 대 사기 버튼");
            Assert.That(m_fishing.TryChoose(ActionTable.k_Summon, stake, rod), Is.True, "대 사기 칩");
        }

        // 미끼 노점 앞에 선다
        private void AtHut()
        {
            m_mall.Wombat.Mover.Place(m_fishing.Hut.Position + new Vector2(0f, -0.3f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(m_fishing.Hut), "미끼 노점이 대상");
        }

        [Test]
        public void Fish_SwimTheStream_AndEscapeAtTheEnd()
        {
            Create();
            int waves = 0;
            int escaped = 0;
            m_bus.Subscribe<Events.WaveStarted>(_ => waves++);
            m_bus.Subscribe<Events.FishEscaped>(_ => escaped++);

            Run(1d);
            Assert.That(waves, Is.EqualTo(1), "물때 시작 사건");
            Assert.That(m_fishing.Fish, Is.Not.Empty);
            Fish first = m_fishing.Fish[0];
            float s = first.S;
            Run(1d);
            Assert.That(first.S - s, Is.EqualTo((float)first.Kind.Speed).Within(0.1f), "속도대로");

            Assert.That(RunUntil(() => escaped > 0, 60d), Is.True, "대가 없으면 끝에서 놓친다(놓칠 때마다 사건)");
        }

        // 설계 46: 열린 말뚝에서 계열을 골라 산다(값은 살 때마다 summonGrowth배, 등급은 뽑기). 꽂힌 말뚝에서 다시 사면 옛 대가 바뀐다
        [Test]
        public void Buy_ChosenFamily_PriceGrows_Replaces_RodCatchesFish()
        {
            Create();
            GoFishing();
            double coins = m_state.Coins;
            double price = m_fishing.SummonPrice;
            StakeInteractable summoned = null;
            m_bus.Subscribe<Events.RodSummoned>(e => summoned = e.Stake);

            Buy(Stake(0), RodTable.k_Iron);
            Assert.That(summoned, Is.SameAs(Stake(0)));
            Assert.That(m_state.Coins, Is.EqualTo(coins - price).Within(1e-6));
            Assert.That(Stake(0).Rod.Id, Is.EqualTo(RodTable.k_Iron), "고른 계열의 첫 종류");
            Assert.That(m_fishing.SummonPrice, Is.EqualTo(price * Config.SummonGrowth).Within(1e-6));

            Buy(Stake(0), RodTable.k_Bamboo);
            Assert.That(Stake(0).Rod.Id, Is.EqualTo(RodTable.k_Bamboo), "꽂힌 말뚝에서 다시 사면 옛 대는 사라진다");
            Assert.That(m_fishing.Summons, Is.EqualTo(2));

            int minnows = m_state.Count("fish_minnow");
            bool reeled = false;
            Assert.That(RunUntil(() => (reeled |= Stake(0).Reeling != null) && m_state.Count("fish_minnow") > minnows, 120d), Is.True, "지나가는 피라미를 감아 낚는다");
            Assert.That(reeled, Is.True, "감는 동안 Reeling(낚싯줄)");
            Assert.That(m_fishing.Log["minnow"].Caught, Is.GreaterThan(0));
        }

        // 단 뽑기: 난수가 tierWeights 누적 비중의 어디에 떨어지나(80 · 18 · 2 → 0.3 첫 종류, 0.9 둘째, 0.99 셋째). 꽂히는 것은 그 계열의 그 단 종류
        [TestCase(0.3, "bamboo")]
        [TestCase(0.9, "bamboo_2")]
        [TestCase(0.99, "bamboo_3")]
        public void Buy_RollsTier_ByWeights(double roll, string kind)
        {
            Create(roll);
            GoFishing();
            Buy(Stake(0));
            Assert.That(Stake(0).Rod.Id, Is.EqualTo(kind));
        }

        // 설계 49 낚시판 보기: 대를 빈 말뚝에 놓으면 옮기고, 다른 종류에 놓으면 자리를 바꾼다. 잠긴 말뚝에는 못 놓는다
        [Test]
        public void MoveRod_ToEmptyStake_Moves_ToOtherKind_Swaps()
        {
            Create();
            GoFishing();
            Buy(Stake(0));
            Assert.That(m_fishing.TryMoveRod(Stake(0), Stake(1)), Is.True);
            Assert.That(Stake(0).Rod, Is.Null);
            Assert.That(Stake(1).Rod.Id, Is.EqualTo(RodTable.k_Bamboo));

            Buy(Stake(0), RodTable.k_Iron);
            Assert.That(m_fishing.TryMoveRod(Stake(0), Stake(1)), Is.True);
            Assert.That(Stake(0).Rod.Id, Is.EqualTo(RodTable.k_Bamboo), "자리를 바꾼다");
            Assert.That(Stake(1).Rod.Id, Is.EqualTo(RodTable.k_Iron));

            Assert.That(m_fishing.TryMoveRod(Stake(0), m_fishing.Stakes.First(s => !s.Open)), Is.False, "잠긴 말뚝");
            Assert.That(m_fishing.TryMoveRod(Stake(2), Stake(0)), Is.False, "빈 말뚝은 끌 것이 없다");
        }

        // 설계 49 합치기: 같은 종류 둘 → 그 계열의 윗 종류(끈 쪽 말뚝은 빈다). 맨 윗 단끼리는 못 합친다
        [Test]
        public void MoveRod_OntoSameKind_MergesIntoTheNextTier()
        {
            Create();
            GoFishing();
            Buy(Stake(0));
            Buy(Stake(1));
            Events.RodMerged merged = default;
            m_bus.Subscribe<Events.RodMerged>(e => merged = e);

            Assert.That(m_fishing.TryMoveRod(Stake(0), Stake(1)), Is.True);
            Assert.That(Stake(0).Rod, Is.Null);
            Assert.That(Stake(1).Rod.Id, Is.EqualTo("bamboo_2"));
            Assert.That(merged.Stake, Is.SameAs(Stake(1)));
            Assert.That(merged.From, Is.SameAs(Stake(0)));

            Create(0.99);
            GoFishing();
            Buy(Stake(0));
            Buy(Stake(1));
            Assert.That(Stake(0).Rod.Id, Is.EqualTo("bamboo_3"));
            Assert.That(m_fishing.TryMoveRod(Stake(0), Stake(1)), Is.False, "맨 윗 단");
            Assert.That(Stake(0).Rod, Is.SameAs(Stake(1).Rod), "둘 다 그대로");
        }

        // 월척을 붙잡은 대는 못 옮긴다(그 대에 놓지도 못한다)
        [Test]
        public void MoveRod_WhileHoldingTrophy_IsRefused()
        {
            Create(tweak: t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).TrophyChance = 1d);
            GoFishing();
            Buy(Stake(0));
            Assert.That(RunUntil(() => Stake(0).Hooked != null, 60d), Is.True);
            Assert.That(m_fishing.TryMoveRod(Stake(0), Stake(2)), Is.False);
            Buy(Stake(2));
            Assert.That(m_fishing.TryMoveRod(Stake(2), Stake(0)), Is.False);
            Assert.That(Stake(0).Rod.Id, Is.EqualTo(RodTable.k_Bamboo));
        }

        // 한 틱에 감긴(Reeled가 늘어난) 물고기 수의 최댓값
        private int MostReeledAtOnce(double seconds)
        {
            Dictionary<Fish, double> before = new Dictionary<Fish, double>();
            int most = 0;

            for (double t = 0d; t < seconds; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
                most = Math.Max(most, m_fishing.Fish.Count(f => before.TryGetValue(f, out double reeled) && f.Reeled > reeled));
                before = m_fishing.Fish.ToDictionary(f => f, f => f.Reeled);
            }

            return most;
        }

        // 설계 49 종류 특징: 대나무대는 한 마리씩, 쌍줄대(targets 2)는 두 마리를 함께 감는다
        [TestCase(0.3, 1)]
        [TestCase(0.9, 2)]
        public void Kind_Targets_ReelsThatManyFishAtOnce(double roll, int targets)
        {
            Create(roll);
            GoFishing();
            Buy(Stake(0));
            Assert.That(Stake(0).Rod.Targets, Is.EqualTo(targets));
            Assert.That(MostReeledAtOnce(40d), Is.EqualTo(targets));
        }

        // 설계 49 종류 특징: 작살대(bigGame)는 월척을 붙잡지 않고 그대로 감는다
        [Test]
        public void Kind_BigGame_ReelsTrophyWithoutHooking()
        {
            Create(0.9, t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).TrophyChance = 1d);
            GoFishing();
            Buy(Stake(0), RodTable.k_Iron);
            Assert.That(Stake(0).Rod.BigGame, Is.GreaterThan(0d), "작살대");
            bool hooked = false;
            Assert.That(RunUntil(() => (hooked |= Stake(0).Hooked != null) || m_fishing.Fish.Any(f => f.Trophy && f.Reeled > 0d), 60d), Is.True);
            Assert.That(hooked, Is.False);
        }

        // 설계 49 종류 특징: 닻대(bossSlow)는 사거리 안 대물을 느리게 한다
        [Test]
        public void Kind_BossSlow_SlowsTheBossInReach()
        {
            Create(0.99, t =>
            {
                FishingConfigTable c = t.Get<FishingConfigTable>(FishingConfigTable.k_Main);
                c.WavesPerStage = 1;
                c.Boss.Weight = 100000d;
            });
            GoFishing();
            Buy(Stake(0), RodTable.k_Iron);
            RodTable anchor = Stake(0).Rod;
            Assert.That(anchor.BossSlow, Is.LessThan(1d), "닻대");
            Assert.That(RunUntil(() => m_fishing.Fish.Any(f => f.Boss), 30d), Is.True, "대물");
            Fish boss = m_fishing.Fish.First(f => f.Boss);
            double slowest = double.MaxValue;
            double fastest = 0d;

            for (double t = 0d; t < 60d && m_fishing.Fish.Contains(boss); t += k_Dt)
            {
                float s = boss.S;
                m_mall.Tick(k_Dt);
                slowest = Math.Min(slowest, (boss.S - s) / k_Dt);
                fastest = Math.Max(fastest, (boss.S - s) / k_Dt);
            }

            Assert.That(fastest, Is.EqualTo(boss.Speed).Within(0.01), "사거리 밖");
            Assert.That(slowest, Is.EqualTo(boss.Speed * anchor.BossSlow).Within(0.01), "사거리 안");
        }

        // 설계 49 종류 특징: 그물대(stunEvery · stun)는 사거리 안 물고기를 주기마다 멈춘다
        [Test]
        public void Kind_Stun_StopsFishInReach()
        {
            Create(tweak: t => t.Get<RodTable>(RodTable.k_Net).UnlockStage = 1);
            GoFishing();
            Buy(Stake(0), RodTable.k_Net);
            m_mall.Wombat.Mover.Place(m_fishing.Layout.HoleFloor);
            Assert.That(RunUntil(() => m_fishing.Fish.Any(f => f.StunLeft > 0d), 60d), Is.True, "쿵 없이 멈춘다");
        }

        // 그물은 꽂은 뒤 한 주기를 기다린다(꽂거나 옮기자마자 멈추면 말뚝을 돌려 가며 물고기를 세울 수 있다): 사거리에 물고기가 있어도 바로 멈추지 않는다
        [Test]
        public void Kind_Stun_WaitsOneIntervalAfterTheRodIsPlaced()
        {
            Create(tweak: t => t.Get<RodTable>(RodTable.k_Net).UnlockStage = 1);
            GoFishing();
            Run(3.2d);
            Buy(Stake(1), RodTable.k_Net);
            bool reeling = false;

            for (double t = 0d; t < 0.5d; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
                reeling |= Stake(1).Reeling != null;
                Assert.That(m_fishing.Fish.Any(f => f.StunLeft > 0d), Is.False, "꽂자마자 멈추지 않는다");
            }

            Assert.That(reeling, Is.True, "사거리에 물고기가 있었다");
            Assert.That(RunUntil(() => m_fishing.Fish.Any(f => f.StunLeft > 0d), 60d), Is.True, "한 주기 뒤에는 멈춘다");
        }

        // 소용돌이는 대물을 밀지 않는다(느린 대물이 되돌려지기만 하면 물때가 끝나지 않는다): 대가 없으면 대물은 끝까지 가서 빠져나간다
        [Test]
        public void Whirl_DoesNotPushTheBoss_SoTheBossWaveEnds()
        {
            Create(tweak: t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).WavesPerStage = 1);
            GoFishing();
            BuyUpgrade(FishingUpgradeData.k_Whirl, m_fishing.Upgrade(FishingUpgradeData.k_Whirl).MaxLevel);
            bool pushed = false;
            bool escaped = false;
            m_bus.Subscribe<Events.FishWhirled>(e => pushed |= e.Fish.Boss);
            m_bus.Subscribe<Events.BossEscaped>(_ => escaped = true);

            Assert.That(RunUntil(() => escaped, 300d), Is.True, "빠져나간다");
            Assert.That(pushed, Is.False);
            Assert.That(m_fishing.BossWave, Is.False);
        }

        // 설계 49 종류 특징: 꿀떡밥대(bonus) 사거리 안에서 낚이면 재료가 그만큼 더 나온다
        [Test]
        public void Kind_Bonus_AddsItemsForFishCaughtInReach()
        {
            Create(0.99);
            GoFishing();
            Buy(Stake(0), RodTable.k_Bait);
            Assert.That(Stake(0).Rod.Bonus, Is.EqualTo(1), "꿀떡밥대");
            Events.FishCaught caught = default;
            m_bus.Subscribe<Events.FishCaught>(e => caught = e);
            Assert.That(RunUntil(() => caught.Fish != null, 120d), Is.True);
            Assert.That(caught.Count, Is.EqualTo(m_fishing.Yield + 1));
        }

        // 대물 무게를 거의 0으로 · 물때마다 대물: 대 하나로 단계를 차례로 올린다
        private void CreateEasyBoss(int yieldEvery = 3)
        {
            Create(tweak: t =>
            {
                FishingConfigTable c = t.Get<FishingConfigTable>(FishingConfigTable.k_Main);
                c.WavesPerStage = 1;
                c.Boss.Weight = 0.01d;
                c.YieldEvery = yieldEvery;
            });
            GoFishing();
            Buy(Stake(0));
        }

        // 설계 49: wavesPerStage번째 물때 떼 끝에 대물. 대물이 떠 있는 동안 다음 떼는 오지 않고, 빠져나가면 단계는 그대로 · 물때는 1부터
        [Test]
        public void Boss_ComesOnTheLastWave_HoldsTheNextWave_AndEscapingKeepsTheStage()
        {
            Create(tweak: t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).WavesPerStage = 2);
            int waves = 0;
            bool spawned = false;
            bool escaped = false;
            m_bus.Subscribe<Events.WaveStarted>(_ => waves++);
            m_bus.Subscribe<Events.BossSpawned>(_ => spawned = true);
            m_bus.Subscribe<Events.BossEscaped>(_ => escaped = true);

            Run(1d);
            Assert.That(m_fishing.Wave, Is.EqualTo(1));
            Assert.That(spawned, Is.False, "첫 물때는 보통 떼");
            Assert.That(RunUntil(() => spawned, Config.WaveSeconds + 1d), Is.True, "둘째 물때에 대물");
            Assert.That(m_fishing.Wave, Is.EqualTo(2));
            Assert.That(m_fishing.BossWave, Is.True);
            Assert.That(RunUntil(() => m_fishing.Fish.Any(f => f.Boss), 30d), Is.True, "떼 끝에 따라 나온다");
            Fish boss = m_fishing.Fish.First(f => f.Boss);
            Assert.That(boss.Speed, Is.EqualTo(boss.Kind.Speed * Config.Boss.SpeedScale).Within(1e-9));
            Assert.That(boss.Weight, Is.EqualTo(Config.Boss.Weight).Within(1e-9), "1단계 무게");

            Run(Config.WaveSeconds + 2d);
            Assert.That(waves, Is.EqualTo(2), "대물이 떠 있는 동안 다음 떼는 없다");
            Assert.That(RunUntil(() => escaped, 120d), Is.True, "대가 없으면 빠져나간다");
            Assert.That(m_fishing.Stage, Is.EqualTo(1));
            Assert.That(m_fishing.Wave, Is.EqualTo(0));
            Assert.That(m_fishing.BossWave, Is.False);
            Assert.That(RunUntil(() => waves == 3, Config.WaveSeconds + 1d), Is.True, "다시 물때 1부터");
            Assert.That(m_fishing.Wave, Is.EqualTo(1));
        }

        // 설계 49: 대물을 낚으면 단계 +1 · 재료 boss.catch개 · 물때는 1부터. 다음 대물은 weightGrowth배 무겁다
        [Test]
        public void Boss_Caught_RaisesTheStage()
        {
            CreateEasyBoss();
            int raised = 0;
            m_bus.Subscribe<Events.StageRaised>(_ => raised++);
            int catfish = m_state.Count("fish_catfish");

            Assert.That(RunUntil(() => raised == 1, 60d), Is.True, "대물을 낚는다");
            Assert.That(m_fishing.Stage, Is.EqualTo(2));
            Assert.That(m_fishing.Wave, Is.EqualTo(0));
            Assert.That(m_fishing.BossWave, Is.False);
            Assert.That(m_state.Count("fish_catfish"), Is.EqualTo(catfish + Config.Boss.Catch), "대물은 메기 재료");
            Assert.That(m_fishing.Log.ContainsKey("catfish"), Is.False, "대물은 어종 기록에 넣지 않는다");

            Assert.That(RunUntil(() => m_fishing.Fish.Any(f => f.Boss), Config.WaveSeconds + 30d), Is.True, "다음 물때의 대물");
            Assert.That(m_fishing.Fish.First(f => f.Boss).Weight, Is.EqualTo(Config.Boss.Weight * Config.WeightGrowth).Within(1e-9));
        }

        // 치트 창이 쓰는 길: 단계를 정하면 물때는 1부터, 대물을 부르면 다음 물때가 대물
        [Test]
        public void Cheat_SetsStage_AndCallsTheBoss()
        {
            Create();
            GoFishing();
            Run(1d);
            m_fishing.SetStageNow(7);
            Assert.That(m_fishing.Stage, Is.EqualTo(7));
            Assert.That(m_fishing.Yield, Is.EqualTo(3));
            Assert.That(m_fishing.Wave, Is.EqualTo(0));

            m_fishing.CallBossNow();
            Run(k_Dt * 2);
            Assert.That(m_fishing.BossWave, Is.True);
            Assert.That(m_fishing.Wave, Is.EqualTo(Config.WavesPerStage));
        }

        // 설계 49 단계 보람: 한 마리 재료 = 1 + ⌊(단계 − 1) ÷ yieldEvery⌋. 4단계에 그물 계열이 시트에 열린다
        [Test]
        public void Stage_RaisesYield_AndUnlocksTheNetFamily()
        {
            CreateEasyBoss();
            Assert.That(m_fishing.Yield, Is.EqualTo(1));
            Assert.That(m_fishing.ShopRods.Select(r => r.Id), Is.EquivalentTo(new[] { RodTable.k_Bamboo, RodTable.k_Iron, RodTable.k_Bait }));
            Assert.That(m_fishing.TryChoose(ActionTable.k_Summon, Stake(1), RodTable.k_Net), Is.False, "아직 잠긴 계열");

            Assert.That(RunUntil(() => m_fishing.Stage == 4, 300d), Is.True, "대물 셋");
            Assert.That(m_fishing.Yield, Is.EqualTo(2));
            Assert.That(m_fishing.ShopRods.Select(r => r.Id), Does.Contain(RodTable.k_Net));
            Buy(Stake(1), RodTable.k_Net);
            Assert.That(Stake(1).Rod.Id, Is.EqualTo(RodTable.k_Net));

            Events.FishCaught caught = default;
            m_bus.Subscribe<Events.FishCaught>(e => caught = e.Fish.Boss || e.Fish.Trophy ? caught : e);
            Assert.That(RunUntil(() => caught.Fish != null, 120d), Is.True);
            Assert.That(caught.Count, Is.EqualTo(2), "보통 물고기 한 마리가 재료 2개");
        }

        // 오프라인도 단계 보람을 곱한다(같은 2단계 판에서 yieldEvery 1이면 재료가 두 배)
        [Test]
        public void Offline_MultipliesItemsByTheStageYield()
        {
            double Settled(int yieldEvery)
            {
                CreateEasyBoss(yieldEvery);
                Assert.That(RunUntil(() => m_fishing.Stage == 2, 60d), Is.True);
                Assert.That(m_fishing.Yield, Is.EqualTo(yieldEvery == 1 ? 2 : 1));
                return Offline.Settle(m_state, m_mall, m_tables, 1200d).Items.TryGetValue("fish_minnow", out int n) ? n : 0;
            }

            double plain = Settled(3);
            Assert.That(plain, Is.GreaterThan(0d));
            Assert.That(Settled(1), Is.EqualTo(plain * 2d).Within(2d));
        }

        // 미끼 노점 시트에는 업그레이드 줄만 있다(단계는 사지 않는다)
        [Test]
        public void Hut_HasNoStageRow()
        {
            Create();
            GoFishing();
            Assert.That(m_fishing.SheetActions(m_fishing.Hut).Select(a => a.Table.Id), Is.EquivalentTo(new[] { ActionTable.k_HutUpgrade }));
        }

        [Test]
        public void LockedStake_OpensForItsCost()
        {
            Create();
            GoFishing();
            StakeInteractable locked = m_fishing.Stakes.First(s => !s.Open);
            double coins = m_state.Coins;

            Assert.That(ActionAt(locked), Is.EqualTo(ActionTable.k_OpenStake));
            Press(locked);
            Assert.That(locked.Open, Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins - locked.Cost).Within(1e-6));
        }

        [Test]
        public void FamilySteps_ScaleTheWholeFamily_AnywhereOnTheBoard()
        {
            Create();
            GoFishing();
            Buy(Stake(0));
            Assert.That(m_fishing.FamilyScale(RodTable.k_Bamboo), Is.EqualTo(1d));
            Buy(Stake(3));
            Assert.That(m_fishing.FamilyScale(RodTable.k_Bamboo), Is.EqualTo(Config.FamilyScales[0]));
        }

        [Test]
        public void Thump_StunsNearbyFish_ThenCoolsDown()
        {
            Create();
            GoFishing();
            Run(3d);
            Fish fish = m_fishing.Fish.Last();
            Vector2 at = m_fishing.Layout.PointAt(fish.S) + new Vector2(0f, 1.0f);
            m_mall.Wombat.Mover.Place(at);
            Run(k_Dt);

            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_Thump));
            Assert.That(m_fishing.TryInteract(), Is.True);
            Assert.That(fish.StunLeft, Is.GreaterThan(0d));
            Assert.That(m_mall.Wombat.Busy, Is.True);
            float s = fish.S;
            Run(1d);
            Assert.That(fish.S, Is.EqualTo(s), "멈춰 있다");
            Assert.That(m_fishing.TargetAction?.Id, Is.Not.EqualTo(ActionTable.k_Thump), "쉬는 동안은 쿵이 없다");
        }

        [Test]
        public void Trophy_HoldsTheRod_UntilHauled()
        {
            Create(tweak: t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).TrophyChance = 1d);
            GoFishing();
            Buy(Stake(0));
            Assert.That(RunUntil(() => Stake(0).Hooked != null, 60d), Is.True, "월척을 붙잡는다");
            Fish trophy = Stake(0).Hooked;
            float s = trophy.S;
            Run(5d);
            Assert.That(trophy.S, Is.EqualTo(s), "붙잡힌 월척은 멈춘다");
            Assert.That(Stake(0).Hooked, Is.SameAs(trophy), "아무도 없으면 계속 붙잡고 있다");

            int before = m_state.Count(trophy.Kind.Item);
            bool busyOnCatch = false;
            m_bus.Subscribe<Events.FishCaught>(_ => busyOnCatch = m_mall.Wombat.Busy);
            Assert.That(ActionAt(Stake(0)), Is.EqualTo(ActionTable.k_Haul));
            Press(Stake(0));
            Assert.That(m_state.Count(trophy.Kind.Item), Is.EqualTo(before + Config.TrophyCatch));
            Assert.That(Stake(0).Hooked, Is.Not.SameAs(trophy));
            Assert.That(busyOnCatch, Is.True, "낚은 사건 때 웜뱃이 이미 털썩 중(화면이 끌어올리기 동작을 고른다)");
        }

        private void HireClerk()
        {
            Candidate candidate = m_fishing.Candidates[0];
            Assert.That(m_fishing.TryHire(candidate, m_fishing.Hut, m_fishing.WageFor(candidate, m_fishing.Hut)), Is.True);
        }

        // 월척 건지기(2026-10-06 사용자): 대가 월척을 붙잡으면 점원이 노점에서 바로 걸어가 건진다
        [Test]
        public void Clerk_ScoopsHookedTrophy_RightAway()
        {
            Create(tweak: t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).TrophyChance = 1d);
            GoFishing();
            Buy(Stake(0));
            HireClerk();
            m_bus.Publish(new Events.Passed(m_fishing, PlazaArea.k_Id));

            Assert.That(RunUntil(() => Stake(0).Hooked != null, 60d), Is.True);
            Assert.That(RunUntil(() => m_fishing.Log.Count > 0, 20d), Is.True, "걷는 시간 안에 건진다");
        }

        // 오프라인도 점원이 있으면 걸리는 순간 건진 것으로 센다: 대가 막히지 않아 물때마다 월척 여럿
        [Test]
        public void Offline_WithClerk_ScoopsEveryHookedTrophy()
        {
            Create(tweak: t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).TrophyChance = 1d);
            GoFishing();
            Buy(Stake(0));
            double Settled() => m_tables.GetAll<FishTable>().Select(f => f.Item).Distinct()
                .Sum(id => Offline.Settle(m_state, m_mall, m_tables, 1200d).Items.TryGetValue(id, out int n) ? n : 0);

            Assert.That(Settled(), Is.EqualTo(0d), "점원이 없으면 대가 첫 월척을 붙잡은 채 멈춘다");
            HireClerk();
            Assert.That(Settled(), Is.GreaterThan(Config.TrophyCatch * 1200d / Config.WaveSeconds), "물때 하나에 월척 하나보다 많다");
        }

        // 단계가 오르면 다음 떼부터 물고기가 weightGrowth배 무겁다
        [Test]
        public void Stage_MakesTheNextSchoolHeavier()
        {
            CreateEasyBoss();
            Run(1d);
            double before = m_fishing.Fish[0].Weight;
            Assert.That(RunUntil(() => m_fishing.Stage == 2, 60d), Is.True);
            bool wave = false;
            m_bus.Subscribe<Events.WaveStarted>(_ => wave = true);
            Assert.That(RunUntil(() => wave, Config.WaveSeconds + 1d), Is.True, "다음 물때");
            Run(k_Dt * 2);
            Assert.That(m_fishing.Fish.First(f => !f.Boss).Weight, Is.EqualTo(before * Config.WeightGrowth).Within(1e-6));
        }

        private int BuyUpgrade(string id, int times = 1)
        {
            AtHut();

            for (int i = 0; i < times; i++)
            {
                Assert.That(m_fishing.TryChoose(ActionTable.k_HutUpgrade, m_fishing.Hut, id), Is.True, id);
            }

            return m_fishing.LevelOf(id);
        }

        // 설계 46 불빛: 모든 대 사거리 + effectPerLevel/단계. 값은 단계마다 costGrowth배, 최대 단계에서는 더 못 산다
        [Test]
        public void HutLight_RaisesEveryRodRange_UpToMax()
        {
            Create();
            GoFishing();
            Buy(Stake(0));
            float range = m_fishing.RangeOf(Stake(0));
            FishingUpgradeData light = m_fishing.Upgrade(FishingUpgradeData.k_Light);
            double price = m_fishing.UpgradePrice(light.Id);

            Assert.That(BuyUpgrade(light.Id), Is.EqualTo(1));
            Assert.That(m_fishing.RangeOf(Stake(0)), Is.EqualTo(range * (1f + (float)light.EffectPerLevel)).Within(1e-4f));
            Assert.That(m_fishing.UpgradePrice(light.Id), Is.EqualTo(price * light.CostGrowth).Within(1e-6));

            BuyUpgrade(light.Id, light.MaxLevel - 1);
            Assert.That(m_fishing.TryChoose(ActionTable.k_HutUpgrade, m_fishing.Hut, light.Id), Is.False, "최대 단계");
            Assert.That(m_fishing.LevelOf(light.Id), Is.EqualTo(light.MaxLevel));
        }

        // 설계 46 소용돌이: 간격마다 물길 맨 앞 물고기를 whirlDistance만큼 되돌린다
        [Test]
        public void HutWhirl_PushesTheFrontFishBack()
        {
            Create();
            GoFishing();
            Events.FishWhirled whirled = default;
            m_bus.Subscribe<Events.FishWhirled>(e => whirled = e);
            BuyUpgrade(FishingUpgradeData.k_Whirl);
            Run(3d);
            whirled = default;

            Assert.That(RunUntil(() => whirled.Fish != null, m_fishing.WhirlInterval + 1d), Is.True, "간격마다");
            Assert.That(whirled.Fish.S, Is.EqualTo(Math.Max(0f, whirled.From - (float)Config.WhirlDistance)).Within(0.1f));
            Assert.That(m_fishing.Fish.Where(f => f != whirled.Fish).All(f => f.S <= whirled.From + 0.1f), Is.True, "맨 앞 물고기");
        }

        [Test]
        public void PlazaDoor_LeadsToFishing_AndBack()
        {
            Create();
            GoFishing();
            Assert.That(m_mall.Active, Is.SameAs(m_fishing));
            Assert.That(m_mall.Wombat.Mover.Position, Is.EqualTo(m_fishing.Layout.HoleFloor));

            m_bus.Publish(new Events.Passed(m_fishing, PlazaArea.k_Id));
            Assert.That(m_mall.Active, Is.SameAs(m_plaza));
            Assert.That(m_mall.Wombat.Mover.Position, Is.EqualTo(m_plaza.Layout.DoorTo(FishingArea.k_Id).Floor), "낚시터 문 앞에 선다");
        }

        [Test]
        public void Save_RoundTrip_KeepsBoardStageAndLog()
        {
            Create();
            GoFishing();
            Buy(Stake(0));
            Buy(Stake(1), RodTable.k_Bait);
            Press(m_fishing.Stakes.First(s => !s.Open));
            BuyUpgrade(FishingUpgradeData.k_Light, 2);
            m_mall.Wombat.Mover.Place(m_fishing.StreamEnd.Position);
            Run(k_Dt);
            Assert.That(m_fishing.TryInteract(), Is.True, "물길 한 칸 파기");
            Assert.That(RunUntil(() => m_fishing.Log.Count > 0, 120d), Is.True);
            SaveData data = GameSave.Capture(m_state, m_mall);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            FishingArea before = m_fishing;

            Create();
            GameSave.Apply(Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json), m_state, m_mall, m_tables);

            for (int i = 0; i < before.Stakes.Count; i++)
            {
                Assert.That(m_fishing.Stakes[i].Open, Is.EqualTo(before.Stakes[i].Open));
                Assert.That(m_fishing.Stakes[i].Rod?.Id, Is.EqualTo(before.Stakes[i].Rod?.Id));
            }

            Assert.That(m_fishing.Summons, Is.EqualTo(before.Summons));
            Assert.That(m_fishing.Stage, Is.EqualTo(before.Stage));
            Assert.That(m_fishing.LevelOf(FishingUpgradeData.k_Light), Is.EqualTo(2));
            Assert.That(m_fishing.Dug, Is.EqualTo(1));
            Assert.That(m_fishing.Layout.Length, Is.EqualTo(before.Layout.Length).Within(1e-3f));
            Assert.That(m_fishing.Log.Keys, Is.EquivalentTo(before.Log.Keys));
        }

        // 설계 49: 단계와 종류가 저장된다. 옛 저장(설계 46)의 별 등급은 그 계열의 그 단 종류로 넘어온다
        [Test]
        public void Save_KeepsStageAndKinds_AndReadsOldGradesAsTiers()
        {
            CreateEasyBoss();
            Buy(Stake(1), RodTable.k_Iron);
            Buy(Stake(2));
            Assert.That(m_fishing.TryMoveRod(Stake(2), Stake(0)), Is.True, "쌍줄대");
            Assert.That(RunUntil(() => m_fishing.Stage == 2, 60d), Is.True);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(GameSave.Capture(m_state, m_mall));
            const string k_Saved = "\"Rod\":\"iron\",\"Grade\":0";
            Assert.That(json, Does.Contain(k_Saved));

            Create();
            GameSave.Apply(Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json.Replace(k_Saved, "\"Rod\":\"iron\",\"Grade\":3")), m_state, m_mall, m_tables);
            Assert.That(m_fishing.Stage, Is.EqualTo(2));
            Assert.That(Stake(0).Rod.Id, Is.EqualTo("bamboo_2"));
            Assert.That(Stake(1).Rod.Id, Is.EqualTo("iron_3"), "옛 ★3 쇠낚싯대 → 닻대");
        }

        // 설계 45: 처음은 dugStart까지만 물. 막다른 끝 앞에서 파면 코인을 내고 digStep만큼 늘고, 웜뱃은 새 물 밖으로 비킨다. 끝까지 파면 파기 대상이 없다
        [Test]
        public void DigStream_ExtendsForCoins_UntilThePlanEnds()
        {
            Create();
            GoFishing();
            Assert.That(m_fishing.Layout.Length, Is.EqualTo((float)Config.DugStart).Within(1e-3f));
            int digs = 0;

            while (!m_fishing.StreamFull)
            {
                double coins = m_state.Coins;
                double price = m_fishing.DigPrice;
                float length = m_fishing.Layout.Length;
                m_mall.Wombat.Mover.Place(m_fishing.StreamEnd.Position);
                Run(k_Dt);
                Assert.That(m_fishing.Target, Is.SameAs(m_fishing.StreamEnd), "막다른 끝 앞이 대상");
                Assert.That(m_fishing.TryInteract(), Is.True, "파기 버튼");
                Assert.That(m_state.Coins, Is.EqualTo(coins - price).Within(1e-6));
                Assert.That(m_fishing.Layout.Length, Is.EqualTo(Math.Min(length + (float)Config.DigStep, m_fishing.Layout.PlanLength)).Within(1e-3f));
                Assert.That(m_fishing.Layout.Nav.IsWalkable(m_mall.Wombat.Mover.Position), Is.True, "웜뱃은 새 물 밖");
                Assert.That(m_fishing.DigPrice, Is.EqualTo(price * Config.DigGrowth).Within(1e-6));
                digs++;
                Run(1d);
            }

            Assert.That(digs, Is.EqualTo(12));
            Assert.That(m_fishing.Layout.Length, Is.EqualTo(m_fishing.Layout.PlanLength).Within(1e-3f));
            m_mall.Wombat.Mover.Place(m_fishing.StreamEnd.Position + new Vector2(0f, 1.2f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.Not.SameAs(m_fishing.StreamEnd), "끝까지 팠으면 파기 대상이 없다");
        }

        // 설계 45: 대는 말뚝 위쪽 반원만 본다(사거리 안이라도 말뚝보다 아래 물고기는 안 감는다)
        [Test]
        public void Rod_ReelsOnlyFishAboveItsStake()
        {
            Create();
            GoFishing();
            Buy(Stake(0));
            m_mall.Wombat.Mover.Place(new Vector2(0.5f, -2.4f));
            bool reeled = false;
            bool below = false;

            for (double t = 0d; t < 90d; t += k_Dt)
            {
                Fish fish = Stake(0).Reeling;

                if (fish != null)
                {
                    reeled = true;
                    below |= m_fishing.Layout.PointAt(fish.S).Y < Stake(0).Position.Y;
                }

                m_mall.Tick(k_Dt);
            }

            Assert.That(reeled, Is.True, "위 물길 물고기는 감는다");
            Assert.That(below, Is.False, "말뚝 아래 물고기는 안 감는다");
        }

        // 오프라인 공식(물때 하나를 시뮬레이션 × 물때 수)이 실제 20분과 크게 다르지 않다(오프라인에는 대물이 없으니 대물 없이 잰다)
        [Test]
        public void Offline_MatchesTwentyMinutesOfSimulation()
        {
            Create(tweak: t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).WavesPerStage = int.MaxValue);
            GoFishing();

            for (int i = 0; i < 4; i++)
            {
                Buy(Stake(i));
            }

            m_bus.Publish(new Events.Passed(m_fishing, PlazaArea.k_Id));
            Dictionary<string, int> start = new[] { "fish_minnow", "fish_crucian", "fish_catfish" }.ToDictionary(id => id, id => m_state.Count(id));
            Run(1200d);
            double real = start.Sum(pair => m_state.Count(pair.Key) - pair.Value);

            OfflineReport report = Offline.Settle(m_state, m_mall, m_tables, 1200d);
            double formula = start.Keys.Sum(id => report.Items.TryGetValue(id, out int n) ? n : 0);

            Assert.That(real, Is.GreaterThan(0d));
            Assert.That(formula, Is.EqualTo(real).Within(real * 0.25), $"실제 {real} · 공식 {formula}");
        }
    }
}
