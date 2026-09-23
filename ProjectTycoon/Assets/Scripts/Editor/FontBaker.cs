using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using TMPro;

namespace ZooTycoon.Editor
{
    // UI-디자인-규칙 v1.0 F1: 갈무리 픽셀 폰트를 TMP 비트맵(래스터) 동적 폰트 에셋으로 만든다. 샘플링 = 폰트 픽셀 크기, 패딩 1, 안티에일리어싱 없음.
    // 한글은 실행 중 동적으로 채워진다(Atlas Population Dynamic)
    public static class FontBaker
    {
        const string k_Dir = "Assets/Fonts/Galmuri/";

        static readonly (string file, int size)[] k_Fonts =
        {
            ("Galmuri9", 9),
            ("Galmuri11", 11),
            ("Galmuri11-Bold", 11),
            ("Galmuri14", 14),
        };

        [MenuItem("ZooTycoon/Bake/Fonts")]
        public static void Bake()
        {
            foreach ((string file, int size) in k_Fonts)
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(k_Dir + file + ".ttf");
                string path = k_Dir + file + ".asset";
                AssetDatabase.DeleteAsset(path);
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, size, 1, GlyphRenderMode.RASTER_HINTED, 512, 512, AtlasPopulationMode.Dynamic, true);
                asset.name = file;
                asset.material.name = file + " Material";
                asset.atlasTexture.name = file + " Atlas";
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Galmuri font assets " + k_Fonts.Length);
        }
    }
}
