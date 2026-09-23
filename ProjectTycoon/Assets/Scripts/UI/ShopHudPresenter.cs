using System;
using System.Numerics;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 09: 조이스틱 → 웜뱃 걷기, 상호작용 버튼 → 대상의 manual 행동, 시트가 필요하면 SheetRequested.
    // v0.4: 버튼 아이콘은 행동 표(actions.json)의 icon. 대상이나 그 행동이 바뀔 때(TargetChanged)만 갱신한다
    public sealed class ShopHudPresenter : IDisposable
    {
        private readonly ShopHudView m_view;
        private readonly ShopSim m_shop;

        public event Action<Interactable> SheetRequested;

        public ShopHudPresenter(ShopHudView view, ShopSim shop)
        {
            m_view = view;
            m_shop = shop;
            m_view.JoystickMoved += View_JoystickMoved;
            m_view.InteractClicked += View_InteractClicked;
            m_shop.TargetChanged += Shop_TargetChanged;
            Refresh();
        }

        public void Dispose()
        {
            m_view.JoystickMoved -= View_JoystickMoved;
            m_view.InteractClicked -= View_InteractClicked;
            m_shop.TargetChanged -= Shop_TargetChanged;
        }

        // 할 수 있는 행동이 없으면 마지막 아이콘을 흐리게 둔다
        private void Refresh()
        {
            ActionRecord action = m_shop.TargetAction;
            m_view.SetInteract(action?.Icon, action != null);
        }

        private void View_JoystickMoved(Vector2 value)
        {
            m_shop.SetWombatInput(value);
        }

        private void View_InteractClicked()
        {
            if (!m_shop.TryInteract() && m_shop.TargetAction != null)
            {
                OnSheetRequested(m_shop.Target.Value);
            }
        }

        private void Shop_TargetChanged()
        {
            Refresh();
        }

        private void OnSheetRequested(Interactable target)
        {
            SheetRequested?.Invoke(target);
        }
    }
}
