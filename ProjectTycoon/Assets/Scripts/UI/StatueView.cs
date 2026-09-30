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
    // 설계 29 웜뱃 석상 팝업: 능력 세 줄(등급 알약 · 능력 글 · 잠금 버튼) + 안내 + 보유 반짝돌 · 굴리기(값). 광장 석상 앞 버튼으로 열린다.
    // 굴리면 굴리는 줄이 능력 이름을 빠르게 넘기다 위부터 차례로 멈춘다(전설은 소리 + 한 번 부풂). 표시와 입력만, 규칙은 Core Statue · StatuePresenter
    public sealed class StatueView : UIView
    {
        // 줄 하나. Grade가 null이면 빈 줄(알약 숨김)
        public sealed class LineData
        {
            public string Grade;
            public StatueGrade GradeLevel;
            public string Text;
            public string LockLabel;
            public bool Locked;
            public bool CanToggle;
        }

        // 굴리는 줄이 도는 시간 · 줄마다 늦게 멈추는 간격 · 이름이 바뀌는 간격(초)
        private const float k_SpinSeconds = 0.6f;
        private const float k_StopGap = 0.15f;
        private const float k_SpinStep = 0.06f;
        // 등급 알약 색(보통 · 드묾 · 전설)과 그 위 글자색
        private static readonly Color[] k_GradeColors = { new Color32(0x8A, 0x7B, 0x6E, 0xFF), new Color32(0x3F, 0x7F, 0xA6, 0xFF), new Color32(0xF2, 0xC1, 0x4E, 0xFF) };
        private static readonly Color[] k_GradeText = { new Color32(0xFB, 0xF4, 0xE6, 0xFF), new Color32(0xFB, 0xF4, 0xE6, 0xFF), new Color32(0x2E, 0x23, 0x20, 0xFF) };
        // 전설 줄은 줄 틀을 금빛으로 물들인다(UI에는 입자를 넣지 않으므로 금빛 틀 · 부풂 · 소리로 알린다)
        private static readonly Color k_LegendRow = new Color32(0xFF, 0xE2, 0x96, 0xFF);
        private static readonly Color k_LockOnText = new Color32(0xFB, 0xF4, 0xE6, 0xFF);
        private static readonly Color k_LockOffText = new Color32(0x2E, 0x23, 0x20, 0xFF);

        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [Tooltip("줄 템플릿: Grade(pill + Label) · Text · Lock(버튼 + Label)")]
        [SerializeField] private RectTransform m_lineTemplate;
        [SerializeField] private TextMeshProUGUI m_hint;
        [SerializeField] private TextMeshProUGUI m_have;
        [SerializeField] private Button m_rollButton;
        [SerializeField] private TextMeshProUGUI m_rollLabel;
        [SerializeField] private TextMeshProUGUI m_rollCost;
        [Tooltip("잠금 버튼: 켜짐(주 버튼) · 꺼짐(보조 버튼) 그림")]
        [SerializeField] private Sprite m_lockOn;
        [SerializeField] private Sprite m_lockOff;

        private readonly List<RectTransform> m_lines = new List<RectTransform>();
        private Vector2 m_panelRest;
        private Coroutine m_fx;
        private Coroutine m_spin;

        public event Action CloseRequested;
        public event Action<int> LockClicked;
        public event Action RollClicked;

        public bool IsOpen => m_root.activeSelf;
        // 줄이 도는 중(그동안 새 결과를 덮어 그리지 않는다)
        public bool Spinning => m_spin != null;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_rollButton.onClick.AddListener(() => RollClicked?.Invoke());
            m_lineTemplate.gameObject.SetActive(false);
            m_panelRest = m_panel.anchoredPosition;

            for (int i = 0; i < Statue.k_Lines; i++)
            {
                int index = i;
                RectTransform line = Instantiate(m_lineTemplate, m_lineTemplate.parent);
                line.gameObject.SetActive(true);
                line.Find("Lock").GetComponent<Button>().onClick.AddListener(() => LockClicked?.Invoke(index));
                m_lines.Add(line);
            }

            m_root.SetActive(false);
        }

        public void SetLabels(string title, string hint, string roll)
        {
            m_title.text = title;
            m_hint.text = hint;
            m_rollLabel.text = roll;
        }

        public void Open()
        {
            if (m_root.activeSelf)
            {
                return;
            }

            m_root.SetActive(true);
            Run(ref m_fx, UiFx.Appear(m_rootGroup, m_panel, m_panelRest, 0f));
            SoundManager.Instance.Play(SoundTable.k_UiOpen);
        }

        public void Close()
        {
            if (!m_root.activeSelf)
            {
                return;
            }

            StopSpin();
            Run(ref m_fx, UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_UiClose);
        }

        // 줄 · 보유 · 굴리기 값. canRoll이 아니면 버튼이 꺼진다(모자람)
        public void Show(IReadOnlyList<LineData> lines, string have, string cost, bool canRoll)
        {
            for (int i = 0; i < m_lines.Count; i++)
            {
                SetLine(m_lines[i], lines[i]);
            }

            m_have.text = have;
            m_rollCost.text = cost;
            m_rollButton.interactable = canRoll && !Spinning;
        }

        // 굴렸다: rolled 줄은 names를 빠르게 넘기다 위부터 차례로 멈추며 lines 값이 된다. 멈출 때 전설이면 소리 + 부풂
        public void PlayRoll(IReadOnlyList<LineData> lines, bool[] rolled, IReadOnlyList<string> names, string have, string cost, bool canRoll)
        {
            StopSpin();
            m_have.text = have;
            m_rollCost.text = cost;
            SoundManager.Instance.Play(SoundTable.k_StatueRoll);
            m_spin = StartCoroutine(Spin(lines, rolled, names, canRoll));
        }

        private IEnumerator Spin(IReadOnlyList<LineData> lines, bool[] rolled, IReadOnlyList<string> names, bool canRoll)
        {
            m_rollButton.interactable = false;
            float elapsed = 0f;
            bool[] stopped = new bool[m_lines.Count];

            for (int i = 0; i < m_lines.Count; i++)
            {
                stopped[i] = !rolled[i];
                SetLine(m_lines[i], lines[i], rolled[i]);
            }

            while (Array.IndexOf(stopped, false) >= 0)
            {
                int shown = 0;

                for (int i = 0; i < m_lines.Count; i++)
                {
                    if (stopped[i])
                    {
                        continue;
                    }

                    if (elapsed >= k_SpinSeconds + shown * k_StopGap)
                    {
                        stopped[i] = true;
                        SetLine(m_lines[i], lines[i]);
                        StartCoroutine(UiFx.Pulse(m_lines[i]));

                        if (lines[i].GradeLevel == StatueGrade.Legend)
                        {
                            SoundManager.Instance.Play(SoundTable.k_StatueLegend);
                        }
                    }
                    else
                    {
                        m_lines[i].Find("Text").GetComponent<TMP_Text>().text = names[UnityEngine.Random.Range(0, names.Count)];
                    }

                    shown++;
                }

                yield return new WaitForSeconds(k_SpinStep);
                elapsed += k_SpinStep;
            }

            m_spin = null;
            m_rollButton.interactable = canRoll;
        }

        // spinning: 도는 줄은 등급 알약을 숨기고 잠금 버튼을 끈다
        private void SetLine(RectTransform line, LineData data, bool spinning = false)
        {
            GameObject grade = line.Find("Grade").gameObject;
            grade.SetActive(data.Grade != null && !spinning);
            line.GetComponent<Image>().color = data.Grade != null && !spinning && data.GradeLevel == StatueGrade.Legend ? k_LegendRow : Color.white;

            if (data.Grade != null)
            {
                grade.GetComponent<Image>().color = k_GradeColors[(int)data.GradeLevel];
                TMP_Text label = grade.GetComponentInChildren<TMP_Text>();
                label.text = data.Grade;
                label.color = k_GradeText[(int)data.GradeLevel];
            }

            line.Find("Text").GetComponent<TMP_Text>().text = data.Text;
            Button lockButton = line.Find("Lock").GetComponent<Button>();
            lockButton.interactable = data.CanToggle && !spinning;
            lockButton.GetComponent<Image>().sprite = data.Locked ? m_lockOn : m_lockOff;
            TMP_Text lockLabel = lockButton.GetComponentInChildren<TMP_Text>();
            lockLabel.text = data.LockLabel;
            lockLabel.color = data.Locked ? k_LockOnText : k_LockOffText;
        }

        private void StopSpin()
        {
            if (m_spin != null)
            {
                StopCoroutine(m_spin);
                m_spin = null;
            }
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
