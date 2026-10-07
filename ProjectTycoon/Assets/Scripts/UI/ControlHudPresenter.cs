using System;
using System.Numerics;
using GameKit.Events;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 09: 조이스틱 → 웜뱃 걷기, 상호작용 버튼 → 대상의 manual 행동, 시트가 필요하면 SheetRequested.
    // v0.4: 버튼 아이콘은 행동 표(ActionTable)의 icon. 대상이나 그 행동이 바뀔 때(TargetChanged)만 갱신한다.
    // 설계 11: 웜뱃이 있는 곳(Mall.Active, 빵집·광장)의 대상을 보고, 곳을 옮기면 검은 화면에서 페이드 인
    public sealed class ControlHudPresenter : IDisposable
    {
        private readonly ControlHudView m_view;
        private readonly Mall m_mall;
        private readonly IDisposable m_target;
        private readonly IDisposable m_area;
        private bool m_reelPress;

        public event Action<Interactable> SheetRequested;

        public ControlHudPresenter(ControlHudView view, Mall mall, EventBus bus)
        {
            m_view = view;
            m_mall = mall;
            m_view.JoystickMoved += View_JoystickMoved;
            m_view.InteractClicked += View_InteractClicked;
            m_view.InteractPressed += View_InteractPressed;
            m_view.InteractReleased += View_InteractReleased;
            m_target = bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged);
            m_area = bus.Subscribe<Events.AreaChanged>(Bus_AreaChanged);
            Refresh();
        }

        public void Dispose()
        {
            m_view.JoystickMoved -= View_JoystickMoved;
            m_view.InteractClicked -= View_InteractClicked;
            m_view.InteractPressed -= View_InteractPressed;
            m_view.InteractReleased -= View_InteractReleased;
            m_target.Dispose();
            m_area.Dispose();
        }

        // 할 수 있는 행동이 없으면 마지막 아이콘을 흐리게 둔다
        private void Refresh()
        {
            ActionTable action = m_mall.Active.TargetAction;
            m_view.SetInteract(action?.Icon, action != null);
        }

        private void View_JoystickMoved(Vector2 value)
        {
            m_mall.Wombat.SetInput(value);
        }

        private void View_InteractClicked()
        {
            // 줄다리기 동안 쥐고 있던 손을 떼는 것은 클릭이 아니다(낚은 · 끊긴 직후 그 뗌으로 다시 던지지 않게, 사용자 2026-10-07)
            if (m_reelPress)
            {
                m_reelPress = false;
                return;
            }

            WombatArea area = m_mall.Active;

            if (!area.TryInteract() && area.TargetAction != null)
            {
                OnSheetRequested(area.Target);
            }
        }

        // 설계 52: 버튼을 누르고 있는 동안이 줄다리기의 감기(Core가 매 틱 읽는다)
        private void View_InteractPressed()
        {
            m_reelPress = m_mall.Active is FishingArea fishing && fishing.Phase == FishingPhase.Tug;
            m_mall.Wombat.SetHolding(true);
        }

        private void View_InteractReleased()
        {
            m_mall.Wombat.SetHolding(false);
        }

        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area == m_mall.Active)
            {
                Refresh();
            }
        }

        private void Bus_AreaChanged(Events.AreaChanged e)
        {
            m_mall.Wombat.SetHolding(false);
            Refresh();
            m_view.FadeIn();
        }

        private void OnSheetRequested(Interactable target)
        {
            SheetRequested?.Invoke(target);
        }
    }
}
