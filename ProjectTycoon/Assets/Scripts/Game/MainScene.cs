using UnityEngine;
using GameKit.UI;
using ZooTycoon.Core;
using ZooTycoon.UI;
using ZooTycoon.World;

namespace ZooTycoon.Game
{
    // 씬 조립: GameManager가 만든 서비스로 View·Presenter를 잇는다. 설계 09: 조작 HUD의 상호작용 버튼 → 사물 시트
    public sealed class MainScene : MonoBehaviour
    {
        private TopBarPresenter m_topBarPresenter;
        private ControlHudPresenter m_hudPresenter;
        private ObjectSheetPresenter m_sheetPresenter;

        private void Awake()
        {
            GameManager game = GameManager.Instance;
            game.Init();

            UIManager ui = UIManager.Instance;
            TopBarView topBarView = ui.Open<TopBarView>();
            ControlHudView hudView = ui.Open<ControlHudView>();
            ObjectSheetView sheetView = ui.Open<ObjectSheetView>();

            m_topBarPresenter = new TopBarPresenter(topBarView, game.State, game.Tables);
            m_hudPresenter = new ControlHudPresenter(hudView, game.Mall);
            m_sheetPresenter = new ObjectSheetPresenter(sheetView, game.Mall.Bakery, game.Tables);
            m_hudPresenter.SheetRequested += Hud_SheetRequested;

            WorldManager.Instance.Initialize(game.Tables, game.Mall);
        }

        private void OnDestroy()
        {
            if (m_hudPresenter != null)
            {
                m_hudPresenter.SheetRequested -= Hud_SheetRequested;
            }

            m_topBarPresenter?.Dispose();
            m_hudPresenter?.Dispose();
            m_sheetPresenter?.Dispose();
        }

        private void Hud_SheetRequested(Interactable target)
        {
            m_sheetPresenter.Show(target);
        }
    }
}
