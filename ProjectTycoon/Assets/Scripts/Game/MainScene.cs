using UnityEngine;
using GameKit.UI;
using ZooTycoon.UI;
using ZooTycoon.World;

namespace ZooTycoon.Game
{
    // Main 씬 전용 조립(설계 04 D1). 게임 상태·서비스는 GameManager가 갖고, 월드는 WorldManager(싱글턴)에 서비스를 넘긴다
    public sealed class MainScene : MonoBehaviour
    {
        private TopBarPresenter m_topBarPresenter;
        private GachaButtonPresenter m_gachaButtonPresenter;
        private GachaResultPresenter m_gachaResultPresenter;
        private FacilityPopupPresenter m_facilityPopupPresenter;

        private void Awake()
        {
            GameManager game = GameManager.Instance;
            game.Init();

            UIManager ui = UIManager.Instance;
            TopBarView topBarView = ui.Open<TopBarView>();
            GachaButtonView gachaButtonView = ui.Open<GachaButtonView>();
            GachaResultView gachaResultView = ui.GetView<GachaResultView>();
            FacilityButtonView facilityButtonView = ui.Open<FacilityButtonView>();
            FacilityPopupView facilityPopupView = ui.GetView<FacilityPopupView>();

            m_topBarPresenter = new TopBarPresenter(topBarView, game.State, game.ZooLevel, game.Income, game.Tables);
            WorldManager.Instance.Initialize(game.State, game.Income, game.Tables);
            m_gachaButtonPresenter = new GachaButtonPresenter(gachaButtonView, game.Gacha, game.State, game.Tables);
            m_gachaResultPresenter = new GachaResultPresenter(gachaResultView, game.Gacha, game.Tables);
            m_facilityPopupPresenter = new FacilityPopupPresenter(
                facilityButtonView, facilityPopupView, game.Facility, game.State, game.ZooLevel, game.Tables);
        }

        private void OnDestroy()
        {
            m_topBarPresenter?.Dispose();
            m_gachaButtonPresenter?.Dispose();
            m_gachaResultPresenter?.Dispose();
            m_facilityPopupPresenter?.Dispose();
        }
    }
}
