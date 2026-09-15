using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    public sealed class WorldView : MonoBehaviour
    {
        [SerializeField] private CageView[] m_cages;
        [SerializeField] private Animal m_animalPrefab;
        [SerializeField] private VisitorSpawner m_visitors;
        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("우리 바깥으로 카메라 초점이 나갈 수 있는 여유(유닛)")]
        [SerializeField] private float m_panMargin = 2f;

        public int CageCount => m_cages.Length;
        public IReadOnlyList<CageView> Cages => m_cages;
        public VisitorSpawner Visitors => m_visitors;
        public FrameCache Frames { get; } = new FrameCache();
        public Quaternion BillboardRotation => m_camera.BillboardRotation;

        private void Start()
        {
            m_camera.SetBounds(CameraBounds());
        }

        public void SpawnAnimal(AnimalRecord record, int cageIndex)
        {
            CageView cage = m_cages[cageIndex];
            Animal animal = Instantiate(m_animalPrefab, cage.RandomWalkPoint(), Quaternion.identity, cage.transform);
            animal.Initialize(record, cage, Frames, m_camera.BillboardRotation);
            cage.AnimalCount++;
        }

        // 설계 06 P10: 관광객이 구경할 수 있는(동물이 한 마리라도 있는) 우리
        public List<CageView> CagesWithAnimals()
        {
            List<CageView> result = new List<CageView>();

            for (int i = 0; i < m_cages.Length; i++)
            {
                if (m_cages[i].AnimalCount > 0)
                {
                    result.Add(m_cages[i]);
                }
            }

            return result;
        }

        // 설계 03 A3: 팬 범위(XZ) = 모든 우리와 길을 감싸는 사각형 + 여유
        private Rect CameraBounds()
        {
            Rect union = m_cages[0].WorldBounds;

            for (int i = 0; i < m_cages.Length; i++)
            {
                union = Union(union, m_cages[i].WorldBounds);
                union = Union(union, m_cages[i].PathBounds);
            }

            return Rect.MinMaxRect(
                union.xMin - m_panMargin, union.yMin - m_panMargin,
                union.xMax + m_panMargin, union.yMax + m_panMargin);
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }
    }
}
