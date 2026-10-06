using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 44 · 46 · 데이터-테이블-규칙 8.29: 낚싯대(RodTable.json 행). 열린 말뚝 시트에서 웜뱃이 골라 사고 등급은 뽑기. 설치물 효과는 미끼 노점 업그레이드(FishingConfigTable hutUpgrades)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class RodTable : Table<string>
    {
        public const string k_Bamboo = "bamboo";
        public const string k_Iron = "iron";
        public const string k_Bait = "bait";

        // 계열(계열 문턱을 센다). 계열 대는 자기 id
        public string Family { get; set; }
        // 아직 시트에 나오지 않는 계열(그물 · 빛은 뒤로)
        public bool Locked { get; set; }
        // 초당 감는 힘(1등급) · 사거리 반지름(유닛)
        public double Reel { get; set; }
        public double Range { get; set; }
        // 크기 배수: 작은 것 · 큰 것(보통은 1)
        public double SmallScale { get; set; }
        public double BigScale { get; set; }
        // 사거리 안 물고기 속도 배수(미끼, 1 = 그대로)
        public double Slow { get; set; }
        // 시트 칩 아이콘(Resources/ 기준 경로, 확장자 없음)
        public string Icon { get; set; }
    }
}
