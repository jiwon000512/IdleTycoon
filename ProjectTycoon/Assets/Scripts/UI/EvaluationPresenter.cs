using System;
using System.Collections.Generic;
using System.Globalization;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 40 평가 팝업: 평가판 버튼(EvaluationBoardOpened)으로 「빵집 평가」를 열고, 부르기를 Core Evaluation에 잇는다.
    // 평가가 끝나면(EvaluationEnded) 같은 틀에 「굴 소식」: 통과면 별 · 평가단장 한마디 · 쌓인 능력 · 보상 · 새 마일스톤, 실패면 모자란 조건. 편집 모드에서는 닫는다
    public sealed class EvaluationPresenter : IDisposable
    {
        private readonly EvaluationView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;
        private Evaluation m_evaluation;
        // 소식지를 보이는 중(버튼이 닫기)
        private bool m_news;

        public EvaluationPresenter(EvaluationView view, ZooState state, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;
            m_view.CloseRequested += View_CloseRequested;
            m_view.ButtonClicked += View_ButtonClicked;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.EvaluationBoardOpened>(Bus_BoardOpened),
                bus.Subscribe<Events.EvaluationEnded>(Bus_EvaluationEnded),
            };
        }

        public void Dispose()
        {
            m_view.CloseRequested -= View_CloseRequested;
            m_view.ButtonClicked -= View_ButtonClicked;

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        public void SetEditing(bool editing)
        {
            if (editing)
            {
                m_view.Close();
            }
        }

        // 퍼센트 숫자(0.14 → 「14」)
        private static string Percent(double value)
        {
            return (value * 100d).ToString("0.#", CultureInfo.InvariantCulture);
        }

        private int Stars => m_state.Stars.Count(m_evaluation.Shop);

        private void ShowBoard()
        {
            m_news = false;
            StarConfigTable config = m_evaluation.Config;
            int stars = Stars;
            List<string> lines = new List<string>
            {
                m_tables.Format(m_evaluation.IsBig ? "eval_next_big" : "eval_next", m_evaluation.NextStar, m_evaluation.Seconds.ToString("0", CultureInfo.InvariantCulture)),
                m_tables.Format("eval_rush", config.Rush.ToString("0.#", CultureInfo.InvariantCulture)),
            };

            foreach (Evaluation.Goal goal in m_evaluation.NextGoals)
            {
                lines.Add(GoalLine(goal));
            }

            lines.Add(string.Empty);
            lines.Add(m_tables.Format("eval_unlock", m_evaluation.NextStar));
            lines.Add(m_tables.Format("eval_bonus_price", Percent(config.PriceBonus)));
            lines.Add(m_tables.Format("eval_bonus_visitors", Percent(config.VisitorsBonus)));
            lines.AddRange(MilestoneLines(m_state.Stars.Milestone(m_evaluation.Shop, m_evaluation.NextStar)));
            lines.Add(m_tables.Format("eval_reward_line", config.RewardCount));

            m_view.Show(new EvaluationView.PageData
            {
                Title = m_tables.Text("eval_title"),
                Stars = stars,
                Name = stars > 0 ? m_tables.Format("eval_stars", stars) : m_tables.Text("eval_no_stars"),
                Effect = m_tables.Format("eval_bonus_price", Percent(stars * config.PriceBonus)),
                Time = m_tables.Format("eval_bonus_visitors", Percent(stars * config.VisitorsBonus)),
                Hint = string.Join("\n", lines),
                Button = () => m_evaluation.Running ? m_tables.Text("eval_running")
                    : m_evaluation.CanStart ? m_tables.Text("eval_call") : m_tables.Format("eval_again", BigNumberFormatter.Clock(m_evaluation.Cooldown)),
                ButtonEnabled = () => m_evaluation.CanStart,
            });
        }

        // 통과: 쌓인 능력 · 팁 · 보상 · 새 마일스톤. 실패: 못 채운 조건(진행/목표)
        private void ShowNews(bool passed)
        {
            m_news = true;
            StarConfigTable config = m_evaluation.Config;
            int stars = Stars;
            List<string> lines = new List<string>();

            if (passed)
            {
                lines.Add(m_tables.Format("eval_bonus_price", Percent(stars * config.PriceBonus)));
                lines.Add(m_tables.Format("eval_bonus_visitors", Percent(stars * config.VisitorsBonus)));
                double tip = m_state.Stars.TipChance(m_evaluation.Shop);

                if (tip > 0d)
                {
                    lines.Add(m_tables.Format("eval_tip", Percent(tip)));
                }

                lines.Add(m_tables.Format("eval_reward_line", config.RewardCount));
                lines.AddRange(MilestoneLines(m_state.Stars.Milestone(m_evaluation.Shop, stars)));
            }
            else
            {
                lines.Add(m_tables.Text("news_short"));

                foreach (Evaluation.Goal goal in m_evaluation.Goals)
                {
                    if (!goal.Done || goal.Kind == Evaluation.GoalKind.Lost && goal.Progress > goal.Target)
                    {
                        lines.Add(GoalLine(goal) + " " + goal.Progress + "/" + goal.Target);
                    }
                }
            }

            m_view.Show(new EvaluationView.PageData
            {
                Title = m_tables.Text("news_title"),
                Stars = passed ? stars : 0,
                Name = passed ? m_tables.Format("news_pass", stars) : m_tables.Text("news_fail"),
                Effect = m_tables.Text(m_evaluation.LastLine),
                Time = string.Empty,
                Hint = string.Join("\n", lines),
                Button = () => m_tables.Text(passed ? "news_ok" : "news_retry"),
                ButtonEnabled = () => true,
            });
        }

        private string GoalLine(Evaluation.Goal goal)
        {
            switch (goal.Kind)
            {
                case Evaluation.GoalKind.Serve: return m_tables.Format("goal_serve_line", goal.Target);
                case Evaluation.GoalKind.Sell: return m_tables.Format("goal_sell_line", goal.Bread.Name, goal.Target);
                default: return m_tables.Format("goal_lost_line", goal.Target);
            }
        }

        // 마일스톤 한 행: 바뀌는 상한마다 한 줄 + 그 밖의 해금 글
        private IEnumerable<string> MilestoneLines(StarMilestoneTable milestone)
        {
            if (milestone == null)
            {
                yield break;
            }

            foreach ((int value, string key) in new[] { (milestone.ShelfMax, "milestone_shelf"), (milestone.OvenMax, "milestone_oven"), (milestone.CounterMax, "milestone_counter"), (milestone.UpgradeMax, "milestone_upgrade") })
            {
                if (value > 0)
                {
                    yield return m_tables.Format(key, value);
                }
            }

            if (milestone.Text != null)
            {
                yield return m_tables.Text(milestone.Text);
            }
        }

        private void Bus_BoardOpened(Events.EvaluationBoardOpened e)
        {
            m_evaluation = e.Evaluation;
            ShowBoard();
            m_view.Open();
        }

        private void Bus_EvaluationEnded(Events.EvaluationEnded e)
        {
            m_evaluation = e.Evaluation;
            ShowNews(e.Passed);
            m_view.Open();
            int stars = Stars;
            m_view.PlayResult(e.Passed, e.Passed && stars > 1 && stars % e.Evaluation.Config.BigEvery == 1);
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        // 평가판: 부르면 닫는다(가게로 돌아가 손님을 받는다). 소식지: 닫는다
        private void View_ButtonClicked()
        {
            if (!m_news)
            {
                m_evaluation.TryStart();
            }

            m_view.Close();
        }
    }
}
