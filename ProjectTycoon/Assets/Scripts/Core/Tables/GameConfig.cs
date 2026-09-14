namespace ZooTycoon.Core
{
    // 기획서 6.2~6.4 · 데이터-테이블-규칙 8.4
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class GameConfig
    {
        public StartConfig Start { get; set; }
        public GachaConfig Gacha { get; set; }
        public AnimalLevelConfig AnimalLevel { get; set; }
        public PromotionConfig Promotion { get; set; }
        public OfflineConfig Offline { get; set; }
        public IncomeConfig Income { get; set; }

        public sealed class StartConfig
        {
            public int Coins { get; set; }
        }

        public sealed class GachaConfig
        {
            public int BaseCost { get; set; }
            public double CostGrowth { get; set; }
        }

        public sealed class AnimalLevelConfig
        {
            public double IncomeBonusPerLevel { get; set; }
        }

        public sealed class PromotionConfig
        {
            public int MaxStage { get; set; }
            public double MultiplierPerStage { get; set; }
            public int BaseCost { get; set; }
            public double CostGrowth { get; set; }
            public int VisitorsPerStage { get; set; }
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
    }
}
