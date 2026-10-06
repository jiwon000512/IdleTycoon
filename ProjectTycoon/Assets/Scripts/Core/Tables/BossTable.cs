using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 50 · 데이터-테이블-규칙 8.33: 낚시터 대물(BossTable.json 행 = 대물 하나). 표 순서 = 단계 순서이고 끝나면 처음부터 되풀이한다(단계 n → (n − 1) mod 행 수).
    // 물속에서는 그림자(공통)로 헤엄치고 낚이는 순간 이 행의 그림으로 드러난다. 이름 · 소식지 글은 StringTable boss_<id> · boss_<id>_title · boss_<id>_line
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class BossTable : Table<string>
    {
        // 낚인 옆모습(Resources/ 기준 경로, 확장자 없음): 드러나는 연출과 낚시 소식의 그림
        public string Sprite { get; set; }
        // 무게 · 속도 배수(FishingConfigTable boss.weight × 단계 배수, boss.fish의 속도 위에 곱한다)
        public double WeightScale { get; set; }
        public double SpeedScale { get; set; }
        // 낚으면 받는 재료(ItemTable id, 없으면 null)와 개수(× 단계 보람) · 반짝돌 개수(단계 보람을 곱하지 않는다)
        public string Item { get; set; }
        public int Count { get; set; }
        public int Gems { get; set; }
    }
}
