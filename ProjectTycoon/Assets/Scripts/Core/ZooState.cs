using System;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 지갑(코인). 웜뱃의 일꾼(Worker)이 갖고, 계산대가 값을 넣는다
    public sealed class ZooState
    {
        public double Coins { get; private set; }

        public event Action CoinsChanged;

        private ZooState(double coins)
        {
            Coins = coins;
        }

        // 기획서 6.4: 시작 상태 — 코인 350
        public static ZooState CreateNew(TableSet tables)
        {
            return new ZooState(tables.Get<ConfigTable>(ConfigTable.k_StartCoins).Value);
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
            CoinsChanged?.Invoke();
        }
    }
}
