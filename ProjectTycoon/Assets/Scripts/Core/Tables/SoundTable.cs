using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 10 · 데이터-테이블-규칙 8.12: 효과음(SoundTable.json 행). 어느 사건에 나는지는 코드(World BakerySound)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class SoundTable : Table<string>
    {
        public const string k_Pay = "pay";
        public const string k_OvenDone = "oven_done";
        public const string k_GiveUp = "give_up";

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
