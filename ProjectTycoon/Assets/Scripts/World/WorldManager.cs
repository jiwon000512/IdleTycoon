using GameKit.Singleton;
using UnityEngine;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 월드 매니저(2026-09-16: WorldView + WorldPresenter 통합. MVP는 UI에만 둔다). 설계 09: 지상 없이 빵집 굴 하나를 실행 중 생성하고 카메라가 웜뱃을 따라가게 한다.
    // 설계 11: 굴 밖 광장도 실행 중 생성(빵집 위쪽 멀리)하고, 웜뱃이 옮겨 가면 카메라 대상·경계를 그곳으로 바꾼다.
    // 씬의 World 오브젝트에 붙어 있고 MainScene.Awake가 GameManager.Init 뒤에 Initialize로 서비스를 넘긴다
    public sealed class WorldManager : MonoSingleton<WorldManager>
    {
        // 광장 원점(빵집 원점에서 떨어진 곳. 두 곳이 한 화면에 같이 보이지 않게)
        private static readonly Vector3 k_PlazaOrigin = new Vector3(0f, 60f, 0f);

        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("빵집 프리팹(설계 08 v0.5). 씬에는 두지 않고 실행 중에 원점에 생성한다")]
        [SerializeField] private ShopView m_shopPrefab;
        [Tooltip("광장 프리팹(설계 11). 씬에는 두지 않고 실행 중에 생성한다")]
        [SerializeField] private PlazaView m_plazaPrefab;

        private ShopView m_shopView;
        private PlazaView m_plazaView;
        private Mall m_mall;

        public FrameCache Frames { get; } = new FrameCache();

        public void Initialize(TableSet tables, Mall mall)
        {
            m_mall = mall;
            m_shopView = Instantiate(m_shopPrefab, Vector3.zero, Quaternion.identity, transform);
            m_shopView.Bind(mall.Bakery, Frames, tables);
            m_shopView.GetComponent<ShopCustomerSpawner>().Initialize(mall.Bakery, m_shopView, tables, Frames);
            m_shopView.gameObject.AddComponent<ShopSound>().Initialize(mall.Bakery, tables);
            m_shopView.Expanded += ShopView_Expanded;

            m_plazaView = Instantiate(m_plazaPrefab, k_PlazaOrigin, Quaternion.identity, transform);
            m_plazaView.GetComponent<PlazaVisitorSpawner>().Initialize(mall.Plaza, tables, Frames);
            m_plazaView.Bind(mall.Plaza, Frames, tables);
            m_mall.AreaChanged += Mall_AreaChanged;
            FollowWombat();
        }

        protected override void OnDestroy()
        {
            if (m_shopView != null)
            {
                m_shopView.Expanded -= ShopView_Expanded;
            }

            if (m_mall != null)
            {
                m_mall.AreaChanged -= Mall_AreaChanged;
            }

            base.OnDestroy();
        }

        private void FollowWombat()
        {
            if (m_mall.Current == Area.Bakery)
            {
                m_camera.Follow(m_shopView.Wombat, m_shopView.Bounds);
            }
            else
            {
                m_camera.Follow(m_plazaView.Wombat, m_plazaView.Bounds);
            }
        }

        private void ShopView_Expanded()
        {
            if (m_mall.Current == Area.Bakery)
            {
                m_camera.SetBounds(m_shopView.Bounds);
            }
        }

        private void Mall_AreaChanged()
        {
            FollowWombat();
        }
    }
}
