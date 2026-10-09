using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 54 · 데이터-테이블-규칙 8.36: 오늘의 일 후보(MissionTable.json 행). 하루마다 할 수 있는 행(area가 열림 · needs 상태가 1 이상)만 weight 비중으로 뽑는다.
    // 다 하고 받으면 points점
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class MissionTable : Table<string>
    {
        public string Kind { get; set; }
        public string Param { get; set; }
        public int Count { get; set; }
        public int Points { get; set; }
        public double Weight { get; set; }
        // 이 곳 id가 열렸을 때만 나오고, 같은 곳 상한(missionSameAreaMax)을 센다
        public string Area { get; set; }
        // "종류:대상" 상태가 1 이상일 때만(예 unlock:strawberry). null이면 늘
        public string Needs { get; set; }
        public string Icon { get; set; }
        public string Text { get; set; }
    }
}
