using System;
using System.Collections.Generic;
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
    // 설계 25: 농장도 실행 중 생성(광장 위쪽 멀리). 곳마다 화면 하나(IAreaView)를 두고 웜뱃이 있는 곳의 화면을 쓴다.
    // 씬의 World 오브젝트에 붙어 있고 MainScene.Awake가 GameManager.Init 뒤에 Initialize로 서비스를 넘긴다
    public sealed class WorldManager : MonoSingleton<WorldManager>
    {
        // 광장 원점(빵집 원점에서 떨어진 곳. 두 곳이 한 화면에 같이 보이지 않게)
        private static readonly Vector3 k_PlazaOrigin = new Vector3(0f, 60f, 0f);
        private static readonly Vector3 k_FarmOrigin = new Vector3(0f, 120f, 0f);
        // 고용한 점원이 굴에서 나오는 모습을 당겨서 보여 주는 시간(초)과 발끝에서 몸 가운데까지(유닛)
        private const float k_HireSpotSeconds = 1.8f;
        private const float k_BodyCenter = 0.5f;

        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("빵집 프리팹(설계 08 v0.5). 씬에는 두지 않고 실행 중에 원점에 생성한다")]
        [SerializeField] private BakeryView m_shopPrefab;
        [Tooltip("광장 프리팹(설계 11). 씬에는 두지 않고 실행 중에 생성한다")]
        [SerializeField] private PlazaView m_plazaPrefab;
        [Tooltip("농장 프리팹(설계 25). 씬에는 두지 않고 실행 중에 생성한다")]
        [SerializeField] private FarmView m_farmPrefab;

        private readonly Dictionary<WombatArea, IAreaView> m_views = new Dictionary<WombatArea, IAreaView>();
        private BakeryView m_shopView;
        private FarmView m_farmView;
        private Mall m_mall;
        private IDisposable m_areaChanged;
        private IDisposable m_clerkHired;

        public FrameCache Frames { get; } = new FrameCache();

        public void Initialize(TableSet tables, Mall mall, EventBus bus)
        {
            m_mall = mall;
            m_shopView = Instantiate(m_shopPrefab, Vector3.zero, Quaternion.identity, transform);
            m_shopView.Bind(mall.Bakery, bus, Frames, tables);
            m_shopView.GetComponent<BakeryVisitorSpawner>().Initialize(mall.Bakery, m_shopView, bus, tables, Frames);
            gameObject.AddComponent<WorldSound>().Initialize(mall, bus);
            gameObject.AddComponent<AreaBgm>().Initialize(mall, bus, tables);
            m_shopView.Expanded += BakeryView_Expanded;

            PlazaView plazaView = Instantiate(m_plazaPrefab, k_PlazaOrigin, Quaternion.identity, transform);
            plazaView.GetComponent<PlazaVisitorSpawner>().Initialize(mall.Plaza, plazaView, bus, tables, Frames);
            plazaView.Bind(mall.Plaza, bus, Frames, tables);
            m_farmView = Instantiate(m_farmPrefab, k_FarmOrigin, Quaternion.identity, transform);
            m_farmView.Bind(mall.Farm, bus, Frames, tables);
            m_farmView.Expanded += FarmView_Expanded;
            m_views[mall.Bakery] = m_shopView;
            m_views[mall.Plaza] = plazaView;
            m_views[mall.Farm] = m_farmView;
            m_areaChanged = bus.Subscribe<Events.AreaChanged>(Bus_AreaChanged);
            m_clerkHired = bus.Subscribe<Events.ClerkHired>(Bus_ClerkHired);
            FollowWombat();
        }

        // 곳 원점(월드). 곳 좌표 = 월드 − 원점
        public System.Numerics.Vector2 OriginOf(WombatArea area)
        {
            Vector3 origin = m_views[area].Origin;
            return new System.Numerics.Vector2(origin.x, origin.y);
        }

        // 편집 모드: 카메라는 서고(팬만), 빵집은 파기 태그를 늘 보인다
        public void SetEditing(bool editing)
        {
            m_camera.SetFollowing(!editing);

            foreach (IAreaView view in m_views.Values)
            {
                view.SetEditing(editing);
            }

            if (!editing)
            {
                HideGhost();
            }
        }

        // 잡은 사물(null이면 없음)의 외곽선을 노랗게
        public void SetHeld(IPlaced held)
        {
            foreach (IAreaView view in m_views.Values)
            {
                view.SetHeld(held);
            }
        }

        public void Pan(System.Numerics.Vector2 delta)
        {
            m_camera.Pan(new Vector2(delta.X, delta.Y));
        }

        // 끌고 있는 사물의 그림자(놓을 수 있으면 초록, 아니면 빨강)를 웜뱃이 있는 곳에 그린다
        public void ShowGhost(IPlacedKind kind, System.Numerics.Vector2 at, bool ok)
        {
            m_views[m_mall.Active].ShowGhost(kind, at, ok);
        }

        public void HideGhost()
        {
            foreach (IAreaView view in m_views.Values)
            {
                view.HideGhost();
            }
        }

        protected override void OnDestroy()
        {
            if (m_shopView != null)
            {
                m_shopView.Expanded -= BakeryView_Expanded;
            }

            if (m_farmView != null)
            {
                m_farmView.Expanded -= FarmView_Expanded;
            }

            m_areaChanged?.Dispose();
            m_clerkHired?.Dispose();

            base.OnDestroy();
        }

        private void FollowWombat()
        {
            IAreaView view = m_views[m_mall.Active];
            m_camera.Follow(view.Wombat, view.Bounds);
        }

        private void BakeryView_Expanded()
        {
            if (m_mall.Active == m_mall.Bakery)
            {
                m_camera.SetBounds(m_shopView.Bounds);
            }
        }

        private void FarmView_Expanded()
        {
            if (m_mall.Active == m_mall.Farm)
            {
                m_camera.SetBounds(m_farmView.Bounds);
            }
        }

        private void Bus_AreaChanged(Events.AreaChanged e)
        {
            FollowWombat();
        }

        // 웜뱃이 그 가게에 있을 때만. 조이스틱을 움직이면 바로 웜뱃에게 돌아온다(조작을 빼앗지 않는다)
        private void Bus_ClerkHired(Events.ClerkHired e)
        {
            if (m_mall.Active != m_mall.Bakery || e.Clerk.Bakery != m_mall.Bakery)
            {
                return;
            }

            Clerk clerk = e.Clerk;
            m_camera.Spot(() => (Vector2)m_shopView.ToWorld(clerk.Position) + Vector2.up * k_BodyCenter, k_HireSpotSeconds,
                () => m_mall.Wombat.Input != System.Numerics.Vector2.Zero);
        }
    }
}
