using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 13: 웜뱃 하나. Mall이 빵집·광장에 같이 넘기고, 있는 곳이 걷게 한다(위치는 그곳 좌표). 든 빵은 곳을 옮겨도 그대로
    public sealed class Wombat
    {
        public Mover Mover { get; }
        // v0.5: 행동하는 쪽(손 + 지갑)
        public Worker Worker { get; }
        // 조이스틱 걷기 속도(유닛/초)
        public double Speed { get; }
        // 조이스틱 방향(길이 1까지)
        public Vector2 Input { get; private set; }
        public bool Moving { get; private set; }

        public Wombat(TableSet tables, ZooState wallet)
        {
            Mover = new Mover(Vector2.Zero, Facing.Down);
            Worker = new Worker(new Hands((int)tables.Get<ConfigTable>(ConfigTable.k_CarryCapacity).Value), wallet);
            Speed = tables.Get<ConfigTable>(ConfigTable.k_WombatSpeed).Value;
        }

        public void SetInput(Vector2 input)
        {
            float length = input.Length();
            Input = length > 1f ? input / length : input;
        }

        // 설계 09 3장 · 설계 11: 조이스틱 방향으로 걷고, 막히면 X만·Y만 시도해 벽을 따라 미끄러진다.
        // 보는 방향은 미끄러진 쪽이 아니라 조이스틱 쪽(2026-09-24 사용자 지적: 벽에 비비면 고개가 돌아간다)
        internal void Walk(BurrowNav nav, double dt)
        {
            if (Input != Vector2.Zero)
            {
                Mover.Facing = Mover.FacingOf(Input);
            }

            Vector2 from = Mover.Position;
            Vector2 step = Input * (float)(Speed * dt);
            Vector2 to = from + step;

            if (!nav.IsWalkable(to))
            {
                to = from + new Vector2(step.X, 0f);
            }

            if (!nav.IsWalkable(to))
            {
                to = from + new Vector2(0f, step.Y);
            }

            Moving = to != from && nav.IsWalkable(to);

            if (Moving)
            {
                Mover.Place(to);
            }
        }

        internal void Stop()
        {
            Moving = false;
        }
    }
}
