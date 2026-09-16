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
        [SerializeField] private Facility m_facilityPrefab;
        [SerializeField] private VisitorSpawner m_visitors;
        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("지점 바깥으로 카메라 초점이 나갈 수 있는 여유(화면 유닛)")]
        [SerializeField] private float m_panMargin = 2f;

        private readonly Dictionary<string, int> m_spawnedByAnimal = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> m_shownFacilities = new HashSet<string>(StringComparer.Ordinal);
        private ZooState m_state;
        private IncomeService m_income;
        private GameTables m_tables;
        private BranchView m_branch;

        // 설계 07-3: 08(존·입주)까지는 첫 존(z01)만 쓴다
        public ZoneView Zone => Branch.Zones[0];
        public FrameCache Frames { get; } = new FrameCache();

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

        public void Initialize(ZooState state, IncomeService income, GameTables tables)
        {
            m_state = state;
            m_income = income;
            m_tables = tables;

            m_visitors.Initialize(tables.Visitors);
            m_camera.SetBounds(CameraBounds());
            m_state.AnimalsChanged += State_AnimalsChanged;
            m_state.FacilitiesChanged += State_FacilitiesChanged;
            m_income.IncomeChanged += Income_IncomeChanged;
            SyncAnimals();
            SyncFacilities();
            SyncVisitors();
        }

        protected override void OnDestroy()
        {
            if (m_state != null)
            {
                m_state.AnimalsChanged -= State_AnimalsChanged;
                m_state.FacilitiesChanged -= State_FacilitiesChanged;
                m_income.IncomeChanged -= Income_IncomeChanged;
            }

            base.OnDestroy();
        }

        private void State_AnimalsChanged()
        {
            SyncAnimals();
        }

        private void State_FacilitiesChanged()
        {
            SyncFacilities();
        }

        private void Income_IncomeChanged()
        {
            SyncVisitors();
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

        // 설계 07 P6: 1단계 이상인 시설을 존 자식으로 슬롯 자리에 한 번 세운다
        private void SyncFacilities()
        {
            for (int i = 0; i < m_tables.Facilities.Count; i++)
            {
                FacilityRecord record = m_tables.Facilities[i];

                if (m_state.GetFacilityStage(record.Id) > 0 && m_shownFacilities.Add(record.Id))
                {
                    Vector2 logical = Zone.Center + new Vector2((float)record.SlotX, (float)record.SlotZ);
                    Facility facility = Instantiate(m_facilityPrefab, Iso.ToScreen(logical), Quaternion.identity, Zone.transform);
                    facility.Initialize(Resources.Load<Sprite>(record.Sprite));
                }
            }
        }

        // 설계 06 P1·P9: 관광객 수는 초당 수입에서 계산한다
        private void SyncVisitors()
        {
            m_visitors.TargetCount = VisitorCalculator.Count(m_income.IncomePerSecond, m_tables.Config.Visitors);
        }

        // 설계 03 A3: 팬 범위(화면 XY) = 지점 사각형의 화면 경계 + 여유
        private Rect CameraBounds()
        {
            Rect screen = Iso.ToScreenBounds(Branch.MapArea);

            return Rect.MinMaxRect(
                screen.xMin - m_panMargin, screen.yMin - m_panMargin,
                screen.xMax + m_panMargin, screen.yMax + m_panMargin);
        }
    }
}
