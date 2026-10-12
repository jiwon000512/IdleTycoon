using System;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 52 · 아트방 「흙 도랑」 재료 그대로(Fishing/Source~/make_stream.py 머리말이 원본): 강물을 굴 그림처럼 칸마다 칠한다(한 칸 = 1픽셀, 굴 그림과 같은 원점 · 크기).
    // 물 = 물 사각형 안(방 안 · 윗벽 띠 아래만). 물 칸: 위가 물 밖에서 k_FaceRows줄은 흙 면(stream_face, 둑의 벽), 그다음 한 줄은 거품, 나머지는 물 타일(water_tile), 좌우가 물 밖 2칸 안은 짙은 물.
    // 물 밖 둑 고리(4방향 거리): 1 · 4 = 선, 2 · 3 = 턱(아트방 둑 띠가 오면 둑은 그리지 않는다). 그 밖은 투명.
    // 설계 53: 열렸지만 댐 뒤라 마른 강바닥(layout.DryBeds)도 같은 흙 면 아래 마른 바닥 타일(dry_bed, 없으면 흙 면을 어둡게 반복)로 칠한다
    public static class WaterPainter
    {
        public const int k_FaceRows = 14;
        private const int k_SideShade = 2;
        private static readonly Color32 k_Light = new Color32(110, 182, 170, 255);
        private static readonly Color32 k_Deep = new Color32(48, 120, 114, 255);
        private static readonly Color32 k_Line = new Color32(52, 32, 32, 255);
        private static readonly Color32 k_Lip = new Color32(168, 120, 96, 255);

        public static Sprite Paint(BurrowShape.Result shape, FishingLayout layout, Texture2D waterTile, Texture2D streamFace, Texture2D dryTile, bool bank)
        {
            int w = shape.Width;
            int h = shape.Height;
            float ppu = BurrowShape.k_PixelsPerUnit;
            int top = BurrowShape.k_EntranceFloorTop - shape.OriginY;
            bool[,] water = new bool[w, h];
            bool[,] dry = new bool[w, h];
            Fill(water, shape, layout.Water, top);

            foreach (NavRect bed in layout.DryBeds)
            {
                Fill(dry, shape, bed, top);
            }

            bool[,] river = new bool[w, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    river[x, y] = water[x, y] || dry[x, y];
                }
            }

            Color32[] tile = waterTile.GetPixels32();
            Color32[] face = streamFace.GetPixels32();
            Color32[] dryPixels = dryTile != null ? dryTile.GetPixels32() : null;
            int tileSize = waterTile.width;
            int[,] up = new int[w, h];
            Color32[] pixels = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!river[x, y])
                    {
                        continue;
                    }

                    up[x, y] = y > 0 ? up[x, y - 1] + 1 : 1;
                    Color32 color;

                    if (up[x, y] <= k_FaceRows)
                    {
                        color = face[(streamFace.height - up[x, y]) * streamFace.width + Mod(shape.OriginX + x, streamFace.width)];
                    }
                    else if (dry[x, y])
                    {
                        color = dryTile != null
                            ? dryPixels[(dryTile.height - 1 - Mod(shape.OriginY + y, dryTile.height)) * dryTile.width + Mod(shape.OriginX + x, dryTile.width)]
                            : Darken(face[Mod(up[x, y], streamFace.height) * streamFace.width + Mod(shape.OriginX + x, streamFace.width)]);
                    }
                    else if (up[x, y] == k_FaceRows + 1)
                    {
                        color = k_Light;
                    }
                    else
                    {
                        int gx = Mod(shape.OriginX + x, tileSize);
                        int gy = Mod(shape.OriginY + y, tileSize);
                        color = SideRun(water, x, y) <= k_SideShade ? k_Deep : tile[(tileSize - 1 - gy) * tileSize + gx];
                    }

                    pixels[(h - 1 - y) * w + x] = color;
                }
            }

            if (bank)
            {
                PaintBank(river, shape, top, pixels);
            }

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0f, 1f), ppu, 0, SpriteMeshType.FullRect);
        }

        // 좌우로 물 밖까지 가까운 쪽 칸 수(1 = 바로 옆이 물 밖)
        private static int SideRun(bool[,] water, int x, int y)
        {
            int w = water.GetLength(0);
            int left = 0;
            int right = 0;

            while (x - left >= 0 && water[x - left, y] && left <= k_SideShade)
            {
                left++;
            }

            while (x + right < w && water[x + right, y] && right <= k_SideShade)
            {
                right++;
            }

            return Math.Min(left, right);
        }

        // 물 밖 둑 고리 4칸(4방향 거리, 방 안 · 윗벽 띠 아래만)
        private static void PaintBank(bool[,] water, BurrowShape.Result shape, int top, Color32[] pixels)
        {
            int w = water.GetLength(0);
            int h = water.GetLength(1);
            bool[,] current = (bool[,])water.Clone();

            for (int k = 1; k <= 4; k++)
            {
                bool[,] grown = (bool[,])current.Clone();

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (current[x, y])
                        {
                            continue;
                        }

                        bool near = x > 0 && current[x - 1, y] || x < w - 1 && current[x + 1, y] || y > 0 && current[x, y - 1] || y < h - 1 && current[x, y + 1];

                        if (!near)
                        {
                            continue;
                        }

                        grown[x, y] = true;

                        if (shape.Mask[x, y] && y >= top)
                        {
                            pixels[(h - 1 - y) * w + x] = k == 1 || k == 4 ? k_Line : k_Lip;
                        }
                    }
                }

                current = grown;
            }
        }

        // 사각형 안 · 굴 안 · 윗벽 띠 아래 칸
        private static void Fill(bool[,] into, BurrowShape.Result shape, NavRect rect, int top)
        {
            float ppu = BurrowShape.k_PixelsPerUnit;
            int w = into.GetLength(0);
            int h = into.GetLength(1);
            int x0 = (int)Math.Round(rect.XMin * ppu) - shape.OriginX;
            int x1 = (int)Math.Round(rect.XMax * ppu) - shape.OriginX;
            int y0 = (int)Math.Round(-rect.YMax * ppu) - shape.OriginY;
            int y1 = (int)Math.Round(-rect.YMin * ppu) - shape.OriginY;

            for (int y = Math.Max(y0, top); y < Math.Min(y1, h); y++)
            {
                for (int x = Math.Max(x0, 0); x < Math.Min(x1, w); x++)
                {
                    into[x, y] = shape.Mask[x, y];
                }
            }
        }

        // 마른 바닥 대신 쓰는 흙 면(조금 어둡게)
        private static Color32 Darken(Color32 c)
        {
            return new Color32((byte)(c.r * 0.72f), (byte)(c.g * 0.72f), (byte)(c.b * 0.72f), c.a);
        }

        private static int Mod(int value, int period)
        {
            int m = value % period;
            return m < 0 ? m + period : m;
        }
    }
}
