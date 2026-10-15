using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 30 검증: 무료로 빌면 축복 하나가 걸리고 쉬는 시간이 시작, 쉬는 중엔 못 빎, 시간이 다 되면 풀림, 늘 하나(새 축복이 덮음),
    // 효과가 오븐 · 밭 · 덤 · 손님에 걸림, 광장 석상(분수 자리 · 편집에서 못 집음 · 버튼으로 팝업). 실제 JSON 값
    public sealed class BlessingTests
    {
        private const double k_Dt = 0.02;

        private TableSet m_tables;
        private EventBus m_bus;
        private ZooState m_state;
        private Wombat m_wombat;
        private BakeryArea m_shop;
        private PlazaArea m_plaza;
        private FarmArea m_farm;
        private Mall m_mall;

        private Blessing Blessing => m_state.Blessing;
        private PlazaConfigTable Config => m_tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
        private string Gem => m_tables.Get<FarmConfigTable>(FarmConfigTable.k_Main).BonusItem;

        [SetUp]
        public void Create()
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            m_wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, new SequenceRandom(new double[2000]), m_wombat, m_bus);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 4000).ToArray()), m_wombat, m_bus);
            m_farm = new FarmArea(m_tables, new SequenceRandom(Enumerable.Repeat(0.99, 200).ToArray()), m_wombat, m_bus);
            m_mall = new Mall(m_shop, m_plaza, m_farm, m_bus);
        }

        private void Run(double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }
        }

        // 표 순서에서 그 축복이 뽑히는 난수(비중 가운데)
        private double Roll(string id)
        {
            var all = m_tables.GetAll<BlessingTable>();
            double total = all.Sum(b => b.Weight);
            double before = all.TakeWhile(b => b.Id != id).Sum(b => b.Weight);
            return (before + m_tables.Get<BlessingTable>(id).Weight * 0.5d) / total;
        }

        private void Grant(string id)
        {
            Assert.That(Blessing.TryPray(new SequenceRandom(Roll(id))), Is.True);
        }

        private double Value(string id)
        {
            return m_tables.Get<BlessingTable>(id).Value;
        }

        [Test]
        public void Pray_AppliesOneBlessing_AndStartsCooldown()
        {
            int prayed = 0;
            m_bus.Subscribe<Events.BlessingChanged>(e => prayed += e.Prayed ? 1 : 0);
            int gems = m_state.Count(Gem);

            Grant(BlessingTable.k_Bake);

            Assert.That(prayed, Is.EqualTo(1));
            Assert.That(Blessing.Active.Id, Is.EqualTo(BlessingTable.k_Bake));
            Assert.That(Blessing.Remaining, Is.EqualTo(m_tables.Get<BlessingTable>(BlessingTable.k_Bake).Seconds));
            Assert.That(Blessing.Cooldown, Is.EqualTo(Config.BlessingCooldown));
            Assert.That(Blessing.Boost(BlessingTable.k_Bake), Is.EqualTo(Value(BlessingTable.k_Bake)));
            Assert.That(Blessing.Boost(BlessingTable.k_Grow), Is.EqualTo(0d));
            Assert.That(m_state.Count(Gem), Is.EqualTo(gems));
        }

        [Test]
        public void Pray_DuringCooldown_ChangesNothing()
        {
            Grant(BlessingTable.k_Bake);
            Blessing.Tick(Config.BlessingCooldown - 1d);

            Assert.That(Blessing.CanPray, Is.False);
            Assert.That(Blessing.TryPray(new SequenceRandom(Roll(BlessingTable.k_Grow))), Is.False);
            Assert.That(Blessing.Cooldown, Is.EqualTo(1d).Within(1e-9));
        }

        // 걸린 시간이 다 되면 풀리고, 쉬는 시간이 끝나면 다시 빌 수 있다(둘 다 BlessingChanged)
        [Test]
        public void Blessing_EndsAfterSeconds_ThenCooldownEnds()
        {
            int changed = 0;
            m_bus.Subscribe<Events.BlessingChanged>(e => changed += e.Prayed ? 0 : 1);
            Grant(BlessingTable.k_Grow);
            double seconds = m_tables.Get<BlessingTable>(BlessingTable.k_Grow).Seconds;

            Blessing.Tick(seconds - 1d);
            Assert.That(Blessing.Active, Is.Not.Null);
            Blessing.Tick(1d);

            Assert.That(Blessing.Active, Is.Null);
            Assert.That(Blessing.Boost(BlessingTable.k_Grow), Is.EqualTo(0d));
            Assert.That(changed, Is.EqualTo(1));

            Blessing.Tick(Config.BlessingCooldown - seconds);

            Assert.That(Blessing.CanPray, Is.True);
            Assert.That(changed, Is.EqualTo(2));
        }

        // 늘 하나: 앞 축복이 남아 있어도 새로 빌면 덮는다
        [Test]
        public void NewPrayer_ReplacesBlessing()
        {
            m_tables.Get<BlessingTable>(BlessingTable.k_Bake).Seconds = Config.BlessingCooldown * 2d;
            Grant(BlessingTable.k_Bake);
            Blessing.Tick(Config.BlessingCooldown);

            Grant(BlessingTable.k_Price);

            Assert.That(Blessing.Active.Id, Is.EqualTo(BlessingTable.k_Price));
            Assert.That(Blessing.Boost(BlessingTable.k_Bake), Is.EqualTo(0d));
        }

        // Mall이 매 프레임 축복 시간을 줄인다
        [Test]
        public void MallTick_CountsBlessingDown()
        {
            Grant(BlessingTable.k_Grow);
            double start = Blessing.Remaining;

            Run(1d);

            Assert.That(start - Blessing.Remaining, Is.EqualTo(1d).Within(1e-6));
        }

        // 화덕 축복이면 오븐이 (1 + 값)배로 굽는다
        [Test]
        public void BakeBlessing_OvenBakesFaster()
        {
            Grant(BlessingTable.k_Bake);
            OvenInteractable oven = m_shop.Ovens[0];
            Assert.That(oven.TryStart(m_tables.GetAll<BreadTable>()[0]), Is.True);
            double start = oven.Remaining;

            Run(1d);

            Assert.That(start - oven.Remaining, Is.EqualTo(1d + Value(BlessingTable.k_Bake)).Within(1e-6));
        }

        [Test]
        public void GrowBlessing_FieldGrowsFaster()
        {
            Grant(BlessingTable.k_Grow);
            PlotInteractable plot = m_farm.Plots.First(p => p.IsTilled);
            plot.Plant(m_farm.UnlockedCrops[0], false);
            double start = plot.Remaining;

            Run(1d);

            Assert.That(start - plot.Remaining, Is.EqualTo(1d + Value(BlessingTable.k_Grow)).Within(1e-6));
        }

        // 반짝 축복이면 덤 확률이 × (1 + 값): 기본 확률보다 조금 큰 난수도 덤이 나온다
        [Test]
        public void BonusBlessing_MultipliesBonusChance()
        {
            Grant(BlessingTable.k_Bonus);
            PlotInteractable plot = m_farm.Plots.First(p => p.IsTilled);
            plot.Plant(m_farm.UnlockedCrops[0], false);
            double chance = m_tables.Get<FarmConfigTable>(FarmConfigTable.k_Main).BonusChance;
            int gems = m_state.Count(Gem);

            plot.Harvest(m_state, new SequenceRandom(chance * (1d + Value(BlessingTable.k_Bonus) * 0.5d)));

            Assert.That(m_state.Count(Gem), Is.EqualTo(gems + 1));
        }

        // 손님 축복이면 계단으로 오는 간격이 ÷ (1 + 값): 첫 손님 뒤 기본 간격이 되기 전에 둘째가 온다
        [Test]
        public void VisitorsBlessing_CustomersArriveSooner()
        {
            Grant(BlessingTable.k_Visitors);
            int arrived = 0;
            m_bus.Subscribe<Events.PlazaVisitorArrived>(_ => arrived++);
            double boosted = Config.ArrivalSeconds / (1d + Value(BlessingTable.k_Visitors));

            Run(k_Dt + (boosted + Config.ArrivalSeconds) * 0.5d);

            Assert.That(arrived, Is.EqualTo(2));
        }

        // 석상은 분수 자리에 서서 길을 막고(장식을 겹쳐 놓을 수 없음), 편집에서 집히지 않으며, 곁에 가면 버튼으로 팝업을 연다
        [Test]
        public void PlazaStatue_StandsFixed_AndOpensWithButton()
        {
            StatueInteractable statue = m_plaza.Statue;
            Assert.That(statue.Position, Is.EqualTo(new Vector2((float)Config.StatueX, (float)Config.StatueY)));
            Assert.That(m_plaza.ThingAt(statue.Position + new Vector2(0f, 0.3f)), Is.Null);
            Assert.That(m_plaza.CanPlace(m_tables.GetAll<DecorationTable>()[0].Id, statue.Position), Is.Not.EqualTo(PlacementCheck.Ok));

            int opened = 0;
            m_bus.Subscribe<Events.StatueOpened>(e => opened += e.Thing == statue ? 1 : 0);
            m_bus.Publish(new Events.Passed(m_shop, PlazaArea.k_Id));
            m_wombat.Mover.Place(statue.Position + new Vector2(0f, -0.7f));
            Run(k_Dt);

            Assert.That(m_plaza.TargetAction.Id, Is.EqualTo(ActionTable.k_Statue));
            Assert.That(m_plaza.TryInteract(), Is.True);
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(statue.TryPray(), Is.True);
            Assert.That(Blessing.Active, Is.Not.Null);
        }
    }
}
