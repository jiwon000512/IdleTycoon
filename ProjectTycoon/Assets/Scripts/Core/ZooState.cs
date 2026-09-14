using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    public sealed class ZooState
    {
        private readonly List<PlacedAnimal> m_placedAnimals = new List<PlacedAnimal>();
        private readonly List<string> m_waitingRoom = new List<string>();
        private double m_coins;
        private double m_totalCoinsEarned;

        public double Coins => m_coins;
        public double TotalCoinsEarned => m_totalCoinsEarned;
        public int PullCount { get; private set; }
        public int PromotionStage { get; private set; }
        public IReadOnlyList<PlacedAnimal> PlacedAnimals => m_placedAnimals;
        public IReadOnlyList<string> WaitingRoom => m_waitingRoom;

        public event Action<double> CoinsChanged;

        private ZooState(double coins)
        {
            m_coins = coins;
        }

        // 기획서 6.4: 시작 상태 — 코인 300, 동물 0, 홍보 잠김
        public static ZooState CreateNew(GameConfig config)
        {
            return new ZooState(config.Start.Coins);
        }

        public void AddCoins(double amount)
        {
            m_coins += amount;
            m_totalCoinsEarned += amount;
            OnCoinsChanged(m_coins);
        }

        public bool TrySpendCoins(double amount)
        {
            if (m_coins < amount)
            {
                return false;
            }

            m_coins -= amount;
            OnCoinsChanged(m_coins);
            return true;
        }

        private void OnCoinsChanged(double coins)
        {
            CoinsChanged?.Invoke(coins);
        }
    }
}
