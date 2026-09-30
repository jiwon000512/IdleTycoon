using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 10·23 · 데이터-테이블-규칙 8.12: 효과음(SoundTable.json 행). 어느 사건에 나는지는 코드(World WorldSound · UI View)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class SoundTable : Table<string>
    {
        public const string k_Pay = "pay";
        public const string k_OvenDone = "oven_done";
        public const string k_Receipt = "receipt";
        public const string k_BakeStart = "bake_start";
        public const string k_TakeOut = "take_out";
        public const string k_Put = "put";
        public const string k_Pick = "pick";
        public const string k_Dig = "dig";
        public const string k_Upgrade = "upgrade";
        public const string k_Place = "place";
        public const string k_ClerkHired = "clerk_hired";
        public const string k_ClerkFired = "clerk_fired";
        public const string k_Payday = "payday";
        public const string k_Wake = "wake";
        public const string k_Passage = "passage";
        public const string k_Say = "say";
        public const string k_UiTap = "ui_tap";
        public const string k_UiOpen = "ui_open";
        public const string k_UiClose = "ui_close";
        public const string k_NegoGood = "nego_good";
        public const string k_NegoBad = "nego_bad";
        // 설계 24: 똥 떨어짐 · 치움
        public const string k_Poop = "poop";
        public const string k_Clean = "clean";
        // 설계 25: 밭에 심음 · 거둠
        public const string k_Plant = "plant";
        public const string k_Harvest = "harvest";
        // 웜뱃 걸음(걷기 프레임 1 · 5)
        public const string k_Step = "step";
        // 설계 28: 거둘 때 덤이 나옴
        public const string k_Bonus = "bonus";
        // 설계 30: 석상에 빌기(이름이 돌기 시작) · 축복이 멈춰 걸림 · 축복이 풀림
        public const string k_StatueRoll = "statue_roll";
        public const string k_StatueBlessed = "statue_blessed";
        public const string k_BlessingEnd = "blessing_end";

        // 코드가 부르는 효과음 전부(표에 모두 있어야 한다)
        public static readonly string[] Ids =
        {
            k_Pay,
            k_OvenDone,
            k_Receipt,
            k_BakeStart,
            k_TakeOut,
            k_Put,
            k_Pick,
            k_Dig,
            k_Upgrade,
            k_Place,
            k_ClerkHired,
            k_ClerkFired,
            k_Payday,
            k_Wake,
            k_Passage,
            k_Say,
            k_UiTap,
            k_UiOpen,
            k_UiClose,
            k_NegoGood,
            k_NegoBad,
            k_Poop,
            k_Clean,
            k_Plant,
            k_Harvest,
            k_Step,
            k_Bonus,
            k_StatueRoll,
            k_StatueBlessed,
            k_BlessingEnd,
        };

        // Resources/ 기준, 확장자 없음
        public string Clip { get; set; }
        public double Volume { get; set; }
        // 이 간격 안에 다시 나면 건너뛴다(초)
        public double MinGap { get; set; }
        // 이 시간 안에 이어 나면 피치를 pitchStep씩 올린다(초). 0이면 안 올림
        public double ComboSeconds { get; set; }
        public double PitchStep { get; set; }
        public double PitchMax { get; set; }
    }
}
