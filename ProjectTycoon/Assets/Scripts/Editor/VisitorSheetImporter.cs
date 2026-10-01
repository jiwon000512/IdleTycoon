using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;

namespace ZooTycoon.Editor
{
    // 손님 시트(Shop/Source~/make_anim.py: 가로 1행, 칸 폭 104px, 발끝 = 아래 끝)를 칸마다 자른다. 정지 그림은 Single.
    // 가게 유닛 기본 크기(BakeryBaker.k_UnitPpu = 80), 피벗 하단 중앙
    public static class VisitorSheetImporter
    {
        const string k_Root = "Assets/Resources/Sprites/Visitors";
        const int k_FrameWidth = 104;
        const float k_Ppu = 80f;
        // 딴짓 시트는 머묾을 같은 프레임 되풀이로 만들어 2048px를 넘는다(기본 최대 2048이면 줄어들어 칸 위가 잘린다). make_anim.py가 4096 안으로 막는다
        const int k_MaxSize = 4096;

        [MenuItem("ZooTycoon/Bake/Import Visitor Sheets")]
        public static void ImportAll()
        {
            foreach (string path in Directory.GetFiles(k_Root, "*.png", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                bool sheet = Path.GetFileNameWithoutExtension(assetPath).Contains("_");
                // 너구리만 꼬리가 옆으로 나와 칸 폭 112px(make_anim.py CELLS)
                Import(assetPath, sheet, assetPath.Contains("/Tanuki/") ? 112 : k_FrameWidth);
            }
        }

        // frameWidth: 시트 칸 폭(px). 가게 말풍선 시트(wait_sheet, 52px)도 BakeryBaker가 이걸로 자른다
        internal static void Import(string path, bool sheet, int frameWidth = k_FrameWidth)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            ti.spritePixelsPerUnit = k_Ppu;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = k_MaxSize;

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
            // 칸은 원본 크기로 자른다(가져온 텍스처 크기가 달라도 칸이 어긋나지 않게)
            ti.GetSourceTextureWidthAndHeight(out int width, out int height);
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(ti);
            provider.InitSpriteEditorDataProvider();
            string name = Path.GetFileNameWithoutExtension(path);
            List<SpriteRect> rects = new List<SpriteRect>();

            for (int i = 0; i < width / frameWidth; i++)
            {
                rects.Add(new SpriteRect
                {
                    name = name + "_" + i,
                    rect = new Rect(i * frameWidth, 0, frameWidth, height),
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
