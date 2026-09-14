using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    public sealed class ZooState
    {
        private readonly List<OwnedAnimal> m_ownedAnimals = new List<OwnedAnimal>();

        public double Coins { get; private set; }
        public double TotalCoinsEarned { get; private set; }
        public int PullCount { get; private set; }
        public int PromotionStage { get; private set; }
        public IReadOnlyList<OwnedAnimal> OwnedAnimals => m_ownedAnimals;

        public event Action CoinsChanged;
        public event Action AnimalsChanged;

        private ZooState(double coins)
        {
            Coins = coins;
        }

        // 기획서 6.4: 시작 상태 — 코인 350, 동물 0, 홍보 잠김
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

        public void RecordPull()
        {
            PullCount++;
        }

        public bool Owns(string animalId)
        {
            return Find(animalId) != null;
        }

        // 기획서 3장: 새 종은 우리에 한 마리로 시작
        public void AddAnimal(string animalId)
        {
            if (Owns(animalId))
            {
                throw new InvalidOperationException($"동물 '{animalId}'는 이미 보유 중이다. LevelUpAnimal을 써야 한다.");
            }

            m_ownedAnimals.Add(new OwnedAnimal(animalId, 1));
            OnAnimalsChanged();
        }

        // 기획서 6.2: 중복 1회당 한 마리 추가(Level = 마리 수), 상한 없음
        public int LevelUpAnimal(string animalId)
        {
            OwnedAnimal animal = Find(animalId);

            if (animal == null)
            {
                throw new InvalidOperationException($"동물 '{animalId}'를 보유하고 있지 않다. AddAnimal을 써야 한다.");
            }

            animal.Level++;
            OnAnimalsChanged();
            return animal.Level;
        }

        private OwnedAnimal Find(string animalId)
        {
            return m_ownedAnimals.Find(a => a.AnimalId == animalId);
        }

        private void OnCoinsChanged()
        {
            CoinsChanged?.Invoke();
        }

        private void OnAnimalsChanged()
        {
            AnimalsChanged?.Invoke();
        }
    }
}
