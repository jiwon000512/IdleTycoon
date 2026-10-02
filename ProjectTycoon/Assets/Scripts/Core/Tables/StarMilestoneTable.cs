using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 40 · 데이터-테이블-규칙 8.27: 별 마일스톤(StarMilestoneTable.json 행 = 가게의 한 별). 가게마다 star 0 행이 시작 상한.
    // 상한 0 = 앞 마일스톤 그대로. text = 평가 팝업에 보일 그 밖의 해금 글(StringTable, 없으면 null)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class StarMilestoneTable : Table<string>
    {
        public string Shop { get; set; }
        public int Star { get; set; }
        public int ShelfMax { get; set; }
        public int OvenMax { get; set; }
        public int CounterMax { get; set; }
        public int UpgradeMax { get; set; }
        public string Text { get; set; }
    }
}
