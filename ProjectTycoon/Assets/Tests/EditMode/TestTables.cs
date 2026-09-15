using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 실제 JSON 테이블을 읽어 테스트에 넘긴다. 경로는 프로젝트 폴더 기준.
    internal static class TestTables
    {
        private const string k_DataPath = "Assets/Resources/Data";

        public static TableFile<TRow> LoadFile<TRow>(string table)
        {
            string text = File.ReadAllText(Path.Combine(k_DataPath, table + ".json"));
            return JsonConvert.DeserializeObject<TableFile<TRow>>(text);
        }

        public static List<TRow> LoadRows<TRow>(string table)
        {
            return LoadFile<TRow>(table).Rows;
        }

        public static GameConfig LoadConfig()
        {
            string text = File.ReadAllText(Path.Combine(k_DataPath, "game_config.json"));
            return JsonConvert.DeserializeObject<GameConfig>(text);
        }

        public static GameTables Build(
            IReadOnlyList<AnimalRecord> animals = null,
            IReadOnlyList<GradeRecord> grades = null,
            IReadOnlyList<ZooLevelRecord> zooLevels = null,
            IReadOnlyList<VisitorRecord> visitors = null,
            IReadOnlyList<FacilityRecord> facilities = null,
            IReadOnlyList<StringRecord> strings = null,
            GameConfig config = null)
        {
            return new GameTables(
                animals ?? LoadRows<AnimalRecord>("animals"),
                grades ?? LoadRows<GradeRecord>("grades"),
                zooLevels ?? LoadRows<ZooLevelRecord>("zoo_levels"),
                visitors ?? LoadRows<VisitorRecord>("visitors"),
                facilities ?? LoadRows<FacilityRecord>("facilities"),
                strings ?? LoadRows<StringRecord>("strings"),
                config ?? LoadConfig());
        }
    }
}
