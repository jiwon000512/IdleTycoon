using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    public enum ClerkBadge
    {
        None,
        Working,
        Idling,
        Away,
        Empty,
    }

    // 점원 UI 폴리싱(2026-09-26 시안 D1 명찰 카드): 왼쪽 초상 틀(빈 자리는 점선 틀 + 「+」)과 그 아래 상태 배지, 가운데 이름(굵게) + 자리 알약 + 두 칸 표(ClerkStatView), 오른쪽 세로로 긴 버튼.
    // 정보 하나에 글 하나 — 「·」로 이어 붙이지 않는다
    public sealed class ClerkRowView : MonoBehaviour
    {
        private static readonly Color k_Working = new Color32(0x5D, 0x76, 0x4D, 255);
        private static readonly Color k_Idling = new Color32(0xD8, 0x78, 0x48, 255);
        private static readonly Color k_Away = new Color32(0x5B, 0x5A, 0xA8, 255);
        private static readonly Color k_Empty = new Color32(0x93, 0x83, 0x7C, 255);

        [SerializeField] private Image m_portrait;
        [SerializeField] private Button m_emptyFrame;   // 빈 자리 점선 틀: 눌러도 줄 버튼(고용)과 같다
        [SerializeField] private Image m_badge;
        [SerializeField] private TextMeshProUGUI m_badgeText;
        [SerializeField] private TextMeshProUGUI m_name;
        [SerializeField] private GameObject m_slotPill;
        [SerializeField] private TextMeshProUGUI m_slotText;
        [SerializeField] private ClerkStatView m_stat;
        [SerializeField] private Button m_button;
        [SerializeField] private TextMeshProUGUI m_buttonLabel;

        public event Action<ClerkRowView> ButtonClicked;

        private void Awake()
        {
            m_button.onClick.AddListener(() => ButtonClicked?.Invoke(this));
            m_emptyFrame.onClick.AddListener(() => ButtonClicked?.Invoke(this));
        }

        public void Show(ClerkPopupView.RowData data, Sprite icon)
        {
            m_portrait.sprite = icon;
            m_portrait.enabled = icon != null;
            m_emptyFrame.gameObject.SetActive(icon == null);
            m_badge.gameObject.SetActive(data.Badge != ClerkBadge.None);
            m_badge.color = BadgeColor(data.Badge);
            m_badgeText.text = data.BadgeText ?? string.Empty;
            m_name.text = data.Name;
            m_slotPill.SetActive(data.Slot != null);
            m_slotText.text = data.Slot ?? string.Empty;

            if (data.Skill >= 0)
            {
                m_stat.Show(data.SkillHeader, data.WageHeader, data.Skill, data.Wage);
            }
            else
            {
                m_stat.Hide();
            }

            m_buttonLabel.text = data.Button;
            m_button.interactable = data.Enabled;
            m_emptyFrame.interactable = data.Enabled;
            gameObject.SetActive(true);
        }

        private static Color BadgeColor(ClerkBadge badge)
        {
            switch (badge)
            {
                case ClerkBadge.Working: return k_Working;
                case ClerkBadge.Idling: return k_Idling;
                case ClerkBadge.Away: return k_Away;
                default: return k_Empty;
            }
        }
    }
}
