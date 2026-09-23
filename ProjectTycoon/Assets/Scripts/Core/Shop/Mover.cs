using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2: 화면이 그릴 방향. 오른쪽 = 옆모습, 왼쪽 = 옆모습 뒤집기
    public enum Facing
    {
        Down,
        Up,
        Left,
        Right,
    }

    // 꺾임점을 차례로 지나 일정한 빠르기로 움직인다(가게 원점 기준 유닛, y 위). 보는 방향은 지금 선분 방향
    public sealed class Mover
    {
        private readonly List<Vector2> m_points = new List<Vector2>();
        private int m_next;

        public Vector2 Position { get; private set; }
        public Facing Facing { get; set; }
        // 길 끝에 닿으면 볼 방향(진열대 쪽, 줄 앞사람 쪽 등)
        public Facing ArriveFacing { get; private set; }
        public bool Moving => m_next < m_points.Count;
        public Vector2 Destination => Moving ? m_points[m_points.Count - 1] : Position;

        public Mover(Vector2 position, Facing facing)
        {
            Position = position;
            Facing = facing;
        }

        // 걸을 길(지금 위치 다음 점부터). 빈 목록이면 그 자리에서 arrive 쪽을 본다
        public void Follow(IReadOnlyList<Vector2> points, Facing arrive)
        {
            m_points.Clear();
            m_next = 0;
            ArriveFacing = arrive;

            foreach (Vector2 point in points)
            {
                m_points.Add(point);
            }

            TurnToNext();
        }

        // 걷는 중이면 지금 선분 끝점(격자 점)을 지나야 할 첫 점으로 남긴다. 새 길은 그 점에서 찾는다
        public Vector2 NextNode => Moving ? m_points[m_next] : Position;

        public void Place(Vector2 position)
        {
            m_points.Clear();
            m_next = 0;
            Position = position;
        }

        public void Advance(double distance)
        {
            float left = (float)distance;

            while (left > 0f && Moving)
            {
                Vector2 target = m_points[m_next];
                float gap = Vector2.Distance(Position, target);

                if (gap <= left)
                {
                    Position = target;
                    left -= gap;
                    m_next++;
                    TurnToNext();
                    continue;
                }

                Position += (target - Position) * (left / gap);
                left = 0f;
            }
        }

        public static Facing FacingOf(Vector2 delta)
        {
            if (Math.Abs(delta.X) >= Math.Abs(delta.Y))
            {
                return delta.X < 0f ? Facing.Left : Facing.Right;
            }

            return delta.Y < 0f ? Facing.Down : Facing.Up;
        }

        private void TurnToNext()
        {
            if (!Moving)
            {
                Facing = ArriveFacing;
                return;
            }

            Vector2 delta = m_points[m_next] - Position;

            if (delta.LengthSquared() > 1e-8f)
            {
                Facing = FacingOf(delta);
            }
        }
    }
}
