using UnityEngine;
using GameKit.UI;
using ZooTycoon.Core;
using ZooTycoon.UI;
using ZooTycoon.World;

namespace ZooTycoon.Game
{
    // 씬 조립: GameManager가 만든 서비스로 View·Presenter를 잇는다. 설계 09: 조작 HUD의 상호작용 버튼 → 사물 시트.
    // 설계 18: 편집 모드 Presenter의 알림(편집 시작/끝·그림자·팬·파기 시트)을 HUD·월드에 잇는다
    public sealed class MainScene : MonoBehaviour
    {
        private TopBarPresenter m_topBarPresenter;
        private ControlHudPresenter m_hudPresenter;
        private ObjectSheetPresenter m_sheetPresenter;
        private EditModePresenter m_editPresenter;
        private ClerkPresenter m_clerkPresenter;
        private ControlHudView m_hudView;

        private void Awake()
        {
            GameManager game = GameManager.Instance;
            game.Init();

            UIManager ui = UIManager.Instance;
            TopBarView topBarView = ui.Open<TopBarView>();
            m_hudView = ui.Open<ControlHudView>();
            ObjectSheetView sheetView = ui.Open<ObjectSheetView>();
            EditModeView editView = ui.Open<EditModeView>();
            ClerkPopupView clerkView = ui.Open<ClerkPopupView>();
            WorldManager world = WorldManager.Instance;

            m_topBarPresenter = new TopBarPresenter(topBarView, game.State, game.Bus, game.Tables);
            m_hudPresenter = new ControlHudPresenter(m_hudView, game.Mall, game.Bus);
            m_sheetPresenter = new ObjectSheetPresenter(sheetView, game.Mall.Bakery, game.Bus, game.Tables);
            m_editPresenter = new EditModePresenter(editView, game.Mall, game.Bus, game.Tables, world.OriginOf);
            m_clerkPresenter = new ClerkPresenter(clerkView, game.Mall, game.Bus, game.Tables);
            m_hudPresenter.SheetRequested += Hud_SheetRequested;
            m_editPresenter.SheetRequested += Hud_SheetRequested;
            m_editPresenter.EditingChanged += Edit_EditingChanged;
            m_editPresenter.GhostChanged += world.ShowGhost;
            m_editPresenter.GhostHidden += world.HideGhost;
            m_editPresenter.Panned += world.Pan;
            m_editPresenter.HeldChanged += world.SetHeld;

            world.Initialize(game.Tables, game.Mall, game.Bus);
        }

        private void OnDestroy()
        {
            if (m_hudPresenter != null)
            {
                m_hudPresenter.SheetRequested -= Hud_SheetRequested;
            }

            if (m_editPresenter != null)
            {
                m_editPresenter.SheetRequested -= Hud_SheetRequested;
                m_editPresenter.EditingChanged -= Edit_EditingChanged;
            }

            m_topBarPresenter?.Dispose();
            m_hudPresenter?.Dispose();
            m_sheetPresenter?.Dispose();
            m_editPresenter?.Dispose();
            m_clerkPresenter?.Dispose();
        }

        private void Hud_SheetRequested(Interactable target)
        {
            m_sheetPresenter.Show(target);
        }

        private void Edit_EditingChanged(bool editing)
        {
            m_hudView.SetEditing(editing);
            m_clerkPresenter.SetEditing(editing);
            WorldManager.Instance.SetEditing(editing);
        }
    }
}
