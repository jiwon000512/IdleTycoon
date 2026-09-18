using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.U2D.Sprites;

namespace ZooTycoon.Editor
{
    // make_tiles.py가 만든 마름모 18장 시트(64×32 가로 1행)를 스프라이트로 자르고 Tile 에셋 <Prefix>_00~17을 다시 만든다.
    // 색인 규칙은 BranchBaker.Index와 같다. 칠하기는 BranchBaker가 한다
    public static class TileSheetImporter
    {
        const int k_Count = 18;

        [MenuItem("ZooTycoon/Bake/Import Yard Tiles")]
        public static void ImportYard()
        {
            Debug.Log(Import("Assets/Sprites/World/Tiles/yard.png", "Yard"));
        }

        public static string Import(string sheetPath, string prefix)
        {
            AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = 64f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 2048;

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(ti);
            provider.InitSpriteEditorDataProvider();
            List<SpriteRect> rects = new List<SpriteRect>();
            string lower = prefix.ToLowerInvariant();
            for (int i = 0; i < k_Count; i++)
            {
                rects.Add(new SpriteRect
                {
                    name = lower + "_" + i.ToString("00"),
                    rect = new Rect(i * 64, 0, 64, 32),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate(),
                });
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();
            ti.SaveAndReimport();

            Sprite[] sprites = new Sprite[k_Count];
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                if (o is Sprite sprite)
                {
                    sprites[int.Parse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1))] = sprite;
                }
            }

            // 같은 경로에 다시 만들면 옛 객체가 "이름 없는 Tile"로 남으므로 지우고 새로 만든다(진행상황 도구 함정)
            for (int i = 0; i < k_Count; i++)
            {
                AssetDatabase.DeleteAsset("Assets/Tiles/" + prefix + "_" + i.ToString("00") + ".asset");
            }
            AssetDatabase.SaveAssets();
            for (int i = 0; i < k_Count; i++)
            {
                Tile tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprites[i];
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, "Assets/Tiles/" + prefix + "_" + i.ToString("00") + ".asset");
            }
            AssetDatabase.SaveAssets();
            return prefix + " tiles " + k_Count;
        }
    }
}
