using System;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 08: 가게 화면일 때만 HUD를 보인다. 뒤로 = 지상 복귀
    public sealed class ShopHudPresenter : IDisposable
    {
        private const string k_TitleKey = "shop_title_bakery";
        private const string k_BackKey = "shop_back";

        private readonly ShopHudView m_view;
        private readonly Navigation m_navigation;

        public ShopHudPresenter(ShopHudView view, Navigation navigation, GameTables tables)
        {
            m_view = view;
            m_navigation = navigation;
            m_view.SetTexts(tables.Strings.Get(k_TitleKey), tables.Strings.Get(k_BackKey));
            m_view.BackClicked += View_BackClicked;
            m_navigation.Changed += Navigation_Changed;
            Refresh();
        }

        public void Dispose()
        {
            m_view.BackClicked -= View_BackClicked;
            m_navigation.Changed -= Navigation_Changed;
        }

        private void View_BackClicked()
        {
            m_navigation.ExitShop();
        }

        private void Navigation_Changed()
        {
            Refresh();
        }

        private void Refresh()
        {
            m_view.SetVisible(m_navigation.Current == GameScreen.Shop);
        }
    }
}
