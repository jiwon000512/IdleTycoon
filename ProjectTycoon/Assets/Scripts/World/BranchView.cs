using System.Collections.Generic;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 07-3: 지점 프리팹의 루트. 바닥 타일맵(잔디·흙·길)은 프리팹에 미리 칠해져 있고, 존들은 자식 ZoneView
    public sealed class BranchView : MonoBehaviour
    {
        [Tooltip("지점 전체(논리 XZ, 타일 좌표). 카메라 팬 범위의 기준")]
        [SerializeField] private Rect m_mapArea;
        [SerializeField] private ZoneView[] m_zones;

        public Rect MapArea => m_mapArea;

        public IReadOnlyList<ZoneView> Zones => m_zones;
    }
}
