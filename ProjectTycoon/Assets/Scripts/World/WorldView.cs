using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    public sealed class WorldView : MonoBehaviour
    {
        [SerializeField] private BranchView m_branch;
        [SerializeField] private Animal m_animalPrefab;
        [SerializeField] private Facility m_facilityPrefab;
        [SerializeField] private VisitorSpawner m_visitors;
        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("지점 바깥으로 카메라 초점이 나갈 수 있는 여유(화면 유닛)")]
        [SerializeField] private float m_panMargin = 2f;

        // 설계 07-3: 08(존·입주)까지는 첫 존(z01)만 쓴다
        public ZoneView Zone => m_branch.Zones[0];
        public VisitorSpawner Visitors => m_visitors;
        public FrameCache Frames { get; } = new FrameCache();

        private void Start()
        {
            m_camera.SetBounds(CameraBounds());
        }

        public void SpawnAnimal(AnimalRecord record)
        {
            Animal animal = Instantiate(m_animalPrefab, Zone.transform);
            animal.Initialize(record, Zone, Frames);
            Zone.AnimalCount++;
        }

        // 설계 07 P6: 시설은 존 자식으로 슬롯 자리에 한 번 세운다
        public void ShowFacility(FacilityRecord record)
        {
            Vector2 logical = Zone.Center + new Vector2((float)record.SlotX, (float)record.SlotZ);
            Facility facility = Instantiate(m_facilityPrefab, Iso.ToScreen(logical), Quaternion.identity, Zone.transform);
            facility.Initialize(Resources.Load<Sprite>(record.Sprite));
        }

        // 설계 03 A3: 팬 범위(화면 XY) = 지점 사각형의 화면 경계 + 여유
        private Rect CameraBounds()
        {
            Rect screen = Iso.ToScreenBounds(m_branch.MapArea);

            return Rect.MinMaxRect(
                screen.xMin - m_panMargin, screen.yMin - m_panMargin,
                screen.xMax + m_panMargin, screen.yMax + m_panMargin);
        }
    }
}
