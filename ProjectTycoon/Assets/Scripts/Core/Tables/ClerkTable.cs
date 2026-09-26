using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 21 · 데이터-테이블-규칙 8.15: 점원 역할(ClerkTable.json 행). id = 붙는 사물(InteractableTable id): 오븐 점원·계산 점원
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ClerkTable : Table<string>
    {
        public string Name { get; set; }
        // 일머리 0일 때 월급(코인/주기). 기본 월급 = BaseWage × (1 + 일머리/100 × ClerkConfigTable.WagePerSkill)
        public double BaseWage { get; set; }
    }
}
