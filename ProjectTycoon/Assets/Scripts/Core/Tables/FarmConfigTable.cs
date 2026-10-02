using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27 · 데이터-테이블-규칙 8.22: 농장 굴 숫자(FarmConfigTable.json 행, id main)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FarmConfigTable : Table<string>
    {
        public const string k_Main = "main";

        // 한 층의 칸 범위(가로 가운데 정렬, 세로 첫 줄은 입구 줄. 칸 크기는 ConfigTable)
        public int FloorCols { get; set; }
        public int FloorRows { get; set; }
        // 새 게임에 판 줄 수(입구 줄부터, 가운데 두 열)와 갈아 둔 밭 수
        public int StartRows { get; set; }
        public int StartFields { get; set; }
        // 파기 값 = digBaseCost × digCostGrowth^(판 칸 − 시작 칸). 갈기 값은 고정
        public double DigBaseCost { get; set; }
        public double DigCostGrowth { get; set; }
        public double TillCost { get; set; }
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
