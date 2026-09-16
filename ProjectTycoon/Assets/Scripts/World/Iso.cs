using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 07-2: 논리 좌표(XZ, 유닛)와 화면 좌표(XY, 2:1 쿼터뷰)의 변환. 월드는 순수 2D이고 이 함수만이 두 좌표계를 잇는다
    // 논리 +X는 화면 오른쪽 위, +Z는 화면 왼쪽 위. 논리 1유닛의 화면 폭 = 2 × k_X
    public static class Iso
    {
        public const float k_X = 0.70710678f;
        public const float k_Y = 0.35355339f;

        public static Vector3 ToScreen(Vector2 logical)
        {
            return new Vector3((logical.x - logical.y) * k_X, (logical.x + logical.y) * k_Y, 0f);
        }

        public static Vector2 ToLogical(Vector3 screen)
        {
            float u = screen.x / k_X;
            float v = screen.y / k_Y;
            return new Vector2((u + v) * 0.5f, (v - u) * 0.5f);
        }

        // 논리 사각형의 네 꼭짓점을 감싸는 화면 사각형
        public static Rect ToScreenBounds(Rect logical)
        {
            Vector3 bottom = ToScreen(new Vector2(logical.xMin, logical.yMin));
            Vector3 right = ToScreen(new Vector2(logical.xMax, logical.yMin));
            Vector3 top = ToScreen(new Vector2(logical.xMax, logical.yMax));
            Vector3 left = ToScreen(new Vector2(logical.xMin, logical.yMax));
            return Rect.MinMaxRect(left.x, bottom.y, right.x, top.y);
        }
    }
}
