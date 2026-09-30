using System.Collections.Generic;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 29 · 데이터-테이블-규칙 8.23: 웜뱃 석상 능력 한 가지(StatueTable.json 행). 값은 등급(보통 · 드묾 · 전설)마다 하나(비율, 0.1 = 10%), 비중은 굴릴 때 뽑힐 몫
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class StatueTable : Table<string>
    {
        // 코드가 능력을 거는 곳: 오븐 · 계산대 · 계산 값 · 광장 손님 간격 · 웜뱃 걸음 · 밭 자람 · 거둘 때 덤 확률(더하기)
        public const string k_Bake = "bake";
        public const string k_Checkout = "checkout";
        public const string k_Price = "price";
        public const string k_Visitors = "visitors";
        public const string k_Walk = "walk";
        public const string k_Grow = "grow";
        public const string k_Bonus = "bonus";

        public static readonly string[] Ids = { k_Bake, k_Checkout, k_Price, k_Visitors, k_Walk, k_Grow, k_Bonus };

        // 이름 · 줄 글 형식(StringTable 키. 형식은 {0} 이름, {1} 값 숫자)
        public string Name { get; set; }
        public string Format { get; set; }
        // 등급마다 값: [보통, 드묾, 전설]
        public List<double> Values { get; set; }
        public double Weight { get; set; }
    }
}
