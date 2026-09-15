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

        // 기획서 3장·6.2: 새 종은 한 마리로 시작, 이미 있으면 한 마리 추가(상한 없음). 마리 수를 돌려준다
        public int AddAnimal(string animalId)
        {
            OwnedAnimal animal = Find(animalId);

            if (animal == null)
            {
                animal = new OwnedAnimal(animalId, 0);
                m_ownedAnimals.Add(animal);
            }

            animal.Count++;
            OnAnimalsChanged();
            return animal.Count;
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
