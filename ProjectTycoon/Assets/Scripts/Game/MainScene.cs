using UnityEngine;
using GameKit.UI;
using ZooTycoon.Core;
using ZooTycoon.UI;
using ZooTycoon.World;

namespace ZooTycoon.Game
{
    // 씬 조립: GameManager가 만든 서비스로 View·Presenter를 잇는다. 설계 09: 가게 HUD의 상호작용 버튼 → 사물 시트
    public sealed class MainScene : MonoBehaviour
    {
        private TopBarPresenter m_topBarPresenter;
        private ShopHudPresenter m_shopHudPresenter;
        private ObjectSheetPresenter m_sheetPresenter;

        private void Awake()
        {
            GameManager game = GameManager.Instance;
            game.Init();

            UIManager ui = UIManager.Instance;
            TopBarView topBarView = ui.Open<TopBarView>();
            ShopHudView shopHudView = ui.Open<ShopHudView>();
            ObjectSheetView sheetView = ui.Open<ObjectSheetView>();

            m_topBarPresenter = new TopBarPresenter(topBarView, game.State, game.Tables);
            m_shopHudPresenter = new ShopHudPresenter(shopHudView, game.Shop, game.Tables);
            m_sheetPresenter = new ObjectSheetPresenter(sheetView, game.Shop, game.State, game.Tables);
            m_shopHudPresenter.SheetRequested += ShopHud_SheetRequested;

            WorldManager.Instance.Initialize(game.Tables, game.Shop);
        }

        private void OnDestroy()
        {
            if (m_shopHudPresenter != null)
            {
                m_shopHudPresenter.SheetRequested -= ShopHud_SheetRequested;
            }

            m_topBarPresenter?.Dispose();
            m_shopHudPresenter?.Dispose();
            m_sheetPresenter?.Dispose();
        }

        private void ShopHud_SheetRequested(Interactable target)
        {
            m_sheetPresenter.Show(target);
        }
    }
}
