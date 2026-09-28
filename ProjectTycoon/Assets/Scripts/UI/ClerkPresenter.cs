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
        // 결과 말풍선의 월급이 처음 값에서 결과 값으로 세어 가는 시간
        private const double k_CountSeconds = 0.4;

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
        // 해고할까 창에 올라 있는 점원
        private Clerk m_firing;
        private Negotiation m_negotiation;
        private double m_resultTimer;
        // 버튼 배지에 지금 떠 있는 것: 0 = 없음, -1 = 월급 모자람, 그 밖 = 딴짓 수
        private int m_badge;

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

        // 단위 없이 숫자만(코인 그림이 옆에 있다). 주기는 요약 줄의 월급날 게이지가 알린다
        private static string Wage(int wage)
        {
            return wage.ToString();
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
                        IconPath = null,
                        Badge = ClerkBadge.Empty,
                        BadgeText = m_tables.Text("clerk_empty"),
                        Name = slotName,
                        Slot = null,
                        Button = m_tables.Text("clerk_hire"),
                        Enabled = true,
                    });
                    continue;
                }

                ClerkBadge badge = clerk.Away ? ClerkBadge.Away : clerk.Idling ? ClerkBadge.Idling : ClerkBadge.Working;
                m_rows.Add(new ClerkPopupView.RowData
                {
                    IconPath = clerk.Look.Sprite,
                    Badge = badge,
                    BadgeText = m_tables.Text(badge == ClerkBadge.Away ? "clerk_away" : badge == ClerkBadge.Idling ? "clerk_idling" : "clerk_working"),
                    Name = clerk.Name,
                    Slot = slotName,
                    SkillHeader = m_tables.Text("clerk_col_skill"),
                    WageHeader = m_tables.Text("clerk_col_wage"),
                    Skill = clerk.Skill,
                    Wage = Wage(clerk.Wage),
                    Button = m_tables.Text("clerk_fire"),
                    Enabled = true,
                });
            }

            m_view.ShowList(m_tables.Text("clerk_title"), m_tables.Text("clerk_tab_bakery"),
                m_tables.Format("clerk_summary_count", m_bakery.Clerks.Count, m_slots.Count), m_tables.Text("clerk_summary_wage"), Wage(wageSum),
                m_rows, null, null, null, false);

            if (m_firing != null && m_bakery.ClerkOf(m_firing.Thing) == m_firing)
            {
                ShowFireAsk();
            }
            else
            {
                m_firing = null;
                m_view.HideAsk();
            }

            m_view.SetPayday(m_bakery.Clerks.Count > 0 ? m_tables.Text("clerk_payday") : null, (float)m_bakery.PaydayProgress, m_bakery.PaydayShort);
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
                    Badge = ClerkBadge.None,
                    Name = candidate.Name,
                    Slot = null,
                    SkillHeader = m_tables.Text("clerk_col_skill"),
                    WageHeader = m_tables.Text("clerk_col_base_wage"),
                    Skill = candidate.Skill,
                    Wage = Wage(m_bakery.WageFor(candidate, m_slot)),
                    Button = m_tables.Text("clerk_select"),
                    Enabled = true,
                });
            }

            double refresh = m_bakery.ClerkConfig.RefreshCost;
            m_view.ShowList(m_tables.Format("clerk_candidates_title", m_tables.Format("clerk_slot", m_tables.Text("kind_" + m_slot.Table.Id), IndexAmongKind(m_slot))), null, null, null, null,
                m_rows, null, m_tables.Text("clerk_refresh"), BigNumberFormatter.Format(refresh), m_bakery.Wombat.Worker.Wallet.Coins >= refresh);

            if (m_asking)
            {
                ShowAsk();
            }
        }

        private void ShowAsk()
        {
            m_asking = true;
            m_view.ShowAsk(m_candidate.Look.Sprite, m_tables.Text("clerk_ask"), m_candidate.Name,
                m_tables.Format("clerk_slot", m_tables.Text("kind_" + m_slot.Table.Id), IndexAmongKind(m_slot)),
                m_tables.Text("clerk_col_skill"), m_tables.Text("clerk_col_base_wage"), m_candidate.Skill, Wage(m_bakery.WageFor(m_candidate, m_slot)),
                m_tables.Text("clerk_hire_plain"), m_tables.Text("clerk_negotiate"), m_tables.Text("clerk_nego_risk"));
        }

        // 해고는 되돌릴 수 없어 한 번 묻는다(기획서 v0.14). 고용할까 창과 같은 틀: 왼쪽 취소 · 오른쪽 해고
        private void ShowFireAsk()
        {
            m_view.ShowAsk(m_firing.Look.Sprite, m_tables.Text("clerk_fire_ask"), m_firing.Name,
                m_tables.Format("clerk_slot", m_tables.Text("kind_" + m_firing.Thing.Table.Id), IndexAmongKind(m_firing.Thing)),
                m_tables.Text("clerk_col_skill"), m_tables.Text("clerk_col_wage"), m_firing.Skill, Wage(m_firing.Wage),
                m_tables.Text("clerk_fire_cancel"), m_tables.Text("clerk_fire"), m_tables.Text("clerk_fire_hint"));
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
            m_firing = null;
            m_view.HideAsk();
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
            m_view.PlayRows();
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
                m_view.PlayRows();
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
                    m_firing = clerk;
                    ShowFireAsk();
                    return;
                }

                m_slot = slot;
                ShowCandidates();
                m_view.PlayRows();
                return;
            }

            m_candidate = m_bakery.Candidates[index];
            ShowAsk();
        }

        private void View_FootClicked()
        {
            if (m_mode == Mode.Candidates && m_bakery.TryRefreshCandidates())
            {
                m_view.PlayRows();
            }
        }

        private void View_AskPlainClicked()
        {
            if (m_firing != null)
            {
                View_AskCancelClicked();
                return;
            }

            Hire(m_bakery.WageFor(m_candidate, m_slot));
        }

        private void View_AskNegotiateClicked()
        {
            if (m_firing != null)
            {
                Clerk clerk = m_firing;
                m_firing = null;
                m_view.HideAsk();
                m_bakery.Fire(clerk, FireReason.Fired);
                return;
            }

            m_asking = false;
            m_negotiation = m_bakery.Negotiate(m_candidate, m_slot);
            m_resultTimer = k_ResultSeconds;
            m_view.ShowNegotiation(m_tables.Text("clerk_nego_title"), m_tables.Text("clerk_nego_hint"), m_tables.Text("clerk_nego_now"),
                m_candidate.Look.SideIdleSheet ?? m_candidate.Look.Sprite,
                m_tables.Format("clerk_nego_offer", Wage(m_negotiation.BaseWage)));
        }

        private void View_AskCancelClicked()
        {
            m_asking = false;
            m_firing = null;
            m_view.HideAsk();
        }

        private void View_Tapped()
        {
            if (m_negotiation == null || m_negotiation.Done)
            {
                return;
            }

            m_negotiation.Stop();
            ShowResult();
        }

        // 표시를 움직이고, 결과가 나오면 후보 말풍선을 바꾼 뒤 잠시 보여 주고 고용한다
        private void View_Ticked(float dt)
        {
            RefreshBadge();

            if (!m_view.IsVisible)
            {
                return;
            }

            if (m_negotiation == null)
            {
                if (m_mode == Mode.Slots && m_bakery.Clerks.Count > 0)
                {
                    m_view.SetPayday(m_tables.Text("clerk_payday"), (float)m_bakery.PaydayProgress, m_bakery.PaydayShort);
                }

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
                ShowResult();
            }

            m_resultTimer -= dt;
            double counted = Math.Min(1d, (k_ResultSeconds - m_resultTimer) / k_CountSeconds);
            ShowResultBubble((int)Math.Round(m_negotiation.BaseWage + (m_negotiation.Wage - m_negotiation.BaseWage) * counted));

            if (m_resultTimer <= 0d)
            {
                Hire(m_negotiation.Wage);
            }
        }

        // 월급 모자람이 딴짓 수보다 먼저다(해고는 되돌릴 수 없다)
        private void RefreshBadge()
        {
            int badge = 0;

            if (m_bakery.PaydayShort)
            {
                badge = -1;
            }
            else
            {
                foreach (Clerk clerk in m_bakery.Clerks)
                {
                    if (clerk.Idling || clerk.Away)
                    {
                        badge++;
                    }
                }
            }

            if (badge == m_badge)
            {
                return;
            }

            m_badge = badge;
            m_view.SetButtonBadge(badge == 0 ? null : badge < 0 ? m_tables.Text("clerk_badge_short") : badge.ToString(), badge < 0);
        }

        // 결과가 나온 순간(탭 또는 시간 초과) 후보 말풍선을 결과로 바꾼다
        private void ShowResult()
        {
            ShowResultBubble(m_negotiation.BaseWage);
            m_view.PlayResult(m_negotiation.Outcome != NegotiationOutcome.Up, m_tables.Text("clerk_nego_result_" + OutcomeKey()));
        }

        private string OutcomeKey()
        {
            return m_negotiation.Outcome == NegotiationOutcome.Keep ? "keep" : m_negotiation.Outcome == NegotiationOutcome.Down ? "down" : "up";
        }

        private void ShowResultBubble(int wage)
        {
            m_view.SetBubble(m_tables.Format("clerk_nego_" + OutcomeKey(), Wage(wage)));
        }

        private void Bus_ClerkFired(Events.ClerkFired e)
        {
            if (e.Clerk.Bakery != m_bakery)
            {
                return;
            }

            if (e.Reason == FireReason.Unpaid)
            {
                m_view.ShowToast(m_tables.Format("clerk_fired_toast", e.Clerk.Name), m_tables.Text("clerk_fired_reason"));
            }

            if (m_view.IsVisible && m_mode == Mode.Slots && m_negotiation == null && m_slots.Contains(e.Clerk.Thing))
            {
                m_view.PlayRowOut(m_slots.IndexOf(e.Clerk.Thing));
            }

            RefreshIfOpen();
        }
    }
}
