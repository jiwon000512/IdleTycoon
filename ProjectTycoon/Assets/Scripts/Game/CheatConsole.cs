#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Game
{
    // 에디터 전용 치트 도구(빌드에 들어가지 않는다): ` 로 열고 닫는다. 치트를 고르고(누르기 · ↑↓) 값이 있으면 넣고 Enter.
    // 화면 밖 개발 도구라 문구는 StringTable이 아니라 여기 둔다. 열린 동안은 Input System 키보드를 꺼 글을 칠 때 웜뱃이 걷지 않는다(IMGUI 글 칸은 따로 받는다)
    public sealed class CheatConsole : MonoBehaviour
    {
        private const string k_InputName = "cheat_input";
        // 시간 건너뛰기 한 틱(초)
        private const double k_SkipStep = 0.1;

        private sealed class Cheat
        {
            public string Name;
            public string Hint;
            public string Default;
            public Func<string, string> Run;
        }

        private readonly List<Cheat> m_cheats = new List<Cheat>();
        private GameManager m_game;
        private bool m_open;
        private int m_selected;
        private string m_input = string.Empty;
        private string m_result = string.Empty;
        private bool m_focus;

        private Mall Mall => m_game.Mall;
        private ZooState State => m_game.State;

        public void Bind(GameManager game)
        {
            m_game = game;
            string items = string.Join(" · ", game.Tables.GetAll<ItemTable>().Select(item => item.Id));
            Add("코인", "개수", "10000", s => { State.AddCoins(Number(s)); return "코인 +" + s; });
            Add("재료", "id 개수 (" + items + ")", "gem 50", AddItem);
            Add("시간 건너뛰기", "초 (가게 · 밭 · 행상 · 축복 · 월급이 모두 흐른다)", "60", Skip);
            Add("배속", "배 (1 = 보통)", "4", s => { Time.timeScale = (float)Number(s); return "배속 ×" + s; });
            Add("행상 부르기", "값 없음 (없을 때 바로 계단으로 온다)", string.Empty, Summon);
            Add("곳 이동", BakeryArea.k_Id + " · " + PlazaArea.k_Id + " · " + FarmArea.k_Id, PlazaArea.k_Id, Move);
            Add("똥 싸기", "개수 (지금 있는 곳, 웜뱃 둘레)", "3", Poop);
            Select(0);
        }

        private void Add(string name, string hint, string value, Func<string, string> run)
        {
            m_cheats.Add(new Cheat { Name = name, Hint = hint, Default = value, Run = run });
        }

        private void Select(int index)
        {
            m_selected = (index + m_cheats.Count) % m_cheats.Count;
            m_input = m_cheats[m_selected].Default;
            m_focus = true;
        }

        private static double Number(string s)
        {
            return double.Parse(s.Trim(), CultureInfo.InvariantCulture);
        }

        private string AddItem(string s)
        {
            string[] parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int count = parts.Length > 1 ? (int)Number(parts[1]) : 1;

            if (!m_game.Tables.GetAll<ItemTable>().Any(item => item.Id == parts[0]))
            {
                return "모르는 재료: " + parts[0];
            }

            State.AddItem(parts[0], count);
            return parts[0] + " +" + count + " (지금 " + State.Count(parts[0]) + ")";
        }

        private string Skip(string s)
        {
            double seconds = Number(s);

            for (double t = 0d; t < seconds; t += k_SkipStep)
            {
                Mall.Tick(k_SkipStep);
            }

            return seconds + "초 건너뜀";
        }

        private string Summon(string s)
        {
            RelicMerchant merchant = Mall.Plaza.Merchant;

            if (merchant.Phase != MerchantPhase.Away)
            {
                return "이미 와 있다(" + merchant.Phase + ")";
            }

            merchant.Summon();
            return "행상이 계단으로 온다";
        }

        private string Move(string s)
        {
            string to = s.Trim();

            if (Mall.Active.Id == to)
            {
                return "이미 " + to;
            }

            if (!Mall.Areas.Any(area => area.Id == to))
            {
                return "모르는 곳: " + to;
            }

            m_game.Bus.Publish(new Events.Passed(Mall.Active, to));
            return to + "(으)로";
        }

        private string Poop(string s)
        {
            int dropped = 0;
            int count = (int)Number(s);
            System.Numerics.Vector2 at = Mall.Wombat.Mover.Position;

            for (int i = 0; i < count * 4 && dropped < count; i++)
            {
                float angle = i * 2.4f;
                float radius = 0.8f + 0.3f * i;
                dropped += Mall.Active.TryDropPoop(at + new System.Numerics.Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius)) ? 1 : 0;
            }

            return "똥 " + dropped + "개";
        }

        private void OnGUI()
        {
            Event e = Event.current;

            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.BackQuote || e.character == '`'))
            {
                if (e.keyCode == KeyCode.BackQuote)
                {
                    m_open = !m_open;
                    m_focus = m_open;
                    SetKeyboard(!m_open);
                }

                e.Use();
            }

            if (!m_open || m_game == null)
            {
                return;
            }

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow)
                {
                    Select(m_selected + (e.keyCode == KeyCode.UpArrow ? -1 : 1));
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Run();
                    e.Use();
                }
            }

            int size = Mathf.Max(14, Screen.height / 45);
            GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
            GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = size, alignment = TextAnchor.MiddleLeft };
            GUIStyle field = new GUIStyle(GUI.skin.textField) { fontSize = size };
            float width = Mathf.Min(Screen.width - 20f, size * 22f);

            Rect area = new Rect(10f, 10f, width, Screen.height - 20f);
            // 상자를 겹쳐 그려 뒤 화면이 덜 비치게
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("치트 (` 닫기 · ↑↓ 고르기 · Enter 실행)", label);

            for (int i = 0; i < m_cheats.Count; i++)
            {
                if (GUILayout.Toggle(i == m_selected, (i == m_selected ? "▶ " : "   ") + m_cheats[i].Name, button) && i != m_selected)
                {
                    Select(i);
                }
            }

            GUILayout.Label(m_cheats[m_selected].Hint, label);
            GUI.SetNextControlName(k_InputName);
            m_input = GUILayout.TextField(m_input, field);

            if (GUILayout.Button("실행", button))
            {
                Run();
            }

            GUILayout.Label(m_result, label);
            GUILayout.EndArea();

            if (m_focus)
            {
                GUI.FocusControl(k_InputName);
                m_focus = false;
            }
        }

        // 플레이를 끝낼 때 열려 있었어도 키보드를 되살린다(장치 상태는 플레이를 넘어 남는다)
        private void OnDestroy()
        {
            SetKeyboard(true);
        }

        private static void SetKeyboard(bool enabled)
        {
            if (Keyboard.current == null || Keyboard.current.enabled == enabled)
            {
                return;
            }

            if (enabled)
            {
                InputSystem.EnableDevice(Keyboard.current);
            }
            else
            {
                InputSystem.DisableDevice(Keyboard.current);
            }
        }

        private void Run()
        {
            Cheat cheat = m_cheats[m_selected];

            try
            {
                m_result = cheat.Run(m_input);
            }
            catch (Exception ex) when (ex is FormatException || ex is IndexOutOfRangeException)
            {
                m_result = "값을 확인하세요: " + cheat.Hint;
            }

            Debug.Log("[치트] " + cheat.Name + " " + m_input + " → " + m_result);
        }
    }
}
#endif
