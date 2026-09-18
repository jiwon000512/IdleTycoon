namespace ZooTycoon.Core
{
    // 설계 08 v0.5: Bread가 null이면 빈 오븐. 다 구우면 Ready개가 진열대에 자리가 날 때까지 대기한다
    public sealed class Oven
    {
        public BreadRecord Bread { get; internal set; }
        public double Remaining { get; internal set; }
        public int Ready { get; internal set; }

        public bool IsEmpty => Bread == null;
    }
}
