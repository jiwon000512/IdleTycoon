using UnityEngine;
using GameKit.UI;
using ZooTycoon.Core;
using ZooTycoon.UI;
using ZooTycoon.World;

namespace ZooTycoon.Game
{
    // Main 씬 전용 조립(설계 04 D1). 게임 상태·서비스는 GameManager가 갖고, 월드는 WorldManager(싱글턴)에 서비스를 넘긴다
    public sealed class MainScene : MonoBehaviour
    {
        private TopBarPresenter m_topBarPresenter;
        private ShopHudPresenter m_shopHudPresenter;
        private BakePopupPresenter m_bakePopupPresenter;
        private UpgradePopupPresenter m_upgradePopupPresenter;

        private void Awake()
        {
            GameManager game = GameManager.Instance;
            game.Init();

            UIManager ui = UIManager.Instance;
            TopBarView topBarView = ui.Open<TopBarView>();
            ShopHudView shopHudView = ui.Open<ShopHudView>();
            BakePopupView bakePopupView = ui.Open<BakePopupView>();
            UpgradePopupView upgradePopupView = ui.Open<UpgradePopupView>();

            m_topBarPresenter = new TopBarPresenter(topBarView, game.State, game.ZooLevel, game.Income, game.Tables);
            m_bakePopupPresenter = new BakePopupPresenter(bakePopupView, game.Shop, game.Tables);
            m_upgradePopupPresenter = new UpgradePopupPresenter(upgradePopupView, game.Shop, game.State, game.Tables);
            m_shopHudPresenter = new ShopHudPresenter(shopHudView, game.Navigation, m_upgradePopupPresenter, game.Tables);

            WorldManager world = WorldManager.Instance;
            world.Initialize(game.State, game.Income, game.Tables, game.Navigation, game.Shop);
            world.OvenTapped += World_OvenTapped;
        }

        private void OnDestroy()
        {
            m_topBarPresenter?.Dispose();
            m_shopHudPresenter?.Dispose();
            m_bakePopupPresenter?.Dispose();
            m_upgradePopupPresenter?.Dispose();

            if (WorldManager.HasInstance)
            {
                WorldManager.Instance.OvenTapped -= World_OvenTapped;
            }
        }

        // 설계 08: World와 UI를 잇는 자리. 빈 오븐이고 웜뱃이 계산대에 있을 때만 굽기 팝업을 연다(v0.6 심부름은 한 번에 하나)
        private void World_OvenTapped(int oven)
        {
            ShopSim shop = GameManager.Instance.Shop;

            if (shop.Ovens[oven].IsEmpty && shop.WombatAtCounter)
            {
                m_bakePopupPresenter.Show(oven);
            }
        }
    }
}
