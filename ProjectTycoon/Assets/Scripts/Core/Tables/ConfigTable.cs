using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 데이터-테이블-규칙 8.4: 게임 전체에 하나뿐인 값(ConfigTable.json 행, Id = 값 이름)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ConfigTable : Table<string>
    {
        public const string k_StartCoins = "startCoins";
        public const string k_OfflineMaxSeconds = "offlineMaxSeconds";
        // 굴 칸 크기(유닛)·입구 줄 높이: 빵집·광장 공통
        public const string k_CellWidth = "cellWidth";
        public const string k_CellHeight = "cellHeight";
        public const string k_EntranceHeight = "entranceHeight";
        // 손님 걷기·웜뱃 조이스틱 속도(유닛/초), 구멍·문에서 톡 뛰는 시간
        public const string k_WalkSpeed = "walkSpeed";
        public const string k_WombatSpeed = "wombatSpeed";
        public const string k_HopSeconds = "hopSeconds";

        public double Value { get; set; }
    }
}
