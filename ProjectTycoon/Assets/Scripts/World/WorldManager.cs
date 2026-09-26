using System;
using GameKit.Events;
using GameKit.Singleton;
using UnityEngine;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 월드 매니저(2026-09-16: WorldView + WorldPresenter 통합. MVP는 UI에만 둔다). 설계 09: 지상 없이 빵집 굴 하나를 실행 중 생성하고 카메라가 웜뱃을 따라가게 한다.
    // 설계 11: 굴 밖 광장도 실행 중 생성(빵집 위쪽 멀리)하고, 웜뱃이 옮겨 가면 카메라 대상·경계를 그곳으로 바꾼다.
    // 설계 18: 편집 모드(카메라 멈춤·팬, 놓을 자리 그림자, 파기 태그 상시)를 곳 화면에 넘긴다. 곳 좌표 ↔ 월드 좌표는 곳 원점 차이뿐.
    // 씬의 World 오브젝트에 붙어 있고 MainScene.Awake가 GameManager.Init 뒤에 Initialize로 서비스를 넘긴다
    public sealed class WorldManager : MonoSingleton<WorldManager>
    {
        // 광장 원점(빵집 원점에서 떨어진 곳. 두 곳이 한 화면에 같이 보이지 않게)
        private static readonly Vector3 k_PlazaOrigin = new Vector3(0f, 60f, 0f);

        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("빵집 프리팹(설계 08 v0.5). 씬에는 두지 않고 실행 중에 원점에 생성한다")]
        [SerializeField] private BakeryView m_shopPrefab;
        [Tooltip("광장 프리팹(설계 11). 씬에는 두지 않고 실행 중에 생성한다")]
        [SerializeField] private PlazaView m_plazaPrefab;

        private BakeryView m_shopView;
        private PlazaView m_plazaView;
        private Mall m_mall;
        private IDisposable m_areaChanged;

        public FrameCache Frames { get; } = new FrameCache();

        public void Initialize(TableSet tables, Mall mall, EventBus bus)
        {
            m_mall = mall;
            m_shopView = Instantiate(m_shopPrefab, Vector3.zero, Quaternion.identity, transform);
            m_shopView.Bind(mall.Bakery, bus, Frames, tables);
            m_shopView.GetComponent<BakeryVisitorSpawner>().Initialize(mall.Bakery, m_shopView, bus, tables, Frames);
            m_shopView.gameObject.AddComponent<BakerySound>().Initialize(mall.Bakery, bus, tables);
            m_shopView.Expanded += BakeryView_Expanded;

            m_plazaView = Instantiate(m_plazaPrefab, k_PlazaOrigin, Quaternion.identity, transform);
            m_plazaView.GetComponent<PlazaVisitorSpawner>().Initialize(mall.Plaza, bus, tables, Frames);
            m_plazaView.Bind(mall.Plaza, bus, Frames, tables);
            m_areaChanged = bus.Subscribe<Events.AreaChanged>(Bus_AreaChanged);
            FollowWombat();
        }

        // 곳 원점(월드). 곳 좌표 = 월드 − 원점
        public System.Numerics.Vector2 OriginOf(WombatArea area)
        {
            Vector3 origin = area == m_mall.Plaza ? k_PlazaOrigin : Vector3.zero;
            return new System.Numerics.Vector2(origin.x, origin.y);
        }

        // 편집 모드: 카메라는 서고(팬만), 빵집은 파기 태그를 늘 보인다
        public void SetEditing(bool editing)
        {
            m_camera.SetFollowing(!editing);
            m_shopView.SetEditing(editing);

            if (!editing)
            {
                HideGhost();
            }
        }

        public void Pan(System.Numerics.Vector2 delta)
        {
            m_camera.Pan(new Vector2(delta.X, delta.Y));
        }

        // 끌고 있는 사물의 그림자(놓을 수 있으면 초록, 아니면 빨강)를 웜뱃이 있는 곳에 그린다
        public void ShowGhost(IPlacedKind kind, System.Numerics.Vector2 at, bool ok)
        {
            if (m_mall.Active == m_mall.Bakery)
            {
                m_shopView.ShowGhost(kind, at, ok);
            }
            else
            {
                m_plazaView.ShowGhost(kind, at, ok);
            }
        }

        public void HideGhost()
        {
            m_shopView.HideGhost();
            m_plazaView.HideGhost();
        }

        protected override void OnDestroy()
        {
            if (m_shopView != null)
            {
                m_shopView.Expanded -= BakeryView_Expanded;
            }

            m_areaChanged?.Dispose();

            base.OnDestroy();
        }

        private void FollowWombat()
        {
            if (m_mall.Active == m_mall.Bakery)
            {
                m_camera.Follow(m_shopView.Wombat, m_shopView.Bounds);
            }
            else
            {
                m_camera.Follow(m_plazaView.Wombat, m_plazaView.Bounds);
            }
        }

        private void BakeryView_Expanded()
        {
            if (m_mall.Active == m_mall.Bakery)
            {
                m_camera.SetBounds(m_shopView.Bounds);
            }
        }

        private void Bus_AreaChanged(Events.AreaChanged e)
        {
            FollowWombat();
        }
    }
}
