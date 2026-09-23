using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;

namespace ZooTycoon.Editor
{
    // 손님 시트(make_visitor_sheets.py: 가로 1행, 칸 폭 104px, 발끝 = 아래 끝)를 칸마다 자른다. 정지 그림은 Single.
    // 가게 유닛 기본 크기(ShopBaker.k_UnitPpu = 80), 피벗 하단 중앙
    public static class VisitorSheetImporter
    {
        const string k_Root = "Assets/Resources/Sprites/Visitors";
        const int k_FrameWidth = 104;
        const float k_Ppu = 80f;

        [MenuItem("ZooTycoon/Bake/Import Visitor Sheets")]
        public static void ImportAll()
        {
            foreach (string path in Directory.GetFiles(k_Root, "*.png", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                bool sheet = Path.GetFileNameWithoutExtension(assetPath).Contains("_");
                Import(assetPath, sheet);
            }
        }

        static void Import(string path, bool sheet)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            ti.spritePixelsPerUnit = k_Ppu;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;

            if (!sheet)
            {
                TextureImporterSettings settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
                return;
            }

            ti.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(ti);
            provider.InitSpriteEditorDataProvider();
            string name = Path.GetFileNameWithoutExtension(path);
            List<SpriteRect> rects = new List<SpriteRect>();

            for (int i = 0; i < texture.width / k_FrameWidth; i++)
            {
                rects.Add(new SpriteRect
                {
                    name = name + "_" + i,
                    rect = new Rect(i * k_FrameWidth, 0, k_FrameWidth, texture.height),
                    alignment = SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                    spriteID = GUID.Generate(),
                });
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();
            ti.SaveAndReimport();
        }
    }
}
