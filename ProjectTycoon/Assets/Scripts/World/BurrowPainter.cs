using System;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 굴 격자 설계 v0.5 3장: BurrowShape 마스크를 텍스처 한 장으로 칠한다(한 칸 = 1픽셀, PPU = ShopLayout.k_PixelsPerUnit, Point).
    // 마스크 안 = 바닥 타일(띠는 윗벽 면), 밖 = 벽 타일, 마스크 바깥 여섯 칸 = 턱(k_Ledge, 8방향 거리마다 색. 아트방 목업과 같은 팽창).
    // 굴 환경 A2: 띠(WallRow)는 wall_face를 행은 띠 윗변부터, 열은 굴 원점 기준 칸 x로 샘플해 지층이 곧은 가로줄로 이어진다
    // 타일은 굴 원점 기준 칸 좌표로 샘플해 흙 배경(같은 타일을 원점에 맞춰 깐 것)과 이음새 없이 이어진다
    public static class BurrowPainter
    {
        // 굴 환경 A2 턱(2026-09-24 사용자 선택): 거리 1·6 = 선, 2~5 = 턱(입구 아치 턱과 같은 색)
        private static readonly Color32[] k_Ledge =
        {
            new Color32(52, 32, 32, 255),
            new Color32(168, 120, 96, 255),
            new Color32(168, 120, 96, 255),
            new Color32(168, 120, 96, 255),
            new Color32(168, 120, 96, 255),
            new Color32(52, 32, 32, 255),
        };

        public static Sprite Paint(BurrowShape.Result shape, Texture2D floorTile, Texture2D wallTile, Texture2D wallFace)
        {
            int w = shape.Width;
            int h = shape.Height;
            bool[,] mask = shape.Mask;
            Color32[] floor = floorTile.GetPixels32();
            Color32[] wall = wallTile.GetPixels32();
            Color32[] face = wallFace.GetPixels32();
            int tile = floorTile.width;
            int[,] ledge = LedgeDistance(mask);
            Color32[] pixels = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int gx = Mod(shape.OriginX + x, tile);
                    int gy = Mod(shape.OriginY + y, tile);
                    int row = shape.WallRow[x, y];
                    Color32 color = mask[x, y] && row > 0
                        ? face[(wallFace.height - row) * wallFace.width + Mod(shape.OriginX + x, wallFace.width)]
                        : (mask[x, y] ? floor : wall)[(tile - 1 - gy) * tile + gx];

                    if (ledge[x, y] > 0)
                    {
                        color = k_Ledge[ledge[x, y] - 1];
                    }

                    pixels[(h - 1 - y) * w + x] = color;
                }
            }

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0f, 1f), ShopLayout.k_PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        // 방 밖 칸의 방까지 8방향 거리(1 ~ 턱 두께), 방 안·먼 곳은 0. 한 겹씩 넓힌다
        private static int[,] LedgeDistance(bool[,] mask)
        {
            int w = mask.GetLength(0);
            int h = mask.GetLength(1);
            int[,] distance = new int[w, h];

            for (int k = 1; k <= k_Ledge.Length; k++)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (!mask[x, y] && distance[x, y] == 0 && Near(mask, distance, x, y, k - 1))
                        {
                            distance[x, y] = k;
                        }
                    }
                }
            }

            return distance;
        }

        // 8방향 이웃 중 거리 d인 칸이 있나(d = 0이면 방 안 칸)
        private static bool Near(bool[,] mask, int[,] distance, int x, int y, int d)
        {
            for (int ny = Math.Max(y - 1, 0); ny <= Math.Min(y + 1, mask.GetLength(1) - 1); ny++)
            {
                for (int nx = Math.Max(x - 1, 0); nx <= Math.Min(x + 1, mask.GetLength(0) - 1); nx++)
                {
                    if (d == 0 ? mask[nx, ny] : !mask[nx, ny] && distance[nx, ny] == d)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int Mod(int value, int period)
        {
            int m = value % period;
            return m < 0 ? m + period : m;
        }
    }
}
