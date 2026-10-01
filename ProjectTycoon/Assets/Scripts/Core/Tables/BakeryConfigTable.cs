using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 데이터-테이블-규칙 8.4: 빵집 시뮬 숫자(BakeryConfigTable.json 행, Id = 빵집 종류). 전부 시작값이며 프로토타입에서 튠한다
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class BakeryConfigTable : Table<string>
    {
        public const string k_Bakery = "bakery";

        public int MaxCustomers { get; set; }
        // 손님 동선 설계 v0.2: 빈 진열대 앞 두리번 시간 합계. 다 쓰면 「!!」로 나간다
        public double PatienceSeconds { get; set; }
        // 한 진열대에서 두리번하는 최대 시간. 지나면 다른 빵을 찾아간다
        public double LookSeconds { get; set; }
        public double CheckoutSeconds { get; set; }
        public int ShelfCapacity { get; set; }
        public double PickSeconds { get; set; }
        // 굴 격자 설계 v0.5: 파기 비용 = digBaseCost × digCostGrowth^(판 칸 수)
        public double DigBaseCost { get; set; }
        public double DigCostGrowth { get; set; }
    }
}
