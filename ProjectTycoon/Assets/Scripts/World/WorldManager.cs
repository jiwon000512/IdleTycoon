using System;
using System.Collections;
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
    // 설계 39: 농장은 층마다 화면 하나(1층 위로 k_FloorGap씩)
    // 씬의 World 오브젝트에 붙어 있고 MainScene.Awake가 GameManager.Init 뒤에 Initialize로 서비스를 넘긴다
    public sealed class WorldManager : MonoSingleton<WorldManager>
    {
        // 광장 원점(빵집 원점에서 떨어진 곳. 두 곳이 한 화면에 같이 보이지 않게)
        private static readonly Vector3 k_PlazaOrigin = new Vector3(0f, 60f, 0f);
        private static readonly Vector3 k_FarmOrigin = new Vector3(0f, 120f, 0f);
        private static readonly Vector3 k_FloorGap = new Vector3(0f, 60f, 0f);
        // 설계 44: 낚시터는 광장 옆(농장 층이 늘어나는 위쪽과 겹치지 않게)
        private static readonly Vector3 k_FishingOrigin = new Vector3(60f, 60f, 0f);
        // 설계 47: 횟집은 낚시터 옆
        private static readonly Vector3 k_RestaurantOrigin = new Vector3(120f, 60f, 0f);
        // 고용한 점원이 굴에서 나오는 모습을 당겨서 보여 주는 시간(초)과 발끝에서 몸 가운데까지(유닛)
        private const float k_HireSpotSeconds = 1.8f;
        private const float k_BodyCenter = 0.5f;
        // 설계 53: 물이 차오르는 구간을 비추는 시간(초)
        private const float k_FloodSpotSeconds = 1.6f;

        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("빵집 프리팹(설계 08 v0.5). 씬에는 두지 않고 실행 중에 원점에 생성한다")]
        [SerializeField] private BakeryView m_shopPrefab;
        [Tooltip("광장 프리팹(설계 11). 씬에는 두지 않고 실행 중에 생성한다")]
        [SerializeField] private PlazaView m_plazaPrefab;
        [Tooltip("농장 프리팹(설계 25). 씬에는 두지 않고 실행 중에 생성한다")]
        [SerializeField] private FarmView m_farmPrefab;
        [Tooltip("낚시터 프리팹(설계 44). 씬에는 두지 않고 실행 중에 생성한다")]
        [SerializeField] private FishingView m_fishingPrefab;
        [Tooltip("횟집 프리팹(설계 47). 씬에는 두지 않고 실행 중에 생성한다")]
        [SerializeField] private RestaurantView m_restaurantPrefab;

        private readonly Dictionary<WombatArea, IAreaView> m_views = new Dictionary<WombatArea, IAreaView>();
        private BakeryView m_shopView;
        private RestaurantView m_restaurantView;
        private FishingView m_fishingView;
        private readonly List<FarmView> m_farmViews = new List<FarmView>();
        private Mall m_mall;
        private IDisposable m_areaChanged;
        private IDisposable m_clerkHired;
        private IDisposable m_cast;
        private IDisposable m_damBroken;

        public FrameCache Frames { get; } = new FrameCache();

        public void Initialize(TableSet tables, Mall mall, EventBus bus)
        {
            m_mall = mall;
            m_shopView = Instantiate(m_shopPrefab, Vector3.zero, Quaternion.identity, transform);
            m_shopView.Bind(mall.Bakery, bus, Frames, tables);
            m_shopView.GetComponent<BakeryVisitorSpawner>().Initialize(mall.Bakery, m_shopView, bus, tables, Frames);
            gameObject.AddComponent<WorldSound>().Initialize(mall, bus);
            gameObject.AddComponent<AreaBgm>().Initialize(mall, bus, tables);
            m_shopView.Expanded += View_Expanded;

            PlazaView plazaView = Instantiate(m_plazaPrefab, k_PlazaOrigin, Quaternion.identity, transform);
            plazaView.GetComponent<PlazaVisitorSpawner>().Initialize(mall.Plaza, plazaView, bus, tables, Frames);
            plazaView.Bind(mall.Plaza, bus, Frames, tables);
            m_views[mall.Bakery] = m_shopView;
            m_views[mall.Plaza] = plazaView;

            for (int i = 0; i < mall.Farms.Count; i++)
            {
                FarmView farmView = Instantiate(m_farmPrefab, k_FarmOrigin + k_FloorGap * i, Quaternion.identity, transform);
                farmView.Bind(mall.Farms[i], bus, Frames, tables);
                farmView.Expanded += View_Expanded;
                m_farmViews.Add(farmView);
                m_views[mall.Farms[i]] = farmView;
            }

            m_fishingView = Instantiate(m_fishingPrefab, k_FishingOrigin, Quaternion.identity, transform);
            m_fishingView.Bind(mall.Fishing, bus, Frames, tables);
            m_fishingView.Expanded += View_Expanded;
            m_views[mall.Fishing] = m_fishingView;

            m_restaurantView = Instantiate(m_restaurantPrefab, k_RestaurantOrigin, Quaternion.identity, transform);
            m_restaurantView.Bind(mall.Restaurant, bus, Frames, tables);
            m_restaurantView.Expanded += View_Expanded;
            m_views[mall.Restaurant] = m_restaurantView;

            m_areaChanged = bus.Subscribe<Events.AreaChanged>(Bus_AreaChanged);
            m_clerkHired = bus.Subscribe<Events.ClerkHired>(Bus_ClerkHired);
            m_cast = bus.Subscribe<Events.CastThrown>(Bus_CastThrown);
            m_damBroken = bus.Subscribe<Events.DamBroken>(Bus_DamBroken);
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
                m_shopView.Expanded -= View_Expanded;
            }

            foreach (FarmView farmView in m_farmViews)
            {
                farmView.Expanded -= View_Expanded;
            }

            if (m_restaurantView != null)
            {
                m_restaurantView.Expanded -= View_Expanded;
            }

            if (m_fishingView != null)
            {
                m_fishingView.Expanded -= View_Expanded;
            }

            m_areaChanged?.Dispose();
            m_clerkHired?.Dispose();
            m_cast?.Dispose();
            m_damBroken?.Dispose();

            base.OnDestroy();
        }

        private void FollowWombat()
        {
            IAreaView view = m_views[m_mall.Active];
            m_camera.Follow(view.Wombat, view.Bounds);
        }

        // 굴을 팠다(빵집 · 농장 층): 웜뱃이 있는 곳의 카메라 경계를 다시 잰다
        private void View_Expanded()
        {
            m_camera.SetBounds(m_views[m_mall.Active].Bounds);
        }

        private void Bus_AreaChanged(Events.AreaChanged e)
        {
            FollowWombat();
        }

        // 웜뱃이 그 점원의 곳(빵집 · 농장)에 있을 때만. 조이스틱을 움직이면 바로 웜뱃에게 돌아온다(조작을 빼앗지 않는다)
        private void Bus_ClerkHired(Events.ClerkHired e)
        {
            if (m_mall.Active != e.Clerk.Home)
            {
                return;
            }

            Clerk clerk = e.Clerk;
            Vector3 origin = m_views[clerk.Home].Origin;
            m_camera.Spot(() => (Vector2)origin + new Vector2(clerk.Position.X, clerk.Position.Y) + Vector2.up * k_BodyCenter, k_HireSpotSeconds,
                () => m_mall.Wombat.Input != System.Numerics.Vector2.Zero);
        }

        // 설계 53: 댐이 무너지면 물이 차오르는 구간을 잠깐 비춘다(배율 그대로, 조이스틱을 움직이면 바로 돌아온다)
        private void Bus_DamBroken(Events.DamBroken e)
        {
            if (m_mall.Active != e.Fishing)
            {
                return;
            }

            Vector3 at = m_fishingView.Origin + new Vector3(e.Fishing.Layout.CenterOf(e.Stretch), e.Fishing.Layout.BankY - 2f, 0f);
            m_camera.Spot(() => at, k_FloodSpotSeconds, () => m_mall.Wombat.Input != System.Numerics.Vector2.Zero, 1f);
        }

        // 설계 52: 던지면 줄이 끝날 때까지 웜뱃과 찌의 가운데를 비춘다(배율 그대로, 사용자 2026-10-07 「당길 때 물고기 자리가 화면 밖」)
        private void Bus_CastThrown(Events.CastThrown e)
        {
            if (m_mall.Active == e.Fishing && m_views[e.Fishing] is FishingView view)
            {
                m_camera.Spot(() => view.Focus, float.PositiveInfinity, () => e.Fishing.Phase == FishingPhase.Idle, 1f);
            }
        }
    }
}
