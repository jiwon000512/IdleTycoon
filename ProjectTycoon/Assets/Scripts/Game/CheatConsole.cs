#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.Game
{
    // 에디터 전용 치트 도구(빌드에 들어가지 않는다): ` 로 열고 닫는다. 치트마다 고를 값이 버튼으로 늘어서 있고, 누르면 바로 실행된다(글 입력 없음).
    // 지금 상태(배속 · 웜뱃이 있는 곳)는 버튼에 ▶ 표시. 화면 밖 개발 도구라 문구는 StringTable이 아니라 여기 둔다
    public sealed class CheatConsole : MonoBehaviour
    {
        // 시간 건너뛰기 한 틱(초)
        private const double k_SkipStep = 0.1;

        private sealed class Option
        {
            public string Label;
            public Func<string> Run;
            public Func<bool> On;
        }

        private sealed class Cheat
        {
            public string Name;
            public Option[] Options;
        }

        private readonly List<Cheat> m_cheats = new List<Cheat>();
        private GameManager m_game;
        private bool m_open;
        private string m_result = string.Empty;
        private Vector2 m_scroll;

        private Mall Mall => m_game.Mall;
        private ZooState State => m_game.State;

        public void Bind(GameManager game)
        {
            m_game = game;
            Add("코인", new[] { 1000d, 10000d, 100000d, 1000000d }.Select(n => Opt("+" + Short(n), () => { State.AddCoins(n); return "코인 +" + Short(n); })));

            foreach (int n in new[] { 10, 100 })
            {
                Add("재료 +" + n, game.Tables.GetAll<ItemTable>().Select(item => Opt(item.Name, () => AddItem(item, n))));
            }

            Add("시간 건너뛰기", new[] { (10d, "10초"), (60d, "1분"), (300d, "5분"), (900d, "15분") }.Select(t => Opt(t.Item2, () => Skip(t.Item1, t.Item2))));
            Add("배속", new[] { 1f, 2f, 4f, 8f }.Select(x => Opt("×" + x, () => { Time.timeScale = x; return "배속 ×" + x; }, () => Mathf.Approximately(Time.timeScale, x))));
            Add("곳 이동", game.Mall.Areas.Select(area => Opt(AreaName(area), () => Move(area), () => Mall.Active == area)));
            Add("행상", new[] { Opt("부르기", Summon) });
            Add("똥 싸기(웜뱃 둘레)", new[] { 1, 3, 5 }.Select(n => Opt(n + "개", () => Poop(n))));
            Add("농장 층", new[] { Opt("지금 층 다 파기", DigFloor), Opt("아래층 열기(무료)", OpenLower) });
            Add("빵집 평가", new[] { Opt("바로 통과(별 +1)", () => PassEvaluation(1)), Opt("별 +5", () => PassEvaluation(5)) });
            // 설계 43: 오프라인 정산을 바로(팝업까지) · 저장
            Add("오프라인", new[] { (600d, "10분"), (3600d, "1시간"), (28800d, "8시간") }.Select(t => Opt(t.Item2, () => Offline(t.Item1, t.Item2))));
            Add("저장", new[] { Opt("지금 저장", () => { m_game.Save(); return "저장함"; }), Opt("저장 지우기(다음 플레이 새 게임)", () => { m_game.DeleteSave(); return "저장을 지웠다. 이번 플레이는 닫을 때 저장하지 않는다"; }) });
        }

        private void Add(string name, IEnumerable<Option> options)
        {
            m_cheats.Add(new Cheat { Name = name, Options = options.ToArray() });
        }

        private static Option Opt(string label, Func<string> run, Func<bool> on = null)
        {
            return new Option { Label = label, Run = run, On = on };
        }

        private static string Short(double n)
        {
            return n >= 1000000d ? n / 1000000d + "M" : n >= 1000d ? n / 1000d + "K" : n.ToString();
        }

        private static string AreaName(WombatArea area)
        {
            return area is FarmArea farm ? "농장 " + farm.Number + "층" : area is BakeryArea ? "빵집" : "광장";
        }

        private string AddItem(ItemTable item, int count)
        {
            State.AddItem(item.Id, count);
            return item.Name + " +" + count + " (지금 " + State.Count(item.Id) + ")";
        }

        private string Skip(double seconds, string label)
        {
            for (double t = 0d; t < seconds; t += k_SkipStep)
            {
                Mall.Tick(k_SkipStep);
            }

            return label + " 건너뜀(가게 · 밭 · 행상 · 축복 · 월급)";
        }

        private string Offline(double seconds, string label)
        {
            OfflineReport report = m_game.SettleOffline(seconds);
            return label + " 정산: 코인 +" + report.Coins + "(판매 " + report.Sales + " · 월급 " + report.Wages + "), 재료 " + string.Join(", ", report.Items.Select(p => p.Key + " " + p.Value));
        }

        private string Summon()
        {
            RelicMerchant merchant = Mall.Plaza.Merchant;

            if (merchant.Phase != MerchantPhase.Away)
            {
                return "행상이 이미 와 있다(" + merchant.Phase + ")";
            }

            merchant.Summon();
            return "행상이 계단으로 온다";
        }

        // 닫힌 농장 층도 간다(치트)
        private string Move(WombatArea area)
        {
            if (Mall.Active == area)
            {
                return "이미 " + AreaName(area);
            }

            m_game.Bus.Publish(new Events.Passed(Mall.Active, area.Id));
            return AreaName(area) + "(으)로";
        }

        // 설계 39: 지금 층의 남은 칸을 모두 판다(계단 표식까지 바로 보려고)
        private string DigFloor()
        {
            if (!(Mall.Active is FarmArea farm))
            {
                return "농장 층에서만";
            }

            int dug = 0;

            while (!farm.IsFull)
            {
                farm.Grid.Dig(farm.Grid.Frontier().First());
                dug++;
            }

            return AreaName(farm) + " " + dug + "칸 팠다";
        }

        // 설계 39: 지금 층을 다 파고 아래층 값만큼 코인을 더해 계단을 연다(값 · 연출은 실제 계단 파기 그대로)
        private string OpenLower()
        {
            if (!(Mall.Active is FarmArea farm) || farm.Lower == null || farm.Lower.IsOpen)
            {
                return "열 아래층이 있는 농장 층에서만";
            }

            DigFloor();
            State.AddCoins(farm.Lower.Floor.OpenCost);
            StairInteractable stair = farm.Things.OfType<StairInteractable>().Single();
            farm.TryChoose(ActionTable.k_DigFloor, stair, null);
            return AreaName(farm.Lower) + " 열림";
        }

        // 설계 40: 쉬는 시간을 무시하고 평가를 count번 통과(소식지 · 보상까지 실제와 같다)
        private string PassEvaluation(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Mall.Bakery.Evaluation.PassNow();
            }

            return "빵집 별 " + State.Stars.Count(BakeryArea.k_Id) + "개";
        }

        private string Poop(int count)
        {
            int dropped = 0;
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
                }

                e.Use();
            }

            if (!m_open || m_game == null)
            {
                return;
            }

            int size = Mathf.Max(14, Screen.height / 50);
            GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
            GUIStyle name = new GUIStyle(label) { fontStyle = FontStyle.Bold };
            GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = size };
            float width = Mathf.Min(Screen.width - 20f, size * 26f);

            Rect area = new Rect(10f, 10f, width, Screen.height - 20f);
            // 상자를 겹쳐 그려 뒤 화면이 덜 비치게
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("치트 (` 닫기 · 누르면 바로 실행)", label);
            GUILayout.Label(m_result, label);
            m_scroll = GUILayout.BeginScrollView(m_scroll);

            foreach (Cheat cheat in m_cheats)
            {
                GUILayout.Label(cheat.Name, name);
                GUILayout.BeginHorizontal();

                foreach (Option option in cheat.Options)
                {
                    bool on = option.On != null && option.On();

                    if (GUILayout.Button((on ? "▶ " : string.Empty) + option.Label, button))
                    {
                        Run(cheat, option);
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Run(Cheat cheat, Option option)
        {
            m_result = option.Run();
            Debug.Log("[치트] " + cheat.Name + " " + option.Label + " → " + m_result);
        }
    }
}
#endif
