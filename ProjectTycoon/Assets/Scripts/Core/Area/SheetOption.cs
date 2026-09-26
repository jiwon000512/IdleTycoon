namespace ZooTycoon.Core
{
    // 시트 줄의 상태: 살 수 있음 · 코인 부족 · 최대 · 지금은 못 함(오븐이 굽는 중)
    public enum SheetOptionState
    {
        Enabled,
        Poor,
        Max,
        Blocked,
    }

    // 설계 13 v0.5: 시트 한 줄의 데이터. 글자는 UI가 행동 id별 서식으로 만든다
    public sealed class SheetOption
    {
        // 고른 것의 id(빵·업그레이드). 없으면 null
        public string Option { get; }
        public SheetOptionState State { get; }
        public double Cost { get; }
        // 업그레이드: 지금 단계와 효과 전후 값
        public int Level { get; }
        public double Before { get; }
        public double After { get; }

        public SheetOption(string option, SheetOptionState state, double cost = 0d, int level = 0, double before = 0d, double after = 0d)
        {
            Option = option;
            State = state;
            Cost = cost;
            Level = level;
            Before = before;
            After = after;
        }

        // 가격을 낼 수 있으면 Enabled, 아니면 Poor
        public static SheetOptionState Afford(Worker worker, double cost)
        {
            return worker.Wallet.Coins >= cost ? SheetOptionState.Enabled : SheetOptionState.Poor;
        }
    }
}
