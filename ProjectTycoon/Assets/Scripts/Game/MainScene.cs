using UnityEngine;
using GameKit.UI;
using ZooTycoon.Core;
using ZooTycoon.UI;
using ZooTycoon.World;

namespace ZooTycoon.Game
{
    // 씬 조립: GameManager가 만든 서비스로 View·Presenter를 잇는다. 사물 터치 기획: 월드의 사물 탭 → 사물 시트
    public sealed class MainScene : MonoBehaviour
    {
        private TopBarPresenter m_topBarPresenter;
        private ShopHudPresenter m_shopHudPresenter;
        private ObjectSheetPresenter m_sheetPresenter;
        private Navigation m_navigation;

        private void Awake()
        {
            GameManager game = GameManager.Instance;
            game.Init();
            m_navigation = game.Navigation;

            UIManager ui = UIManager.Instance;
            TopBarView topBarView = ui.Open<TopBarView>();
            ShopHudView shopHudView = ui.Open<ShopHudView>();
            ObjectSheetView sheetView = ui.Open<ObjectSheetView>();

            m_topBarPresenter = new TopBarPresenter(topBarView, game.State, game.Tables);
            m_shopHudPresenter = new ShopHudPresenter(shopHudView, game.Navigation, game.Tables);
            m_sheetPresenter = new ObjectSheetPresenter(sheetView, game.Shop, game.State, game.Tables);

            WorldManager world = WorldManager.Instance;
            world.Initialize(game.Tables, game.Navigation, game.Shop);
            world.TargetTapped += World_TargetTapped;
            m_navigation.Changed += Navigation_Changed;
        }

        private void OnDestroy()
        {
            m_topBarPresenter?.Dispose();
            m_shopHudPresenter?.Dispose();
            m_sheetPresenter?.Dispose();

            if (m_navigation != null)
            {
                m_navigation.Changed -= Navigation_Changed;
            }

            if (WorldManager.HasInstance)
            {
                WorldManager.Instance.TargetTapped -= World_TargetTapped;
            }
        }

        // World의 대상 종류를 UI의 종류로(같은 순서). UI는 World를 참조하지 않는다
        private void World_TargetTapped(ShopTarget target)
        {
            m_sheetPresenter.Show((SheetTargetKind)(int)target.Kind, target.Index);
        }

        private void Navigation_Changed()
        {
            if (m_navigation.Current != GameScreen.Shop)
            {
                m_sheetPresenter.Hide();
            }
        }
    }
}
