using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GameKit.Tables;

namespace ZooTycoon.Tests
{
    // 실제 JSON 테이블을 읽어 테스트에 넘긴다. 경로는 프로젝트 폴더 기준. 부를 때마다 새로 읽어 행을 고쳐도 다른 테스트에 번지지 않는다
    internal static class TestTables
    {
        private const string k_DataPath = "Assets/Resources/Data";

        // editRows: table 한 파일의 rows를 읽기 전에 고친다(행 빼기 등)
        public static TableSet Load(string table = null, Action<JArray> editRows = null)
        {
            return new TableSet(name =>
            {
                string text = File.ReadAllText(Path.Combine(k_DataPath, name + ".json"));

                if (name != table)
                {
                    return text;
                }

                JObject file = JObject.Parse(text);
                editRows((JArray)file["rows"]);
                return file.ToString();
            });
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
