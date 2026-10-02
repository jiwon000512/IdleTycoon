using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27 · 데이터-테이블-규칙 8.22: 농장 공통 숫자(FarmConfigTable.json 행, id main). 층마다 다른 칸 · 값은 FarmFloorTable(설계 39)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FarmConfigTable : Table<string>
    {
        public const string k_Main = "main";

        // 밭 몸통 여백(유닛, 칸 둘레): 익은 밭은 발이 몸통 안에 들 때만 거둔다(여백은 걷는 길)
        public double FieldInset { get; set; }
        // 설계 28: 심을 때 창고에 있으면 1개 쓰는 거름 재료와 그 밭의 자라는 시간 배율(0 초과 1 이하)
        public string ManureItem { get; set; }
        public double ManureGrowScale { get; set; }
        // 거둘 때 bonusChance로 하나 나오는 덤 재료. 거름 준 밭은 확률 × manureBonusScale
        public string BonusItem { get; set; }
        public double BonusChance { get; set; }
        public double ManureBonusScale { get; set; }
        // 설계 38: 농장 점원 자리(작업대 앞 바닥, 농장 원점 기준 유닛). 그림은 입구 장식 모종 작업대
        public double BarnX { get; set; }
        public double BarnY { get; set; }
    }
}
