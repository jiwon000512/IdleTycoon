using System;
using System.Collections.Generic;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 21: 점원 팝업. 자리 목록(오븐·계산대마다 줄: 고용/해고) → 후보 5명 목록(선택) → 고용할까 창(그냥 고용/협상하기/취소) → 협상(탭 한 번) → 고용.
    // 규칙(후보·월급·협상 결과·해고)은 전부 BakeryArea.Clerks·Negotiation이 정하고 여기는 글자와 흐름만. 지금 가게 탭은 빵집 하나
    public sealed class ClerkPresenter : IDisposable
    {
        private enum Mode
        {
            Slots,
            Candidates,
        }

        // 협상 결과를 보여 주고 닫기까지
        private const double k_ResultSeconds = 1.2;

        private readonly ClerkPopupView m_view;
        private readonly BakeryArea m_bakery;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;
        private readonly List<Interactable> m_slots = new List<Interactable>();
        private readonly List<ClerkPopupView.RowData> m_rows = new List<ClerkPopupView.RowData>();

        private Mode m_mode;
        private Interactable m_slot;
        private Candidate m_candidate;
        private bool m_asking;
        private Negotiation m_negotiation;
        private double m_resultTimer;

        public ClerkPresenter(ClerkPopupView view, Mall mall, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_bakery = mall.Bakery;
            m_tables = tables;
            m_view.OpenClicked += View_OpenClicked;
            m_view.CloseClicked += View_CloseClicked;
            m_view.RowButtonClicked += View_RowButtonClicked;
            m_view.FootClicked += View_FootClicked;
            m_view.AskPlainClicked += View_AskPlainClicked;
            m_view.AskNegotiateClicked += View_AskNegotiateClicked;
            m_view.AskCancelClicked += View_AskCancelClicked;
            m_view.Tapped += View_Tapped;
            m_view.Ticked += View_Ticked;

            NegotiationZone green = ZoneOf(ZoneColor.Green);
            NegotiationZone white = ZoneOf(ZoneColor.White);
            m_view.SetZones((float)green.HalfWidth, (float)white.HalfWidth);

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.ClerkHired>(_ => RefreshIfOpen()),
                bus.Subscribe<Events.ClerkFired>(Bus_ClerkFired),
                bus.Subscribe<Events.CandidatesChanged>(_ => RefreshIfOpen()),
                bus.Subscribe<Events.CoinsChanged>(_ => RefreshIfOpen()),
                bus.Subscribe<Events.LayoutChanged>(_ => RefreshIfOpen()),
                bus.Subscribe<Events.ThingChanged>(_ => RefreshIfOpen()),
            };
        }

        public void Dispose()
        {
            m_view.OpenClicked -= View_OpenClicked;
            m_view.CloseClicked -= View_CloseClicked;
            m_view.RowButtonClicked -= View_RowButtonClicked;
            m_view.FootClicked -= View_FootClicked;
            m_view.AskPlainClicked -= View_AskPlainClicked;
            m_view.AskNegotiateClicked -= View_AskNegotiateClicked;
            m_view.AskCancelClicked -= View_AskCancelClicked;
            m_view.Tapped -= View_Tapped;
            m_view.Ticked -= View_Ticked;

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        // 편집 모드에서는 버튼을 숨기고 팝업을 닫는다
        public void SetEditing(bool editing)
        {
            m_view.SetButtonVisible(!editing);

            if (editing)
            {
                Close();
            }
        }

        private NegotiationZone ZoneOf(ZoneColor color)
        {
            foreach (NegotiationZone zone in m_bakery.ClerkConfig.Zones)
            {
                if (zone.Color == color)
                {
                    return zone;
                }
            }

            return null;
        }

        private string Wage(int wage)
        {
            return m_tables.Format("clerk_wage", wage);
        }

        private void ShowSlots()
        {
            m_mode = Mode.Slots;
            m_negotiation = null;
            m_asking = false;
            m_slots.Clear();
            m_slots.AddRange(m_bakery.Ovens);
            m_slots.AddRange(m_bakery.Counters);
            m_rows.Clear();
            int wageSum = 0;

            foreach (Clerk clerk in m_bakery.Clerks)
            {
                wageSum += clerk.Wage;
            }

            for (int i = 0; i < m_slots.Count; i++)
            {
                Interactable slot = m_slots[i];
                Clerk clerk = m_bakery.ClerkOf(slot);
                string slotName = m_tables.Format("clerk_slot", m_tables.Text("kind_" + slot.Table.Id), IndexAmongKind(slot));

                if (clerk == null)
                {
                    m_rows.Add(new ClerkPopupView.RowData
                    {
                        Icon = null,
                        IconPath = null,
                        Name = slotName,
                        Sub = m_tables.Text("clerk_empty"),
                        Button = m_tables.Text("clerk_hire"),
                        Enabled = true,
                    });
                    continue;
                }

                string state = clerk.Idling ? m_tables.Text("clerk_idling") : m_tables.Text("clerk_working");
                m_rows.Add(new ClerkPopupView.RowData
                {
                    IconPath = clerk.Look.Sprite,
                    Name = m_tables.Format("clerk_slot_named", slotName, clerk.Name),
                    Sub = m_tables.Format("clerk_status", clerk.Skill, state, Wage(clerk.Wage)),
                    Button = m_tables.Text("clerk_fire"),
                    Enabled = true,
                });
            }

            m_view.ShowList(m_tables.Text("clerk_title"), m_tables.Text("clerk_tab_bakery"),
                m_tables.Format("clerk_summary", m_bakery.Clerks.Count, m_slots.Count, Wage(wageSum)),
                m_rows, m_tables.Format("clerk_foot", m_bakery.ClerkConfig.WagePeriodSeconds), null, false);
        }

        // 같은 종류 안의 번호(오븐 1·오븐 2)
        private int IndexAmongKind(Interactable slot)
        {
            int index = 0;

            foreach (Interactable other in m_slots)
            {
                if (other.Table.Id == slot.Table.Id)
                {
                    index++;
                }

                if (other == slot)
                {
                    return index;
                }
            }

            return index;
        }

        private void ShowCandidates()
        {
            m_mode = Mode.Candidates;
            m_rows.Clear();

            foreach (Candidate candidate in m_bakery.Candidates)
            {
                m_rows.Add(new ClerkPopupView.RowData
                {
                    IconPath = candidate.Look.Sprite,
                    Name = candidate.Name,
                    Sub = m_tables.Format("clerk_candidate_sub", candidate.Skill, Wage(m_bakery.WageFor(candidate, m_slot))),
                    Button = m_tables.Text("clerk_select"),
                    Enabled = true,
                });
            }

            double refresh = m_bakery.ClerkConfig.RefreshCost;
            m_view.ShowList(m_tables.Format("clerk_candidates_title", m_tables.Format("clerk_slot", m_tables.Text("kind_" + m_slot.Table.Id), IndexAmongKind(m_slot))), null, null,
                m_rows, null, m_tables.Format("clerk_refresh", BigNumberFormatter.Format(refresh)), m_bakery.Wombat.Worker.Wallet.Coins >= refresh);

            if (m_asking)
            {
                ShowAsk();
            }
        }

        private void ShowAsk()
        {
            m_asking = true;
            m_view.ShowAsk(m_candidate.Look.Sprite, m_tables.Format("clerk_ask", m_candidate.Name),
                m_tables.Format("clerk_ask_sub", m_candidate.Skill, Wage(m_bakery.WageFor(m_candidate, m_slot))),
                m_tables.Text("clerk_hire_plain"), m_tables.Text("clerk_negotiate"), m_tables.Text("clerk_cancel"));
        }

        private void Hire(int wage)
        {
            m_bakery.TryHire(m_candidate, m_slot, wage);
            Close();
        }

        private void Close()
        {
            m_negotiation = null;
            m_asking = false;
            m_view.Close();
        }

        private void RefreshIfOpen()
        {
            if (!m_view.IsVisible || m_negotiation != null)
            {
                return;
            }

            // 자리가 사라졌으면(보관) 목록으로
            if (m_mode == Mode.Candidates && !IsPlaced(m_slot))
            {
                ShowSlots();
                return;
            }

            if (m_mode == Mode.Slots)
            {
                ShowSlots();
            }
            else
            {
                ShowCandidates();
            }
        }

        private bool IsPlaced(Interactable slot)
        {
            foreach (Interactable thing in m_bakery.Things)
            {
                if (thing == slot)
                {
                    return true;
                }
            }

            return false;
        }

        private void View_OpenClicked()
        {
            m_view.Open();
            ShowSlots();
        }

        private void View_CloseClicked()
        {
            if (m_negotiation != null)
            {
                return;
            }

            if (m_mode == Mode.Candidates)
            {
                ShowSlots();
                return;
            }

            Close();
        }

        private void View_RowButtonClicked(int index)
        {
            if (m_mode == Mode.Slots)
            {
                Interactable slot = m_slots[index];
                Clerk clerk = m_bakery.ClerkOf(slot);

                if (clerk != null)
                {
                    m_bakery.Fire(clerk, FireReason.Fired);
                    return;
                }

                m_slot = slot;
                ShowCandidates();
                return;
            }

            m_candidate = m_bakery.Candidates[index];
            ShowAsk();
        }

        private void View_FootClicked()
        {
            if (m_mode == Mode.Candidates)
            {
                m_bakery.TryRefreshCandidates();
            }
        }

        private void View_AskPlainClicked()
        {
            Hire(m_bakery.WageFor(m_candidate, m_slot));
        }

        private void View_AskNegotiateClicked()
        {
            m_asking = false;
            m_negotiation = m_bakery.Negotiate(m_candidate, m_slot);
            m_resultTimer = k_ResultSeconds;
            m_view.ShowNegotiation(m_tables.Format("clerk_nego_title", m_candidate.Name), m_tables.Text("clerk_nego_hint"),
                m_candidate.Look.SideIdleSheet ?? m_candidate.Look.Sprite,
                m_tables.Format("clerk_nego_offer", Wage(m_negotiation.BaseWage)), m_tables.Text("clerk_nego_think"));
        }

        private void View_AskCancelClicked()
        {
            m_asking = false;
            m_view.HideAsk();
        }

        private void View_Tapped()
        {
            m_negotiation?.Stop();
        }

        // 표시를 움직이고, 결과가 나오면 후보 말풍선을 바꾼 뒤 잠시 보여 주고 고용한다
        private void View_Ticked(float dt)
        {
            if (m_negotiation == null)
            {
                return;
            }

            bool wasDone = m_negotiation.Done;
            m_negotiation.Tick(dt);
            m_view.SetMarker((float)m_negotiation.Marker);

            if (!m_negotiation.Done)
            {
                return;
            }

            if (!wasDone)
            {
                string key = m_negotiation.Outcome == NegotiationOutcome.Keep ? "clerk_nego_keep" : m_negotiation.Outcome == NegotiationOutcome.Down ? "clerk_nego_down" : "clerk_nego_up";
                m_view.SetBubbles(m_tables.Format("clerk_nego_offer", Wage(m_negotiation.BaseWage)), m_tables.Format(key, Wage(m_negotiation.Wage)));
            }

            m_resultTimer -= dt;

            if (m_resultTimer <= 0d)
            {
                Hire(m_negotiation.Wage);
            }
        }

        private void Bus_ClerkFired(Events.ClerkFired e)
        {
            if (e.Clerk.Bakery != m_bakery)
            {
                return;
            }

            if (e.Reason == FireReason.Unpaid)
            {
                m_view.ShowToast(m_tables.Format("clerk_fired_toast", e.Clerk.Name));
            }

            RefreshIfOpen();
        }
    }
}
