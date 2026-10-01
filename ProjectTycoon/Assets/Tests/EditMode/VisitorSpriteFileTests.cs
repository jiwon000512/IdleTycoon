using System.IO;
using System.Linq;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 데이터-테이블-규칙 7장: VisitorTable의 sprite·시트 경로, ActionTable의 icon·SoundTable의 clip·DecorationTable의 sprite·ItemTable의 icon·CropTable의 단계 그림 경로에 파일이 있어야 한다
    public sealed class VisitorSpriteFileTests
    {
        private const string k_ResourcesPath = "Assets/Resources";

        [Test]
        public void SpriteAndSheets_OfEveryVisitor_ExistUnderResources()
        {
            foreach (VisitorTable visitor in TestTables.Load().GetAll<VisitorTable>())
            {
                foreach (string path in new[] { visitor.Sprite, visitor.IdleSheet, visitor.MoveSheet, visitor.BackIdleSheet, visitor.BackMoveSheet, visitor.SideIdleSheet, visitor.SideMoveSheet, visitor.BlinkSheet, visitor.SideBlinkSheet, visitor.FidgetSheet, visitor.BackFidgetSheet, visitor.SideFidgetSheet })
                {
                    if (path == null)
                    {
                        continue;
                    }

                    string directory = Path.Combine(k_ResourcesPath, Path.GetDirectoryName(path));
                    string fileName = Path.GetFileName(path);
                    bool exists = Directory.Exists(directory)
                        && Directory.GetFiles(directory, fileName + ".*").Any(f => !f.EndsWith(".meta"));

                    Assert.That(exists, Is.True, $"VisitorTable '{visitor.Id}': {path} 파일이 없다.");
                }
            }
        }

        // 설계 09 v0.4: ActionTable의 icon 경로에도 파일이 있어야 한다
        [Test]
        public void Icon_OfEveryAction_ExistsUnderResources()
        {
            foreach (ActionTable action in TestTables.Load().GetAll<ActionTable>())
            {
                if (action.Icon == null)
                {
                    continue;
                }

                string directory = Path.Combine(k_ResourcesPath, Path.GetDirectoryName(action.Icon));
                bool exists = Directory.Exists(directory)
                    && Directory.GetFiles(directory, Path.GetFileName(action.Icon) + ".*").Any(f => !f.EndsWith(".meta"));

                Assert.That(exists, Is.True, $"ActionTable '{action.Id}': {action.Icon} 파일이 없다.");
            }
        }

        // 설계 10: SoundTable의 clip 경로에도 파일이 있어야 한다
        [Test]
        public void Clip_OfEverySound_ExistsUnderResources()
        {
            foreach (SoundTable sound in TestTables.Load().GetAll<SoundTable>())
            {
                string directory = Path.Combine(k_ResourcesPath, Path.GetDirectoryName(sound.Clip));
                bool exists = Directory.Exists(directory)
                    && Directory.GetFiles(directory, Path.GetFileName(sound.Clip) + ".*").Any(f => !f.EndsWith(".meta"));

                Assert.That(exists, Is.True, $"SoundTable '{sound.Id}': {sound.Clip} 파일이 없다.");
            }
        }

        // 설계 11
        [Test]
        public void Sprite_OfEveryDecoration_ExistsUnderResources()
        {
            foreach (DecorationTable decor in TestTables.Load().GetAll<DecorationTable>())
            {
                string directory = Path.Combine(k_ResourcesPath, Path.GetDirectoryName(decor.Sprite));
                bool exists = Directory.Exists(directory)
                    && Directory.GetFiles(directory, Path.GetFileName(decor.Sprite) + ".*").Any(f => !f.EndsWith(".meta"));

                Assert.That(exists, Is.True, $"DecorationTable '{decor.Id}': {decor.Sprite} 파일이 없다.");

                for (int i = 0; i < decor.Frames; i++)
                {
                    string frame = Path.GetFileName(decor.Sprite) + "_" + i + ".png";
                    Assert.That(File.Exists(Path.Combine(directory, frame)), Is.True, $"DecorationTable '{decor.Id}': {frame} 파일이 없다.");
                }
            }
        }

        // 설계 25 · 27: 재료 아이콘, 작물 단계 그림(sprite_단계_반쪽, 단계 0 ~ stages − 1 × 반쪽 0 · 1). 무럭무럭(2026-09-30): 단계 1부터 흔들림 판 _l · _r. 작물마다 다 익음 표시(readyMark, 2026-10-01)
        [Test]
        public void Icon_OfEveryItem_AndStages_OfEveryCrop_ExistUnderResources()
        {
            foreach (ItemTable item in TestTables.Load().GetAll<ItemTable>())
            {
                Assert.That(File.Exists(Path.Combine(k_ResourcesPath, item.Icon + ".png")), Is.True, $"ItemTable '{item.Id}': {item.Icon} 파일이 없다.");
            }

            foreach (CropTable crop in TestTables.Load().GetAll<CropTable>())
            {
                Assert.That(File.Exists(Path.Combine(k_ResourcesPath, crop.ReadyMark + ".png")), Is.True, $"CropTable '{crop.Id}': {crop.ReadyMark} 파일이 없다.");

                for (int i = 0; i < crop.Stages; i++)
                {
                    for (int half = 0; half < 2; half++)
                    {
                        foreach (string sway in i == 0 ? new[] { "" } : new[] { "", "_l", "_r" })
                        {
                            string stage = crop.Sprite + "_" + i + "_" + half + sway + ".png";
                            Assert.That(File.Exists(Path.Combine(k_ResourcesPath, stage)), Is.True, $"CropTable '{crop.Id}': {stage} 파일이 없다.");
                        }
                    }
                }
            }
        }
    }
}
