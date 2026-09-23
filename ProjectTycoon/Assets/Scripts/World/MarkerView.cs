using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 굴 격자 설계 v0.5: 칸 위 표시(파기 비용 태그·빈 자리 점선). 글자 하나와 튀는 몸체(m_body)만 갖는다
    public sealed class MarkerView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private TextMeshPro m_text;

        public void Show(string text)
        {
            m_text.text = text;
            gameObject.SetActive(true);
        }

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }
    }
}
