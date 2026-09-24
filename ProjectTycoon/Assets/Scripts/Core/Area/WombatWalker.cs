using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 09 3장 · 설계 11: 조이스틱 방향으로 걷고, 막히면 X만·Y만 시도해 벽을 따라 미끄러진다. 빵집·광장이 같이 쓴다.
    // 보는 방향은 미끄러진 쪽이 아니라 조이스틱 쪽(2026-09-24 사용자 지적: 벽에 비비면 고개가 돌아간다)
    public static class WombatWalker
    {
        // 움직였으면 true. 조이스틱을 밀고 있으면 벽에 막혀도 그쪽을 본다
        public static bool Step(Mover wombat, Vector2 input, double speed, double dt, BurrowNav nav)
        {
            if (input != Vector2.Zero)
            {
                wombat.Facing = Mover.FacingOf(input);
            }

            Vector2 from = wombat.Position;
            Vector2 step = input * (float)(speed * dt);
            Vector2 to = from + step;

            if (!nav.IsWalkable(to))
            {
                to = from + new Vector2(step.X, 0f);
            }

            if (!nav.IsWalkable(to))
            {
                to = from + new Vector2(0f, step.Y);
            }

            if (to == from || !nav.IsWalkable(to))
            {
                return false;
            }

            wombat.Place(to);
            return true;
        }
    }
}
