using System;
using System.Collections.Generic;
using GameKit.Singleton;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 월드 매니저(2026-09-16: WorldView + WorldPresenter 통합. MVP는 UI에만 둔다). 지점 프리팹을 실행 중 생성하고 Core 상태 이벤트를 월드 표시로 옮긴다.
    // 씬의 World 오브젝트에 붙어 있고 MainScene.Awake가 GameManager.Init 뒤에 Initialize로 서비스를 넘긴다
    public sealed class WorldManager : MonoSingleton<WorldManager>
    {
        [Tooltip("지점 프리팹. 씬에는 두지 않고 실행 중에 생성한다(설계 07-3)")]
        [SerializeField] private BranchView m_branchPrefab;
        [SerializeField] private Animal m_animalPrefab;
        [SerializeField] private VisitorSpawner m_visitors;
        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("빵집 프리팹(설계 08 v0.5). 지점과 겹치지 않는 먼 곳에 실행 중 생성한다")]
        [SerializeField] private ShopView m_shopPrefab;

        private readonly Dictionary<string, int> m_spawnedByAnimal = new Dictionary<string, int>(StringComparer.Ordinal);
        private ZooState m_state;
        private IncomeService m_income;
        private GameTables m_tables;
        private BranchView m_branch;
        private Navigation m_navigation;
        private ShopView m_shopView;

        private static readonly Vector3 k_ShopOrigin = new Vector3(1000f, 0f, 0f);

        // 설계 07-3: 08(존·입주)까지는 첫 존(z01)만 쓴다
        public ZoneView Zone => Branch.Zones[0];
        public FrameCache Frames { get; } = new FrameCache();

        // 설계 08 v0.5: 가게 화면에서 오븐을 눌렀다(오븐 번호). MainScene이 굽기 팝업을 연다
        public event Action<int> OvenTapped;

        private BranchView Branch
        {
            get
            {
                if (m_branch == null)
                {
                    m_branch = Instantiate(m_branchPrefab, transform);
                }

                return m_branch;
            }
        }

        public void Initialize(ZooState state, IncomeService income, GameTables tables, Navigation navigation, ShopSim shop)
        {
            m_state = state;
            m_income = income;
            m_tables = tables;
            m_navigation = navigation;
            m_shopView = Instantiate(m_shopPrefab, k_ShopOrigin, Quaternion.identity, transform);
            m_shopView.Bind(shop, Frames);
            m_shopView.GetComponent<ShopCustomerSpawner>().Initialize(shop, m_shopView, tables, Frames);
            m_shopView.Expanded += ShopView_Expanded;

            m_visitors.Initialize(tables.Visitors);
            m_camera.SetBounds(Iso.ToScreenBounds(Branch.MapArea));
            m_state.AnimalsChanged += State_AnimalsChanged;
            m_income.IncomeChanged += Income_IncomeChanged;
            m_navigation.Changed += Navigation_Changed;
            m_camera.Tapped += Camera_Tapped;
            SyncAnimals();
            SyncVisitors();
        }

        protected override void OnDestroy()
        {
            if (m_state != null)
            {
                m_state.AnimalsChanged -= State_AnimalsChanged;
                m_income.IncomeChanged -= Income_IncomeChanged;
                m_navigation.Changed -= Navigation_Changed;
                m_camera.Tapped -= Camera_Tapped;
                m_shopView.Expanded -= ShopView_Expanded;
            }

            base.OnDestroy();
        }

        private void State_AnimalsChanged()
        {
            SyncAnimals();
        }

        private void Income_IncomeChanged()
        {
            SyncVisitors();
        }

        // 설계 08: 지상에서 첫 굴(z01)을 누르면 빵집으로(다른 굴은 12). 가게에서는 오븐 탭
        private void Camera_Tapped(Vector3 world)
        {
            if (m_navigation.Current == GameScreen.Overworld)
            {
                if (Zone.Contains(Iso.ToLogical(world)))
                {
                    m_navigation.EnterShop();
                }

                return;
            }

            int oven = m_shopView.OvenAt(world);

            if (oven >= 0)
            {
                OnOvenTapped(oven);
            }
        }

        private void Navigation_Changed()
        {
            if (m_navigation.Current == GameScreen.Shop)
            {
                m_camera.EnterShop(m_shopView.Bounds, m_shopView.Top);
                return;
            }

            m_camera.ExitShop();
        }

        // 굴을 넓히면 새 층을 보여 준다
        private void ShopView_Expanded(Vector2 focus)
        {
            if (m_navigation.Current == GameScreen.Shop)
            {
                m_camera.ShowShop(m_shopView.Bounds, focus);
            }
        }

        private void OnOvenTapped(int oven)
        {
            OvenTapped?.Invoke(oven);
        }

        // 설계 03 P7: 종별 마리 수만큼 개체가 있도록 부족분을 스폰한다
        private void SyncAnimals()
        {
            for (int i = 0; i < m_state.OwnedAnimals.Count; i++)
            {
                OwnedAnimal owned = m_state.OwnedAnimals[i];
                m_spawnedByAnimal.TryGetValue(owned.AnimalId, out int spawned);

                if (spawned >= owned.Count)
                {
                    continue;
                }

                AnimalRecord record = m_tables.GetAnimal(owned.AnimalId);

                for (int n = spawned; n < owned.Count; n++)
                {
                    Animal animal = Instantiate(m_animalPrefab, Zone.transform);
                    animal.Initialize(record, Zone, Frames);
                    Zone.AnimalCount++;
                }

                m_spawnedByAnimal[owned.AnimalId] = owned.Count;
            }
        }

        // 설계 06 P1·P9: 관광객 수는 초당 수입에서 계산한다
        private void SyncVisitors()
        {
            m_visitors.TargetCount = VisitorCalculator.Count(m_income.IncomePerSecond, m_tables.Config.Visitors);
        }
    }
}
