using System.IO;
using System.Linq;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 데이터-테이블-규칙 7장: visitors의 sprite·시트 6개 경로에 파일이 있어야 한다
    public sealed class VisitorSpriteFileTests
    {
        private const string k_ResourcesPath = "Assets/Resources";

        [Test]
        public void SpriteAndSheets_OfEveryVisitor_ExistUnderResources()
        {
            foreach (VisitorRecord visitor in TestTables.LoadRows<VisitorRecord>("visitors"))
            {
                foreach (string path in new[] { visitor.Sprite, visitor.IdleSheet, visitor.MoveSheet, visitor.BackIdleSheet, visitor.BackMoveSheet, visitor.SideIdleSheet, visitor.SideMoveSheet })
                {
                    if (path == null)
                    {
                        continue;
                    }

                    string directory = Path.Combine(k_ResourcesPath, Path.GetDirectoryName(path));
                    string fileName = Path.GetFileName(path);
                    bool exists = Directory.Exists(directory)
                        && Directory.GetFiles(directory, fileName + ".*").Any(f => !f.EndsWith(".meta"));

                    Assert.That(exists, Is.True, $"visitors '{visitor.Id}': {path} 파일이 없다.");
                }
            }
        }
    }
}
