namespace ZooTycoon.Core
{
    // 기획서 6.2~6.4 · 데이터-테이블-규칙 8.4 (version 6)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class GameConfig
    {
        public StartConfig Start { get; set; }
        public GachaConfig Gacha { get; set; }
        public AnimalCountConfig AnimalCount { get; set; }
        public PromotionConfig Promotion { get; set; }
        public OfflineConfig Offline { get; set; }
        public IncomeConfig Income { get; set; }
        public VisitorsConfig Visitors { get; set; }

        public sealed class StartConfig
        {
            public int Coins { get; set; }
        }

        public sealed class GachaConfig
        {
            public int BaseCost { get; set; }
            public double CostGrowth { get; set; }
        }

        public sealed class AnimalCountConfig
        {
            public double IncomeBonusPerAnimal { get; set; }
        }

        public sealed class PromotionConfig
        {
            public int MaxStage { get; set; }
            public double MultiplierPerStage { get; set; }
            public int BaseCost { get; set; }
            public double CostGrowth { get; set; }
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

        // 설계 06 P1: 관광객 수 = f(초당 수입). 기획서 6.4(v0.14)
        public sealed class VisitorsConfig
        {
            public int MaxCount { get; set; }
            public double IncomeUnit { get; set; }
        }
    }
}
