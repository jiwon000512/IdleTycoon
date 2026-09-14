using System.IO;
using System.Linq;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 데이터-테이블-규칙 7장: animals.sprite 경로에 파일이 있어야 한다(더미여도 됨)
    public sealed class AnimalSpriteFileTests
    {
        private const string k_ResourcesPath = "Assets/Resources";

        [Test]
        public void Sprite_OfEveryAnimal_ExistsUnderResources()
        {
            foreach (AnimalRecord animal in TestTables.LoadRows<AnimalRecord>("animals"))
            {
                string directory = Path.Combine(k_ResourcesPath, Path.GetDirectoryName(animal.Sprite));
                string fileName = Path.GetFileName(animal.Sprite);
                bool exists = Directory.Exists(directory)
                    && Directory.GetFiles(directory, fileName + ".*").Any(f => !f.EndsWith(".meta"));

                Assert.That(exists, Is.True, $"animals '{animal.Id}': {animal.Sprite} 파일이 없다.");
            }
        }
    }
}
