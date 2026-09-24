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
        public bool Moving { get; internal set; }

        public Wombat(TableSet tables, ZooState wallet)
        {
            Mover = new Mover(Vector2.Zero, Facing.Down);
            Worker = new Worker(new Hands(tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).CarryCapacity), wallet);
            Speed = tables.Get<ConfigTable>(ConfigTable.k_WombatSpeed).Value;
        }

        public void SetInput(Vector2 input)
        {
            float length = input.Length();
            Input = length > 1f ? input / length : input;
        }
    }
}
