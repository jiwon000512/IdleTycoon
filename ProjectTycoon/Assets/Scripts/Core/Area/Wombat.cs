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
        // 설계 22: 머리 위 이모지 말풍선(대화·감정)
        public BubbleState Bubble { get; }
        // 조이스틱 걷기 속도(유닛/초)
        public double Speed { get; }
        // 조이스틱 방향(길이 1까지)
        public Vector2 Input { get; private set; }
        public bool Moving { get; private set; }
        // 곳에 들어온 직후: 누르고 있던 조이스틱을 놓을 때까지 걷지 않는다(굴을 등진 채 서고, 놓기 전에 통로 띠로 되돌아가지 않는다)
        public bool WaitingRelease { get; private set; }
        // 시스템이 걷게 하는 중(계산대 붙기, 앞으로 미션 가이드). 조이스틱을 건드리면 그만둔다
        public bool Guided => Mover.Moving;

        public Wombat(TableSet tables, ZooState wallet)
        {
            Mover = new Mover(Vector2.Zero, Facing.Down);
            Worker = new Worker(new Hands((int)tables.Get<ConfigTable>(ConfigTable.k_CarryCapacity).Value), wallet);
            Speed = tables.Get<ConfigTable>(ConfigTable.k_WombatSpeed).Value;
            Bubble = new BubbleState(tables);
        }

        public void SetInput(Vector2 input)
        {
            float length = input.Length();
            Input = length > 1f ? input / length : input;
        }

        // 설계 19: 시스템 이동 — 길을 따라 target까지 걷고 arrive 쪽을 본다(계산대 자리 붙기, 미션 가이드). 조이스틱을 건드리면 그 자리에서 그만둔다
        public void Guide(BurrowNav nav, Vector2 target, Facing arrive)
        {
            Mover.WalkTo(nav, target, arrive);
        }

        // 설계 09 3장 · 설계 11: 조이스틱 방향으로 걷고, 막히면 X만·Y만 시도해 벽을 따라 미끄러진다.
        // 보는 방향은 미끄러진 쪽이 아니라 조이스틱 쪽(2026-09-24 사용자 지적: 벽에 비비면 고개가 돌아간다)
        internal void Walk(BurrowNav nav, double dt)
        {
            Bubble.Tick(dt);

            if (WaitingRelease)
            {
                WaitingRelease = Input != Vector2.Zero;
                Moving = false;
                return;
            }

            if (Input == Vector2.Zero && Guided)
            {
                Mover.Advance(Speed * dt);
                Moving = true;
                return;
            }

            if (Input != Vector2.Zero)
            {
                Mover.Place(Mover.Position);
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
            Mover.Place(Mover.Position);
            Moving = false;
        }

        internal void WaitRelease()
        {
            WaitingRelease = true;
        }
    }
}
