using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 실제 JSON 테이블을 읽어 테스트에 넘긴다. 경로는 프로젝트 폴더 기준. 부를 때마다 새로 읽어 행을 고쳐도 다른 테스트에 번지지 않는다.
    // 설계 25: 창고 시작 재료는 넉넉하게 바꾼다(굽기 테스트가 재료로 멈추지 않게). 재료 수를 보는 테스트는 창고를 직접 비운다
    internal static class TestTables
    {
        private const string k_DataPath = "Assets/Resources/Data";
        public const int k_PlentyItems = 100000;

        // editRows: table 한 파일의 rows를 읽기 전에 고친다(행 빼기 등)
        public static TableSet Load(string table = null, Action<JArray> editRows = null)
        {
            return new TableSet(name =>
            {
                string text = File.ReadAllText(Path.Combine(k_DataPath, name + ".json"));

                if (name != table && name != "ItemTable")
                {
                    return text;
                }

                JObject file = JObject.Parse(text);

                if (name == "ItemTable")
                {
                    foreach (JToken row in file["rows"])
                    {
                        row["start"] = k_PlentyItems;
                    }
                }

                if (name == table)
                {
                    editRows((JArray)file["rows"]);
                }

                return file.ToString();
            });
        }

        // 설계 40: 가게 별 마일스톤 상한을 걷어 낸다(별과 무관한 사물 값 · 업그레이드 시험용)
        public static void LiftStarCaps(TableSet tables)
        {
            foreach (StarMilestoneTable milestone in tables.GetAll<StarMilestoneTable>())
            {
                milestone.ShelfMax = milestone.OvenMax = milestone.CounterMax = milestone.UpgradeMax = 99;
            }
        }

        public static TableSet LoadWithout(string table, string id)
        {
            return Load(table, rows => rows.First(row => (string)row["id"] == id).Remove());
        }

        // 봉투(table·version) 검사용
        public static TableFile<object> LoadFile(string table)
        {
            return JsonConvert.DeserializeObject<TableFile<object>>(File.ReadAllText(Path.Combine(k_DataPath, table + ".json")));
        }
    }
}
