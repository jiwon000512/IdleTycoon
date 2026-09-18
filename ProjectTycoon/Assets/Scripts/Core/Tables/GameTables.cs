using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    public sealed class GameTables
    {
        public IReadOnlyList<ZooLevelRecord> ZooLevels { get; }
        public IReadOnlyList<VisitorRecord> Visitors { get; }
        public IReadOnlyList<BreadRecord> Breads { get; }
        public IReadOnlyList<ShopUpgradeRecord> ShopUpgrades { get; }
        public IReadOnlyList<StringRecord> StringRows { get; }
        public StringTable Strings { get; }
        public GameConfig Config { get; }

        public GameTables(
            IReadOnlyList<ZooLevelRecord> zooLevels,
            IReadOnlyList<VisitorRecord> visitors,
            IReadOnlyList<BreadRecord> breads,
            IReadOnlyList<ShopUpgradeRecord> shopUpgrades,
            IReadOnlyList<StringRecord> strings,
            GameConfig config)
        {
            ZooLevels = zooLevels;
            Visitors = visitors;
            Breads = breads;
            ShopUpgrades = shopUpgrades;
            StringRows = strings;
            Strings = new StringTable(strings);
            Config = config;
        }
    }
}
