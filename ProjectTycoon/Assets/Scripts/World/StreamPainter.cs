using System;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 44 · 아트방 「흙 도랑」(Fishing/Source~/make_stream.py 머리말이 원본): 낚시터 물길을 굴 그림처럼 칸마다 칠한다(한 칸 = 1픽셀, 굴 그림과 같은 원점 · 크기).
    // 물 = 선분마다 폭 사각형(끝은 폭 절반 늘여 꺾임이 맞물린다, 방 안 · 윗벽 띠 아래만). 물 칸: 위가 물 밖에서 k_FaceRows줄은 흙 면(stream_face), 그다음 한 줄은 거품,
    // 나머지는 물 타일(water_tile, 굴 원점 기준 칸 좌표), 좌우가 물 밖 2칸 안은 짙은 물. 물 밖 둑 고리(4방향 거리): 1 · 4 = 선, 2 · 3 = 턱. 그 밖은 투명
    public static class StreamPainter
    {
        // 흙 면 줄 수(stream_face 높이)
        public const int k_FaceRows = 14;
        private const int k_SideShade = 2;
        private static readonly Color32 k_Light = new Color32(110, 182, 170, 255);
        private static readonly Color32 k_Deep = new Color32(48, 120, 114, 255);
        private static readonly Color32 k_Line = new Color32(52, 32, 32, 255);
        private static readonly Color32 k_Lip = new Color32(168, 120, 96, 255);

        public static Sprite Paint(BurrowShape.Result shape, FishingLayout layout, Texture2D waterTile, Texture2D streamFace)
        {
            int w = shape.Width;
            int h = shape.Height;
            float ppu = BurrowShape.k_PixelsPerUnit;
            int top = BurrowShape.k_EntranceFloorTop - shape.OriginY;
            bool[,] water = new bool[w, h];
            int half = (int)Math.Round(layout.StreamWidth * ppu / 2f);

            for (int i = 1; i < layout.Stream.Count; i++)
            {
                System.Numerics.Vector2 a = layout.Stream[i - 1];
                System.Numerics.Vector2 b = layout.Stream[i];
                int x0 = (int)Math.Round(Math.Min(a.X, b.X) * ppu) - shape.OriginX - half;
                int x1 = (int)Math.Round(Math.Max(a.X, b.X) * ppu) - shape.OriginX + half;
                int y0 = (int)Math.Round(-Math.Max(a.Y, b.Y) * ppu) - shape.OriginY - half;
                int y1 = (int)Math.Round(-Math.Min(a.Y, b.Y) * ppu) - shape.OriginY + half;

                for (int y = Math.Max(y0, top); y < Math.Min(y1, h); y++)
                {
                    for (int x = Math.Max(x0, 0); x < Math.Min(x1, w); x++)
                    {
                        water[x, y] = shape.Mask[x, y];
                    }
                }
            }

            Color32[] tile = waterTile.GetPixels32();
            Color32[] face = streamFace.GetPixels32();
            int tileSize = waterTile.width;
            int[,] up = new int[w, h];
            Color32[] pixels = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!water[x, y])
                    {
                        continue;
                    }

                    up[x, y] = y > 0 ? up[x, y - 1] + 1 : 1;
                    Color32 color;

                    if (up[x, y] <= k_FaceRows)
                    {
                        color = face[(streamFace.height - up[x, y]) * streamFace.width + Mod(shape.OriginX + x, streamFace.width)];
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

            PaintBank(water, shape, top, pixels);
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

        private static int Mod(int value, int period)
        {
            int m = value % period;
            return m < 0 ? m + period : m;
        }
    }
}
