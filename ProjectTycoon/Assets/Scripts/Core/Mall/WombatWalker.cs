using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 09 3장 · 설계 11: 조이스틱 방향으로 걷고, 막히면 X만·Y만 시도해 벽을 따라 미끄러진다. 빵집·광장이 같이 쓴다
    public static class WombatWalker
    {
        // 움직였으면 true(보는 방향도 바꾼다)
        public static bool Step(Mover wombat, Vector2 input, double speed, double dt, BurrowNav nav)
        {
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
            wombat.Facing = Mover.FacingOf(to - from);
            return true;
        }
    }
}
