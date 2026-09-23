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
        [SerializeField] private Sprite m_baseBody;
        [SerializeField] private Sprite m_upgradedBody;

        public void SetLook(bool upgraded)
        {
            m_body.sprite = upgraded ? m_upgradedBody : m_baseBody;
        }

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }

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

        // 손님 동선 설계 v0.2: 진행 막대만 매 프레임(오븐 사건은 초가 바뀔 때만 온다)
        public void SetProgress(float progress)
        {
            m_barFill.localScale = new Vector3(Mathf.Clamp01(progress), 1f, 1f);
        }

        public void ShowEmpty()
        {
            m_body.color = Color.white;
            m_icon.enabled = false;
            m_bar.SetActive(false);
            m_readyText.enabled = false;
        }
    }
}
