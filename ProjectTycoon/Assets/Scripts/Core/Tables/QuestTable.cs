using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 54 · 데이터-테이블-규칙 8.35: 퀘스트 사슬 한 걸음(QuestTable.json 행). 행 순서 = 사슬 순서, chapter = 장(1부터, 오늘의 일 상자 단계).
    // 무엇을 세나는 kind · param · count(GoalCounter), 「가기」는 area · thing(곳 id · 사물 종류 id) 또는 menu(메뉴 버튼 id), 보상은 코인 + 재료 하나
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class QuestTable : Table<string>
    {
        // menu 칸에 쓰는 메뉴 버튼 id(오른쪽 메뉴 줄)
        public const string k_MenuEdit = "edit";
        public const string k_MenuClerk = "clerk";
        public const string k_MenuStorage = "storage";
        public const string k_MenuRelic = "relic";
        public const string k_MenuMission = "mission";

        public int Chapter { get; set; }
        public string Kind { get; set; }
        // 세는 대상(빵 · 작물 · 곳 · 사물 · 구간 id). null이면 아무것이나
        public string Param { get; set; }
        public int Count { get; set; }
        // 알약 아이콘(Resources/ 기준, 확장자 없음)
        public string Icon { get; set; }
        // 알약 글(StringTable id)
        public string Text { get; set; }
        public string Area { get; set; }
        public string Thing { get; set; }
        public string Menu { get; set; }
        public double Coins { get; set; }
        public string Item { get; set; }
        public int ItemCount { get; set; }
    }
}
