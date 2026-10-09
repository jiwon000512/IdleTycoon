using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 55: 할 일 카드(위 가운데). 카드 자체가 버튼 — 진행 중이면 누르면 가기(오른쪽 금빛 화살표), 다 하면 누르면 받기(✓ 완료 · 오른쪽 보상 · 「!」).
    // 안내 중(길잡이가 켜진 동안): 화살표가 땅 화살표와 같은 박자로 톡톡 + 테두리 위 금빛 빛줄기 + 초록 「안내 중」(2026-10-09 사용자 가-1 + 가-2).
    // 받기: 완료 도장이 쾅 찍히는 순간 코인 여섯 개가 도장 자리에서 튀어나와 코인 알약으로 날아가고, 카드가 흐려진 뒤 다음 할 일이 떠오른다
    // (2026-10-09 사용자 받-2 + 받-1, 시간표는 아트방). 위 가운데는 평가 카드가 먼저다
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class QuestCard : MonoBehaviour
    {
        // 움직임은 4px(UI 한 칸) 단위. 아이콘은 원본 2배. 폭은 2칸 단위(가운데 기준이라 양 끝이 칸에 맞는다)
        private const float k_Px = 4f;
        private const float k_IconScale = 2f;
        private const float k_WidthStep = 8f;
        // 화살표 톡톡: 폭(2칸) · 박자(GuideView 땅 화살표와 같다)
        private const float k_Tap = 8f;
        private const float k_TapSpeed = 6f;
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

        [SerializeField] private Button m_button;
        [SerializeField] private Image m_icon;
        [SerializeField] private TMP_Text m_text;
        [SerializeField] private TMP_Text m_value;
        [Tooltip("안내 중일 때 진행 옆 초록 글")]
        [SerializeField] private TMP_Text m_guiding;
        [SerializeField] private GameObject m_check;
        [SerializeField] private TMP_Text m_done;
        [Tooltip("진행 중 오른쪽 가기 칸. 톡톡은 그 안의 화살표")]
        [SerializeField] private GameObject m_go;
        [SerializeField] private RectTransform m_arrow;
        [Tooltip("다 하면 오른쪽: 코인 위 · 값 아래. 도장이 찍히면 투명하게(자리는 남겨 카드 폭이 그대로)")]
        [SerializeField] private CanvasGroup m_reward;
        [SerializeField] private Image m_rewardCoin;
        [SerializeField] private TMP_Text m_rewardValue;
        [SerializeField] private GameObject m_badge;
        [Tooltip("테두리 빛줄기(자식 점, 머리부터)")]
        [SerializeField] private RectTransform m_sweep;
        [Tooltip("완료 도장(동그라미 테 quest_stamp + 글)")]
        [SerializeField] private CanvasGroup m_stamp;
        [SerializeField] private TMP_Text m_stampText;
        [Tooltip("도장 자리: 보상 칸 가운데에서 비키는 만큼(동그라미 테가 카드 띠 안에 들게, 아트방)")]
        [SerializeField] private Vector2 m_stampShift;
        [Tooltip("코인이 날아갈 곳(코인 알약의 코인)과 닿을 때 톡 튀는 알약")]
        [SerializeField] private RectTransform m_coinTarget;
        [SerializeField] private RectTransform m_coinPill;

        private Vector2 m_rest;
        private bool m_hidden = true;
        private bool m_blocked;
        private bool m_shown;
        private bool m_isDone;
        private bool m_tracking;
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

        // 스스로 꺼질 때(평가 카드 · 사슬 끝)만 미룬 표시를 마저 바꾼다. 부모째 꺼질 때(씬 정리)는 정리만(꺼지는 중에 SetActive를 부르지 않게)
        private void OnDisable()
        {
            if (m_rewarding != null)
            {
                Finish(!gameObject.activeSelf);
            }
        }

        private void Update()
        {
            if (m_guiding.gameObject.activeSelf)
            {
                m_arrow.anchoredPosition = new Vector2(Snap(-Mathf.Sin(Time.time * k_TapSpeed) * k_Tap), 0f);
                PoseSweep(Mathf.Repeat(Time.unscaledTime / k_SweepSeconds, 1f));
            }
        }

        // 「안내 중」 · 「완료」(도장 글도)
        public void SetLabels(string guiding, string done)
        {
            m_guiding.text = guiding;
            m_done.text = done;
            m_stampText.text = done;
        }

        // 다 하면 진행 대신 ✓ 완료 · 보상(reward가 null이면 코인 칸 없이) · 「!」, 막 다 됐으면 한 번 톡
        public void Show(Sprite icon, string text, string value, bool done, string reward)
        {
            if (m_holding)
            {
                m_pending = () => Show(icon, text, value, done, reward);
                return;
            }

            m_hidden = false;
            Refresh();
            m_icon.sprite = icon;
            m_icon.enabled = icon != null;

            // 원본 2배, 칸을 넘치면 1배(긴 낚싯대 · 접시, 2026-10-09 리뷰)
            if (icon != null)
            {
                Vector2 slot = ((RectTransform)m_icon.transform.parent).rect.size;
                Vector2 size = icon.rect.size * k_IconScale;
                m_icon.rectTransform.sizeDelta = size.x <= slot.x && size.y <= slot.y ? size : icon.rect.size;
            }

            m_text.text = text;
            m_value.text = value;
            m_value.gameObject.SetActive(!done);
            m_check.SetActive(done);
            m_done.gameObject.SetActive(done);
            m_go.SetActive(!done);
            m_reward.gameObject.SetActive(done && reward != null);
            m_reward.alpha = 1f;
            m_rewardValue.text = reward;
            m_badge.SetActive(done);
            bool pulse = done && !m_isDone && m_shown;
            m_shown = true;
            m_isDone = done;
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

        // 길잡이가 켜진 동안(다 한 카드는 안내하지 않는다)
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

        // 꺼지는 중(OnDisable)에 다시 부르지 않게 바뀔 때만
        private void Refresh()
        {
            bool on = !m_hidden && !m_blocked;

            if (gameObject.activeSelf != on)
            {
                gameObject.SetActive(on);
            }
        }

        private void Track()
        {
            bool on = m_tracking && !m_isDone;
            m_guiding.gameObject.SetActive(on);
            m_sweep.gameObject.SetActive(on);
            m_arrow.anchoredPosition = Vector2.zero;
            Fit();

            if (on)
            {
                PoseSweep(Mathf.Repeat(Time.unscaledTime / k_SweepSeconds, 1f));
            }
        }

        // 폭: 내용(최소 폭은 프리팹 LayoutElement)을 2칸 단위로 올림
        private void Fit()
        {
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
            RectTransform stampRect = (RectTransform)m_stamp.transform;
            stampRect.position = m_reward.transform.position;
            stampRect.anchoredPosition += m_stampShift;
            RectTransform space = (RectTransform)Rect.parent;
            m_coins = new RectTransform[k_Coins];

            for (int i = 0; i < k_Coins; i++)
            {
                Image coin = new GameObject("FlyingCoin", typeof(RectTransform)).AddComponent<Image>();
                coin.sprite = m_rewardCoin.sprite;
                coin.raycastTarget = false;
                coin.rectTransform.SetParent(space, false);
                coin.rectTransform.sizeDelta = m_rewardCoin.rectTransform.sizeDelta;
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

        // t초의 모습: 도장(커졌다 쾅) · 카드 눌림 · 나는 코인 · 코인 알약 톡 · 흐려짐 · 다음 카드 떠오름. 쾅부터 「!」와 보상 칸을 도장이 대신한다
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
    }
}
