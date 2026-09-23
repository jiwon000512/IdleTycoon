using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using TMPro;

namespace ZooTycoon.Editor
{
    // UI-디자인-규칙 v1.0 F1: 갈무리 픽셀 폰트를 TMP 비트맵(래스터) 동적 폰트 에셋으로 만든다.
    // 원본은 비트맵 스트라이크가 든 *Bitmap TTF(Galmuri9B 등): 샘플링 = 스트라이크 픽셀 크기라 획이 그대로 찍힌다(벡터판은 얇은 획이 끊겼다, 2026-09-23).
    // 아틀라스는 Point 필터(확대 시 번짐 없음), 셰이더는 TextMeshPro/Bitmap. 한글은 실행 중 동적으로 채워진다
    public static class FontBaker
    {
        const string k_Dir = "Assets/Fonts/Galmuri/";

        // (에셋 이름, TTF 파일, 픽셀 크기)
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
            foreach ((string name, string file, int size) in k_Fonts)
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(k_Dir + file + ".ttf");
                string path = k_Dir + name + ".asset";
                AssetDatabase.DeleteAsset(path);
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, size, 1, GlyphRenderMode.RASTER, 512, 512, AtlasPopulationMode.Dynamic, true);
                asset.name = name;
                asset.material.name = name + " Material";
                asset.material.shader = Shader.Find("TextMeshPro/Bitmap");
                asset.atlasTexture.name = name + " Atlas";
                asset.atlasTexture.filterMode = FilterMode.Point;
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            // 11px의 굵은 글씨 = Galmuri11-Bold 에셋(가짜 굵기 대신 실제 굵은 스트라이크)
            TMP_FontAsset regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_Dir + "Galmuri11.asset");
            TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_Dir + "Galmuri11-Bold.asset");
            regular.fontWeightTable[7].regularTypeface = bold;
            EditorUtility.SetDirty(regular);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Galmuri font assets " + k_Fonts.Length);
        }
    }
}
