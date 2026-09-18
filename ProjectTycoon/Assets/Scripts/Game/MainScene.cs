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

        private void Awake()
        {
            GameManager game = GameManager.Instance;
            game.Init();

            TopBarView topBarView = UIManager.Instance.Open<TopBarView>();
            m_topBarPresenter = new TopBarPresenter(topBarView, game.State, game.ZooLevel, game.Income, game.Tables);
            WorldManager.Instance.Initialize(game.State, game.Income, game.Tables);
        }

        private void OnDestroy()
        {
            m_topBarPresenter?.Dispose();
        }
    }
}
