using GameKit.Singleton;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 월드 매니저(2026-09-16: WorldView + WorldPresenter 통합. MVP는 UI에만 둔다). 설계 09: 지상 없이 빵집 굴 하나를 실행 중 생성하고 카메라가 웜뱃을 따라가게 한다.
    // 씬의 World 오브젝트에 붙어 있고 MainScene.Awake가 GameManager.Init 뒤에 Initialize로 서비스를 넘긴다
    public sealed class WorldManager : MonoSingleton<WorldManager>
    {
        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("빵집 프리팹(설계 08 v0.5). 씬에는 두지 않고 실행 중에 원점에 생성한다")]
        [SerializeField] private ShopView m_shopPrefab;

        private ShopView m_shopView;

        public FrameCache Frames { get; } = new FrameCache();

        public void Initialize(GameTables tables, ShopSim shop)
        {
            m_shopView = Instantiate(m_shopPrefab, Vector3.zero, Quaternion.identity, transform);
            m_shopView.Bind(shop, Frames, tables);
            m_shopView.GetComponent<ShopCustomerSpawner>().Initialize(shop, m_shopView, tables, Frames);
            m_shopView.gameObject.AddComponent<ShopSound>().Initialize(shop, tables);
            m_shopView.Expanded += ShopView_Expanded;
            m_camera.Follow(m_shopView.Wombat, m_shopView.Bounds);
        }

        protected override void OnDestroy()
        {
            if (m_shopView != null)
            {
                m_shopView.Expanded -= ShopView_Expanded;
            }

            base.OnDestroy();
        }

        private void ShopView_Expanded()
        {
            m_camera.SetBounds(m_shopView.Bounds);
        }
    }
}
