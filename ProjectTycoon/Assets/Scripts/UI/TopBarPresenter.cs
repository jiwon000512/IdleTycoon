using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    public sealed class TopBarPresenter : IDisposable
    {
        private const string k_CoinsKey = "topbar_coins";

        private readonly TopBarView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly IDisposable m_coins;
        private readonly IDisposable m_blessing;
        private readonly IDisposable m_merchant;
        private readonly IDisposable[] m_evaluation;
        private readonly Mall m_mall;

        private double m_last;
        private double m_spent;

        public TopBarPresenter(TopBarView view, ZooState state, EventBus bus, TableSet tables, RelicMerchant merchant, Mall mall)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;
            m_mall = mall;

            m_last = state.Coins;
            m_coins = bus.Subscribe<Events.CoinsChanged>(Bus_CoinsChanged);
            m_blessing = bus.Subscribe<Events.BlessingChanged>(Bus_BlessingChanged);
            m_merchant = bus.Subscribe<Events.MerchantChanged>(Bus_MerchantChanged);
            m_evaluation = new[]
            {
                bus.Subscribe<Events.EvaluationStarted>(Bus_EvaluationStarted),
                bus.Subscribe<Events.EvaluationEnded>(_ => m_view.HideEvaluation()),
            };
            RefreshCoins();
            ShowMerchant(merchant, false);
            m_view.HideWave();

            // 설계 43: 불러온 축복(돌아가는 연출 없이)
            BlessingTable active = state.Blessing.Active;

            if (active != null)
            {
                m_view.ShowBlessing(active.Icon, StatuePresenter.EffectText(m_tables, active), () => state.Blessing.Remaining, 0f);
            }
        }

        public void Dispose()
        {
            m_coins.Dispose();
            m_blessing.Dispose();
            m_merchant.Dispose();

            foreach (IDisposable subscription in m_evaluation)
            {
                subscription.Dispose();
            }
        }

        // 설계 41: 평가 카드(「★7 평가」 · 남은 시간 · 막대)와 조건 칸(♥ 만족 · 판 빵 · !! 실망, 말풍선과 같은 그림)
        private void Bus_EvaluationStarted(Events.EvaluationStarted e)
        {
            Evaluation evaluation = e.Evaluation;
            List<TopBarView.GoalData> goals = evaluation.Goals.Select(goal => new TopBarView.GoalData
            {
                Icon = goal.Kind == Evaluation.GoalKind.Sell ? goal.Bread.Sprite : goal.Kind == Evaluation.GoalKind.Serve ? "heart" : "angry",
                Value = () => m_tables.Format("eval_progress", goal.Progress, goal.Target),
                State = () => goal.Kind == Evaluation.GoalKind.Lost
                    ? goal.Progress >= goal.Target ? InfoTile.State.Bad : InfoTile.State.Normal
                    : goal.Done ? InfoTile.State.Done : InfoTile.State.Normal,
            }).ToList();
            m_view.ShowEvaluation(evaluation.NextStar, m_tables.Format(evaluation.IsBig ? "eval_tag_big" : "eval_tag", evaluation.NextStar), () => evaluation.Remaining, evaluation.Seconds, goals);
        }

        // 설계 31 · 32 · 34: 행상 알약은 늘 보인다. 오는 중 · 서 있음은 「행상」 + 떠날 때까지(서기 전에는 머무는 초 그대로),
        // 그 밖(가는 중 · 없음)은 회색 얼굴 + 다음에 올 때까지. 계단에서 나오는 순간 톡 튀며 소리
        private void Bus_MerchantChanged(Events.MerchantChanged e)
        {
            ShowMerchant(e.Merchant, e.Merchant.Phase == MerchantPhase.Coming);
        }

        private void ShowMerchant(RelicMerchant merchant, bool arrived)
        {
            bool here = merchant.Phase == MerchantPhase.Coming || merchant.IsOpen;

            if (here)
            {
                m_view.ShowMerchant(true, () => merchant.OpenLeft, arrived);
            }
            else
            {
                m_view.ShowMerchant(false, () => merchant.UntilNext, false);
            }
        }

        // 설계 30: 축복이 걸리면 알약, 풀리면 숨긴다(쉬는 시간이 끝난 알림은 알약과 상관없다)
        private void Bus_BlessingChanged(Events.BlessingChanged e)
        {
            BlessingTable active = e.Blessing.Active;

            if (active == null)
            {
                m_view.HideBlessing();
            }
            else if (e.Prayed)
            {
                m_view.ShowBlessing(active.Icon, StatuePresenter.EffectText(m_tables, active), () => e.Blessing.Remaining, StatueView.k_SpinSeconds);
            }
        }

        private void Bus_CoinsChanged(Events.CoinsChanged e)
        {
            if (e.Wallet != m_state)
            {
                return;
            }

            // 쓴 코인: 표시가 떠 있는 동안 이어 쓰면 합친다(월급날은 점원마다 한 번씩 빠진다)
            if (m_state.Coins < m_last)
            {
                m_spent = (m_view.SpentVisible ? m_spent : 0d) + m_last - m_state.Coins;
                m_view.ShowSpent("-" + BigNumberFormatter.Format(m_spent));
            }

            m_last = m_state.Coins;
            RefreshCoins();
        }

        private void RefreshCoins()
        {
            m_view.SetCoins(m_state.Coins, coins => m_tables.Format(k_CoinsKey, BigNumberFormatter.Format(coins)));
        }
    }
}
