using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Events;

namespace ZooTycoon.Core
{
    // 설계 40: 가게 하나의 별 평가. 평가판(칠판)에서 부르면 시간 동안 손님(맛 평가단)이 rush배로 몰려오고 조건을 센다(평가가 시작된 뒤 들어온 손님만 평가단):
    // 평가단 만족(계산) · 정해진 빵 판매(큰 평가는 두 종) · 실망한 평가단(빈손으로 나감) 상한. 조건은 StarConfigTable 공식으로 별마다 커진다.
    // 만족 · 판매를 다 채우면 그 자리에서 통과(별 +1 · 보상), 실망이 상한을 넘거나 시간이 다 되면 실패(쉬는 시간). 벌은 없다
    public sealed class Evaluation
    {
        public enum GoalKind
        {
            // 평가단 만족(계산까지 마친 손님)
            Serve,
            // 정해진 빵 판매
            Sell,
            // 실망한 평가단 상한(넘으면 실패)
            Lost,
        }

        public sealed class Goal
        {
            public GoalKind Kind { get; }
            // Sell만: 빵
            public BreadTable Bread { get; }
            public int Target { get; }
            public int Progress { get; internal set; }
            public bool Done => Kind == GoalKind.Lost ? Progress <= Target : Progress >= Target;

            internal Goal(GoalKind kind, BreadTable bread, int target)
            {
                Kind = kind;
                Bread = bread;
                Target = target;
            }
        }

        private readonly BakeryArea m_shop;
        private readonly Stars m_stars;
        private readonly EventBus m_bus;
        private readonly List<Goal> m_goals = new List<Goal>();
        // 평가 중에 들어온 손님(= 맛 평가단). 그 전부터 있던 손님은 세지 않는다
        private readonly HashSet<BakeryVisitor> m_judges = new HashSet<BakeryVisitor>();

        public StarConfigTable Config { get; }
        public bool Running { get; private set; }
        public double Remaining { get; private set; }
        // 떨어진 뒤 다시 부를 때까지 남은 초
        public double Cooldown { get; private set; }
        public bool CanStart => !Running && Cooldown <= 0d;
        // 이번(또는 다음) 평가가 주는 별 · 큰 평가인가
        public int NextStar => m_stars.Count(Shop) + 1;
        public bool IsBig => NextStar % Config.BigEvery == 0;
        public double Seconds => IsBig ? Config.BigTime : Math.Min(Config.TimeBase + Config.TimePerStar * (NextStar - 1), Config.TimeMax);
        // 이번(끝났으면 지난) 평가의 조건과 진행 · 다음 평가의 조건(평가 팝업이 미리 보인다)
        public IReadOnlyList<Goal> Goals => m_goals;
        public IReadOnlyList<Goal> NextGoals => MakeGoals();
        public string Shop => m_shop.Id;

        // 설계 41: 평가 중에 들어온 손님(맛 평가단)인가. 머리 위 수첩 표식
        public bool IsJudge(BakeryVisitor visitor) => Running && m_judges.Contains(visitor);

        // 끝난 평가의 평가단장 한마디(StringTable id, DialogueTable judge_pass · judge_fail 중 하나). 말풍선 · 소식지가 같은 줄을 쓴다
        public string LastLine { get; private set; }

        internal Evaluation(BakeryArea shop, Stars stars, EventBus bus)
        {
            m_shop = shop;
            m_stars = stars;
            m_bus = bus;
            Config = stars.Config(shop.Id);
            bus.Subscribe<Events.BakeryVisitorArrived>(Bus_Arrived);
            bus.Subscribe<Events.BakeryVisitorPaid>(Bus_Paid);
            bus.Subscribe<Events.BakeryVisitorGaveUp>(Bus_GaveUp);
        }

        // 쉬는 중이거나 평가 중이면 false
        public bool TryStart()
        {
            if (!CanStart)
            {
                return false;
            }

            m_goals.Clear();
            m_goals.AddRange(MakeGoals());
            m_judges.Clear();
            Remaining = Seconds;
            Running = true;
            m_bus.Publish(new Events.EvaluationStarted(this));
            return true;
        }

        public void Tick(double dt)
        {
            if (Cooldown > 0d)
            {
                Cooldown = Math.Max(0d, Cooldown - dt);
            }

            if (!Running)
            {
                return;
            }

            Remaining -= dt;

            if (Remaining <= 0d)
            {
                Remaining = 0d;
                End(false);
            }
        }

        // 치트 · 테스트: 쉬는 시간을 무시하고 (평가 중이 아니면) 시작해 만족 · 판매 조건을 모두 채운다(통과 → 별 +1)
        public void PassNow()
        {
            Cooldown = 0d;

            if (!Running)
            {
                TryStart();
            }

            foreach (Goal goal in m_goals.Where(g => g.Kind != GoalKind.Lost))
            {
                goal.Progress = goal.Target;
            }

            Check();
        }

        // n = 지금 별 수. 정해진 빵은 해금한 빵을 별마다 돌리고, 큰 평가는 그다음 빵도(해금한 빵이 하나면 한 종)
        private List<Goal> MakeGoals()
        {
            int n = NextStar - 1;
            IReadOnlyList<BreadTable> breads = m_shop.UnlockedBreads;
            int sell = Config.SellBase + (int)Math.Floor(Config.SellPerStar * n);
            List<Goal> goals = new List<Goal> { new Goal(GoalKind.Serve, null, Config.ServeBase + Config.ServePerStar * n) };
            goals.Add(new Goal(GoalKind.Sell, breads[n % breads.Count], sell));

            if (IsBig && breads.Count > 1)
            {
                goals.Add(new Goal(GoalKind.Sell, breads[(n + 1) % breads.Count], sell));
            }

            goals.Add(new Goal(GoalKind.Lost, null, Math.Max(0, Config.LostBase - n / Config.LostEvery)));
            return goals;
        }

        private void Bus_Arrived(Events.BakeryVisitorArrived e)
        {
            if (Running && e.Visitor.Bakery == m_shop)
            {
                m_judges.Add(e.Visitor);
            }
        }

        private void Bus_Paid(Events.BakeryVisitorPaid e)
        {
            if (!Running || !m_judges.Contains(e.Visitor))
            {
                return;
            }

            foreach (Goal goal in m_goals)
            {
                if (goal.Kind == GoalKind.Serve || goal.Kind == GoalKind.Sell && goal.Bread == e.Visitor.Bread)
                {
                    goal.Progress++;

                    if (goal.Progress == goal.Target)
                    {
                        m_bus.Publish(new Events.EvaluationProgressed(this, true));
                    }
                }
            }

            Check();
        }

        private void Bus_GaveUp(Events.BakeryVisitorGaveUp e)
        {
            if (!Running || !m_judges.Contains(e.Visitor))
            {
                return;
            }

            Goal lost = m_goals.Single(g => g.Kind == GoalKind.Lost);
            lost.Progress++;
            m_bus.Publish(new Events.EvaluationProgressed(this, false));
            Check();
        }

        // 실망이 상한을 넘으면 실패, 만족 · 판매를 다 채우면 통과
        private void Check()
        {
            if (!m_goals.Single(g => g.Kind == GoalKind.Lost).Done)
            {
                End(false);
            }
            else if (m_goals.All(g => g.Done))
            {
                End(true);
            }
        }

        private void End(bool passed)
        {
            if (!Running)
            {
                return;
            }

            Running = false;
            DialogueLineData line = m_shop.Tables.Get<DialogueTable>(passed ? DialogueTable.k_JudgePass : DialogueTable.k_JudgeFail).Lines[0];
            LastLine = line.Texts[Math.Min(line.Texts.Count - 1, (int)(m_shop.Random.NextDouble() * line.Texts.Count))];

            if (passed)
            {
                m_stars.Add(Shop);
                m_shop.Wallet.AddItem(Config.RewardItem, Config.RewardCount);
            }
            else
            {
                Cooldown = Config.Cooldown;
            }

            m_bus.Publish(new Events.EvaluationEnded(this, passed));
        }
    }
}
