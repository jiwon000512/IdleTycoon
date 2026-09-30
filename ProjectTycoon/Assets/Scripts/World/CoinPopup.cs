using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 06 P8: 머리 위에서 떠올라 사라지는 코인 연출. 연출 1차: 금액 글자("+10")를 함께 띄운다. 설계 25: 코인 대신 재료 아이콘(거두기)
    // 획득 팝업은 서로 겹치지 않는다(2026-09-30 사용자): 같은 부모 아래 떠 있는 팝업과 겹치면 그 위로 쌓는다.
    // 모두 같은 속도로 떠오르므로 한 번 벌려 두면 사라질 때까지 겹치지 않는다
    public sealed class CoinPopup : MonoBehaviour
    {
        private const float k_StackGap = 0.05f;

        [SerializeField] private SpriteRenderer m_renderer;
        [SerializeField] private TextMeshPro m_amountText;
        [SerializeField] private float m_riseHeight = 0.8f;
        [SerializeField] private float m_seconds = 0.7f;

        private float m_elapsed;

        public void Show(string amount, Sprite icon = null)
        {
            m_amountText.text = amount;

            if (icon != null)
            {
                m_renderer.sprite = icon;
            }

            m_amountText.ForceMeshUpdate();
            Stack();
        }

        // 겹치는 형제 팝업이 없을 때까지 그 윗변 위로 올린다(위로만 가므로 끝난다)
        private void Stack()
        {
            bool moved = true;

            while (moved)
            {
                moved = false;
                Bounds mine = Area();

                foreach (Transform sibling in transform.parent)
                {
                    if (sibling == transform || !sibling.TryGetComponent(out CoinPopup other))
                    {
                        continue;
                    }

                    Bounds theirs = other.Area();

                    if (mine.min.x < theirs.max.x && theirs.min.x < mine.max.x && mine.min.y < theirs.max.y && theirs.min.y < mine.max.y)
                    {
                        transform.position += Vector3.up * (theirs.max.y - mine.min.y + k_StackGap);
                        moved = true;
                        break;
                    }
                }
            }
        }

        // 그림 + 글자가 차지하는 월드 영역
        private Bounds Area()
        {
            Bounds bounds = m_renderer.bounds;
            bounds.Encapsulate(m_amountText.renderer.bounds);
            return bounds;
        }

        private void Update()
        {
            m_elapsed += Time.deltaTime;
            transform.position += Vector3.up * (m_riseHeight * Time.deltaTime / m_seconds);

            float alpha = 1f - m_elapsed / m_seconds;
            Color color = m_renderer.color;
            color.a = alpha;
            m_renderer.color = color;
            Color textColor = m_amountText.color;
            textColor.a = alpha;
            m_amountText.color = textColor;

            if (m_elapsed >= m_seconds)
            {
                Destroy(gameObject);
            }
        }
    }
}
