using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    public sealed class GameTables
    {
        public IReadOnlyList<VisitorRecord> Visitors { get; }
        public IReadOnlyList<BreadRecord> Breads { get; }
        public IReadOnlyList<ShopUpgradeRecord> ShopUpgrades { get; }
        public IReadOnlyList<ActionRecord> Actions { get; }
        public IReadOnlyList<InteractableRecord> Interactables { get; }
        public IReadOnlyList<SoundRecord> Sounds { get; }
        public IReadOnlyList<StringRecord> StringRows { get; }
        public StringTable Strings { get; }
        public GameConfig Config { get; }

        public GameTables(
            IReadOnlyList<VisitorRecord> visitors,
            IReadOnlyList<BreadRecord> breads,
            IReadOnlyList<ShopUpgradeRecord> shopUpgrades,
            IReadOnlyList<ActionRecord> actions,
            IReadOnlyList<InteractableRecord> interactables,
            IReadOnlyList<SoundRecord> sounds,
            IReadOnlyList<StringRecord> strings,
            GameConfig config)
        {
            Visitors = visitors;
            Breads = breads;
            ShopUpgrades = shopUpgrades;
            Actions = actions;
            Interactables = interactables;
            Sounds = sounds;
            StringRows = strings;
            Strings = new StringTable(strings);
            Config = config;
        }
    }
}
