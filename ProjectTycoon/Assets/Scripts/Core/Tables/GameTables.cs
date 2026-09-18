using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    public sealed class GameTables
    {
        private readonly Dictionary<string, AnimalRecord> m_animalsById =
            new Dictionary<string, AnimalRecord>(StringComparer.Ordinal);

        public IReadOnlyList<AnimalRecord> Animals { get; }
        public IReadOnlyList<ZooLevelRecord> ZooLevels { get; }
        public IReadOnlyList<VisitorRecord> Visitors { get; }
        public IReadOnlyList<BreadRecord> Breads { get; }
        public IReadOnlyList<ShopUpgradeRecord> ShopUpgrades { get; }
        public IReadOnlyList<StringRecord> StringRows { get; }
        public StringTable Strings { get; }
        public GameConfig Config { get; }

        public GameTables(
            IReadOnlyList<AnimalRecord> animals,
            IReadOnlyList<ZooLevelRecord> zooLevels,
            IReadOnlyList<VisitorRecord> visitors,
            IReadOnlyList<BreadRecord> breads,
            IReadOnlyList<ShopUpgradeRecord> shopUpgrades,
            IReadOnlyList<StringRecord> strings,
            GameConfig config)
        {
            Animals = animals;
            ZooLevels = zooLevels;
            Visitors = visitors;
            Breads = breads;
            ShopUpgrades = shopUpgrades;
            StringRows = strings;
            Strings = new StringTable(strings);
            Config = config;

            for (int i = 0; i < animals.Count; i++)
            {
                m_animalsById[animals[i].Id] = animals[i];
            }
        }

        public AnimalRecord GetAnimal(string id)
        {
            if (!m_animalsById.TryGetValue(id, out AnimalRecord animal))
            {
                throw new KeyNotFoundException($"동물 ID '{id}'가 animals.json에 없다.");
            }

            return animal;
        }
    }
}
