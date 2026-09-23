namespace ZooTycoon.Core
{
    public enum CustomerPhase
    {
        ToShelf,
        AtShelf,
        ToQueue,
        Queued,
    }

    // 설계 08 v0.5: 빵집 손님 한 명. Timer = 지금 단계의 남은 시간(줄에서는 줄 머리일 때만 계산 시간)
    public sealed class Customer
    {
        public int Id { get; }
        public BreadRecord Bread { get; }
        // 굴 격자 설계 v0.5: 가려는 진열대의 칸
        public Cell Cell { get; }
        public CustomerPhase Phase { get; internal set; }
        public double Timer { get; internal set; }

        internal Customer(int id, BreadRecord bread, Cell cell, double timer)
        {
            Id = id;
            Bread = bread;
            Cell = cell;
            Phase = CustomerPhase.ToShelf;
            Timer = timer;
        }
    }
}
