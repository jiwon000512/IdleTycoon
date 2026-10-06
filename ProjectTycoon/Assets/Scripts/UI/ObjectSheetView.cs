using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;
using GameKit.Audio;
using ZooTycoon.Core;

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
        // 칩 시안 A(2026-10-01): 오른쪽 위 개수 배지(진열 재고 · 창고 개수, 해금 칩은 null). 0이면 배지가 강조색
        public string Count;
        public bool CountEmpty;
        // 아래 줄: 시계 + 걸리는 초, 값 칩은 대신 코인 값(Cost, 모자라면 CostPoor)이고 해금 칩이면 자물쇠(Locked)도
        public string Seconds;
        public string Cost;
        public bool CostPoor;
        public bool Locked;
        public bool Highlighted;
        public bool Enabled = true;
        // 설계 25: 레시피(칩 왼쪽 위에 재료마다 한 줄)
        public List<ChipIngredient> Ingredients = new List<ChipIngredient>();
    }

    public sealed class ChipIngredient
    {
        public string IconPath;
        public string Count;
        // 창고에 모자라면 부족 색
        public bool Short;
    }

    public sealed class SheetRow
    {
        public string Name;
        public string Effect;
        public string Cost;
        public SheetRowState State;
    }

    // 사물 터치 기획(2026-09-23): 하단 시트 한 벌. 헤더(이름·상태·닫기) + 칩 줄(선택) + 행 목록(구매).
    // 모양은 UI 규칙(.claude/rules/ui.md), 프리팹은 Resources/UI/ObjectSheetView. 표시와 입력 이벤트만 갖고 규칙은 Presenter에
    public sealed class ObjectSheetView : UIView
    {
        private const float k_OpenSeconds = 0.15f;
        private const float k_CloseSeconds = 0.10f;
        private const float k_FlashSeconds = 0.15f;
        // 레시피 줄이 없는 칩(작물)의 큰 아이콘 가운데: 칩 윗변에서(시안 A의 밀 · 딸기 자리)
        private const float k_NoRecipeIconCenter = 100f;
        private static readonly Color k_Flash = Color.white;
        private static readonly Color k_CostOk = new Color32(0x3E, 0x7A, 0x4C, 255);
        private static readonly Color k_CostPoor = new Color32(0xA6, 0x4B, 0x3C, 255);
        private static readonly Color k_CostMuted = new Color32(0x9A, 0x8A, 0x7C, 255);
        private static readonly Color k_CostValue = new Color32(0x2E, 0x23, 0x20, 255);
        // 진갈색 배지: 시안은 85%였지만 선형 색 공간에서는 크림 바탕이 더 비쳐 밝아지므로 95%로 시안 색(76,57,53)에 맞춘다
        private static readonly Color k_Badge = new Color32(0x34, 0x20, 0x20, 242);
        private static readonly Color k_BadgeEmpty = new Color32(0xD8, 0x78, 0x48, 255);
        private static readonly Color k_Sub = new Color32(0x5C, 0x4C, 0x42, 255);

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
        // 칩 큰 아이콘의 윗변 자리(프리팹 값)
        private float m_iconTop;

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
            m_iconTop = ((RectTransform)m_chipTemplate.transform.Find("Icon")).anchoredPosition.y;
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
                button.interactable = chip.Enabled;
                Image icon = button.transform.Find("Icon").GetComponent<Image>();
                Sprite sprite = chip.SpritePath != null ? Resources.Load<Sprite>(chip.SpritePath) : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;

                if (sprite != null)
                {
                    // 월드 스프라이트(한 칸 2px)를 UI 픽셀(화면 4px)에 맞춘다: 1칸 = 4 캔버스 px
                    icon.rectTransform.sizeDelta = new Vector2(sprite.rect.width * 2f, sprite.rect.height * 2f);
                }

                // 칩 시안 A: 레시피 줄이 없는 칩(작물)은 큰 아이콘을 이름 위 빈자리 가운데로 내린다(한 칸 4px 격자에 맞춰 위로 내림)
                float top = chip.Ingredients.Count == 0 && sprite != null ? -Mathf.Floor((k_NoRecipeIconCenter - sprite.rect.height) / 4f) * 4f : m_iconTop;
                icon.rectTransform.anchoredPosition = new Vector2(0f, top);

                button.transform.Find("Label").GetComponent<TMP_Text>().text = chip.Label;
                SetBadge(button.transform.Find("Badge"), chip);
                SetInfo(button.transform.Find("Info"), chip);
                SetRecipe(button.transform.Find("Recipe"), chip.Ingredients);
                CanvasGroup group = button.GetComponent<CanvasGroup>();
                group.alpha = chip.Enabled ? 1f : 0.5f;
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
                TMP_Text cost = button.transform.Find("Price/Cost").GetComponent<TMP_Text>();
                cost.text = row.Cost;
                // 설계 44: 값이 없는 줄(대물 뱃속 3택 1)은 코인 칸을 숨긴다
                button.transform.Find("Price").gameObject.SetActive(!string.IsNullOrEmpty(row.Cost));
                cost.color = row.State == SheetRowState.Enabled ? k_CostOk : row.State == SheetRowState.Poor ? k_CostPoor : k_CostMuted;
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
                SoundManager.Instance.Play(SoundTable.k_UiOpen);
            }
        }

        public void Close()
        {
            if (m_panel.gameObject.activeSelf)
            {
                Slide(m_panel.anchoredPosition.y, m_hiddenY, k_CloseSeconds, true);
                SoundManager.Instance.Play(SoundTable.k_UiClose);
            }
        }

        public void FlashRow(int index)
        {
            if (index < m_rows.Count)
            {
                StartCoroutine(FlashRoutine(m_rows[index].image));
            }
        }

        // 레시피 줄(Icon + Count)을 재료 수만큼. 첫 자식이 틀이고 모자라면 복제한다
        private static void SetBadge(Transform badge, SheetChip chip)
        {
            badge.gameObject.SetActive(chip.Count != null);

            if (chip.Count != null)
            {
                badge.GetComponent<Image>().color = chip.CountEmpty ? k_BadgeEmpty : k_Badge;
                badge.Find("Count").GetComponent<TMP_Text>().text = chip.Count;
            }
        }

        private static void SetInfo(Transform info, SheetChip chip)
        {
            bool priced = chip.Cost != null;
            info.Find("Clock").gameObject.SetActive(!priced);
            info.Find("Seconds").gameObject.SetActive(!priced);
            info.Find("Lock").gameObject.SetActive(priced && chip.Locked);
            info.Find("CoinValue").gameObject.SetActive(priced);

            if (priced)
            {
                TMP_Text cost = info.Find("CoinValue/Cost").GetComponent<TMP_Text>();
                cost.text = chip.Cost;
                cost.color = chip.CostPoor ? k_CostPoor : k_CostValue;
            }
            else
            {
                info.Find("Seconds").GetComponent<TMP_Text>().text = chip.Seconds;
            }
        }

        private static void SetRecipe(Transform recipe, IReadOnlyList<ChipIngredient> ingredients)
        {
            Transform template = recipe.GetChild(0);

            while (recipe.childCount < ingredients.Count)
            {
                Instantiate(template, recipe);
            }

            for (int i = 0; i < recipe.childCount; i++)
            {
                Transform line = recipe.GetChild(i);
                line.gameObject.SetActive(i < ingredients.Count);

                if (i >= ingredients.Count)
                {
                    continue;
                }

                line.Find("Icon").GetComponent<Image>().sprite = Resources.Load<Sprite>(ingredients[i].IconPath);
                TMP_Text count = line.Find("Count").GetComponent<TMP_Text>();
                count.text = ingredients[i].Count;
                count.color = ingredients[i].Short ? k_CostPoor : k_Sub;
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
