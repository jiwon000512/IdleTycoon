using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 설계 08 v0.5: 제목 + 버튼 줄 목록 + 닫기. 굽기·업그레이드 팝업이 같은 모양이라 공유한다
    public abstract class ListPopupView : UIView
    {
        [SerializeField] private TMP_Text m_titleText;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TMP_Text m_closeText;
        [Tooltip("줄 하나의 틀. 꺼 둔 상태로 두고 복제한다")]
        [SerializeField] private Button m_rowTemplate;

        private readonly List<Button> m_rows = new List<Button>();

        public event Action<int> RowClicked;
        public event Action CloseClicked;

        private void Awake()
        {
            m_closeButton.onClick.AddListener(CloseButton_Clicked);
        }

        public void SetTexts(string title, string close)
        {
            m_titleText.text = title;
            m_closeText.text = close;
        }

        public void SetRows(IReadOnlyList<string> labels, IReadOnlyList<bool> interactable)
        {
            while (m_rows.Count < labels.Count)
            {
                int index = m_rows.Count;
                Button row = Instantiate(m_rowTemplate, m_rowTemplate.transform.parent);
                row.transform.SetSiblingIndex(m_rowTemplate.transform.GetSiblingIndex() + 1 + index);
                row.onClick.AddListener(() => RowClicked?.Invoke(index));
                m_rows.Add(row);
            }

            for (int i = 0; i < m_rows.Count; i++)
            {
                bool used = i < labels.Count;
                m_rows[i].gameObject.SetActive(used);

                if (used)
                {
                    m_rows[i].GetComponentInChildren<TMP_Text>().text = labels[i];
                    m_rows[i].interactable = interactable[i];
                }
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void CloseButton_Clicked()
        {
            CloseClicked?.Invoke();
        }
    }
}
