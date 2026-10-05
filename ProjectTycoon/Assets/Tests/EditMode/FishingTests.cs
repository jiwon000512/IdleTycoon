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
    // 설계 44 검증: 낚시 디펜스. 물길을 따라 오는 물고기 · 대의 감기 · 소환 · 셋 합성 · 옮기기 · 계열 문턱 · 쿵 · 월척 · 대물 · 광장 문 · 점원 · 저장 · 오프라인.
    // 웜뱃을 말뚝 앞에 세우고 버튼(TryInteract)을 누르는 실제 길로 다룬다. 낚시터 난수는 늘 같은 값(0.3: 대나무대 · 보통 등급 · 피라미 · 작은 크기 · 월척 아님)
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

        [Test]
        public void Fish_SwimTheStream_AndEscapeAtTheEnd()
        {
            Create();
            int waves = 0;
            int escaped = 0;
            m_bus.Subscribe<Events.WaveStarted>(e => waves += e.Boss ? 0 : 1);
            m_bus.Subscribe<Events.FishEscaped>(_ => escaped++);

            Run(1d);
            Assert.That(m_fishing.Wave, Is.EqualTo(1));
            Assert.That(waves, Is.EqualTo(1), "물때 시작 사건");
            Assert.That(m_fishing.Fish, Is.Not.Empty);
            Fish first = m_fishing.Fish[0];
            float s = first.S;
            Run(1d);
            Assert.That(first.S - s, Is.EqualTo((float)first.Kind.Speed).Within(0.1f), "속도대로");

            Assert.That(RunUntil(() => m_fishing.Missed > 0, 60d), Is.True, "대가 없으면 끝에서 놓친다");
            Assert.That(escaped, Is.EqualTo(m_fishing.Missed), "놓칠 때마다 사건");
        }

        [Test]
        public void Summon_EmptyStakeOnly_PriceGrows_RodCatchesFish()
        {
            Create();
            GoFishing();
            double coins = m_state.Coins;
            double price = m_fishing.SummonPrice;
            StakeInteractable summoned = null;
            m_bus.Subscribe<Events.RodSummoned>(e => summoned = e.Stake);

            Assert.That(ActionAt(Stake(0)), Is.EqualTo(ActionTable.k_Summon));
            Press(Stake(0));
            Assert.That(summoned, Is.SameAs(Stake(0)));
            Assert.That(m_state.Coins, Is.EqualTo(coins - price).Within(1e-6));
            Assert.That(Stake(0).Rod.Id, Is.EqualTo(RodTable.k_Bamboo));
            Assert.That(Stake(0).Grade, Is.EqualTo(1));
            Assert.That(m_fishing.SummonPrice, Is.EqualTo(price * Config.SummonGrowth).Within(1e-6));
            Assert.That(ActionAt(Stake(0)), Is.EqualTo(ActionTable.k_Carry), "꽂힌 말뚝은 소환이 아니라 들기");

            int minnows = m_state.Count("fish_minnow");
            bool reeled = false;
            Assert.That(RunUntil(() => (reeled |= Stake(0).Reeling != null) && m_state.Count("fish_minnow") > minnows, 120d), Is.True, "지나가는 피라미를 감아 낚는다");
            Assert.That(reeled, Is.True, "감는 동안 Reeling(낚싯줄)");
            Assert.That(m_fishing.Log["minnow"].Caught, Is.GreaterThan(0));
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
        public void Merge_ThreeSameRods_RaiseOneGrade_AndFreeTwoStakes()
        {
            Create();
            GoFishing();
            Press(Stake(0));
            Press(Stake(1));
            Assert.That(ActionAt(Stake(0)), Is.EqualTo(ActionTable.k_Carry), "둘이면 합치지 않는다");

            Press(Stake(2));
            Assert.That(ActionAt(Stake(0)), Is.EqualTo(ActionTable.k_Merge));
            Events.RodsMerged merged = default;
            m_bus.Subscribe<Events.RodsMerged>(e => merged = e);
            Press(Stake(0));
            Assert.That(merged.Into, Is.SameAs(Stake(0)));
            Assert.That(merged.From, Is.EquivalentTo(new[] { Stake(1), Stake(2) }));
            Assert.That(Stake(0).Grade, Is.EqualTo(2));
            Assert.That(Stake(1).Rod, Is.Null);
            Assert.That(Stake(2).Rod, Is.Null);
        }

        [Test]
        public void Carry_PicksPlacesSwaps_AndReturnsWhenLeaving()
        {
            Create();
            GoFishing();
            Press(Stake(0));
            Press(Stake(0));
            Assert.That(m_fishing.Carried, Is.Not.Null);
            Assert.That(Stake(0).Rod, Is.Null);

            Press(Stake(3));
            Assert.That(Stake(3).Rod, Is.Not.Null);
            Assert.That(m_fishing.Carried, Is.Null);

            Press(Stake(3));
            m_bus.Publish(new Events.Passed(m_fishing, PlazaArea.k_Id));
            Assert.That(Stake(3).Rod, Is.Not.Null, "들고 나가면 들었던 말뚝으로");
            Assert.That(m_fishing.Carried, Is.Null);
        }

        [Test]
        public void FamilySteps_ScaleTheWholeFamily_AnywhereOnTheBoard()
        {
            Create();
            GoFishing();
            Press(Stake(0));
            Assert.That(m_fishing.FamilyScale(RodTable.k_Bamboo), Is.EqualTo(1d));
            Press(Stake(3));
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
            Press(Stake(0));
            Assert.That(RunUntil(() => Stake(0).Hooked != null, 60d), Is.True, "월척을 붙잡는다");
            Fish trophy = Stake(0).Hooked;
            float s = trophy.S;
            Run(5d);
            Assert.That(trophy.S, Is.EqualTo(s), "붙잡힌 월척은 멈춘다");
            Assert.That(Stake(0).Hooked, Is.SameAs(trophy), "아무도 없으면 계속 붙잡고 있다");

            int before = m_state.Count(trophy.Kind.Item);
            Assert.That(ActionAt(Stake(0)), Is.EqualTo(ActionTable.k_Haul));
            Press(Stake(0));
            Assert.That(m_state.Count(trophy.Kind.Item), Is.EqualTo(before + Config.TrophyCatch));
            Assert.That(Stake(0).Hooked, Is.Not.SameAs(trophy));
        }

        private void HireClerk()
        {
            Candidate candidate = m_fishing.Candidates[0];
            Assert.That(m_fishing.TryHire(candidate, m_fishing.Hut, m_fishing.WageFor(candidate, m_fishing.Hut)), Is.True);
        }

        [Test]
        public void Clerk_MergesTriples()
        {
            Create();
            GoFishing();
            Press(Stake(0));
            Press(Stake(1));
            Press(Stake(2));
            HireClerk();
            m_bus.Publish(new Events.Passed(m_fishing, PlazaArea.k_Id));

            Assert.That(RunUntil(() => m_fishing.Stakes.Any(s => s.Grade == 2), 60d), Is.True, "점원이 셋을 합친다");
        }

        [Test]
        public void Clerk_HaulsOverdueTrophy()
        {
            Create(tweak: t =>
            {
                FishingConfigTable c = t.Get<FishingConfigTable>(FishingConfigTable.k_Main);
                c.TrophyChance = 1d;
                c.TrophyHoldSeconds = 5d;
            });
            GoFishing();
            Press(Stake(0));
            HireClerk();
            m_bus.Publish(new Events.Passed(m_fishing, PlazaArea.k_Id));

            Assert.That(RunUntil(() => Stake(0).Hooked != null, 60d), Is.True);
            Assert.That(RunUntil(() => m_fishing.Log.Count > 0, 120d), Is.True, "오래 버틴 월척을 점원이 거든다");
        }

        [Test]
        public void Boss_CaughtRaisesStage_AndOffersThreeSpecialRods()
        {
            Create(tweak: t =>
            {
                t.Get<FishingConfigTable>(FishingConfigTable.k_Main).BossEvery = 2;
                t.Get<FishTable>("boss_carp").Sizes[0].Min = 0.5d;
            });
            GoFishing();
            Press(Stake(0));
            Press(Stake(2));

            Assert.That(RunUntil(() => m_fishing.Stage == 2, 200d), Is.True, "대물을 낚으면 다음 단계");
            Assert.That(m_fishing.Choice.Count, Is.EqualTo(3));
            Assert.That(m_fishing.Choice.All(rod => rod.IsSpecial), Is.True);
            Assert.That(m_fishing.Wave, Is.Not.EqualTo(Config.BossEvery));

            RodTable chosen = m_fishing.Choice[0];
            Assert.That(m_fishing.Things, Has.Member(m_fishing.Catch), "낚은 대물이 눕는다");
            Assert.That(m_fishing.TryChoose(ActionTable.k_ChooseRod, m_fishing.Catch, chosen.Id), Is.True, "대물 시트에서 고른다");
            Assert.That(m_fishing.Choice, Is.Null);
            Assert.That(m_fishing.Things, Has.No.Member(m_fishing.Catch));
            Assert.That(m_fishing.Stakes.Any(s => s.Rod == chosen), Is.True, "빈 말뚝에 꽂힌다");
        }

        [Test]
        public void Boss_Escaped_RepeatsTheSameStage()
        {
            Create(tweak: t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).BossEvery = 2);
            bool resolved = false;
            m_bus.Subscribe<Events.BossResolved>(e => resolved = !e.Caught || resolved);

            Assert.That(RunUntil(() => resolved, 400d), Is.True, "대가 없으면 대물을 놓친다");
            Assert.That(m_fishing.Stage, Is.EqualTo(1));
            Assert.That(m_fishing.Choice, Is.Null);
            Run(k_Dt * 2);
            Assert.That(m_fishing.Wave, Is.EqualTo(1), "같은 단계 물때 1부터");
        }

        [Test]
        public void Haul_AtBossStake_SitsTheWombat()
        {
            Create(tweak: t => t.Get<FishingConfigTable>(FishingConfigTable.k_Main).BossEvery = 2);
            GoFishing();
            Press(Stake(0));
            Assert.That(RunUntil(() => m_fishing.BossOut && m_fishing.Fish.Any(f => f.Boss && Vector2.Distance(m_fishing.Layout.PointAt(f.S), Stake(0).Position) <= m_fishing.RangeOf(Stake(0))), 200d), Is.True);

            Assert.That(ActionAt(Stake(0)), Is.EqualTo(ActionTable.k_Haul));
            Press(Stake(0));
            Assert.That(m_mall.Wombat.Sitting, Is.True);
            Assert.That(m_fishing.SitStake, Is.SameAs(Stake(0)));

            m_mall.Wombat.SetInput(new Vector2(1f, 0f));
            Run(k_Dt * 2);
            Assert.That(m_mall.Wombat.Sitting, Is.False, "조이스틱을 밀면 일어난다");
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
            Press(Stake(0));
            Press(Stake(1));
            Press(m_fishing.Stakes.First(s => !s.Open));
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
                Assert.That(m_fishing.Stakes[i].Grade, Is.EqualTo(before.Stakes[i].Grade));
            }

            Assert.That(m_fishing.Summons, Is.EqualTo(before.Summons));
            Assert.That(m_fishing.Dug, Is.EqualTo(1));
            Assert.That(m_fishing.Layout.Length, Is.EqualTo(before.Layout.Length).Within(1e-3f));
            Assert.That(m_fishing.Log.Keys, Is.EquivalentTo(before.Log.Keys));
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
            Press(Stake(0));
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

        // 오프라인 공식(물때 하나를 시뮬레이션 × 물때 수)이 실제 20분과 크게 다르지 않다
        [Test]
        public void Offline_MatchesTwentyMinutesOfSimulation()
        {
            Create();
            GoFishing();

            for (int i = 0; i < 4; i++)
            {
                Press(Stake(i));
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
