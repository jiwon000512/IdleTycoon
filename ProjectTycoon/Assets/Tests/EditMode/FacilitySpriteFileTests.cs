using System.IO;
using System.Linq;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 데이터-테이블-규칙 7장: facilities의 sprite 경로에 파일이 있어야 한다
    public sealed class FacilitySpriteFileTests
    {
        private const string k_ResourcesPath = "Assets/Resources";

        [Test]
        public void Sprite_OfEveryFacility_ExistsUnderResources()
        {
            foreach (FacilityRecord facility in TestTables.LoadRows<FacilityRecord>("facilities"))
            {
                string directory = Path.Combine(k_ResourcesPath, Path.GetDirectoryName(facility.Sprite));
                string fileName = Path.GetFileName(facility.Sprite);
                bool exists = Directory.Exists(directory)
                    && Directory.GetFiles(directory, fileName + ".*").Any(f => !f.EndsWith(".meta"));

                Assert.That(exists, Is.True, $"facilities '{facility.Id}': {facility.Sprite} 파일이 없다.");
            }
        }
    }
}
