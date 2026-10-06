using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 폰트가 깨지지 않게(2026-09-24): 갈무리 폰트 에셋은 정적 아틀라스이고 화면에 나올 수 있는 글자를 모두 갖는다.
    // 실패하면 문구에 새 글자가 생긴 것: 메뉴 ZooTycoon/Bake/Fonts로 다시 굽는다
    public sealed class FontAssetTests
    {
        [TestCase("Galmuri9")]
        [TestCase("Galmuri11-Bold")]
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

        // UI 규칙 1장 10(2026-09-30): UI 글자는 굵은 11(44)과 보통 9(36) 두 벌뿐이다. 새 UI 프리팹도 여기서 걸린다
        // 예외는 곳 이름 띠 하나: 굵은 11의 2배(88, 2026-10-06 사용자 시안 B)
        [Test]
        public void UiPrefabs_UseOnlyTheTwoUiFonts()
        {
            string wrong = string.Empty;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/UI", "Assets/Prefabs/UI" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (TextMeshProUGUI text in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    string font = $"{text.font.name}/{text.fontSize}";

                    bool title = path.EndsWith("/LocationView.prefab") && font == "Galmuri11-Bold/88";

                    if (font != "Galmuri11-Bold/44" && font != "Galmuri9/36" && !title)
                    {
                        wrong += $"{path} {text.name} {font} · ";
                    }
                }
            }

            Assert.That(wrong, Is.Empty, "UI 글자는 Galmuri11-Bold 44(제목 · 이름 · 개수 · 이름표 · 값 · 버튼)나 Galmuri9 36(설명 · 안내)이어야 한다: .claude/rules/ui.md 1장 10번");
        }
    }
}
