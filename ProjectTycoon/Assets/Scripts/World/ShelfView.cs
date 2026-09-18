using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 진열대 한 칸. 빵 아이콘 + 재고/용량. 아직 해금 안 된 칸은 비워 흐리게
    public sealed class ShelfView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private SpriteRenderer m_icon;
        [SerializeField] private TextMeshPro m_stockText;
        [Tooltip("손님이 서는 자리")]
        [SerializeField] private Transform m_standPoint;

        public Vector2 StandPoint => m_standPoint.position;

        public void Show(Sprite icon, int stock, int capacity)
        {
            m_body.color = Color.white;
            m_icon.enabled = true;
            m_icon.sprite = icon;
            m_stockText.enabled = true;
            m_stockText.text = $"{stock}/{capacity}";
            m_stockText.color = stock == 0 ? new Color(0.75f, 0.2f, 0.15f) : new Color(0.23f, 0.14f, 0.09f);
        }

        public void ShowLocked()
        {
            m_body.color = new Color(1f, 1f, 1f, 0.35f);
            m_icon.enabled = false;
            m_stockText.enabled = false;
        }
    }
}
