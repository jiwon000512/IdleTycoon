using UnityEngine;
using GameKit.UI;
using ZooTycoon.UI;
using ZooTycoon.World;

namespace ZooTycoon.Game
{
    // Main 씬 전용 조립(설계 04 D1). 게임 상태·서비스는 GameManager가 갖는다
    public sealed class MainScene : MonoBehaviour
    {
        [SerializeField] private WorldView m_worldView;

        private TopBarPresenter m_topBarPresenter;
        private WorldPresenter m_worldPresenter;
        private GachaButtonPresenter m_gachaButtonPresenter;
        private GachaResultPresenter m_gachaResultPresenter;

        private void Awake()
        {
            GameManager game = GameManager.Instance;
            game.Init();

            TopBarView topBarView = UIManager.Instance.Open<TopBarView>();
            GachaButtonView gachaButtonView = UIManager.Instance.Open<GachaButtonView>();

            m_topBarPresenter = new TopBarPresenter(topBarView, game.State, game.ZooLevel, game.Tables);
            m_worldPresenter = new WorldPresenter(m_worldView, game.State, game.Tables);
            m_gachaButtonPresenter = new GachaButtonPresenter(gachaButtonView, game.Gacha, game.State, game.Tables);
            m_gachaResultPresenter = new GachaResultPresenter(game.Gacha, game.Tables);
        }

        private void OnDestroy()
        {
            m_topBarPresenter?.Dispose();
            m_worldPresenter?.Dispose();
            m_gachaButtonPresenter?.Dispose();
            m_gachaResultPresenter?.Dispose();
        }
    }
}
