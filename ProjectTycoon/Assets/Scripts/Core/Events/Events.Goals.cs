using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 54: 가이드 · 퀘스트 · 오늘의 일 사건
    public static partial class Events
    {
        // 퀘스트 걸음이 바뀌었거나 진행이 늘었다(할 일 알약)
        public readonly struct QuestChanged
        {
            public readonly QuestLog Log;

            public QuestChanged(QuestLog log)
            {
                Log = log;
            }
        }

        // 오늘의 일이 새로 나왔거나 진행 · 점수 · 상자가 바뀌었다(판 · 버튼 점)
        public readonly struct MissionsChanged
        {
            public readonly DailyBoard Board;

            public MissionsChanged(DailyBoard board)
            {
                Board = board;
            }
        }

        // 받기 · 상자 열기로 보상이 들어왔다(웜뱃 머리 위 「+N」: 코인 · 재료마다 하나씩 쌓는다)
        public readonly struct GoalRewarded
        {
            public readonly double Coins;
            public readonly IReadOnlyDictionary<string, int> Items;

            public GoalRewarded(double coins, IReadOnlyDictionary<string, int> items)
            {
                Coins = coins;
                Items = items;
            }
        }

        // 길잡이가 켜졌거나 꺼졌거나 다음 곳으로 이어졌다(화살표)
        public readonly struct GuideChanged
        {
            public readonly Guide Guide;

            public GuideChanged(Guide guide)
            {
                Guide = guide;
            }
        }

        // 설계 54: 메뉴가 대상인 걸음의 「가기」 — 그 메뉴 버튼을 톡톡 반짝인다(웜뱃은 그대로)
        public readonly struct MenuPointed
        {
            public readonly string Menu;

            public MenuPointed(string menu)
            {
                Menu = menu;
            }
        }
    }
}
