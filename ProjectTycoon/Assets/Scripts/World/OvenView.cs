using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 오븐 하나. 굽는 빵 아이콘·진행 막대·다 구워 기다리는 개수. 탭 판정은 몸체 영역
    public sealed class OvenView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private SpriteRenderer m_icon;
        [SerializeField] private GameObject m_bar;
        [SerializeField] private Transform m_barFill;
        [SerializeField] private TextMeshPro m_readyText;

        public bool Contains(Vector3 world)
        {
            Bounds bounds = m_body.bounds;
            return world.x >= bounds.min.x && world.x <= bounds.max.x && world.y >= bounds.min.y && world.y <= bounds.max.y;
        }

        public void ShowBaking(Sprite icon, float progress, int ready)
        {
            m_body.color = Color.white;
            m_icon.enabled = true;
            m_icon.sprite = icon;
            m_bar.SetActive(ready == 0);
            m_barFill.localScale = new Vector3(Mathf.Clamp01(progress), 1f, 1f);
            m_readyText.enabled = ready > 0;
            m_readyText.text = $"x{ready}";
        }

        public void ShowEmpty()
        {
            m_body.color = Color.white;
            m_icon.enabled = false;
            m_bar.SetActive(false);
            m_readyText.enabled = false;
        }

        public void ShowLocked()
        {
            ShowEmpty();
            m_body.color = new Color(1f, 1f, 1f, 0.3f);
        }
    }
}
