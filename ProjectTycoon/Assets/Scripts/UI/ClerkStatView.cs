using TMPro;
using UnityEngine;

namespace ZooTycoon.UI
{
    // 점원 UI 폴리싱(2026-09-26 시안 D1): 두 칸 표 — 머리글(일머리 · 월급) 아래 붉은 일머리 게이지 + 숫자 / 코인 + 월급. 자리 줄·후보 줄·고용할까 창이 같이 쓴다
    public sealed class ClerkStatView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_skillHeader;
        [SerializeField] private TextMeshProUGUI m_wageHeader;
        [SerializeField] private RectTransform m_gaugeFill;
        [Tooltip("게이지 안쪽 폭(캔버스 px). 굽기가 넣는다")]
        [SerializeField] private float m_gaugeWidth;
        [SerializeField] private TextMeshProUGUI m_skillText;
        [SerializeField] private TextMeshProUGUI m_wageText;

        public void Show(string skillHeader, string wageHeader, int skill, string wage)
        {
            m_skillHeader.text = skillHeader;
            m_wageHeader.text = wageHeader;
            m_gaugeFill.sizeDelta = new Vector2(Mathf.Round(m_gaugeWidth * Mathf.Clamp01(skill / 100f)), m_gaugeFill.sizeDelta.y);
            m_skillText.text = skill.ToString();
            m_wageText.text = wage;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
