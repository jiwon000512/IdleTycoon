using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 59 ① 특기 · 데이터-테이블-규칙 8.40: 후보 · 점원의 특기 하나(TraitTable.json 행). effect = 어디에 곱하나, value = 곱(1이 기본).
    // speed: 그 점원이 맡은 오븐 굽기 · 계산대 계산 속도 / tip: 그 점원이 계산한 손님의 팁 확률 / idle: 딴짓 확률 / wage: 기본 월급(협상 시작값)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class TraitTable : Table<string>
    {
        public const string k_Speed = "speed";
        public const string k_Tip = "tip";
        public const string k_Idle = "idle";
        public const string k_Wage = "wage";

        public string Effect { get; set; }
        public double Value { get; set; }
        // 명부 아이콘(Resources 기준, 그림이 올 때까지 임시)
        public string Icon { get; set; }
    }
}
