using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 44 · 데이터-테이블-규칙 8.29: 낚싯대(RodTable.json 행). 계열 대는 소환으로 나오고 셋을 합쳐 등급이 오른다, 특별한 대는 대물 뱃속 3택 1에서만
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class RodTable : Table<string>
    {
        public const string k_Bamboo = "bamboo";
        public const string k_Iron = "iron";
        public const string k_Bait = "bait";
        // 특별한 대의 능력
        public const string k_Pulley = "pulley";
        public const string k_Lighthouse = "lighthouse";
        public const string k_Whirlpool = "whirlpool";

        // 계열(계열 문턱을 센다). 계열 대는 자기 id
        public string Family { get; set; }
        // 특별한 대의 능력(pulley · lighthouse · whirlpool), 계열 대는 null
        public string Special { get; set; }
        // 아직 소환에 나오지 않는 계열(그물 · 빛은 뒤로)
        public bool Locked { get; set; }
        // 초당 감는 힘(1등급) · 사거리 반지름(유닛)
        public double Reel { get; set; }
        public double Range { get; set; }
        // 크기 배수: 작은 것 · 큰 것(보통은 1)
        public double SmallScale { get; set; }
        public double BigScale { get; set; }
        // 사거리 안 물고기 속도 배수(미끼, 1 = 그대로)
        public double Slow { get; set; }
        // 특별한 능력의 값: pulley = 대물 감는 힘 배수 · lighthouse = 다른 대 사거리 + 비율 · whirlpool = 사거리만큼 되돌리는 간격(초)
        public double Effect { get; set; }

        public bool IsSpecial => Special != null;
    }
}
