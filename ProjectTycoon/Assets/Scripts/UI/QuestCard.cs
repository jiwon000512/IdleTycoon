using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 55: 할 일 카드(위 가운데). 카드 자체가 버튼 — 진행 중이면 누르면 가기, 다 하면 누르면 받기.
    // 그림 칸 · 이름, 아랫줄에 진행 점(여러 번 세는 걸음만) · 보상 캡슐(늘 보임: 진행 중 크림, 다 하면 금빛 + 그림 칸 체크 + 「!」)
    // (2026-10-12 사용자 A3-나 + 캡슐 A 「금빛 알약」). 새 할 일이 뜨면 카드가 톡 튀고 테두리 빛줄기가 한 바퀴, 안내 중(길잡이가 켜진 동안)은 빛줄기가 계속 돈다.
    // 받기: 완료 도장이 카드 한가운데에 쾅 찍히는 순간 코인 여섯 개가 도장 자리에서 튀어나와 코인 알약으로 날아가고, 카드가 흐려진 뒤 다음 할 일이 떠오른다
    // (2026-10-09 사용자 받-2 + 받-1, 시간표는 아트방). 위 가운데는 평가 카드가 먼저다
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class QuestCard : MonoBehaviour
    {
        // 한 걸음의 모습(QuestPresenter가 채운다). Coins · Extra가 null이면 그 칸을 숨긴다
        public sealed class Data
        {
            public Sprite Icon;
            public string Text;
            public int Have;
            public int Count;
            // 진행 점보다 많이 세는 걸음만 점 대신 글(「2/10」)로
            public string Progress;
            public bool Done;
            public string Coins;
            public Sprite ExtraIcon;
            public string Extra;
        }

        // 움직임은 4px(UI 한 칸) 단위. 폭은 2칸 단위(가운데 기준이라 양 끝이 칸에 맞는다). UI 조각 · 아이콘의 PPU(한 텍셀 4px)
        private const float k_Px = 4f;
        private const float k_UiIconPpu = 25f;
        // 할 일 그림(Resources/Sprites/Quests, PPU 100/3 = 한 텍셀 3px)은 이보다 작은 PPU
        private const float k_QuestIconPpu = 50f;
        private const float k_WidthStep = 8f;
        // 캡슐 안 덤 그림이 이보다 크면 반으로(캡슐 높이 56 안에)
        private const float k_ExtraMaxHeight = 48f;
        // 빛줄기: 한 바퀴 초 · 틀 가장자리에서 주황 띠까지 · 점 크기(유닛, 틀 quest_card 한 칸)
        private const float k_SweepSeconds = 1.6f;
        private const float k_SweepInset = 4f;
        private const float k_Dot = 4f;
        // 받기 연출(초, 아트방 시간표): 한 박 · 도장이 나타남 · 쾅(코인이 둘씩 한 박 간격으로 떠남) · 나는 시간 · 카드가 흐려짐 · 다음 할 일로 바뀜 · 떠오르는 시간 · 끝
        private const float k_Beat = 0.08f;
        private const float k_StampAt = 0.08f;
        private const float k_Land = k_StampAt + 2f * k_Beat;
        private const float k_Flight = 0.64f;
        private const float k_FadeAt = 1.12f;
        private const float k_Turn = k_FadeAt + k_Beat;
        private const float k_RiseSeconds = 0.15f;
        private const float k_End = k_Turn + k_RiseSeconds;
        // 도장 크기 · 진하기(박마다), 쾅 할 때 카드가 눌리는 폭, 다음 카드가 떠오르는 높이(UiFx.Appear와 같다)
        // 1.5배: 한 칸이 6px로 고르다(1.6배는 6 · 7px로 들쭉날쭉, 아트방)
        private static readonly float[] k_StampScale = { 1.5f, 1.25f, 1f };
        private static readonly float[] k_StampAlpha = { 0.5f, 0.8f, 1f };
        private const float k_Dip = 4f;
        private const float k_Rise = 40f;
        // 코인: 수 · 흩어지는 거리와 각도(아래쪽 부채꼴) · 닿을 때 코인 알약이 톡 튀는 폭과 시간(UiFx.Pulse와 같다)
        private const int k_Coins = 6;
        private const float k_Spread = 150f;
        private const float k_SpreadFrom = 200f;
        private const float k_SpreadTo = 340f;
        private const float k_Kick = 0.12f;
        private const float k_KickSeconds = 0.18f;
        // 캡슐 글자: 진행 중(보조) · 다 함(글자)
        private static readonly Color k_InkOff = new Color32(0x5C, 0x4C, 0x42, 0xFF);
        private static readonly Color k_InkOn = new Color32(0x2E, 0x23, 0x20, 0xFF);

        [SerializeField] private Button m_button;
        [SerializeField] private Image m_icon;
        [SerializeField] private TMP_Text m_text;
        [Tooltip("진행 점 줄과 점(왼쪽부터 찬 점). 점보다 많이 세는 걸음은 점 대신 m_value 글")]
        [SerializeField] private GameObject m_pipRow;
        [SerializeField] private Image[] m_pips;
        [SerializeField] private Sprite m_pipOff;
        [SerializeField] private Sprite m_pipOn;
        [SerializeField] private TMP_Text m_value;
        [Tooltip("다 하면 그림 칸 오른쪽 아래 체크")]
        [SerializeField] private GameObject m_check;
        [Tooltip("보상 캡슐(늘 보임). 도장이 찍히면 투명하게(자리는 남겨 카드 폭이 그대로)")]
        [SerializeField] private CanvasGroup m_reward;
        [SerializeField] private Image m_rewardFrame;
        [SerializeField] private Sprite m_rewardOff;
        [SerializeField] private Sprite m_rewardOn;
        [SerializeField] private Image m_rewardCoin;
        [SerializeField] private TMP_Text m_rewardValue;
        [Tooltip("덤(반짝돌 · 물고기) 그림과 수. 그림 크기는 LayoutElement로")]
        [SerializeField] private Image m_extraIcon;
        [SerializeField] private TMP_Text m_extraValue;
        [SerializeField] private GameObject m_badge;
        [Tooltip("테두리 빛줄기(자식 점, 머리부터)")]
        [SerializeField] private RectTransform m_sweep;
        [Tooltip("완료 도장(동그라미 테 quest_stamp + 글), 카드 한가운데")]
        [SerializeField] private CanvasGroup m_stamp;
        [SerializeField] private TMP_Text m_stampText;
        [Tooltip("코인이 날아갈 곳(코인 알약의 코인)과 닿을 때 톡 튀는 알약")]
        [SerializeField] private RectTransform m_coinTarget;
        [SerializeField] private RectTransform m_coinPill;

        private Vector2 m_rest;
        private bool m_hidden = true;
        private bool m_blocked;
        private bool m_shown;
        private bool m_isDone;
        private bool m_tracking;
        private string m_lastText;
        // 새 할 일 빛줄기 한 바퀴가 끝나는 때(unscaledTime). 바퀴 머리도 이때를 기준으로 돈다
        private float m_lapEnd = float.NegativeInfinity;
        private Coroutine m_rewarding;
        // 받는 연출이 뒤집히기 전까지 Show · Hide를 미룬다(마지막 것 하나)
        private bool m_holding;
        private Action m_pending;
        private RectTransform[] m_coins;

        public event Action Clicked;

        private RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            m_rest = Rect.anchoredPosition;
            // 받는 연출 중에는 누름을 받지 않는다(키보드 Enter로 고른 단추가 눌리는 것도, 2026-10-09 리뷰)
            m_button.onClick.AddListener(() =>
            {
                if (m_rewarding == null)
                {
                    Clicked?.Invoke();
                }
            });
            m_stamp.gameObject.SetActive(false);
        }

        // 스스로 꺼질 때(평가 카드 · 사슬 끝)만 미룬 표시를 마저 바꾼다. 부모째 꺼질 때(씬 정리)는 정리만(꺼지는 중에 SetActive를 부르지 않게).
        // 톡(UiFx.Pulse)이 도중에 멈춰도 크기는 제자리로
        private void OnDisable()
        {
            Rect.localScale = Vector3.one;

            if (m_rewarding != null)
            {
                Finish(!gameObject.activeSelf);
            }
        }

        private void Update()
        {
            if (!m_sweep.gameObject.activeSelf)
            {
                return;
            }

            // 새 할 일 한 바퀴가 끝났고 안내 중도 아니면 끈다
            if (!m_tracking && Time.unscaledTime >= m_lapEnd)
            {
                Track();
                return;
            }

            PoseSweep(Phase());
        }

        // 도장 글(「완료」)
        public void SetStampText(string done)
        {
            m_stampText.text = done;
        }

        // 다 하면 그림 칸 체크 · 캡슐 금빛 · 「!」. 새 할 일이면 톡 + 빛줄기 한 바퀴, 막 다 됐으면 톡
        public void Show(Data data)
        {
            if (m_holding)
            {
                m_pending = () => Show(data);
                return;
            }

            m_hidden = false;
            Refresh();
            m_icon.sprite = data.Icon;
            m_icon.enabled = data.Icon != null;

            // 한 텍셀이 정수 px가 되게: 할 일 그림(PPU 100/3)은 3배, UI 아이콘(PPU 25)은 4배, 월드 그림(PPU 80)은 2배. 칸을 넘치면 반씩(2026-10-12 아트방 「물건 하나」 그림 칸 맞춤)
            if (data.Icon != null)
            {
                Vector2 slot = ((RectTransform)m_icon.transform.parent).rect.size;
                float scale = data.Icon.pixelsPerUnit <= k_UiIconPpu ? 4f : data.Icon.pixelsPerUnit < k_QuestIconPpu ? 3f : 2f;

                while (scale > 1f && (data.Icon.rect.width * scale > slot.x || data.Icon.rect.height * scale > slot.y))
                {
                    scale /= 2f;
                }

                m_icon.rectTransform.sizeDelta = data.Icon.rect.size * scale;
            }

            m_text.text = data.Text;
            m_pipRow.SetActive(data.Count >= 2 && data.Count <= m_pips.Length);

            for (int i = 0; i < m_pips.Length; i++)
            {
                m_pips[i].gameObject.SetActive(i < data.Count);
                m_pips[i].sprite = data.Done || i < data.Have ? m_pipOn : m_pipOff;
            }

            m_value.gameObject.SetActive(data.Count > m_pips.Length);
            m_value.text = data.Progress;
            m_check.SetActive(data.Done);
            m_reward.gameObject.SetActive(data.Coins != null || data.Extra != null);
            m_reward.alpha = 1f;
            m_rewardFrame.sprite = data.Done ? m_rewardOn : m_rewardOff;
            m_rewardCoin.gameObject.SetActive(data.Coins != null);
            m_rewardValue.gameObject.SetActive(data.Coins != null);
            m_rewardValue.text = data.Coins;
            m_rewardValue.color = data.Done ? k_InkOn : k_InkOff;
            m_extraIcon.gameObject.SetActive(data.Extra != null);
            m_extraValue.gameObject.SetActive(data.Extra != null);
            m_extraValue.text = data.Extra;
            m_extraValue.color = m_rewardValue.color;
            SnapWidth(m_value);
            SnapWidth(m_rewardValue);
            SnapWidth(m_extraValue);

            if (data.ExtraIcon != null)
            {
                Vector2 size = data.ExtraIcon.rect.size;
                size = size.y > k_ExtraMaxHeight ? size / 2f : size;
                m_extraIcon.sprite = data.ExtraIcon;
                LayoutElement element = m_extraIcon.GetComponent<LayoutElement>();
                element.preferredWidth = size.x;
                element.preferredHeight = size.y;
            }

            m_badge.SetActive(data.Done);
            bool fresh = data.Text != m_lastText;
            bool pulse = fresh || data.Done && !m_isDone && m_shown;
            m_lapEnd = fresh ? Time.unscaledTime + k_SweepSeconds : m_lapEnd;
            m_lastText = data.Text;
            m_shown = true;
            m_isDone = data.Done;
            Track();

            if (pulse && isActiveAndEnabled)
            {
                StartCoroutine(UiFx.Pulse(Rect));
            }
        }

        public void Hide()
        {
            if (m_holding)
            {
                m_pending = Hide;
                return;
            }

            m_hidden = true;
            Refresh();
        }

        // 평가 카드가 떠 있는 동안 숨는다
        public void SetBlocked(bool blocked)
        {
            m_blocked = blocked;
            Refresh();
        }

        // 길잡이가 켜진 동안
        public void SetTracking(bool tracking)
        {
            m_tracking = tracking;
            Track();
        }

        // 받기 연출을 시작한다(다 한 카드가 보일 때만)
        public void PlayReward()
        {
            if (m_rewarding == null && m_isDone && isActiveAndEnabled)
            {
                m_rewarding = StartCoroutine(Reward());
            }
        }

        // 꺼지는 중(OnDisable)에 다시 부르지 않게 바뀔 때만. 켤 때는 폭을 다시 맞춘다(꺼진 동안 바뀐 걸음은 폭을 못 잰다, 2026-10-12 리뷰)
        private void Refresh()
        {
            bool on = !m_hidden && !m_blocked;

            if (gameObject.activeSelf != on)
            {
                gameObject.SetActive(on);

                if (on)
                {
                    Track();
                }
            }
        }

        // 빛줄기: 안내 중이거나 새 할 일 한 바퀴 동안(다 한 카드는 안내하지 않는다)
        private void Track()
        {
            bool on = !m_isDone && (m_tracking || Time.unscaledTime < m_lapEnd);
            m_sweep.gameObject.SetActive(on);
            Fit();

            if (on)
            {
                PoseSweep(Phase());
            }
        }

        // 바퀴 머리: 새 할 일 한 바퀴는 위 왼쪽에서 떠나 거기서 끝난다
        private float Phase()
        {
            return Mathf.Repeat((Time.unscaledTime - m_lapEnd) / k_SweepSeconds, 1f);
        }

        // 폭: 내용을 2칸 단위로 올림(최소 폭 없음). 꺼져 있으면 잴 수 없어(0이 나온다) 켤 때(Refresh) 맞춘다
        private void Fit()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Ceil(LayoutUtility.GetPreferredWidth(Rect) / k_WidthStep) * k_WidthStep);
            LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
        }

        // 빛줄기: 주황 띠 칸을 따라 위 왼쪽에서 시계 방향, 머리(첫 점)부터 한 칸 간격
        private void PoseSweep(float phase)
        {
            Rect rect = Rect.rect;

            for (int i = 0; i < m_sweep.childCount; i++)
            {
                Vector2 at = SweepCell(phase, i, rect.width, rect.height);
                ((RectTransform)m_sweep.GetChild(i)).anchoredPosition = new Vector2(at.x, -at.y);
            }
        }

        // 한 바퀴의 phase에서 머리보다 back칸 뒤의 띠 칸(점 왼쪽 위, 아래로 +). 위 → 오른쪽 → 아래 → 왼쪽 띠,
        // 띠 사이 모서리는 둥글어(맨 끝 칸이 투명) 한 칸 안쪽으로 돈다(아트방)
        private static Vector2 SweepCell(float phase, int back, float w, float h)
        {
            float near = k_SweepInset + 2f * k_Dot;
            float farX = w - k_SweepInset - 3f * k_Dot;
            float farY = h - k_SweepInset - 3f * k_Dot;
            int across = Mathf.RoundToInt((farX - near) / k_Dot) + 1;
            int down = Mathf.RoundToInt((farY - near) / k_Dot) + 1;
            int lap = 2 * (across + down + 2);
            int k = ((Mathf.FloorToInt(phase * lap) - back) % lap + lap) % lap;

            if (k < across)
            {
                return new Vector2(near + k * k_Dot, k_SweepInset);
            }

            k -= across;

            if (k == 0)
            {
                return new Vector2(farX + k_Dot, k_SweepInset + k_Dot);
            }

            k--;

            if (k < down)
            {
                return new Vector2(w - k_SweepInset - k_Dot, near + k * k_Dot);
            }

            k -= down;

            if (k == 0)
            {
                return new Vector2(farX + k_Dot, farY + k_Dot);
            }

            k--;

            if (k < across)
            {
                return new Vector2(farX - k * k_Dot, h - k_SweepInset - k_Dot);
            }

            k -= across;

            if (k == 0)
            {
                return new Vector2(k_SweepInset + k_Dot, farY + k_Dot);
            }

            k--;
            return k < down ? new Vector2(k_SweepInset, farY - k * k_Dot) : new Vector2(k_SweepInset + k_Dot, k_SweepInset + k_Dot);
        }

        private IEnumerator Reward()
        {
            m_holding = true;
            GetComponent<CanvasGroup>().blocksRaycasts = false;
            RectTransform space = (RectTransform)Rect.parent;
            m_coins = new RectTransform[k_Coins];

            for (int i = 0; i < k_Coins; i++)
            {
                Image coin = new GameObject("FlyingCoin", typeof(RectTransform)).AddComponent<Image>();
                coin.sprite = m_rewardCoin.sprite;
                coin.raycastTarget = false;
                coin.rectTransform.SetParent(space, false);
                coin.rectTransform.sizeDelta = m_rewardCoin.sprite.rect.size;
                coin.gameObject.SetActive(false);
                m_coins[i] = coin.rectTransform;
            }

            for (float t = 0f; t < k_End; t += Time.unscaledDeltaTime)
            {
                if (m_holding && t >= k_Turn)
                {
                    Turn();

                    // 사슬 끝(Hide)이면 카드가 꺼지며 이미 정리됐다
                    if (m_rewarding == null)
                    {
                        yield break;
                    }
                }

                Pose(t);
                yield return null;
            }

            Finish(true);
        }

        // t초의 모습: 도장(커졌다 쾅) · 카드 눌림 · 나는 코인 · 코인 알약 톡 · 흐려짐 · 다음 카드 떠오름. 쾅부터 「!」와 보상 캡슐을 도장이 대신한다
        private void Pose(float t)
        {
            int stamp = Mathf.Clamp(Mathf.FloorToInt((t - k_StampAt) / k_Beat), 0, k_StampScale.Length - 1);
            m_stamp.gameObject.SetActive(t >= k_StampAt && t < k_Turn);
            m_stamp.transform.localScale = Vector3.one * k_StampScale[stamp];
            m_stamp.alpha = k_StampAlpha[stamp];

            if (t < k_Turn)
            {
                m_badge.SetActive(t < k_Land);
                m_reward.alpha = t < k_Land ? 1f : 0f;
            }

            bool dip = t >= k_Land && t < k_Land + k_Beat;
            float rise = t >= k_Turn ? 1f - Mathf.Clamp01((t - k_Turn) / k_RiseSeconds) : 0f;
            Rect.anchoredPosition = m_rest + Vector2.down * ((dip ? k_Dip : 0f) + Mathf.Round(k_Rise * rise * rise));
            GetComponent<CanvasGroup>().alpha = t < k_FadeAt ? 1f : t < k_Turn ? 1f - (t - k_FadeAt) / k_Beat : 1f - rise * rise;
            // 코인은 도장 자리에서 코인 알약으로(매번 지금 자리를 잰다)
            RectTransform space = (RectTransform)Rect.parent;
            Vector2 from = space.InverseTransformPoint(m_stamp.transform.position);
            Vector2 to = space.InverseTransformPoint(m_coinTarget.position);
            float kick = 0f;

            for (int i = 0; i < k_Coins; i++)
            {
                float u = (t - k_Land - i / 2 * k_Beat) / k_Flight;
                bool flying = u >= 0f && u < 1f;
                m_coins[i].gameObject.SetActive(flying);

                if (u >= 1f && (u - 1f) * k_Flight < k_KickSeconds)
                {
                    kick = Mathf.Max(kick, Mathf.Sin((u - 1f) * k_Flight / k_KickSeconds * Mathf.PI));
                }

                if (!flying)
                {
                    continue;
                }

                float angle = Mathf.Lerp(k_SpreadFrom, k_SpreadTo, i / (k_Coins - 1f)) * Mathf.Deg2Rad;
                Vector2 bend = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * k_Spread;
                float e = u * u * (3f - 2f * u);
                Vector2 at = (1f - e) * (1f - e) * from + 2f * (1f - e) * e * bend + e * e * to;
                m_coins[i].localPosition = new Vector3(Snap(at.x), Snap(at.y), 0f);
            }

            m_coinPill.localScale = Vector3.one * (1f + k_Kick * kick);
        }

        // 흐려진 뒤: 기다리던 다음 할 일로
        private void Turn()
        {
            m_holding = false;
            Action pending = m_pending;
            m_pending = null;
            pending?.Invoke();
        }

        // 끝 또는 중간에 꺼질 때(평가 카드 · 사슬 끝 · 씬 정리): 날던 코인을 치우고 제자리로. turn이면 아직 안 바뀐 표시를 지금 바꾼다
        private void Finish(bool turn)
        {
            foreach (RectTransform coin in m_coins)
            {
                Destroy(coin.gameObject);
            }

            m_coins = null;
            m_rewarding = null;
            m_coinPill.localScale = Vector3.one;
            m_stamp.gameObject.SetActive(false);
            m_reward.alpha = 1f;
            Rect.anchoredPosition = m_rest;
            CanvasGroup group = GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;

            if (turn && m_holding)
            {
                Turn();
            }

            m_holding = false;
            m_pending = null;
        }

        private static float Snap(float v)
        {
            return Mathf.Round(v / k_Px) * k_Px;
        }

        // 글 폭을 한 칸 단위로 올린다(뒤에 오는 그림 · 캡슐 끝이 픽셀 사이에 걸리지 않게)
        private static void SnapWidth(TMP_Text text)
        {
            text.GetComponent<LayoutElement>().preferredWidth = Mathf.Ceil(text.preferredWidth / k_Px) * k_Px;
        }
    }
}
