using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 31 · 데이터-테이블-규칙 8.24: 유물 한 가지(RelicTable.json 행). effect = 코드가 거는 곳(퍼센트는 축복과 같은 효과 키, 규칙은 여기 k_*),
    // values = 별 [★1, ★2, ★3]의 값, 비중은 뽑을 때 후보로 뜰 몫
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class RelicTable : Table<string>
    {
        // 규칙 유물: N번째 계산마다 값 ×2 · 똥을 치우면 코인 · 거둘 때 확률로 작물 +1 · 점원 딴짓 최대 초
        public const string k_Abacus = "abacus";
        public const string k_Scoop = "scoop";
        public const string k_Basket = "basket";
        public const string k_Clock = "clock";

        // 코드가 거는 효과 키(퍼센트는 ZooState.Scale, 규칙은 위 넷)
        public static readonly string[] Effects =
        {
            BlessingTable.k_Bake, BlessingTable.k_Checkout, BlessingTable.k_Price, BlessingTable.k_Visitors, BlessingTable.k_Grow,
            k_Abacus, k_Scoop, k_Basket, k_Clock,
        };

        // 이름 · 효과 글 형식(StringTable 키. 형식은 {0} 퍼센트 숫자(값 × 100), {1} 값 그대로)
        public string Name { get; set; }
        public string Format { get; set; }
        public string Icon { get; set; }
        public string Effect { get; set; }
        public double[] Values { get; set; }
        public double Weight { get; set; }
    }
}
