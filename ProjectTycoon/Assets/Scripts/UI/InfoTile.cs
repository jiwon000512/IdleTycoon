using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 41 아이콘 칸: 아이콘 · 값 · (이름) · (체크). 평가판 · 소식지의 칸과 상단 평가 카드의 조건이 같이 쓴다(배치는 프리팹마다).
    // 아이콘은 고정 상자(부모, 칸마다 같은 높이라 값 줄이 맞는다) 가운데에 원본 픽셀의 정수 배로(상자보다 크면 줄인다, 최대 m_maxScale배)
    public sealed class InfoTile : MonoBehaviour
    {
        public enum State
        {
            Normal,
            Done,
            Bad,
        }

        // 칸 하나: 아이콘 id(뷰 아이콘 목록) 또는 Resources 경로 · 값 · 이름(없으면 null) · 상태
        public sealed class Data
        {
            public string Icon;
            public string Value;
            public string Caption;
            public State State;
        }

        // 뷰가 이름으로 찾는 아이콘(Resources 밖 그림: 말풍선 칸 · 사물 · UI 조각)
        [Serializable]
        public sealed class IconRef
        {
            public string Id;
            public Sprite Sprite;
        }

        [SerializeField] private Image m_icon;
        [SerializeField] private TMP_Text m_value;
        [SerializeField] private TMP_Text m_caption;
        [SerializeField] private GameObject m_check;
        [Tooltip("아이콘 상자(화면 px)")]
        [SerializeField] private Vector2 m_box = new Vector2(108f, 80f);
        [SerializeField] private int m_maxScale = 4;
        [SerializeField] private Color m_normal = new Color32(0x2E, 0x23, 0x20, 0xFF);
        [SerializeField] private Color m_done = new Color32(0x3E, 0x7A, 0x4C, 0xFF);
        [SerializeField] private Color m_bad = new Color32(0xA6, 0x4B, 0x3C, 0xFF);

        public static Sprite Find(IReadOnlyList<IconRef> icons, string id)
        {
            foreach (IconRef icon in icons)
            {
                if (icon.Id == id)
                {
                    return icon.Sprite;
                }
            }

            return Resources.Load<Sprite>(id);
        }

        public void Show(Sprite icon, Data data)
        {
            SetIcon(icon);

            if (m_caption != null)
            {
                m_caption.text = data.Caption ?? string.Empty;
                m_caption.gameObject.SetActive(!string.IsNullOrEmpty(data.Caption));
            }

            SetValue(data.Value, data.State);
        }

        public void SetIcon(Sprite icon)
        {
            m_icon.sprite = icon;
            Vector2 size = icon != null ? icon.rect.size : Vector2.zero;

            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            float fit = Mathf.Min(m_box.x / size.x, m_box.y / size.y);
            float scale = fit >= 1f ? Mathf.Min(Mathf.Floor(fit), m_maxScale) : fit;
            m_icon.rectTransform.sizeDelta = size * scale;
        }

        public void SetValue(string value, State state)
        {
            m_value.text = value;
            m_value.color = state == State.Done ? m_done : state == State.Bad ? m_bad : m_normal;

            if (m_check != null)
            {
                m_check.SetActive(state == State.Done);
            }
        }
    }
}
