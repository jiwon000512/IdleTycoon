using System.IO;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using TMPro;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Editor
{
    // UI-디자인-규칙 v1.0 F1: 갈무리 픽셀 폰트를 TMP 비트맵(래스터) 폰트 에셋으로 만든다.
    // 원본은 비트맵 스트라이크가 든 *Bitmap TTF(Galmuri9B 등): 샘플링 = 스트라이크 픽셀 크기라 획이 그대로 찍힌다(벡터판은 얇은 획이 끊겼다, 2026-09-23).
    // 아틀라스는 Point 필터(확대 시 번짐 없음), 셰이더는 TextMeshPro/Bitmap.
    // 2026-09-24 정적 아틀라스: 동적이면 플레이마다 글자가 에셋에 더해져 파일이 바뀌고, 되돌리면 글자가 깨졌다.
    // 화면에 나올 수 있는 글자(Texts.Characters)를 미리 굽고 Static으로 둔다. 문구에 새 글자가 생기면 다시 굽는다(FontAssetTests가 잡는다).
    // 있는 에셋은 지우지 않고 그 자리에서 다시 채운다(GUID가 그대로라 프리팹 참조가 안 끊긴다)
    public static class FontBaker
    {
        const string k_Dir = "Assets/Fonts/Galmuri/";
        const string k_DataDir = "Assets/Resources/Data/";

        // (에셋 이름, TTF 파일, 픽셀 크기). 정자체는 1px 획 그대로(2026-09-26: 굵히기·그림자 모두 사용자가 반려 — 굵은체와 어울리지 않는다)
        static readonly (string asset, string file, int size)[] k_Fonts =
        {
            ("Galmuri9", "Galmuri9B", 9),
            ("Galmuri11", "Galmuri11B", 11),
            ("Galmuri11-Bold", "Galmuri11B-Bold", 11),
            ("Galmuri14", "Galmuri14B", 14),
        };

        [MenuItem("ZooTycoon/Bake/Fonts")]
        public static void Bake()
        {
            string characters = Texts.Characters(new TableSet(name => File.ReadAllText(k_DataDir + name + ".json")));

            foreach ((string name, string file, int size) in k_Fonts)
            {
                string path = k_Dir + name + ".asset";
                TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

                if (asset == null)
                {
                    asset = Create(AssetDatabase.LoadAssetAtPath<Font>(k_Dir + file + ".ttf"), size, name, path);
                }

                asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                asset.ClearFontAssetData();
                asset.TryAddCharacters(characters, out string missing);
                asset.atlasPopulationMode = AtlasPopulationMode.Static;

                asset.atlasTexture.filterMode = FilterMode.Point;
                EditorUtility.SetDirty(asset);
                EditorUtility.SetDirty(asset.atlasTexture);

                if (!string.IsNullOrEmpty(missing))
                {
                    Debug.LogWarning($"{name}: 폰트에 없는 글자 {missing}");
                }
            }

            // 11px의 굵은 글씨 = Galmuri11-Bold 에셋(가짜 굵기 대신 실제 굵은 스트라이크)
            TMP_FontAsset regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_Dir + "Galmuri11.asset");
            TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_Dir + "Galmuri11-Bold.asset");
            regular.fontWeightTable[7].regularTypeface = bold;
            EditorUtility.SetDirty(regular);
            AssetDatabase.SaveAssets();
            Debug.Log($"Galmuri font assets {k_Fonts.Length}, characters {characters.Length}");
        }

        static TMP_FontAsset Create(Font font, int size, string name, string path)
        {
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, size, 1, GlyphRenderMode.RASTER, 512, 512, AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            asset.material.name = name + " Material";
            asset.material.shader = Shader.Find("TextMeshPro/Bitmap");
            asset.atlasTexture.name = name + " Atlas";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            return asset;
        }
    }
}
