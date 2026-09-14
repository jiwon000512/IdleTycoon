using System.IO;
using System.Linq;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 데이터-테이블-규칙 7장: animals의 sprite·idleSheet·moveSheet 경로에 파일이 있어야 한다
    public sealed class AnimalSpriteFileTests
    {
        private const string k_ResourcesPath = "Assets/Resources";

        [Test]
        public void SpriteAndSheets_OfEveryAnimal_ExistUnderResources()
        {
            foreach (AnimalRecord animal in TestTables.LoadRows<AnimalRecord>("animals"))
            {
                foreach (string path in new[] { animal.Sprite, animal.IdleSheet, animal.MoveSheet })
                {
                    if (path == null)
                    {
                        continue;
                    }

                    string directory = Path.Combine(k_ResourcesPath, Path.GetDirectoryName(path));
                    string fileName = Path.GetFileName(path);
                    bool exists = Directory.Exists(directory)
                        && Directory.GetFiles(directory, fileName + ".*").Any(f => !f.EndsWith(".meta"));

                    Assert.That(exists, Is.True, $"animals '{animal.Id}': {path} 파일이 없다.");
                }
            }
        }
    }
}
