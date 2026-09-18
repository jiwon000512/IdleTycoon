using System;

namespace ZooTycoon.Core
{
    public sealed class ZooState
    {
        public double Coins { get; private set; }
        public double TotalCoinsEarned { get; private set; }

        public event Action CoinsChanged;

        private ZooState(double coins)
        {
            Coins = coins;
        }

        // 기획서 6.4: 시작 상태 — 코인 350
        public static ZooState CreateNew(GameConfig config)
        {
            return new ZooState(config.Start.Coins);
        }

        public void AddCoins(double amount)
        {
            Coins += amount;
            TotalCoinsEarned += amount;
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
