using System;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 08: 가게 화면일 때만 HUD를 보인다. 뒤로 = 지상 복귀, 업그레이드 = 업그레이드 팝업(v0.5)
    public sealed class ShopHudPresenter : IDisposable
    {
        private const string k_TitleKey = "shop_title_bakery";
        private const string k_BackKey = "shop_back";
        private const string k_UpgradeKey = "shop_upgrade";

        private readonly ShopHudView m_view;
        private readonly Navigation m_navigation;
        private readonly UpgradePopupPresenter m_upgradePopup;

        public ShopHudPresenter(ShopHudView view, Navigation navigation, UpgradePopupPresenter upgradePopup, GameTables tables)
        {
            m_view = view;
            m_navigation = navigation;
            m_upgradePopup = upgradePopup;

            m_view.SetTexts(tables.Strings.Get(k_TitleKey), tables.Strings.Get(k_BackKey), tables.Strings.Get(k_UpgradeKey));
            m_view.BackClicked += View_BackClicked;
            m_view.UpgradeClicked += View_UpgradeClicked;
            m_navigation.Changed += Navigation_Changed;

            Refresh();
        }

        public void Dispose()
        {
            m_view.BackClicked -= View_BackClicked;
            m_view.UpgradeClicked -= View_UpgradeClicked;
            m_navigation.Changed -= Navigation_Changed;
        }

        private void View_BackClicked()
        {
            m_navigation.ExitShop();
        }

        private void View_UpgradeClicked()
        {
            m_upgradePopup.Show();
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
