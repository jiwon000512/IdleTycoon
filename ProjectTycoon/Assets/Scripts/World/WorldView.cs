using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    public sealed class WorldView : MonoBehaviour
    {
        [SerializeField] private CageView m_cage;
        [SerializeField] private Animal m_animalPrefab;
        [SerializeField] private Facility m_facilityPrefab;
        [SerializeField] private VisitorSpawner m_visitors;
        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("우리 바깥으로 카메라 초점이 나갈 수 있는 여유(유닛). 시설 자리는 이 안에 둔다(설계 07 P7)")]
        [SerializeField] private float m_panMargin = 2f;

        // 설계 07 D1: 우리는 지점당 하나
        public CageView Cage => m_cage;
        public VisitorSpawner Visitors => m_visitors;
        public FrameCache Frames { get; } = new FrameCache();
        public Quaternion BillboardRotation => m_camera.BillboardRotation;

        private void Start()
        {
            m_camera.SetBounds(CameraBounds());
        }

        public void SpawnAnimal(AnimalRecord record)
        {
            Animal animal = Instantiate(m_animalPrefab, m_cage.RandomWalkPoint(), Quaternion.identity, m_cage.transform);
            animal.Initialize(record, m_cage, Frames, m_camera.BillboardRotation);
            m_cage.AnimalCount++;
        }

        // 설계 07 P6: 시설은 우리 자식으로 슬롯 자리에 한 번 세운다
        public void ShowFacility(FacilityRecord record)
        {
            Vector3 position = m_cage.transform.position + new Vector3((float)record.SlotX, 0f, (float)record.SlotZ);
            Facility facility = Instantiate(m_facilityPrefab, position, Quaternion.identity, m_cage.transform);
            facility.Initialize(Resources.Load<Sprite>(record.Sprite), m_camera.BillboardRotation);
        }

        // 설계 03 A3: 팬 범위(XZ) = 우리와 길을 감싸는 사각형 + 여유
        private Rect CameraBounds()
        {
            Rect cage = m_cage.WorldBounds;
            Rect path = m_cage.PathBounds;

            return Rect.MinMaxRect(
                Mathf.Min(cage.xMin, path.xMin) - m_panMargin, Mathf.Min(cage.yMin, path.yMin) - m_panMargin,
                Mathf.Max(cage.xMax, path.xMax) + m_panMargin, Mathf.Max(cage.yMax, path.yMax) + m_panMargin);
        }
    }
}
