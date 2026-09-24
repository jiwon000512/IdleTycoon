namespace ZooTycoon.Core
{
    // 설계 13 v0.5: 행동하는 쪽. 손(든 물건) + 지갑(코인). 웜뱃이 하나 갖고, 직원이 생기면 직원도 하나씩
    public sealed class Worker
    {
        public Hands Hands { get; }
        public ZooState Wallet { get; }

        public Worker(Hands hands, ZooState wallet)
        {
            Hands = hands;
            Wallet = wallet;
        }
    }
}
