using System;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 52: 낚시터 배치의 단일 출처. 광장처럼 고정 방(BurrowShape.Room)이고, 방 아래쪽을 가로지르는 강(Water 사각형, 사용자 「우물이 아니라 강가 느낌으로」)은 걸을 수 없다.
    // 웜뱃은 둑(물가) 위에서 물 쪽을 보고 던진다. 좌표는 낚시터 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class FishingLayout
    {
        public BurrowShape.Result Shape { get; }
        public BurrowNav Nav { get; }
        // 광장으로 가는 구멍(첫 줄 가운데, 빵집 · 농장 구멍과 같은 자리)
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        // 물 사각형(방 밖으로 나간 부분은 방 그림에 잘린다)
        public NavRect Water { get; }

        public FishingLayout(TableSet tables)
        {
            FishingConfigTable config = tables.Get<FishingConfigTable>(FishingConfigTable.k_Main);
            Shape = BurrowShape.Room(tables, config.Cols, config.Rows);
            Water = new NavRect((float)config.Water.X0, (float)config.Water.Y0, (float)config.Water.X1, (float)config.Water.Y1);
            Nav = new BurrowNav(Shape, new[] { Water });
        }

        // p에서 가장 가까운 물가(물 사각형 둘레의 점). 물 안이면 가장 가까운 변으로
        public Vector2 Shore(Vector2 p)
        {
            float x = Math.Clamp(p.X, Water.XMin, Water.XMax);
            float y = Math.Clamp(p.Y, Water.YMin, Water.YMax);

            if (x != p.X || y != p.Y)
            {
                return new Vector2(x, y);
            }

            float left = p.X - Water.XMin;
            float right = Water.XMax - p.X;
            float bottom = p.Y - Water.YMin;
            float top = Water.YMax - p.Y;
            float nearest = Math.Min(Math.Min(left, right), Math.Min(bottom, top));
            return nearest == top ? new Vector2(p.X, Water.YMax) : nearest == bottom ? new Vector2(p.X, Water.YMin) : nearest == left ? new Vector2(Water.XMin, p.Y) : new Vector2(Water.XMax, p.Y);
        }

        // 물가 점에서 물 안쪽으로 향하는 단위 벡터(그 변의 안쪽 법선)
        public Vector2 Inward(Vector2 shore)
        {
            if (shore.Y >= Water.YMax)
            {
                return new Vector2(0f, -1f);
            }

            if (shore.Y <= Water.YMin)
            {
                return new Vector2(0f, 1f);
            }

            return shore.X <= Water.XMin ? new Vector2(1f, 0f) : new Vector2(-1f, 0f);
        }

        // 그 물가에서 건너편까지 거리
        public float Depth(Vector2 shore)
        {
            return Inward(shore).X == 0f ? Water.YMax - Water.YMin : Water.XMax - Water.XMin;
        }

        // p에서 본 물가에서 물 안쪽으로 k(0 = 물가, 1 = 건너편)만큼 들어간 점
        public Vector2 Deep(Vector2 p, float k)
        {
            Vector2 shore = Shore(p);
            return shore + Inward(shore) * (Depth(shore) * k);
        }
    }
}
