using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    public sealed class WorldView : MonoBehaviour
    {
        [SerializeField] private CageView[] m_cages;
        [SerializeField] private AnimalActor m_animalPrefab;
        [SerializeField] private WorldCameraController m_camera;
        [Tooltip("우리 바깥으로 카메라 초점이 나갈 수 있는 여유(유닛)")]
        [SerializeField] private float m_panMargin = 2f;

        private int[] m_animalCounts;

        public int CageCount => m_cages.Length;
        public IReadOnlyList<CageView> Cages => m_cages;
        public Quaternion BillboardRotation => m_camera.BillboardRotation;

        private void Awake()
        {
            m_animalCounts = new int[m_cages.Length];
        }

        private void Start()
        {
            m_camera.SetBounds(CameraBounds());
        }

        public void SpawnAnimal(AnimalRecord record, int cageIndex)
        {
            CageView cage = m_cages[cageIndex];
            AnimalActor actor = Instantiate(m_animalPrefab, cage.RandomWalkPoint(), Quaternion.identity, cage.transform);
            actor.Initialize(
                record,
                LoadFrames(record.IdleSheet ?? record.Sprite),
                LoadFrames(record.MoveSheet ?? record.Sprite),
                cage,
                m_camera.BillboardRotation);
            m_animalCounts[cageIndex]++;
        }

        // 설계 06 P10: 관광객이 구경할 수 있는(동물이 한 마리라도 있는) 우리
        public List<CageView> CagesWithAnimals()
        {
            List<CageView> result = new List<CageView>();

            for (int i = 0; i < m_cages.Length; i++)
            {
                if (m_animalCounts[i] > 0)
                {
                    result.Add(m_cages[i]);
                }
            }

            return result;
        }

        // 설계 05 P5: 시트가 없으면 sprite 1장이 프레임 1개. 격자 슬라이스 이름(_0, _1 …) 순으로 정렬
        internal static Sprite[] LoadFrames(string path)
        {
            return Resources.LoadAll<Sprite>(path).OrderBy(s => s.name).ToArray();
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
