using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 가게 한 구역(입구·진열 층·계산대·오븐 줄). 피벗은 구역 윗변 가운데이고 ShopView가 높이만큼 아래로 쌓는다
    public class ShopSection : MonoBehaviour
    {
        [Tooltip("구역 높이(유닛)")]
        [SerializeField] private float m_height = 2f;

        public float Height => m_height;
    }
}
