using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 18: 편집 모드에서 끌고 있는 사물의 그림자. 반투명 스프라이트 하나를 초록(놓을 수 있음)·빨강(못 놓음)으로 물들인다
    public sealed class GhostView : MonoBehaviour
    {
        private static readonly Color k_Ok = new Color(0.55f, 1f, 0.6f, 0.65f);
        private static readonly Color k_Bad = new Color(1f, 0.45f, 0.4f, 0.65f);
        private const int k_Order = 900;

        private SpriteRenderer m_renderer;

        public static GhostView Create(Transform parent)
        {
            GameObject go = new GameObject("Ghost");
            go.transform.SetParent(parent, false);
            GhostView ghost = go.AddComponent<GhostView>();
            ghost.m_renderer = go.AddComponent<SpriteRenderer>();
            ghost.m_renderer.sortingOrder = k_Order;
            ghost.m_renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            go.SetActive(false);
            return ghost;
        }

        public void Show(Sprite sprite, Vector3 position, bool ok)
        {
            m_renderer.sprite = sprite;
            m_renderer.color = ok ? k_Ok : k_Bad;
            transform.position = position;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
