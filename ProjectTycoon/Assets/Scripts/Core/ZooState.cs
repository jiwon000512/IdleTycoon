using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 지갑(코인). 웜뱃의 일꾼(Worker)이 갖고, 계산대가 값을 넣는다. 바뀌면 CoinsChanged 사건
    public sealed class ZooState
    {
        private readonly EventBus m_bus;

        public double Coins { get; private set; }

        private ZooState(double coins, EventBus bus)
        {
            Coins = coins;
            m_bus = bus;
        }

        // 기획서 6.4: 시작 상태 — 코인 350
        public static ZooState CreateNew(TableSet tables, EventBus bus)
        {
            return new ZooState(tables.Get<ConfigTable>(ConfigTable.k_StartCoins).Value, bus);
        }

        public void AddCoins(double amount)
        {
            Coins += amount;
            OnCoinsChanged();
        }

        public bool TrySpendCoins(double amount)
        {
            if (Coins < amount)
            {
                return false;
            }

            Coins -= amount;
            OnCoinsChanged();
            return true;
        }

        private void OnCoinsChanged()
        {
            m_bus.Publish(new Events.CoinsChanged(this));
        }
    }
}
