namespace ZooTycoon.Core
{
    // 설계 08 v0.5: Bread가 null이면 빈 오븐. 설계 09: 다 구우면 Ready개가 웜뱃이 꺼낼 때까지 대기한다
    public sealed class Oven
    {
        // 굴 격자 설계 v0.5: 놓인 칸
        public Cell Cell { get; internal set; }
        public BreadTable Bread { get; internal set; }
        public double Remaining { get; internal set; }
        public int Ready { get; internal set; }

        public bool IsEmpty => Bread == null;
    }
}
