using System;
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
        private InventoryPresenter m_inventoryPresenter;
        private StatuePresenter m_statuePresenter;
        private EvaluationPresenter m_evaluationPresenter;
        private RelicCartPresenter m_relicPresenter;
        private RelicPresenter m_relicsPresenter;
        private LocationPresenter m_locationPresenter;
        private OfflinePresenter m_offlinePresenter;
        private QuestPresenter m_questPresenter;
        private MissionPresenter m_missionPresenter;
        private ControlHudView m_hudView;
        private EditModeView m_editView;
        private ClerkPopupView m_clerkView;
        private InventoryView m_inventoryView;
        private RelicView m_relicsView;
        private MissionView m_missionView;
        private IDisposable m_menuPointed;

        private void Awake()
        {
            GameManager game = GameManager.Instance;
            game.Init();

            UIManager ui = UIManager.Instance;
            TopBarView topBarView = ui.Open<TopBarView>();
            m_hudView = ui.Open<ControlHudView>();
            // 설계 42: 곳 이름 팻말은 HUD와 같은 층(팝업 아래)
            LocationView locationView = ui.Open<LocationView>();
            // 유물 버튼은 메뉴 버튼 줄이라 팝업들보다 아래에 깐다
            RelicView relicsView = ui.Open<RelicView>();
            // 설계 54: 오늘의 일 버튼도 메뉴 줄(유물 아래)
            m_missionView = ui.Open<MissionView>();
            ObjectSheetView sheetView = ui.Open<ObjectSheetView>();
            EditModeView editView = ui.Open<EditModeView>();
            ClerkPopupView clerkView = ui.Open<ClerkPopupView>();
            InventoryView inventoryView = ui.Open<InventoryView>();
            StatueView statueView = ui.Open<StatueView>();
            // 설계 40: 평가 팝업 · 소식지
            EvaluationView evaluationView = ui.Open<EvaluationView>();
            RelicCartView relicView = ui.Open<RelicCartView>();
            // 뽑기 결과는 뽑기 메인 위에 뜬다(설계 36)
            RelicDrawResultView relicResultView = ui.Open<RelicDrawResultView>();
            // 설계 43: 돌아왔을 때 팝업은 맨 위
            OfflineView offlineView = ui.Open<OfflineView>();
            WorldManager world = WorldManager.Instance;

            m_topBarPresenter = new TopBarPresenter(topBarView, game.State, game.Bus, game.Tables, game.Mall.Plaza.Merchant, game.Mall);
            m_hudPresenter = new ControlHudPresenter(m_hudView, game.Mall, game.Bus);
            m_sheetPresenter = new ObjectSheetPresenter(sheetView, game.Mall.Bakery, game.Bus, game.Tables);
            m_editPresenter = new EditModePresenter(editView, game.Mall, game.Bus, game.Tables, world.OriginOf);
            m_clerkPresenter = new ClerkPresenter(clerkView, game.Mall, game.Bus, game.Tables);
            m_inventoryPresenter = new InventoryPresenter(inventoryView, game.State, game.Bus, game.Tables);
            m_statuePresenter = new StatuePresenter(statueView, game.State, game.Bus, game.Tables);
            m_evaluationPresenter = new EvaluationPresenter(evaluationView, game.State, game.Bus, game.Tables);
            m_relicsPresenter = new RelicPresenter(relicsView, game.State, game.Bus, game.Tables);
            m_locationPresenter = new LocationPresenter(locationView, game.Mall, game.Bus, game.Tables);
            m_offlinePresenter = new OfflinePresenter(offlineView, game.Bus, game.Tables);
            m_relicPresenter = new RelicCartPresenter(relicView, relicResultView, game.State, game.Bus, game.Tables, m_relicsPresenter.Open);
            m_questPresenter = new QuestPresenter(topBarView, game.Mall.Quests, game.Bus, game.Tables);
            m_missionPresenter = new MissionPresenter(m_missionView, game.Mall.Missions, game.Bus, game.Tables);
            m_editView = editView;
            m_clerkView = clerkView;
            m_inventoryView = inventoryView;
            m_relicsView = relicsView;
            m_menuPointed = game.Bus.Subscribe<Events.MenuPointed>(Bus_MenuPointed);
            m_hudPresenter.SheetRequested += Hud_SheetRequested;
            m_editPresenter.SheetRequested += Hud_SheetRequested;
            m_editPresenter.EditingChanged += Edit_EditingChanged;
            m_editPresenter.GhostChanged += world.ShowGhost;
            m_editPresenter.GhostHidden += world.HideGhost;
            m_editPresenter.Panned += world.Pan;
            m_editPresenter.HeldChanged += world.SetHeld;

            world.Initialize(game.Tables, game.Mall, game.Bus);
            game.PublishPendingOffline();
#if UNITY_EDITOR
            gameObject.AddComponent<CheatConsole>().Bind(game);
#endif
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
            m_inventoryPresenter?.Dispose();
            m_statuePresenter?.Dispose();
            m_evaluationPresenter?.Dispose();
            m_relicPresenter?.Dispose();
            m_relicsPresenter?.Dispose();
            m_locationPresenter?.Dispose();
            m_offlinePresenter?.Dispose();
            m_questPresenter?.Dispose();
            m_missionPresenter?.Dispose();
            m_menuPointed?.Dispose();
        }

        private void Hud_SheetRequested(Interactable target)
        {
            m_sheetPresenter.Show(target);
        }

        private void Edit_EditingChanged(bool editing)
        {
            m_hudView.SetEditing(editing);
            m_clerkPresenter.SetEditing(editing);
            m_inventoryPresenter.SetEditing(editing);
            m_statuePresenter.SetEditing(editing);
            m_evaluationPresenter.SetEditing(editing);
            m_relicPresenter.SetEditing(editing);
            m_relicsPresenter.SetEditing(editing);
            m_missionPresenter.SetEditing(editing);
            WorldManager.Instance.SetEditing(editing);
        }

        // 설계 54: 할 일 알약의 「가기」가 메뉴를 가리킨다 — 그 메뉴 버튼이 톡톡
        private void Bus_MenuPointed(Events.MenuPointed e)
        {
            switch (e.Menu)
            {
                case QuestTable.k_MenuEdit:
                    m_editView.PointButton();
                    break;
                case QuestTable.k_MenuClerk:
                    m_clerkView.PointButton();
                    break;
                case QuestTable.k_MenuStorage:
                    m_inventoryView.PointButton();
                    break;
                case QuestTable.k_MenuRelic:
                    m_relicsView.PointButton();
                    break;
                case QuestTable.k_MenuMission:
                    m_missionView.PointButton();
                    break;
            }
        }
    }
}
