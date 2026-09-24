using NUnit.Framework;
using TMPro;
using UnityEditor;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 폰트가 깨지지 않게(2026-09-24): 갈무리 폰트 에셋은 정적 아틀라스이고 화면에 나올 수 있는 글자를 모두 갖는다.
    // 실패하면 문구에 새 글자가 생긴 것: 메뉴 ZooTycoon/Bake/Fonts로 다시 굽는다
    public sealed class FontAssetTests
    {
        [TestCase("Galmuri9")]
        [TestCase("Galmuri11")]
        [TestCase("Galmuri11-Bold")]
        [TestCase("Galmuri14")]
        public void Galmuri_IsStatic_AndHasEveryScreenCharacter(string name)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/Galmuri/{name}.asset");
            string missing = string.Empty;

            foreach (char c in Texts.Characters(TestTables.Load()))
            {
                if (!font.HasCharacter(c))
                {
                    missing += c;
                }
            }

            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(missing, Is.Empty);
        }
    }
}
