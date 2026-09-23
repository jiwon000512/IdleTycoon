using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 굴 격자 설계 v0.5 3장: BurrowShape 마스크를 텍스처 한 장으로 칠한다(한 칸 = 1픽셀, PPU 40, Point).
    // 마스크 안 = 바닥 타일, 밖 = 벽 타일, 경계 밖 첫 칸 = 외곽선, 그 밖 한 칸 = 밝은 테두리(옛 make_shop_sections.py와 같은 결).
    // 타일은 굴 원점 기준 칸 좌표로 샘플해 흙 배경(같은 타일을 원점에 맞춰 깐 것)과 이음새 없이 이어진다
    public static class BurrowPainter
    {
        public const float k_PixelsPerUnit = 40f;
        private static readonly Color32 k_Outline = new Color32(52, 32, 32, 255);
        private static readonly Color32 k_Rim = new Color32(192, 144, 120, 255);

        public static Sprite Paint(BurrowShape.Result shape, Texture2D floorTile, Texture2D wallTile)
        {
            int w = shape.Width;
            int h = shape.Height;
            bool[,] mask = shape.Mask;
            Color32[] floor = floorTile.GetPixels32();
            Color32[] wall = wallTile.GetPixels32();
            int tile = floorTile.width;
            bool[,] outline = new bool[w, h];
            Color32[] pixels = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    outline[x, y] = !mask[x, y] && Near(mask, x, y);
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int gx = Mod(shape.OriginX + x, tile);
                    int gy = Mod(shape.OriginY + y, tile);
                    Color32 color = (mask[x, y] ? floor : wall)[(tile - 1 - gy) * tile + gx];

                    if (outline[x, y])
                    {
                        color = k_Outline;
                    }
                    else if (!mask[x, y] && Near(outline, x, y))
                    {
                        color = k_Rim;
                    }

                    pixels[(h - 1 - y) * w + x] = color;
                }
            }

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0f, 1f), k_PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static bool Near(bool[,] set, int x, int y)
        {
            int w = set.GetLength(0);
            int h = set.GetLength(1);
            return (x > 0 && set[x - 1, y]) || (x < w - 1 && set[x + 1, y]) || (y > 0 && set[x, y - 1]) || (y < h - 1 && set[x, y + 1]);
        }

        private static int Mod(int value, int period)
        {
            int m = value % period;
            return m < 0 ? m + period : m;
        }
    }
}
