using System;
using System.Linq;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 52 · 아트방 「흙 도랑」 재료 그대로(Fishing/Source~/make_stream.py 머리말이 원본): 호수를 굴 그림처럼 칸마다 칠한다(한 칸 = 1픽셀, 굴 그림과 같은 원점 · 크기).
    // 설계 61: 물 = 걷는 바닥(띠 아닌 방 안) 중 섬 뭍 밖 전부. 놓인 다리는 물 위에 그림이 덮이므로 땅으로 보지 않는다(아트방: 나무 다리 밑에 흙 벽이 서면 안 됨). 물 칸: 위가 물 밖에서 k_FaceRows줄은 흙 면(stream_face, 둑의 벽), 그다음 한 줄은 거품, 나머지는 물 타일(water_tile),
    // 좌우가 물 밖 2칸 안은 짙은 물. 물 밖 둑 고리(4방향 거리): 1 · 4 = 선, 2 · 3 = 턱 — 섬이 물에 닿는 사방에 늘 그린다(2026-10-14 사용자 「섬 옆 · 위쪽이 끊겨진 느낌」, 아랫변은 둑 띠 그림이 덮는다).
    // 섬 뭍의 네 모서리는 굴 모서리와 같은 반지름으로 둥글려 네모 상자로 보이지 않게 한다(걷는 땅은 네모 그대로, 여유 0.3 안이라 차이가 없다). 자갈 둑 띠는 쓰지 않는다. 그 밖은 투명.
    // 물 색은 아트방 B 「어두운 남빛」 호수 타일(make_lake.py) 기준
    public static class WaterPainter
    {
        public const int k_FaceRows = 14;
        private const int k_SideShade = 2;
        private const float k_CornerRadius = BurrowShape.k_RoundRadius / BurrowShape.k_PixelsPerUnit;
        private static readonly Color32 k_Light = new Color32(84, 140, 154, 255);
        private static readonly Color32 k_Deep = new Color32(30, 72, 90, 255);
        private static readonly Color32 k_Line = new Color32(52, 32, 32, 255);
        private static readonly Color32 k_Lip = new Color32(168, 120, 96, 255);

        // 재료 텍스처의 픽셀(메인 스레드에서 읽어 두면 Pixels는 다른 스레드에서 돌 수 있다)
        public readonly struct Textures
        {
            public readonly Color32[][] Tiles;
            public readonly int[] TileSizes;
            public readonly Color32[] Face;
            public readonly int FaceWidth;
            public readonly int FaceHeight;

            public Textures(Texture2D[] waterTiles, Texture2D streamFace)
            {
                Tiles = System.Array.ConvertAll(waterTiles, tile => tile.GetPixels32());
                TileSizes = System.Array.ConvertAll(waterTiles, tile => tile.width);
                Face = streamFace.GetPixels32();
                FaceWidth = streamFace.width;
                FaceHeight = streamFace.height;
            }
        }

        public static Sprite[] Paint(BurrowShape.Result shape, FishingLayout layout, Texture2D[] waterTiles, Texture2D streamFace)
        {
            Color32[][] frames = Pixels(shape, layout.Lands.ToArray(), new Textures(waterTiles, streamFace));
            return System.Array.ConvertAll(frames, pixels => BurrowPainter.ToSprite(pixels, shape.Width, shape.Height));
        }

        // 물결 프레임마다 픽셀 한 벌(Unity API 없음). 물 마스크 · 흙 면 · 거품 줄 · 옆 그늘 · 둑 고리는 한 번만 계산하고 프레임은 물 타일 칸만 바꾼다(섬을 열 때 멈칫하던 것, 사용자 2026-10-14)
        public static Color32[][] Pixels(BurrowShape.Result shape, NavRect[] lands, Textures t)
        {
            int w = shape.Width;
            int h = shape.Height;
            float ppu = BurrowShape.k_PixelsPerUnit;
            int top = BurrowShape.k_EntranceFloorTop - shape.OriginY;
            bool[,] water = new bool[w, h];

            for (int y = Math.Max(top, 0); y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!shape.IsFloor(x, y))
                    {
                        continue;
                    }

                    System.Numerics.Vector2 p = new System.Numerics.Vector2((shape.OriginX + x + 0.5f) / ppu, -(shape.OriginY + y + 0.5f) / ppu);
                    water[x, y] = !OnLand(lands, p);
                }
            }

            Color32[] face = t.Face;
            int[,] up = new int[w, h];
            Color32[] pixels = new Color32[w * h];
            // 물 타일로 채우는 칸(픽셀 번호)
            System.Collections.Generic.List<int> tiled = new System.Collections.Generic.List<int>();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!water[x, y])
                    {
                        continue;
                    }

                    up[x, y] = y > 0 && water[x, y - 1] ? up[x, y - 1] + 1 : 1;
                    Color32 color;

                    if (up[x, y] <= k_FaceRows)
                    {
                        color = face[(t.FaceHeight - up[x, y]) * t.FaceWidth + Mod(shape.OriginX + x, t.FaceWidth)];
                    }
                    else if (up[x, y] == k_FaceRows + 1)
                    {
                        color = k_Light;
                    }
                    else if (SideRun(water, x, y) <= k_SideShade)
                    {
                        color = k_Deep;
                    }
                    else
                    {
                        tiled.Add((h - 1 - y) * w + x);
                        continue;
                    }

                    pixels[(h - 1 - y) * w + x] = color;
                }
            }

            PaintBank(water, shape, top, pixels);
            Color32[][] frames = new Color32[t.Tiles.Length][];

            for (int i = 0; i < t.Tiles.Length; i++)
            {
                Color32[] tile = t.Tiles[i];
                int tileSize = t.TileSizes[i];
                Color32[] frame = (Color32[])pixels.Clone();

                foreach (int index in tiled)
                {
                    int x = index % w;
                    int y = h - 1 - index / w;
                    frame[index] = tile[(tileSize - 1 - Mod(shape.OriginY + y, tileSize)) * tileSize + Mod(shape.OriginX + x, tileSize)];
                }

                frames[i] = frame;
            }

            return frames;
        }

        private static bool OnLand(NavRect[] lands, System.Numerics.Vector2 p)
        {
            foreach (NavRect land in lands)
            {
                if (FishingLayout.Inside(land, p) && !InCorner(land, p))
                {
                    return true;
                }
            }

            return false;
        }

        // 뭍 네 모서리의 둥글린 바깥 조각(반지름 k_CornerRadius) 안인가(아랫변도 둥글게, 사용자 2026-10-14 「아래쪽이 옆이랑 끊겨 보임」)
        private static bool InCorner(NavRect land, System.Numerics.Vector2 p)
        {
            float r = k_CornerRadius;
            float cx = p.X < land.XMin + r ? land.XMin + r : p.X > land.XMax - r ? land.XMax - r : float.NaN;
            float cy = p.Y > land.YMax - r ? land.YMax - r : p.Y < land.YMin + r ? land.YMin + r : float.NaN;
            return !float.IsNaN(cx) && !float.IsNaN(cy) && System.Numerics.Vector2.Distance(p, new System.Numerics.Vector2(cx, cy)) > r;
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

                        if (shape.IsFloor(x, y) && y >= top)
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
