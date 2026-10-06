using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 18: 편집 모드. 들어가면 웜뱃은 서고 조이스틱이 숨는다(EditingChanged로 HUD·월드가 따라온다).
    // 카드를 탭하면 화면 가운데 근처 빈 자리에 사기(보관함에 있으면 꺼내기, 설계 20), 놓인 사물을 끌면 옮기기, 패널 위에 놓으면 보관, 빈 곳을 끌면 카메라 팬, 팔 수 있는 흙을 누르면 파기 시트.
    // 규칙(놓을 수 있나·값)은 전부 곳(WombatArea.Placement)이 정하고 여기는 손가락을 그 호출로 바꾼다
    // 설계 49: 낚시터의 편집 모드는 낚시판 보기 — 말뚝을 누르면 대 사기 시트(잠긴 말뚝은 열기), 대를 끌어 다른 말뚝에 놓으면 옮기기 · 바꾸기 · 합치기(FishingArea.TryMoveRod)
    public sealed class EditModePresenter : IDisposable
    {
        // 낚시판 보기: 손가락이 말뚝을 잡는 반지름과, 대 그림(말뚝 위로 솟는다)까지 잡히게 올려 보는 높이(유닛)
        private const float k_StakeGrabRadius = 1.1f;
        private const float k_StakeGrabLift = 0.4f;
        // 누른 자리에서 이만큼(유닛) 안 벗어났으면 손가락이 밀린 누르기로 본다
        private const float k_TapSlack = 0.5f;

        private readonly EditModeView m_view;
        private readonly Mall m_mall;
        private readonly TableSet m_tables;
        private readonly Func<WombatArea, Vector2> m_originOf;
        private readonly IDisposable[] m_subscriptions;
        private readonly List<EditModeView.CardData> m_cards = new List<EditModeView.CardData>();

        private bool m_editing;
        // 끌고 있는 사물
        private IPlaced m_dragThing;
        // 빈 곳을 누른 뒤 팬 기준점(월드). 누른 채 움직이지 않고 떼면 파기 시트
        private bool m_panning;
        private bool m_moved;
        private Vector2 m_panFrom;
        private DigInteractable m_pressedDig;
        // 낚시판 보기에서 누른 말뚝
        private StakeInteractable m_pressedStake;
        private Vector2 m_pressedAt;
        private float m_travel;

        public bool Editing => m_editing;

        public event Action<bool> EditingChanged;
        public event Action<IPlacedKind, Vector2, bool> GhostChanged;
        public event Action GhostHidden;
        public event Action<Vector2> Panned;
        public event Action<Interactable> SheetRequested;
        // 잡은 사물(null = 놓음)
        public event Action<IPlaced> HeldChanged;
        // 낚시판 보기: 끄는 대의 말뚝과 손가락 자리(곳 좌표) · 놓았다
        public event Action<StakeInteractable, Vector2> RodDragged;
        public event Action RodDropped;

        public EditModePresenter(EditModeView view, Mall mall, EventBus bus, TableSet tables, Func<WombatArea, Vector2> originOf)
        {
            m_view = view;
            m_mall = mall;
            m_tables = tables;
            m_originOf = originOf;
            m_view.EditClicked += View_EditClicked;
            m_view.DoneClicked += View_DoneClicked;
            m_view.CardClicked += View_CardClicked;
            m_view.WorldPointerDown += View_WorldPointerDown;
            m_view.WorldPointerMoved += View_WorldPointerMoved;
            m_view.WorldPointerUp += View_WorldPointerUp;
            m_view.SetTexts(tables.Text("edit_store_hint"), tables.Text("edit_done"));
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.CoinsChanged>(_ => RefreshCards()),
                bus.Subscribe<Events.LayoutChanged>(_ => RefreshCards()),
                bus.Subscribe<Events.StarsChanged>(_ => RefreshCards()),
                bus.Subscribe<Events.AreaChanged>(_ =>
                {
                    RefreshCards();
                    m_view.SetEditable(Area.Editable);
                }),
            };
        }

        public void Dispose()
        {
            m_view.EditClicked -= View_EditClicked;
            m_view.DoneClicked -= View_DoneClicked;
            m_view.CardClicked -= View_CardClicked;
            m_view.WorldPointerDown -= View_WorldPointerDown;
            m_view.WorldPointerMoved -= View_WorldPointerMoved;
            m_view.WorldPointerUp -= View_WorldPointerUp;

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        private WombatArea Area => m_mall.Active;

        private void SetEditing(bool editing)
        {
            m_editing = editing;
            m_mall.Wombat.SetInput(Vector2.Zero);
            m_view.SetEditing(editing);
            EndDrag();

            if (editing)
            {
                RefreshCards();
            }

            EditingChanged?.Invoke(editing);
        }

        // 카드: 종류마다 하나. 보관함에 있으면 「보관 n」(무료), 아니면 값. 최대면 MAX, 코인이 모자라면 흐리게
        private void RefreshCards()
        {
            if (!m_editing)
            {
                return;
            }

            m_cards.Clear();
            double coins = m_mall.Wombat.Worker.Wallet.Coins;

            foreach (IPlacedKind kind in Area.ShopKinds)
            {
                int stored = Area.StoredCount(kind.Id);
                bool maxed = Area.IsMaxed(kind.Id);
                int star = Area.StarNeeded(kind.Id);
                double price = Area.PriceOf(kind.Id);
                string sub = stored > 0 ? m_tables.Format("edit_stored", stored) : maxed ? m_tables.Text("row_max")
                    : star >= 0 ? m_tables.Format("row_star_needed", star) : BigNumberFormatter.Format(price);
                m_cards.Add(new EditModeView.CardData
                {
                    KindId = kind.Id,
                    IconPath = kind.Icon,
                    Label = m_tables.Text("kind_" + kind.Id),
                    Sub = sub,
                    Coin = stored == 0 && !maxed && star < 0,
                    Locked = stored == 0 && !maxed && star >= 0,
                    Enabled = stored > 0 || !maxed && star < 0 && coins >= price,
                });
            }

            m_view.SetCards(m_cards);
            // 설계 27: 놓을 것이 없는 곳(농장)에서는 보관 안내 대신 파기 안내 · 설계 49: 낚시터는 합치기 안내
            m_view.SetStoreHint(m_tables.Text(Area is FishingArea ? "edit_fishing_hint" : m_cards.Count == 0 ? "edit_dig_hint" : "edit_store_hint"));
        }

        private static StakeInteractable StakeUnder(FishingArea fishing, Vector2 at)
        {
            return fishing.StakeAt(at - new Vector2(0f, k_StakeGrabLift), k_StakeGrabRadius);
        }

        private Vector2 ToArea(Vector2 world)
        {
            return world - m_originOf(Area);
        }

        private void View_EditClicked()
        {
            SetEditing(true);
        }

        private void View_DoneClicked()
        {
            SetEditing(false);
        }

        // 카드 탭: 화면 가운데에서 가장 가까운 빈 자리에 놓는다(자리가 없으면 아무 일 없음)
        private void View_CardClicked(string kindId, Vector2 world)
        {
            if (!Area.TryFindSpot(kindId, ToArea(world), out Vector2 at))
            {
                return;
            }

            if (Area.StoredCount(kindId) > 0)
            {
                Area.TryPlaceStored(kindId, at);
            }
            else
            {
                Area.TryBuy(kindId, at);
            }
        }

        private void ShowGhost(Vector2 world)
        {
            Vector2 at = Area.Snap(ToArea(world));
            GhostChanged?.Invoke(m_dragThing.Kind, at, Area.CanMove(m_dragThing, at) == PlacementCheck.Ok);
        }

        private void View_WorldPointerDown(Vector2 world)
        {
            Vector2 at = ToArea(world);
            m_moved = false;

            if (Area is FishingArea fishing)
            {
                m_pressedStake = StakeUnder(fishing, at);
                m_pressedAt = at;
                m_travel = 0f;
                return;
            }

            m_dragThing = Area.ThingAt(at);
            m_pressedDig = null;
            m_panning = false;

            if (m_dragThing != null)
            {
                HeldChanged?.Invoke(m_dragThing);
                return;
            }

            m_pressedDig = Area.DigAt(at);
            m_panning = true;
            m_panFrom = world;
        }

        private void View_WorldPointerMoved(Vector2 world)
        {
            m_moved = true;

            if (m_pressedStake != null)
            {
                Vector2 at = ToArea(world);
                m_travel = Math.Max(m_travel, Vector2.Distance(at, m_pressedAt));

                // 월척을 붙잡은 대는 못 끈다
                if (m_pressedStake.Rod != null && m_pressedStake.Hooked == null)
                {
                    RodDragged?.Invoke(m_pressedStake, at);
                }

                return;
            }

            if (m_dragThing != null)
            {
                ShowGhost(world);
                return;
            }

            if (m_panning)
            {
                // 카메라가 delta만큼 가면 손가락 밑 월드 점은 다시 m_panFrom이 된다
                Panned?.Invoke(m_panFrom - world);
            }
        }

        // 낚시판 보기: 끌었으면 놓은 자리의 말뚝으로(옮기기 · 바꾸기 · 합치기), 눌렀다 뗐으면 잠긴 말뚝은 열고 열린 말뚝은 대 사기 시트
        private void ReleaseStake(FishingArea fishing, StakeInteractable pressed, Vector2 at)
        {
            StakeInteractable target = m_moved ? StakeUnder(fishing, at) : pressed;

            if (target != pressed)
            {
                if (target != null)
                {
                    fishing.TryMoveRod(pressed, target);
                }

                return;
            }

            // 제 말뚝 위에서 뗐다: 손가락이 조금 밀렸어도 누른 것으로 본다(멀리 끌었다가 되돌아온 것은 끌기 취소)
            if (m_travel > k_TapSlack)
            {
                return;
            }

            if (!pressed.Open)
            {
                fishing.TryOpenStake(pressed);
            }
            else if (pressed.Hooked == null)
            {
                SheetRequested?.Invoke(pressed);
            }
        }

        private void View_WorldPointerUp(Vector2 world, bool overPanel)
        {
            if (m_pressedStake != null)
            {
                StakeInteractable pressed = m_pressedStake;
                EndDrag();
                ReleaseStake(pressed.Fishing, pressed, ToArea(world));
                return;
            }

            if (m_dragThing != null)
            {
                if (overPanel)
                {
                    Area.TryStore(m_dragThing);
                }
                else if (m_moved)
                {
                    Area.TryMove(m_dragThing, Area.Snap(ToArea(world)));
                }
            }
            else if (!m_moved && m_pressedDig != null)
            {
                SheetRequested?.Invoke(m_pressedDig);
            }

            EndDrag();
        }

        private void EndDrag()
        {
            bool wasHolding = m_dragThing != null;
            m_dragThing = null;
            m_panning = false;
            m_pressedDig = null;
            m_pressedStake = null;
            RodDropped?.Invoke();
            GhostHidden?.Invoke();

            if (wasHolding)
            {
                HeldChanged?.Invoke(null);
            }
        }
    }
}
