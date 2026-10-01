using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 데이터-테이블-규칙 8.4: 게임 전체에 하나뿐인 값(ConfigTable.json 행, Id = 값 이름)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ConfigTable : Table<string>
    {
        public const string k_StartCoins = "startCoins";
        // 굴 칸 크기(유닛)·입구 줄 높이: 빵집·광장 공통
        public const string k_CellWidth = "cellWidth";
        public const string k_CellHeight = "cellHeight";
        public const string k_EntranceHeight = "entranceHeight";
        // 손님 걷기·웜뱃 조이스틱 속도(유닛/초), 구멍·문에서 톡 뛰는 시간
        public const string k_WalkSpeed = "walkSpeed";
        public const string k_WombatSpeed = "wombatSpeed";
        public const string k_HopSeconds = "hopSeconds";
        // 설계 09 · 리뷰 R2: 웜뱃이 드는 빵 수(웜뱃의 값이라 빵집 설정이 아니라 여기)
        public const string k_CarryCapacity = "carryCapacity";
        // 설계 18: 배치 격자 한 변(유닛). 사물 밑변 가운데를 여기에 맞춘다
        public const string k_PlaceCell = "placeCell";
        // 설계 24 → 37: 웜뱃 똥(곳 공용). poopEvery 유닛 걸을 때마다 poopChance로 하나(곳마다 바닥에 poopMax까지, 다른 똥 poopGap 안이면 건너뜀). 손님은 둘레 poopAvoidRadius 안을 걷지 않는다
        public const string k_PoopEvery = "poopEvery";
        public const string k_PoopChance = "poopChance";
        public const string k_PoopMax = "poopMax";
        public const string k_PoopGap = "poopGap";
        public const string k_PoopAvoidRadius = "poopAvoidRadius";

        public double Value { get; set; }
    }
}
