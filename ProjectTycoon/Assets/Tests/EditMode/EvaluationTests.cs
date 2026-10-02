using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 40 · 41 검증: 별 마일스톤 상한(사물 수 · 업그레이드) · 조건 공식 · 통과(별 · 능력 · 보상) · 실패(쉬는 시간) · 평가 중 가게 지키기 · 평가단 · 팁 확률. 실제 JSON 값
    public sealed class EvaluationTests
    {
        private const double k_Dt = 0.02;

        private TableSet m_tables;
        private EventBus m_bus;
        private ZooState m_state;
        private BakeryArea m_shop;
        private Mall m_mall;

        private Evaluation Evaluation => m_shop.Evaluation;
        private StarConfigTable Config => m_tables.Get<StarConfigTable>(BakeryArea.k_Id);

        [SetUp]
        public void Create()
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            Wombat wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, new SequenceRandom(new double[2000]), wombat, m_bus);
            PlazaArea plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 4000).ToArray()), wombat, m_bus);
            m_mall = new Mall(m_shop, plaza, new FarmArea(m_tables, new SequenceRandom(new double[200]), wombat, m_bus), m_bus);
            m_state.AddCoins(1000000d);
        }

        private void Run(double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }
        }

        private SheetOption UpgradeOption(Interactable target)
        {
            return m_shop.SheetActions(target).Single(a => a.Table.Id == ActionTable.k_Upgrade).Options(m_shop.Wombat.Worker, target)[0];
        }

        private BakeryVisitor Admit()
        {
            m_shop.Admit(m_tables.GetAll<VisitorTable>()[0]);
            return m_shop.Visitors.Last();
        }

        // ★0은 오븐 2개 · 업그레이드 4단계까지: 코인이 있어도 막히고 「★1 필요」, 별을 따면 풀린다
        [Test]
        public void StarZero_CapsOvensAndUpgrades_UntilFirstStar()
        {
            Assert.That(m_shop.TryBuy(OvenInteractable.k_Id, m_shop.Layout.OvenBase(new Cell(0, 3))), Is.True);
            Assert.That(m_shop.IsCapped(OvenInteractable.k_Id), Is.True);
            Assert.That(m_shop.StarNeeded(OvenInteractable.k_Id), Is.EqualTo(1));
            Assert.That(m_shop.TryBuy(OvenInteractable.k_Id, m_shop.Layout.OvenBase(new Cell(0, 1))), Is.False);

            OvenInteractable oven = m_shop.Ovens[0];

            for (int i = 0; i < 4; i++)
            {
                Assert.That(m_shop.TryChoose(ActionTable.k_Upgrade, oven, null), Is.True);
            }

            Assert.That(UpgradeOption(oven).State, Is.EqualTo(SheetOptionState.Locked));
            Assert.That(m_shop.TryChoose(ActionTable.k_Upgrade, oven, null), Is.False);

            Evaluation.PassNow();
            Assert.That(m_state.Stars.Count(BakeryArea.k_Id), Is.EqualTo(1));
            Assert.That(m_shop.StarNeeded(OvenInteractable.k_Id), Is.EqualTo(-1));
            Assert.That(m_shop.TryBuy(OvenInteractable.k_Id, m_shop.Layout.OvenBase(new Cell(0, 1))), Is.True);
            Assert.That(m_shop.TryChoose(ActionTable.k_Upgrade, oven, null), Is.True);
        }

        // 조건 공식: n = 지금 별 수. 다섯 번째 별은 큰 평가(시간이 길다)
        [Test]
        public void Goals_GrowWithStars_AndFifthIsBig()
        {
            List<Evaluation.Goal> goals = Evaluation.NextGoals.ToList();
            Assert.That(Evaluation.Seconds, Is.EqualTo(Config.TimeBase));
            Assert.That(goals.Single(g => g.Kind == Evaluation.GoalKind.Serve).Target, Is.EqualTo(Config.ServeBase));
            Assert.That(goals.Single(g => g.Kind == Evaluation.GoalKind.Sell).Bread, Is.SameAs(m_shop.UnlockedBreads[0]));
            Assert.That(goals.Single(g => g.Kind == Evaluation.GoalKind.Lost).Target, Is.EqualTo(Config.LostBase));

            for (int i = 0; i < 4; i++)
            {
                Evaluation.PassNow();
            }

            goals = Evaluation.NextGoals.ToList();
            Assert.That(Evaluation.IsBig, Is.True);
            Assert.That(Evaluation.Seconds, Is.EqualTo(Config.BigTime));
            Assert.That(goals.Single(g => g.Kind == Evaluation.GoalKind.Serve).Target, Is.EqualTo(Config.ServeBase + Config.ServePerStar * 4));
            Assert.That(goals.Single(g => g.Kind == Evaluation.GoalKind.Lost).Target, Is.EqualTo(Config.LostBase - 4 / Config.LostEvery));
        }

        // 계산한 손님을 세고, 통과하면 별 +1 · 빵 값 능력 · 반짝돌 보상. 그사이 평가 중엔 가게를 나갈 수 없다
        [Test]
        public void Pass_AddsStar_Bonus_AndReward()
        {
            List<Events.EvaluationEnded> ended = new List<Events.EvaluationEnded>();
            m_bus.Subscribe<Events.EvaluationEnded>(e => ended.Add(e));
            int gems = m_state.Count(Config.RewardItem);

            Assert.That(Evaluation.TryStart(), Is.True);
            Assert.That(m_shop.CanLeave, Is.False);
            m_bus.Publish(new Events.BakeryVisitorPaid(Admit(), 10d, false));
            Assert.That(Evaluation.Goals.Single(g => g.Kind == Evaluation.GoalKind.Serve).Progress, Is.EqualTo(1));

            Evaluation.PassNow();
            Assert.That(ended.Single().Passed, Is.True);
            Assert.That(m_shop.CanLeave, Is.True);
            Assert.That(m_state.Stars.Count(BakeryArea.k_Id), Is.EqualTo(1));
            Assert.That(m_state.Scale(BlessingTable.k_Price), Is.EqualTo(1d + Config.PriceBonus).Within(1e-9));
            Assert.That(m_state.Count(Config.RewardItem), Is.EqualTo(gems + Config.RewardCount));
            Assert.That(Evaluation.CanStart, Is.True);
        }

        // 실망한 평가단이 상한을 넘으면 그 자리에서 실패: 별은 그대로, 쉬는 시간 동안 다시 못 부른다
        [Test]
        public void TooManyLost_Fails_AndCoolsDown()
        {
            List<Events.EvaluationEnded> ended = new List<Events.EvaluationEnded>();
            m_bus.Subscribe<Events.EvaluationEnded>(e => ended.Add(e));
            Assert.That(Evaluation.TryStart(), Is.True);

            for (int i = 0; i <= Config.LostBase; i++)
            {
                m_bus.Publish(new Events.BakeryVisitorGaveUp(Admit()));
            }

            Assert.That(ended.Single().Passed, Is.False);
            Assert.That(Evaluation.Goals.Single(g => g.Kind == Evaluation.GoalKind.Lost).Progress, Is.EqualTo(Config.LostBase + 1), "끝난 뒤에도 지난 평가 진행이 남는다(소식지)");
            Assert.That(m_state.Stars.Count(BakeryArea.k_Id), Is.EqualTo(0));
            Assert.That(Evaluation.CanStart, Is.False);
            Assert.That(Evaluation.TryStart(), Is.False);

            Run(Config.Cooldown + 0.1);
            Assert.That(Evaluation.CanStart, Is.True);
        }

        // 시간이 다 되면 실패
        [Test]
        public void TimeUp_Fails()
        {
            bool? passed = null;
            m_bus.Subscribe<Events.EvaluationEnded>(e => passed = e.Passed);
            Evaluation.TryStart();
            Run(Evaluation.Seconds + 0.1);

            Assert.That(passed, Is.False);
            Assert.That(Evaluation.Running, Is.False);
        }

        // 평가 중 웜뱃은 구멍 앞 띠에 들어서도 나가지 않고 「…」. 평가단장이 구멍에서 나와 평가판 옆에 선다
        [Test]
        public void DuringEvaluation_WombatStays_AndJudgeComesIn()
        {
            Evaluation.TryStart();
            Assert.That(m_shop.Judge, Is.Not.Null);
            m_shop.Wombat.Mover.Place(m_shop.Layout.HoleFloor + new Vector2(0f, 0.6f));
            Run(4d);

            Assert.That(m_mall.Active, Is.SameAs(m_shop));
            Assert.That(m_shop.Wombat.Bubble.Id, Is.EqualTo(BubbleTable.k_Wait));
            Assert.That(Vector2.Distance(m_shop.Judge.Position, m_shop.Layout.Nav.Snap(new Vector2((float)Config.JudgeX, (float)Config.JudgeY))), Is.LessThan(0.05f));
        }

        // 설계 41 수첩 표식: 평가 중에 들어온 손님만 평가단, 평가가 끝나면 아무도 아니다
        [Test]
        public void Judges_AreOnlyVisitorsAdmittedDuringEvaluation()
        {
            BakeryVisitor before = Admit();
            Evaluation.TryStart();
            BakeryVisitor during = Admit();

            Assert.That(Evaluation.IsJudge(before), Is.False);
            Assert.That(Evaluation.IsJudge(during), Is.True);

            Evaluation.PassNow();
            Assert.That(Evaluation.IsJudge(during), Is.False);
        }

        // 설계 41 평가판의 「2→3」: 별 n개일 때 상한(CapAt)은 별을 n개 딴 뒤의 상한(Cap)과 같다
        [Test]
        public void CapAt_MatchesCapAfterThatManyStars()
        {
            Stars stars = m_state.Stars;
            string[] kinds = { ShelfInteractable.k_Id, OvenInteractable.k_Id, CounterInteractable.k_Id, Stars.k_Upgrade };
            int[] now = kinds.Select(k => stars.Cap(BakeryArea.k_Id, k)).ToArray();
            int[] next = kinds.Select(k => stars.CapAt(BakeryArea.k_Id, k, 1)).ToArray();

            Assert.That(kinds.Select(k => stars.CapAt(BakeryArea.k_Id, k, 0)), Is.EqualTo(now));
            Assert.That(next, Is.Not.EqualTo(now), "★1 마일스톤이 상한을 연다");

            Evaluation.PassNow();
            Assert.That(kinds.Select(k => stars.Cap(BakeryArea.k_Id, k)), Is.EqualTo(next));
        }

        // 팁은 tipFrom별부터, 별마다 오르고 최대에서 멈춘다
        [Test]
        public void TipChance_StartsAtTipFrom_AndCaps()
        {
            Stars stars = m_state.Stars;
            Assert.That(stars.TipChance(BakeryArea.k_Id), Is.EqualTo(0d));

            for (int i = 0; i < Config.TipFrom; i++)
            {
                Evaluation.PassNow();
            }

            Assert.That(stars.TipChance(BakeryArea.k_Id), Is.EqualTo(Config.TipBase).Within(1e-9));

            for (int i = 0; i < 60; i++)
            {
                Evaluation.PassNow();
            }

            Assert.That(stars.TipChance(BakeryArea.k_Id), Is.EqualTo(Config.TipMax).Within(1e-9));
        }
    }
}
