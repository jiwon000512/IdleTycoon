using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 29 검증: 반짝돌로 세 줄 굴리기 · 잠금(값이 오르고 두 줄까지) · 모자라면 그대로, 능력이 오븐 · 걸음 · 밭 · 덤 · 손님에 걸림, 광장 석상(분수 자리 · 편집에서 못 집음 · 버튼으로 팝업). 실제 JSON 값
    public sealed class StatueTests
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

        private Statue Statue => m_state.Statue;
        private PlazaConfigTable Config => m_tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
        private string Gem => Config.StatueItem;

        [SetUp]
        public void Create()
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            m_state.TrySpendItem(Gem, m_state.Count(Gem));
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

        // 능력 표 순서의 index번째가 뽑히는 난수(비중이 같다) · 등급이 뽑히는 난수(비중 70 · 25 · 5)
        private double AbilityRoll(string id)
        {
            var abilities = m_tables.GetAll<StatueTable>();
            double total = abilities.Sum(a => a.Weight);
            double before = abilities.TakeWhile(a => a.Id != id).Sum(a => a.Weight);
            return (before + m_tables.Get<StatueTable>(id).Weight * 0.5d) / total;
        }

        private static double GradeRoll(StatueGrade grade)
        {
            return grade == StatueGrade.Common ? 0.1d : grade == StatueGrade.Rare ? 0.8d : 0.99d;
        }

        // 첫 줄에 그 능력 · 등급, 나머지 두 줄은 이 시험과 상관없는 능력(보통)
        private void Grant(string id, StatueGrade grade, string filler)
        {
            m_state.AddItem(Gem, Statue.RollCost);
            SequenceRandom random = new SequenceRandom(AbilityRoll(id), GradeRoll(grade), AbilityRoll(filler), 0.1d, AbilityRoll(filler), 0.1d);
            Assert.That(Statue.TryRoll(m_state, random), Is.True);
        }

        private double Value(string id, StatueGrade grade)
        {
            return m_tables.Get<StatueTable>(id).Values[(int)grade];
        }

        [Test]
        public void Roll_FillsEveryLine_AndSpendsRollCost()
        {
            m_state.AddItem(Gem, Config.StatueRollCost);
            int changed = 0;
            m_bus.Subscribe<Events.StatueChanged>(e => changed += e.Rolled ? 1 : 0);
            SequenceRandom random = new SequenceRandom(AbilityRoll(StatueTable.k_Bake), 0.99d, AbilityRoll(StatueTable.k_Price), 0.8d, AbilityRoll(StatueTable.k_Bonus), 0.1d);

            Assert.That(Statue.TryRoll(m_state, random), Is.True);

            Assert.That(m_state.Count(Gem), Is.EqualTo(0));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(Statue.Lines.Select(l => l.Ability.Id), Is.EqualTo(new[] { StatueTable.k_Bake, StatueTable.k_Price, StatueTable.k_Bonus }));
            Assert.That(Statue.Lines.Select(l => l.Grade), Is.EqualTo(new[] { StatueGrade.Legend, StatueGrade.Rare, StatueGrade.Common }));
            Assert.That(Statue.Boost(StatueTable.k_Bake), Is.EqualTo(Value(StatueTable.k_Bake, StatueGrade.Legend)));
            Assert.That(Statue.Boost(StatueTable.k_Walk), Is.EqualTo(0d));
        }

        // 빈 줄은 못 잠그고, 잠근 줄은 굴려도 그대로이며 값이 lockCost씩 오른다. 잠금은 maxLocks줄까지
        [Test]
        public void Lock_KeepsLine_RaisesCost_UpToMaxLocks()
        {
            Assert.That(Statue.CanLock(0), Is.False);
            Grant(StatueTable.k_Bake, StatueGrade.Legend, StatueTable.k_Grow);

            Statue.SetLocked(0, true);
            Statue.SetLocked(1, true);
            Statue.SetLocked(2, true);

            Assert.That(Statue.LockedCount, Is.EqualTo(Config.StatueMaxLocks));
            Assert.That(Statue.Lines[2].Locked, Is.False);
            Assert.That(Statue.RollCost, Is.EqualTo(Config.StatueRollCost + Config.StatueMaxLocks * Config.StatueLockCost));

            Statue.SetLocked(1, false);
            m_state.AddItem(Gem, Statue.RollCost);
            Assert.That(Statue.TryRoll(m_state, new SequenceRandom(AbilityRoll(StatueTable.k_Walk), 0.1d, AbilityRoll(StatueTable.k_Walk), 0.1d)), Is.True);

            Assert.That(Statue.Lines[0].Ability.Id, Is.EqualTo(StatueTable.k_Bake));
            Assert.That(Statue.Lines[0].Grade, Is.EqualTo(StatueGrade.Legend));
            Assert.That(Statue.Lines[1].Ability.Id, Is.EqualTo(StatueTable.k_Walk));
            Assert.That(Statue.Lines[2].Ability.Id, Is.EqualTo(StatueTable.k_Walk));
            Assert.That(m_state.Count(Gem), Is.EqualTo(0));
        }

        [Test]
        public void Roll_WithTooFewGems_ChangesNothing()
        {
            m_state.AddItem(Gem, Config.StatueRollCost - 1);

            Assert.That(Statue.TryRoll(m_state, new SequenceRandom()), Is.False);

            Assert.That(m_state.Count(Gem), Is.EqualTo(Config.StatueRollCost - 1));
            Assert.That(Statue.Lines.All(l => l.IsEmpty), Is.True);
        }

        // 굽기 전설이면 오븐이 (1 + 값)배로 굽는다
        [Test]
        public void BakeBoost_OvenBakesFaster()
        {
            Grant(StatueTable.k_Bake, StatueGrade.Legend, StatueTable.k_Grow);
            OvenInteractable oven = m_shop.Ovens[0];
            Assert.That(oven.TryStart(m_tables.GetAll<BreadTable>()[0]), Is.True);
            double start = oven.Remaining;

            Run(1d);

            Assert.That(start - oven.Remaining, Is.EqualTo(1d + Value(StatueTable.k_Bake, StatueGrade.Legend)).Within(1e-6));
        }

        [Test]
        public void WalkBoost_WombatWalksFaster()
        {
            double speed = m_wombat.Speed;

            Grant(StatueTable.k_Walk, StatueGrade.Rare, StatueTable.k_Grow);

            Assert.That(m_wombat.Speed, Is.EqualTo(speed * (1d + Value(StatueTable.k_Walk, StatueGrade.Rare))).Within(1e-9));
        }

        [Test]
        public void GrowBoost_FieldGrowsFaster()
        {
            Grant(StatueTable.k_Grow, StatueGrade.Legend, StatueTable.k_Walk);
            PlotInteractable plot = m_farm.Plots.First(p => p.IsTilled);
            plot.Plant(m_farm.Crop, false);
            double start = plot.Remaining;

            Run(1d);

            Assert.That(start - plot.Remaining, Is.EqualTo(1d + Value(StatueTable.k_Grow, StatueGrade.Legend)).Within(1e-6));
        }

        // 덤 전설이면 덤 확률에 값이 더해진다: 난수 0.3은 기본(0.2)으로는 안 나오지만 0.2 + 0.2 = 0.4면 나온다
        [Test]
        public void BonusBoost_AddsToBonusChance()
        {
            Grant(StatueTable.k_Bonus, StatueGrade.Legend, StatueTable.k_Walk);
            PlotInteractable plot = m_farm.Plots.First(p => p.IsTilled);
            plot.Plant(m_farm.Crop, false);
            double roll = m_tables.Get<FarmConfigTable>(FarmConfigTable.k_Main).BonusChance + Value(StatueTable.k_Bonus, StatueGrade.Legend) * 0.5d;

            plot.Harvest(m_state, new SequenceRandom(roll));

            Assert.That(m_state.Count(Gem), Is.EqualTo(1));
        }

        // 손님 전설이면 계단으로 오는 간격이 ÷ (1 + 값): 첫 손님 뒤 기본 간격이 되기 전에 둘째가 온다
        [Test]
        public void VisitorsBoost_CustomersArriveSooner()
        {
            Grant(StatueTable.k_Visitors, StatueGrade.Legend, StatueTable.k_Walk);
            int arrived = 0;
            m_bus.Subscribe<Events.PlazaVisitorArrived>(_ => arrived++);
            double boosted = Config.ArrivalSeconds / (1d + Value(StatueTable.k_Visitors, StatueGrade.Legend));

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
        }
    }
}
