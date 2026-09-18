namespace ZooTycoon.Core
{
    // 설계 08 v0.5: Bread가 null이면 빈 오븐. 다 구우면 Ready개가 진열대에 자리가 날 때까지 대기한다
    // v0.6: 굽기는 웜뱃이 오븐에 도착해야 시작한다(Started)
    public sealed class Oven
    {
        public BreadRecord Bread { get; internal set; }
        public bool Started { get; internal set; }
        public double Remaining { get; internal set; }
        public int Ready { get; internal set; }

        public bool IsEmpty => Bread == null;
    }
}
