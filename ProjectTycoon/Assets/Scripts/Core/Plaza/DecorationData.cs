using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11: 광장에 놓인 장식 하나. 무슨 장식인지는 표 행, 어디 놓였는지(밑변 가운데, 광장 원점 기준 유닛)는 런타임 값
    public sealed class DecorationData
    {
        public DecorationTable Table { get; }
        public Vector2 Position { get; }

        public DecorationData(DecorationTable table, Vector2 position)
        {
            Table = table;
            Position = position;
        }
    }
}
