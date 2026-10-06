using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameKit.UI;
using GameKit.Audio;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 50 낚시 소식: 대물을 낚으면 뜨는 결과 화면(굴 소식과 같은 소식지 틀). 제호 · 제목 · 대물 그림 · 한 줄 · 얻은 것 칸 · 버튼.
    // 닫기가 따로 없다: 어둠 · 버튼 어느 쪽을 눌러도 확인이고, 확인해야 다음 단계가 시작된다
    public sealed class FishingNewsView : UIView
    {
        // 대물 그림은 원본 칸 2px → UI 칸 4px(정수 배)
        private const float k_PictureScale = 2f;

        public sealed class Data
        {
            public string Masthead;
            public string Title;
            // 대물 그림(Resources 경로)
            public string Picture;
            public string Line;
            public string BoxLabel;
            public IReadOnlyList<InfoTile.Data> Rewards;
            // 칸으로 못 그리는 것(다음 단계에 열리는 낚싯대 · 재료 배수), 없으면 숨김
            public string Extra;
            public string Button;
        }

        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private Button m_dim;
        [SerializeField] private RectTransform m_news;
        [SerializeField] private TextMeshProUGUI m_masthead;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private Image m_picture;
        [SerializeField] private TextMeshProUGUI m_line;
        [SerializeField] private TextMeshProUGUI m_boxLabel;
        [SerializeField] private RectTransform m_rewards;
        [SerializeField] private TextMeshProUGUI m_extra;
        [SerializeField] private Button m_button;
        [SerializeField] private TextMeshProUGUI m_buttonLabel;

        private Vector2 m_rest;
        private Coroutine m_fx;

        public event Action Confirmed;

        public bool IsOpen => m_root.activeSelf;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => Confirmed?.Invoke());
            m_button.onClick.AddListener(() => Confirmed?.Invoke());
            m_rest = m_news.anchoredPosition;
            m_root.SetActive(false);
        }

        // delay(게임 시간) 뒤에 뜬다: 낚인 대물이 월드에서 드러나 있는 동안 기다린다
        public void Open(float delay)
        {
            if (!m_root.activeSelf)
            {
                Run(OpenAfter(delay));
            }
        }

        // 아직 뜨지 않은 소식지를 거둔다(드러나는 동안 낚시터를 나갔다)
        public void Cancel()
        {
            if (!m_root.activeSelf && m_fx != null)
            {
                StopCoroutine(m_fx);
                m_fx = null;
            }
        }

        public void Close()
        {
            if (m_root.activeSelf)
            {
                Run(UiFx.Vanish(m_rootGroup, m_root));
                SoundManager.Instance.Play(SoundTable.k_UiClose);
            }
        }

        public void Show(Data page)
        {
            m_masthead.text = page.Masthead;
            m_title.text = page.Title;
            m_picture.sprite = Resources.Load<Sprite>(page.Picture);
            m_picture.rectTransform.sizeDelta = m_picture.sprite.rect.size * k_PictureScale;
            m_line.text = page.Line;
            m_boxLabel.text = page.BoxLabel;
            InfoTile.Fill(m_rewards, page.Rewards, Array.Empty<InfoTile.IconRef>());
            m_extra.text = page.Extra ?? string.Empty;
            m_extra.gameObject.SetActive(!string.IsNullOrEmpty(page.Extra));
            m_buttonLabel.text = page.Button;
        }

        private IEnumerator OpenAfter(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            m_root.SetActive(true);
            SoundManager.Instance.Play(SoundTable.k_UiOpen);
            StartCoroutine(UiFx.Pulse(m_picture.rectTransform));
            yield return UiFx.Appear(m_rootGroup, m_news, m_rest, 0f);
        }

        private void Run(IEnumerator routine)
        {
            if (m_fx != null)
            {
                StopCoroutine(m_fx);
            }

            m_fx = StartCoroutine(routine);
        }
    }
}
