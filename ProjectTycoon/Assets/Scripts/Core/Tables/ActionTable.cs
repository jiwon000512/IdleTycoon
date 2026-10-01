using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 13 v0.5: auto = range 안이면 저절로, manual = 상호작용 버튼, sheet = 시트의 한 줄
    public enum ActionMode
    {
        Manual,
        Auto,
        Sheet,
    }

    // 설계 09 v0.4 · 데이터-테이블-규칙 8.10: 웜뱃 행동(ActionTable.json 행). 하는 일은 행동 클래스(ActionFactory 중첩 클래스), 여기는 수동/자동/시트와 버튼 아이콘
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ActionTable : Table<string>
    {
        public const string k_TakeOut = "take_out";
        public const string k_Fill = "fill";
        public const string k_Serve = "serve";
        public const string k_Open = "open";
        public const string k_OpenDig = "open_dig";
        public const string k_Exit = "exit";
        public const string k_Enter = "enter";
        public const string k_Bake = "bake";
        public const string k_Upgrade = "upgrade";
        public const string k_Dig = "dig";
        // 설계 22: 딴짓 중인 점원 깨우기
        public const string k_Wake = "wake";
        // 설계 24: 똥 치우기
        public const string k_Clean = "clean";
        // 설계 25: 빈 밭에 심기(버튼) · 익은 밭 거두기(auto)
        public const string k_Plant = "plant";
        public const string k_Harvest = "harvest";
        // 설계 27: 판 흙 칸 갈기(버튼)
        public const string k_Till = "till";
        // 설계 29: 광장 석상 팝업 열기(버튼)
        public const string k_Statue = "statue";
        // 설계 34: 광장 행상에게 말 걸기(버튼, 인사 뒤 뽑기 팝업)
        public const string k_Talk = "talk";

        public ActionMode Mode { get; set; }
        // 버튼 아이콘(Resources/ 기준, 확장자 없음). manual만, 나머지는 null
        public string Icon { get; set; }

        public bool IsAuto => Mode == ActionMode.Auto;
    }
}
