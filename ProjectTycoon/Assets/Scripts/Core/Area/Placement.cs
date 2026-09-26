using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 18: 놓는 사물의 자리 규칙. 바닥 사각형(밑변 가운데에서 좌우 halfWidth·위로 depth)과 자리 오프셋·가격은 표(IPlacedKind), 좌표는 놓인 것(IPlaced)
    public enum SpotRole
    {
        // 손님이 서는 자리(진열대 옆·앞, 장식 들를 곳). 하나 이상 걷는 땅이어야 놓인다
        Customer,
        // 웜뱃(점원)이 일하는 자리(오븐 앞, 계산대 뒤). 걷는 땅이어야 놓인다
        Worker,
        // 계산대 줄 머리. 걷는 땅이어야 놓인다
        Queue,
    }

    // 사물 밑변 가운데 기준 자리 오프셋(유닛, y 위)과 거기서 보는 방향
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class SpotOffset
    {
        public double Dx { get; set; }
        public double Dy { get; set; }
        public Facing Face { get; set; }
        public SpotRole Role { get; set; }
    }

    // 사는 값: baseCost × growth^(지금 수 − start). 전체 수가 max에 닿으면 못 산다
    public sealed class PriceInfo
    {
        public double BaseCost { get; set; }
        public double Growth { get; set; }
        public int Max { get; set; }
        public int Start { get; set; }

        public double CostAt(int count)
        {
            return BaseCost * Math.Pow(Growth, Math.Max(0, count - Start));
        }
    }

    // 놓을 수 있는 사물 종류(표 행): 빵집 사물(InteractableTable)·광장 장식(DecorationTable)
    public interface IPlacedKind
    {
        string Id { get; }
        // 상점 카드 그림(Resources/ 기준). null이면 화면이 종류별로 갖고 있는 그림
        string Icon { get; }
        double HalfWidth { get; }
        double Depth { get; }
        IReadOnlyList<SpotOffset> Spots { get; }
        // null이면 살 수 없는(놓을 수 없는) 종류
        PriceInfo Price { get; }
    }

    // 곳에 놓인 사물 하나
    public interface IPlaced
    {
        IPlacedKind Kind { get; }
        Vector2 Position { get; }
        void MoveTo(Vector2 position);
    }

    public enum PlacementCheck
    {
        Ok,
        OutsideFloor,
        Overlaps,
        NoWorkSpot,
        Unreachable,
    }

    public static class Placement
    {
        // 편집에서 사물을 집을 때 보는 높이(그림 키). 바닥 사각형은 얕아서 몸통을 못 집는다
        private const float k_PickHeight = 1.2f;
        private const float k_PickBelow = 0.15f;

        public static NavRect Rect(IPlacedKind kind, Vector2 at)
        {
            float half = (float)kind.HalfWidth;
            return new NavRect(at.X - half, at.Y, at.X + half, at.Y + (float)kind.Depth);
        }

        public static NavRect Rect(IPlaced thing)
        {
            return Rect(thing.Kind, thing.Position);
        }

        public static bool ContainsPick(IPlaced thing, Vector2 p)
        {
            Vector2 at = thing.Position;
            return Math.Abs(p.X - at.X) <= (float)thing.Kind.HalfWidth && p.Y >= at.Y - k_PickBelow && p.Y <= at.Y + k_PickHeight;
        }

        public static Vector2 SpotAt(Vector2 position, SpotOffset spot)
        {
            return position + new Vector2((float)spot.Dx, (float)spot.Dy);
        }

        // 그 역할의 첫 자리. 없으면 사물 자리
        public static Vector2 SpotOf(IPlaced thing, SpotRole role)
        {
            foreach (SpotOffset spot in thing.Kind.Spots)
            {
                if (spot.Role == role)
                {
                    return SpotAt(thing.Position, spot);
                }
            }

            return thing.Position;
        }

        // 서는 자리는 x는 가까운 격자, y는 아래 격자(사물보다 앞에 그려지게)
        public static Vector2 SnapSpot(BurrowNav nav, Vector2 candidate)
        {
            return new Vector2(nav.Snap(candidate).X, (float)Math.Floor(candidate.Y / nav.Step + 1e-4) * nav.Step);
        }

        // 사각형의 네 귀퉁이와 변 가운데가 모두 굴 바닥 안
        public static bool OnFloor(BurrowShape.Result shape, NavRect rect)
        {
            float midX = (rect.XMin + rect.XMax) * 0.5f;
            float midY = (rect.YMin + rect.YMax) * 0.5f;
            Vector2[] points =
            {
                new Vector2(rect.XMin, rect.YMin), new Vector2(rect.XMax, rect.YMin), new Vector2(rect.XMin, rect.YMax), new Vector2(rect.XMax, rect.YMax),
                new Vector2(midX, rect.YMin), new Vector2(midX, rect.YMax), new Vector2(rect.XMin, midY), new Vector2(rect.XMax, midY),
            };

            foreach (Vector2 p in points)
            {
                if (!BurrowNav.IsFloor(shape, p))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
