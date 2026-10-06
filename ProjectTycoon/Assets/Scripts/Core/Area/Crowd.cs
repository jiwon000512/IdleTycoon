using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2 6장 · 설계 47: 가게 손님끼리 비켜 걷기(빵집 · 횟집 공용). 길·자리는 그대로이고 화면만 Visitor.Sidestep을 더한다
    public static class Crowd
    {
        // 이 거리 안의 손님을 피한다(손님 그림 폭). 비키는 최대 거리는 걷는 땅 여유(BurrowNav.k_Clearance 0.3) 안
        private const float k_PersonalSpace = 0.6f;
        private const float k_MaxSidestep = 0.25f;
        private const float k_SidestepRate = 8f;
        // 앞사람이 이만큼도 옆에 있지 않으면 한 줄로 마주 온 것: 오른쪽으로 비킨다
        private const float k_InLine = 0.05f;

        public static void Sidestep<T>(IReadOnlyList<T> visitors, double dt) where T : Visitor
        {
            float blend = (float)Math.Min(1d, dt * k_SidestepRate);

            foreach (T visitor in visitors)
            {
                visitor.Sidestep += (Target(visitor, visitors) - visitor.Sidestep) * blend;
            }
        }

        // 걷는 손님은 앞에 있는 손님 반대쪽 옆으로(한 줄로 마주 오면 오른쪽으로), 선 손님은 가까운 손님 반대쪽으로 비킨다
        private static Vector2 Target<T>(T visitor, IReadOnlyList<T> visitors) where T : Visitor
        {
            if (visitor.Hopping)
            {
                return Vector2.Zero;
            }

            Vector2 self = visitor.Mover.Position;
            Vector2 ahead = visitor.Mover.NextNode - self;
            bool walking = ahead.LengthSquared() > 1e-6f;
            Vector2 left = walking ? Vector2.Normalize(new Vector2(-ahead.Y, ahead.X)) : Vector2.Zero;
            Vector2 push = Vector2.Zero;

            foreach (T other in visitors)
            {
                Vector2 toOther = other.Mover.Position - self;
                float distance = toOther.Length();

                if (other == visitor || other.Hopping || distance >= k_PersonalSpace)
                {
                    continue;
                }

                float strength = 1f - distance / k_PersonalSpace;

                if (walking)
                {
                    if (Vector2.Dot(toOther, ahead) > 0f)
                    {
                        push += left * (Vector2.Dot(toOther, left) < -k_InLine ? strength : -strength);
                    }
                }
                else if (distance > 1e-4f)
                {
                    push -= toOther / distance * strength;
                }
            }

            float length = push.Length();
            return (length > 1f ? push / length : push) * k_MaxSidestep;
        }
    }
}
