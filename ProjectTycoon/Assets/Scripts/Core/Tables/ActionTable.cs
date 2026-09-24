using GameKit.Tables;

namespace ZooTycoon.Core
{
    public enum ActionMode
    {
        Manual,
        Auto,
    }

    // 설계 09 v0.4 · 데이터-테이블-규칙 8.10: 웜뱃 행동(ActionTable.json 행). 하는 일은 ShopSim·PlazaSim(k_Action*), 여기는 수동/자동과 버튼 아이콘
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ActionTable : Table<string>
    {
        public ActionMode Mode { get; set; }
        // 버튼 아이콘(Resources/ 기준, 확장자 없음). auto면 null
        public string Icon { get; set; }

        public bool IsAuto => Mode == ActionMode.Auto;
    }
}
