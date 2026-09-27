using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

namespace ZooTycoon.Editor
{
    // UI-디자인-규칙 v1.0: Sprites/UI/*.png(1px = 1 UI px = 화면 4px)를 PPU 25·Point·비압축으로, ui_slices.json의 border로 9-slice 임포트.
    // 월드 태그(tag_cost)·말풍선(bubble 몸통·bubble_tail 꼬리, UI와 같은 그림)은 Sprites/World/Shop에 복사본을 두고 PPU 40(1px = 월드 한 칸)으로 임포트한다.
    // 말풍선 몸통 피벗은 아래 가운데(글 폭만큼 좌우로 늘어난다), 꼬리는 위 가운데(몸통 아랫선에 한 칸 겹친다)
    public static class UISpriteImporter
    {
        const string k_Dir = "Assets/Sprites/UI/";
        const string k_WorldShop = "Assets/Sprites/World/Shop/";
        // 설계 09 v0.4: 행동 아이콘(ActionTable icon 경로). UI와 같은 PPU, 9-slice 없음
        const string k_ActionDir = "Assets/Resources/Sprites/Actions";
        const float k_UiPpu = 25f;
        const float k_WorldPpu = 40f;

        sealed class Slice
        {
            public int w;
            public int h;
            public int[] border;
            public float ppu;
        }

        [MenuItem("ZooTycoon/Bake/Import UI Sprites")]
        public static void ImportAll()
        {
            Dictionary<string, Slice> slices = JsonConvert.DeserializeObject<Dictionary<string, Slice>>(File.ReadAllText(k_Dir + "ui_slices.json"));

            foreach (KeyValuePair<string, Slice> pair in slices)
            {
                Import(k_Dir + pair.Key + ".png", pair.Value.border, pair.Value.ppu > 0f ? pair.Value.ppu : k_UiPpu);
            }

            foreach (string path in Directory.GetFiles(k_ActionDir, "*.png"))
            {
                Import(path.Replace(Path.DirectorySeparatorChar, '/'), null, k_UiPpu);
            }

            foreach ((string name, SpriteAlignment pivot) in new[] { ("tag_cost", SpriteAlignment.Center), ("bubble", SpriteAlignment.BottomCenter), ("bubble_tail", SpriteAlignment.TopCenter) })
            {
                File.Copy(k_Dir + name + ".png", k_WorldShop + name + ".png", true);
                Import(k_WorldShop + name + ".png", slices[name].border, k_WorldPpu, pivot);
            }
            Debug.Log("UI sprites " + slices.Count);
        }

        static void Import(string path, int[] border, float ppu, SpriteAlignment pivot = SpriteAlignment.Center)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            TextureImporterSettings settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)pivot;
            settings.spriteBorder = border != null ? new Vector4(border[0], border[1], border[2], border[3]) : Vector4.zero;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }
    }
}
