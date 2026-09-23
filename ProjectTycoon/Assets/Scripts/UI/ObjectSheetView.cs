using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    public enum SheetRowState
    {
        Enabled,
        Poor,
        Max,
        Blocked,
    }

    public sealed class SheetChip
    {
        public string SpritePath;
        public string Label;
        public string Sub;
        public bool Highlighted;
        public bool Locked;
        public bool Enabled = true;
    }

    public sealed class SheetRow
    {
        public string Name;
        public string Effect;
        public string Cost;
        public SheetRowState State;
    }

    // 사물 터치 기획(2026-09-23): 하단 시트 한 벌. 헤더(이름·상태·닫기) + 칩 줄(선택) + 행 목록(구매).
    // 모양은 UI-디자인-규칙 v1.0, 프리팹은 UIBaker가 굽는다. 표시와 입력 이벤트만 갖고 규칙은 Presenter에
    public sealed class ObjectSheetView : UIView
    {
        private const float k_OpenSeconds = 0.15f;
        private const float k_CloseSeconds = 0.10f;
        private const float k_FlashSeconds = 0.15f;
        private static readonly Color k_Flash = Color.white;
        private static readonly Color k_CostOk = new Color32(0x3E, 0x7A, 0x4C, 255);
        private static readonly Color k_CostPoor = new Color32(0xA6, 0x4B, 0x3C, 255);
        private static readonly Color k_CostMuted = new Color32(0x9A, 0x8A, 0x7C, 255);

        [SerializeField] private Button m_dim;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private TMP_Text m_titleText;
        [SerializeField] private TMP_Text m_statusText;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private RectTransform m_chipRow;
        [SerializeField] private Button m_chipTemplate;
        [SerializeField] private Sprite m_chipSprite;
        [SerializeField] private Sprite m_chipHighlightSprite;
        [SerializeField] private RectTransform m_rowList;
        [SerializeField] private Button m_rowTemplate;

        private readonly List<Button> m_chips = new List<Button>();
        private readonly List<Button> m_rows = new List<Button>();
        private Coroutine m_slide;
        private float m_hiddenY;

        public event Action<int> ChipClicked;
        public event Action<int> RowClicked;
        public event Action CloseRequested;

        public bool IsVisible => m_panel.gameObject.activeSelf;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_chipTemplate.gameObject.SetActive(false);
            m_rowTemplate.gameObject.SetActive(false);
        }

        public void SetHeader(string title, string status)
        {
            m_titleText.text = title;
            m_statusText.text = status;
        }

        public void SetChips(IReadOnlyList<SheetChip> chips)
        {
            m_chipRow.gameObject.SetActive(chips.Count > 0);
            Fill(m_chips, m_chipTemplate, chips.Count, i => ChipClicked?.Invoke(i));

            for (int i = 0; i < chips.Count; i++)
            {
                SheetChip chip = chips[i];
                Button button = m_chips[i];
                button.image.sprite = chip.Highlighted ? m_chipHighlightSprite : m_chipSprite;
                button.interactable = chip.Enabled && !chip.Locked;
                Image icon = button.transform.Find("Icon").GetComponent<Image>();
                Sprite sprite = chip.SpritePath != null ? Resources.Load<Sprite>(chip.SpritePath) : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;

                if (sprite != null)
                {
                    // 월드 스프라이트(한 칸 2px)를 UI 픽셀(화면 4px)에 맞춘다: 1칸 = 4 캔버스 px
                    icon.rectTransform.sizeDelta = new Vector2(sprite.rect.width * 2f, sprite.rect.height * 2f);
                }

                button.transform.Find("Label").GetComponent<TMP_Text>().text = chip.Label;
                button.transform.Find("Sub").GetComponent<TMP_Text>().text = chip.Sub;
                CanvasGroup group = button.GetComponent<CanvasGroup>();
                group.alpha = chip.Locked || !chip.Enabled ? 0.5f : 1f;
            }
        }

        public void SetRows(IReadOnlyList<SheetRow> rows)
        {
            Fill(m_rows, m_rowTemplate, rows.Count, i => RowClicked?.Invoke(i));

            for (int i = 0; i < rows.Count; i++)
            {
                SheetRow row = rows[i];
                Button button = m_rows[i];
                button.interactable = row.State == SheetRowState.Enabled;
                button.transform.Find("Name").GetComponent<TMP_Text>().text = row.Name;
                button.transform.Find("Effect").GetComponent<TMP_Text>().text = row.Effect;
                TMP_Text cost = button.transform.Find("Pill/Cost").GetComponent<TMP_Text>();
                Image pill = button.transform.Find("Pill").GetComponent<Image>();
                cost.text = row.Cost;
                cost.color = row.State == SheetRowState.Enabled ? k_CostOk : row.State == SheetRowState.Poor ? k_CostPoor : k_CostMuted;
                pill.enabled = row.State != SheetRowState.Blocked;
                button.GetComponent<CanvasGroup>().alpha = row.State == SheetRowState.Poor ? 0.55f : row.State == SheetRowState.Enabled ? 1f : 0.7f;
            }
        }

        public void Open()
        {
            if (!m_panel.gameObject.activeSelf)
            {
                m_dim.gameObject.SetActive(true);
                m_panel.gameObject.SetActive(true);
                Slide(m_hiddenY, 0f, k_OpenSeconds, false);
            }
        }

        public void Close()
        {
            if (m_panel.gameObject.activeSelf)
            {
                Slide(m_panel.anchoredPosition.y, m_hiddenY, k_CloseSeconds, true);
            }
        }

        public void FlashRow(int index)
        {
            if (index < m_rows.Count)
            {
                StartCoroutine(FlashRoutine(m_rows[index].image));
            }
        }

        private void Fill(List<Button> pool, Button template, int count, Action<int> clicked)
        {
            while (pool.Count < count)
            {
                int index = pool.Count;
                Button item = Instantiate(template, template.transform.parent);
                item.onClick.AddListener(() => clicked(index));
                pool.Add(item);
            }

            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(i < count);
            }
        }

        private void Slide(float from, float to, float seconds, bool hideAfter)
        {
            if (m_slide != null)
            {
                StopCoroutine(m_slide);
            }

            m_slide = StartCoroutine(SlideRoutine(from, to, seconds, hideAfter));
        }

        private IEnumerator SlideRoutine(float from, float to, float seconds, bool hideAfter)
        {
            if (m_hiddenY == 0f)
            {
                // 첫 열기: 패널 높이가 잡힌 뒤 숨김 위치(패널 높이만큼 아래)를 정한다
                Canvas.ForceUpdateCanvases();
                m_hiddenY = -m_panel.rect.height;
                from = from == 0f && !hideAfter ? m_hiddenY : from;
            }

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / seconds, 2f);
                m_panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, k));
                yield return null;
            }

            m_panel.anchoredPosition = new Vector2(0f, to);

            if (hideAfter)
            {
                m_panel.gameObject.SetActive(false);
                m_dim.gameObject.SetActive(false);
            }

            m_slide = null;
        }

        private IEnumerator FlashRoutine(Image image)
        {
            Color original = image.color;
            image.color = k_Flash;
            yield return new WaitForSecondsRealtime(k_FlashSeconds);
            image.color = original;
        }
    }
}
