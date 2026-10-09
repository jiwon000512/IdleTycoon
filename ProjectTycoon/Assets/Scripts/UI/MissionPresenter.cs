using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameKit.Audio;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 54: 오늘의 일 판(MissionView). 메뉴 버튼(받을 것이 있으면 배지) → 판: 점수 막대 · 상자 셋(코인 = 분어치 × 그 장의 1분 수입) · 미션(받기) · 새 일까지.
    // 규칙은 Core DailyBoard
    public sealed class MissionPresenter : IDisposable
    {
        private const string k_Title = "mission_title";
        private const string k_Empty = "mission_empty";
        private const string k_Reset = "mission_reset";
        private const string k_Points = "mission_points";
        private const string k_Count = "quest_count";

        private readonly MissionView m_view;
        private readonly DailyBoard m_board;
        private readonly TableSet m_tables;
        private readonly IDisposable m_changed;
        private IReadOnlyList<MissionChestTable> m_chests = new List<MissionChestTable>();

        public MissionPresenter(MissionView view, DailyBoard board, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_board = board;
            m_tables = tables;
            m_view.SetTitle(tables.Text(k_Title), tables.Text(k_Empty), () => tables.Format(k_Reset, BigNumberFormatter.Clock(board.SecondsLeft)));
            m_changed = bus.Subscribe<Events.MissionsChanged>(_ => Refresh());
            m_view.OpenClicked += View_OpenClicked;
            m_view.CloseRequested += m_view.Close;
            m_view.ClaimClicked += View_ClaimClicked;
            m_view.ChestClicked += View_ChestClicked;
            Refresh();
        }

        public void Dispose()
        {
            m_changed.Dispose();
            m_view.OpenClicked -= View_OpenClicked;
            m_view.CloseRequested -= m_view.Close;
            m_view.ClaimClicked -= View_ClaimClicked;
            m_view.ChestClicked -= View_ChestClicked;
        }

        // 편집 모드에서는 버튼을 숨기고 판을 닫는다
        public void SetEditing(bool editing)
        {
            m_view.SetButtonVisible(!editing);

            if (editing)
            {
                m_view.Close();
            }
        }

        private void Refresh()
        {
            List<MissionView.RowData> rows = m_board.Missions.Select(m => new MissionView.RowData(Resources.Load<Sprite>(m.Row.Icon), m_tables.Text(m.Row.Text),
                m_tables.Format(k_Count, m.Progress, m.Row.Count), m_tables.Format(k_Points, m.Row.Points), m.IsDone, m.Claimed)).ToList();
            m_chests = m_board.Chests;
            int max = m_chests.Count == 0 ? 1 : Math.Max(1, m_chests[m_chests.Count - 1].Points);
            List<MissionView.ChestData> chests = m_chests.Select(c => new MissionView.ChestData((float)c.Points / max, BigNumberFormatter.Format(Math.Round(c.Coins)),
                c.Points.ToString(), m_board.Points >= c.Points, m_board.Opened.Contains(c.Chest))).ToList();
            m_view.Show(rows, chests, (float)m_board.Points / max, m_board.Points.ToString());
            m_view.SetBadge(m_board.HasReward);
        }

        private void View_OpenClicked()
        {
            Refresh();
            m_view.Open();
        }

        private void View_ClaimClicked(int index)
        {
            if (m_board.TryClaim(index))
            {
                SoundManager.Instance.Play(SoundTable.k_Tip);
            }
        }

        private void View_ChestClicked(int index)
        {
            if (index < m_chests.Count && m_board.TryOpen(m_chests[index].Chest))
            {
                SoundManager.Instance.Play(SoundTable.k_StarTier);
            }
        }
    }
}
