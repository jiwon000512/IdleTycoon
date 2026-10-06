using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 44 · 데이터-테이블-규칙 8.30: 물고기(FishTable.json 행). 떼 물고기는 spawn 비중으로 고르고 크기 셋 중 하나
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FishTable : Table<string>
    {
        // 낚으면 창고에 드는 재료(ItemTable)
        public string Item { get; set; }
        // 떼에서 나올 비중
        public double Spawn { get; set; }
        // 헤엄 속도(유닛/초)
        public double Speed { get; set; }
        // 크기 셋[작은 · 보통 · 큰]: 무게 범위와 비중
        public FishSizeData[] Sizes { get; set; }
    }

    public sealed class FishSizeData
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Chance { get; set; }
    }
}
