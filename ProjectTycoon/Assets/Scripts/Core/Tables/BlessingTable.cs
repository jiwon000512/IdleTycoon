using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 30 · 데이터-테이블-규칙 8.23: 석상 축복 한 가지(BlessingTable.json 행, Id = 효과). 걸린 동안 그 효과가 × (1 + value)(손님은 오는 간격 ÷ (1 + value)).
    // 비중은 빌 때 뽑힐 몫, 시간은 걸려 있는 초
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class BlessingTable : Table<string>
    {
        // 코드가 효과를 거는 곳: 오븐 속도 · 계산대 속도 · 계산 값 · 광장 손님 간격 · 밭 자람 · 거둘 때 덤 확률
        public const string k_Bake = "bake";
        public const string k_Checkout = "checkout";
        public const string k_Price = "price";
        public const string k_Visitors = "visitors";
        public const string k_Grow = "grow";
        public const string k_Bonus = "bonus";

        public static readonly string[] Ids = { k_Bake, k_Checkout, k_Price, k_Visitors, k_Grow, k_Bonus };

        // 이름 · 효과 글 형식(StringTable 키. 형식은 {0} 퍼센트 숫자(value × 100), {1} 배수(1 + value))
        public string Name { get; set; }
        public string Format { get; set; }
        public string Icon { get; set; }
        public double Value { get; set; }
        public double Seconds { get; set; }
        public double Weight { get; set; }
    }
}
