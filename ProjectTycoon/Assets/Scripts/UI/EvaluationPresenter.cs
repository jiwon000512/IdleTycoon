using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 40 · 41 평가 팝업: 평가판 버튼(EvaluationBoardOpened)으로 「빵집 평가」를 열고, 부르기를 Core Evaluation에 잇는다.
    // 평가가 끝나면(EvaluationEnded) 「굴 소식」: 통과면 별 · 평가단장 한마디 · 얻은 것 칸, 실패면 조건 칸. 글 목록 대신 아이콘 칸(설계 41). 편집 모드에서는 닫는다
    public sealed class EvaluationPresenter : IDisposable
    {
        // 말풍선 칸과 같은 아이콘(EvaluationView 아이콘 목록 id)
        private const string k_HeartIcon = "heart";
        private const string k_AngryIcon = "angry";

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
            int next = m_evaluation.NextStar;
            double tip = m_state.Stars.TipChance(m_evaluation.Shop);
            m_view.ShowBoard(new EvaluationView.BoardData
            {
                Title = m_tables.Text("eval_title"),
                Stars = stars,
                Name = m_tables.Format("eval_stars", stars),
                Sub = stars == 0 ? m_tables.Text("eval_no_stars") : tip > 0d ? m_tables.Format("eval_tip", Percent(tip)) : null,
                Price = m_tables.Format("eval_percent", Percent(stars * config.PriceBonus)),
                Visitors = m_tables.Format("eval_percent", Percent(stars * config.VisitorsBonus)),
                Tag = m_tables.Format(m_evaluation.IsBig ? "eval_tag_big" : "eval_tag", next),
                Time = m_tables.Format("eval_seconds", m_evaluation.Seconds.ToString("0", CultureInfo.InvariantCulture)),
                Rush = m_tables.Format("eval_times", config.Rush.ToString("0.#", CultureInfo.InvariantCulture)),
                Goals = m_evaluation.NextGoals.Select(goal => GoalTile(goal, false)).ToList(),
                UnlockTag = m_tables.Format("eval_unlock", next),
                Rewards = RewardTiles(next),
                Extra = MilestoneText(next),
                Button = () => m_evaluation.Running ? m_tables.Text("eval_running")
                    : m_evaluation.CanStart ? m_tables.Text("eval_call") : m_tables.Format("eval_again", BigNumberFormatter.Clock(m_evaluation.Cooldown)),
                ButtonEnabled = () => m_evaluation.CanStart,
            });
        }

        // 통과: 이번 별로 얻은 것(칸) · 팁 · 글 해금. 실패: 조건 칸(못 채운 것 빨강, 채운 것 초록 체크)
        private void ShowNews(bool passed)
        {
            m_news = true;
            int stars = Stars;
            double tip = m_state.Stars.TipChance(m_evaluation.Shop);
            string[] extra = { tip > 0d ? m_tables.Format("eval_tip", Percent(tip)) : null, MilestoneText(stars) };
            m_view.ShowNews(new EvaluationView.NewsData
            {
                Masthead = m_tables.Text("news_title"),
                Title = passed ? m_tables.Format("news_pass", stars) : m_tables.Text("news_fail"),
                Stars = passed ? stars : 0,
                Line = m_tables.Format("news_line", m_tables.Text(m_evaluation.LastLine)),
                BoxLabel = m_tables.Text(passed ? "news_gain" : "news_short"),
                Rewards = passed ? RewardTiles(stars) : null,
                Goals = passed ? null : m_evaluation.Goals.Select(goal => GoalTile(goal, true)).ToList(),
                Extra = passed ? string.Join("\n", extra.Where(line => line != null)) : null,
                Button = m_tables.Text(passed ? "news_ok" : "news_retry"),
            });
        }

        // 조건 칸: 미리 보기는 목표(10명 · 3개 · 3명까지), 결과는 진행(6/10 · 실망 4명)과 채움 · 모자람
        private InfoTile.Data GoalTile(Evaluation.Goal goal, bool result)
        {
            InfoTile.Data tile = new InfoTile.Data
            {
                State = !result ? InfoTile.State.Normal : goal.Done ? InfoTile.State.Done : InfoTile.State.Bad,
            };

            switch (goal.Kind)
            {
                case Evaluation.GoalKind.Serve:
                    tile.Icon = k_HeartIcon;
                    tile.Caption = m_tables.Text("goal_serve_cap");
                    tile.Value = result ? m_tables.Format("eval_progress", goal.Progress, goal.Target) : m_tables.Format("goal_people", goal.Target);
                    break;
                case Evaluation.GoalKind.Sell:
                    tile.Icon = goal.Bread.Sprite;
                    tile.Caption = m_tables.Format("goal_sell_cap", goal.Bread.Name);
                    tile.Value = result ? m_tables.Format("eval_progress", goal.Progress, goal.Target) : m_tables.Format("goal_pieces", goal.Target);
                    break;
                default:
                    tile.Icon = k_AngryIcon;
                    tile.Caption = m_tables.Text("goal_lost_cap");
                    tile.Value = result ? m_tables.Format("goal_people", goal.Progress) : m_tables.Format("goal_until", goal.Target);
                    break;
            }

            return tile;
        }

        // 별 star개가 되면 얻는 것: 별마다 능력 둘 · 마일스톤 상한(지금 → 다음) · 보상
        private List<InfoTile.Data> RewardTiles(int star)
        {
            StarConfigTable config = m_evaluation.Config;
            string shop = m_evaluation.Shop;
            List<InfoTile.Data> tiles = new List<InfoTile.Data>
            {
                new InfoTile.Data { Icon = m_tables.Get<BlessingTable>(BlessingTable.k_Price).Icon, Value = m_tables.Format("eval_percent", Percent(config.PriceBonus)) },
                new InfoTile.Data { Icon = m_tables.Get<BlessingTable>(BlessingTable.k_Visitors).Icon, Value = m_tables.Format("eval_percent", Percent(config.VisitorsBonus)) },
            };
            StarMilestoneTable milestone = m_state.Stars.Milestone(shop, star);

            if (milestone != null)
            {
                foreach ((int value, string kind) in new[] { (milestone.ShelfMax, ShelfInteractable.k_Id), (milestone.OvenMax, OvenInteractable.k_Id), (milestone.CounterMax, CounterInteractable.k_Id), (milestone.UpgradeMax, ZooTycoon.Core.Stars.k_Upgrade) })
                {
                    if (value > 0)
                    {
                        int from = m_state.Stars.CapAt(shop, kind, star - 1);
                        string text = from == int.MaxValue ? value.ToString(CultureInfo.InvariantCulture) : m_tables.Format("eval_from_to", from, value);
                        tiles.Add(new InfoTile.Data { Icon = kind, Value = text });
                    }
                }
            }

            tiles.Add(new InfoTile.Data { Icon = m_tables.Get<ItemTable>(config.RewardItem).Icon, Value = m_tables.Format("eval_times", config.RewardCount) });
            return tiles;
        }

        // 칸으로 못 그리는 해금(팁이 나온다 · 다음 가게)
        private string MilestoneText(int star)
        {
            string text = m_state.Stars.Milestone(m_evaluation.Shop, star)?.Text;
            return text != null ? m_tables.Text(text) : null;
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
