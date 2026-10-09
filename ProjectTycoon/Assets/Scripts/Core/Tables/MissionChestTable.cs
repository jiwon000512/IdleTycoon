using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 54 · 데이터-테이블-규칙 8.37: 오늘의 일 상자(MissionChestTable.json 행) = chest(1부터) × chapter(퀘스트 장).
    // 하루 점수가 points에 닿으면 연다. 코인 = minutes(분어치) × coinsPerMinute(그 장의 1분 수입), 사슬이 끝나면 마지막 장 값
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class MissionChestTable : Table<string>
    {
        public int Chest { get; set; }
        public int Chapter { get; set; }
        public int Points { get; set; }
        public double Minutes { get; set; }
        public double CoinsPerMinute { get; set; }
        public string Item { get; set; }
        public int ItemCount { get; set; }

        public double Coins => Minutes * CoinsPerMinute;
    }
}
