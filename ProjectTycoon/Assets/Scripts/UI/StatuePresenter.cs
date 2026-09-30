using System;
using System.Collections.Generic;
using System.Globalization;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 29 웜뱃 석상 팝업: 광장 석상 앞 버튼(StatueOpened)으로 열고, 줄 · 잠금 · 굴리기를 Core Statue에 잇는다.
    // 줄 글 = StatueTable format(이름, 값 %). 굴리면 굴린 줄만 돌린다. 반짝돌이 바뀌면 보유 · 버튼을 다시 그린다. 편집 모드에서는 닫는다
    public sealed class StatuePresenter : IDisposable
    {
        private static readonly string[] k_GradeKeys = { "statue_grade_common", "statue_grade_rare", "statue_grade_legend" };

        private readonly StatueView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;
        private readonly List<string> m_names = new List<string>();
        private StatueInteractable m_thing;

        private Statue Statue => m_state.Statue;

        public StatuePresenter(StatueView view, ZooState state, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;
            m_view.CloseRequested += View_CloseRequested;
            m_view.LockClicked += View_LockClicked;
            m_view.RollClicked += View_RollClicked;
            m_view.SetLabels(tables.Text("statue_title"), tables.Text("statue_hint"), tables.Text("statue_roll"));

            foreach (StatueTable ability in tables.GetAll<StatueTable>())
            {
                m_names.Add(tables.Text(ability.Name));
            }

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.StatueOpened>(Bus_StatueOpened),
                bus.Subscribe<Events.StatueChanged>(Bus_StatueChanged),
                bus.Subscribe<Events.ItemsChanged>(Bus_ItemsChanged),
            };
        }

        public void Dispose()
        {
            m_view.CloseRequested -= View_CloseRequested;
            m_view.LockClicked -= View_LockClicked;
            m_view.RollClicked -= View_RollClicked;

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

        private List<StatueView.LineData> Lines()
        {
            List<StatueView.LineData> lines = new List<StatueView.LineData>();

            for (int i = 0; i < Statue.Lines.Count; i++)
            {
                StatueLine line = Statue.Lines[i];
                lines.Add(new StatueView.LineData
                {
                    Grade = line.IsEmpty ? null : m_tables.Text(k_GradeKeys[(int)line.Grade]),
                    GradeLevel = line.Grade,
                    Text = line.IsEmpty
                        ? m_tables.Text("statue_empty")
                        : m_tables.Format(line.Ability.Format, m_tables.Text(line.Ability.Name), (line.Value * 100d).ToString("0.#", CultureInfo.InvariantCulture)),
                    LockLabel = m_tables.Text(line.Locked ? "statue_locked" : "statue_lock"),
                    Locked = line.Locked,
                    CanToggle = line.Locked || Statue.CanLock(i),
                });
            }

            return lines;
        }

        private string Have => m_tables.Format("statue_have", m_state.Count(Statue.Item));
        private bool CanRoll => m_state.Count(Statue.Item) >= Statue.RollCost;

        private void Refresh()
        {
            m_view.Show(Lines(), Have, Statue.RollCost.ToString(CultureInfo.InvariantCulture), CanRoll);
        }

        private void Bus_StatueOpened(Events.StatueOpened e)
        {
            m_thing = e.Thing;
            Refresh();
            m_view.Open();
        }

        private void Bus_StatueChanged(Events.StatueChanged e)
        {
            if (!m_view.IsOpen)
            {
                return;
            }

            if (!e.Rolled)
            {
                Refresh();
                return;
            }

            bool[] rolled = new bool[e.Statue.Lines.Count];

            for (int i = 0; i < rolled.Length; i++)
            {
                rolled[i] = !e.Statue.Lines[i].Locked;
            }

            m_view.PlayRoll(Lines(), rolled, m_names, Have, Statue.RollCost.ToString(CultureInfo.InvariantCulture), CanRoll);
        }

        // 반짝돌이 바뀌었다(굴리기로 줄었거나 밭에서 늘었다). 도는 중에는 줄을 덮지 않는다
        private void Bus_ItemsChanged(Events.ItemsChanged e)
        {
            if (e.Wallet == m_state && m_view.IsOpen && !m_view.Spinning)
            {
                Refresh();
            }
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        private void View_LockClicked(int line)
        {
            Statue.SetLocked(line, !Statue.Lines[line].Locked);
        }

        private void View_RollClicked()
        {
            m_thing?.TryRoll();
        }
    }
}
