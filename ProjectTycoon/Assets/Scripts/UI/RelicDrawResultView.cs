using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameKit.UI;
using GameKit.Audio;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 36 뽑기 결과: 뽑기 메인 위에 따로 뜬다. 카드 뒷면이 가로로 접혔다 앞면(유물 · 이름표 · 별 · 효과)으로 펴지고,
    // 새 별이 차오른 뒤 결과 글과 「유물 보기」 · 「한 번 더」 · 「확인」이 나온다(그동안 버튼은 자리만 남긴다). 공개 연출은 아트방 시안이 오면 바꾼다
    public sealed class RelicDrawResultView : UIView
    {
        private const float k_FlipSeconds = 0.3f;
        private const float k_StarDelay = 0.25f;

        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Sprite m_chip;
        [SerializeField] private Sprite m_chipSelected;
        [SerializeField] private RelicCard m_card;
        [SerializeField] private TextMeshProUGUI m_hint;
        [SerializeField] private Button m_viewButton;
        [SerializeField] private TextMeshProUGUI m_viewLabel;
        [SerializeField] private Button m_againButton;
        [SerializeField] private TextMeshProUGUI m_againLabel;
        [SerializeField] private TextMeshProUGUI m_againCost;
        [SerializeField] private Button m_okButton;
        [SerializeField] private TextMeshProUGUI m_okLabel;

        private Vector2 m_panelRest;
        private Coroutine m_fx;
        private Coroutine m_reveal;

        public event Action CloseRequested;
        public event Action ViewClicked;
        public event Action AgainClicked;

        public bool IsOpen => m_root.activeSelf;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_okButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_viewButton.onClick.AddListener(() => ViewClicked?.Invoke());
            m_againButton.onClick.AddListener(() => AgainClicked?.Invoke());
            m_card.Button.interactable = false;
            m_panelRest = m_panel.anchoredPosition;
            m_root.SetActive(false);
        }

        public void SetLabels(string view, string again, string ok)
        {
            m_viewLabel.text = view;
            m_againLabel.text = again;
            m_okLabel.text = ok;
        }

        // face: 뽑은 유물(새 별까지 든 앞면), stars: 새 별 번호(1부터). 열려 있으면(한 번 더) 그 자리에서 다시 뒤집는다
        public void Show(RelicCard.Data face, int stars, string hint, int cost, bool canDraw)
        {
            if (!m_root.activeSelf)
            {
                m_root.SetActive(true);
                Run(ref m_fx, UiFx.Appear(m_rootGroup, m_panel, m_panelRest, 0f));
                SoundManager.Instance.Play(SoundTable.k_UiOpen);
            }

            Run(ref m_reveal, Reveal(face, stars, hint, cost, canDraw));
        }

        public void Close()
        {
            if (!m_root.activeSelf)
            {
                return;
            }

            if (m_reveal != null)
            {
                StopCoroutine(m_reveal);
                m_reveal = null;
            }

            m_card.Root.localScale = Vector3.one;
            Run(ref m_fx, UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_UiClose);
        }

        private IEnumerator Reveal(RelicCard.Data face, int stars, string hint, int cost, bool canDraw)
        {
            SetFoot(false, cost, canDraw);
            m_hint.text = string.Empty;
            m_card.SetBack(m_chip);
            SoundManager.Instance.Play(SoundTable.k_StatueRoll);
            float half = k_FlipSeconds * 0.5f;
            bool flipped = false;
            Image star = m_card.Stars[Mathf.Clamp(stars, 1, m_card.Stars.Length) - 1];

            for (float t = 0f; t < k_FlipSeconds; t += Time.unscaledDeltaTime)
            {
                if (t >= half && !flipped)
                {
                    flipped = true;
                    m_card.Set(face, m_chip, m_chipSelected);
                    star.color = RelicCard.k_Shadow;
                }

                m_card.Root.localScale = new Vector3(Mathf.Clamp01(Mathf.Abs(t - half) / half), 1f, 1f);
                yield return null;
            }

            m_card.Root.localScale = Vector3.one;
            m_card.Set(face, m_chip, m_chipSelected);
            star.color = RelicCard.k_Shadow;
            yield return new WaitForSecondsRealtime(k_StarDelay);
            star.color = Color.white;
            SoundManager.Instance.Play(SoundTable.k_StatueBlessed);
            yield return UiFx.Pulse(star.rectTransform);
            yield return UiFx.Pulse(m_card.Root);
            m_hint.text = hint;
            SetFoot(true, cost, canDraw);
            m_reveal = null;
        }

        // 공개 동안 버튼은 숨기되 발치 줄은 자리를 지켜 팝업 높이가 그대로다
        private void SetFoot(bool shown, int cost, bool canDraw)
        {
            m_viewButton.gameObject.SetActive(shown);
            m_againButton.gameObject.SetActive(shown);
            m_okButton.gameObject.SetActive(shown);
            m_againCost.text = cost.ToString();
            m_againButton.interactable = canDraw;
        }

        private void Run(ref Coroutine slot, IEnumerator routine)
        {
            if (slot != null)
            {
                StopCoroutine(slot);
            }

            slot = StartCoroutine(routine);
        }
    }
}
