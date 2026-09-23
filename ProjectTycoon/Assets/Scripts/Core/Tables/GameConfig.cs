namespace ZooTycoon.Core
{
    // 기획서 6.2~6.4 · 데이터-테이블-규칙 8.4 (version 11: 동물·관광객 수 섹션 삭제)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class GameConfig
    {
        public StartConfig Start { get; set; }
        public OfflineConfig Offline { get; set; }
        public IncomeConfig Income { get; set; }
        public ShopConfig Shop { get; set; }

        public sealed class StartConfig
        {
            public int Coins { get; set; }
        }

        public sealed class OfflineConfig
        {
            public int MaxSeconds { get; set; }
        }

        // 설계 04 P2: 수입 틱 간격(초)
        public sealed class IncomeConfig
        {
            public double TickSeconds { get; set; }
        }

        // 설계 08 v0.5: 빵집 시뮬 시간(초)·수량의 시작값
        public sealed class ShopConfig
        {
            public double ArrivalSeconds { get; set; }
            public int MaxCustomers { get; set; }
            public double EnterSeconds { get; set; }
            public double ToQueueSeconds { get; set; }
            public double PatienceSeconds { get; set; }
            // 연출 1차: 남은 인내가 이만큼이면 말풍선이 흔들린다(version 12)
            public double PatienceWarnSeconds { get; set; }
            public double CheckoutSeconds { get; set; }
            public int ShelfCapacity { get; set; }
            public int OvenCount { get; set; }
            // v0.6: 웜뱃이 계산대에서 바로 아래 칸 오븐까지 걷는 시간(편도). 더 먼 칸은 walkSpeed로 더한다
            public double WombatWalkSeconds { get; set; }
            // 굴 격자 설계 v0.5(version 13): 칸 크기(유닛)·입구 줄 높이·걷는 속도(유닛/초)·파기 비용
            public double CellWidth { get; set; }
            public double CellHeight { get; set; }
            public double EntranceHeight { get; set; }
            public double WalkSpeed { get; set; }
            public double DigBaseCost { get; set; }
            public double DigCostGrowth { get; set; }
        }
    }
}
