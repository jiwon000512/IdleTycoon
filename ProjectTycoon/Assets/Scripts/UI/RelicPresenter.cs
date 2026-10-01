using System;
using System.Collections.Generic;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 32 유물 화면: HUD 유물 버튼 · 좌판의 「유물 보기」로 열고, 칸 · 격자 · 정보 줄을 Core Relics에 잇는다.
    // 칸이나 모은 유물을 누르면 정보 줄에 그 유물(끼우기/빼기). 열린 동안 RelicsChanged로 갱신, 편집 모드에서는 버튼을 숨기고 닫는다
    public sealed class RelicPresenter : IDisposable
    {
        private readonly RelicView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly IDisposable m_relics;
        private RelicTable m_selected;

        private Relics Relics => m_state.Relics;

        public RelicPresenter(RelicView view, ZooState state, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;
            m_view.OpenClicked += Open;
            m_view.CloseRequested += View_CloseRequested;
            m_view.InfoClicked += View_InfoClicked;
            m_view.SlotClicked += View_SlotClicked;
            m_view.CellClicked += View_CellClicked;
            m_view.SetLabels(tables.Text("relic_title"), tables.Text("relic_slots"), tables.Text("relic_empty"));
            m_relics = bus.Subscribe<Events.RelicsChanged>(Bus_RelicsChanged);
        }

        public void Dispose()
        {
            m_view.OpenClicked -= Open;
            m_view.CloseRequested -= View_CloseRequested;
            m_view.InfoClicked -= View_InfoClicked;
            m_view.SlotClicked -= View_SlotClicked;
            m_view.CellClicked -= View_CellClicked;
            m_relics.Dispose();
        }

        public void Open()
        {
            m_selected = null;
            Refresh();
            m_view.Open();
        }

        public void SetEditing(bool editing)
        {
            m_view.SetButtonVisible(!editing);

            if (editing)
            {
                m_view.Close();
            }
        }

        private RelicCard.Data Card(RelicTable relic)
        {
            int stars = Relics.Stars(relic);
            return new RelicCard.Data
            {
                IconPath = relic.Icon,
                Name = m_tables.Text(relic.Name),
                Effect = stars > 0 ? RelicCartPresenter.EffectText(m_tables, relic, stars) : string.Empty,
                Stars = stars,
                Owned = stars > 0,
                Selected = relic == m_selected,
            };
        }

        private void Refresh()
        {
            List<RelicCard.Data> slots = new List<RelicCard.Data>();

            foreach (RelicTable relic in Relics.Slots)
            {
                slots.Add(relic != null ? Card(relic) : null);
            }

            List<RelicCard.Data> cells = new List<RelicCard.Data>();
            int owned = 0;

            foreach (RelicTable relic in Relics.All)
            {
                cells.Add(Card(relic));
                owned += Relics.Stars(relic) > 0 ? 1 : 0;
            }

            bool equipped = m_selected != null && Relics.IsEquipped(m_selected);
            bool full = IndexOf(Relics.Slots, null) < 0;
            string hint = m_selected != null && !equipped && full ? m_tables.Text("relic_full")
                : Relics.Complete ? m_tables.Text("relic_complete")
                : m_tables.Text("relic_hint");

            m_view.Show(slots, cells, m_tables.Format("relic_collection", owned, Relics.All.Count),
                m_selected != null ? Card(m_selected) : null,
                m_tables.Text(equipped ? "relic_unequip" : "relic_equip"), equipped || !full, hint);
        }

        private void Bus_RelicsChanged(Events.RelicsChanged e)
        {
            if (!m_view.IsOpen)
            {
                return;
            }

            Refresh();
            int slot = IndexOf(Relics.Slots, e.Relic);

            if (e.Change == RelicChange.Equipped && slot >= 0)
            {
                m_view.PulseSlot(slot);
            }
        }

        private static int IndexOf(IReadOnlyList<RelicTable> list, RelicTable relic)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == relic)
                {
                    return i;
                }
            }

            return -1;
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        private void View_SlotClicked(int index)
        {
            RelicTable relic = Relics.Slots[index];

            if (relic != null)
            {
                m_selected = relic;
                Refresh();
            }
        }

        private void View_CellClicked(int index)
        {
            RelicTable relic = Relics.All[index];

            if (Relics.Stars(relic) > 0)
            {
                m_selected = relic;
                Refresh();
            }
        }

        private void View_InfoClicked()
        {
            if (m_selected == null)
            {
                return;
            }

            if (Relics.IsEquipped(m_selected))
            {
                Relics.Unequip(m_selected);
            }
            else
            {
                Relics.TryEquip(m_selected);
            }
        }
    }
}
