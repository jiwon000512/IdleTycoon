using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11 · 설계 18: 광장에 놓인 장식 하나. 무슨 장식인지는 표 행, 어디 놓였는지(밑변 가운데, 광장 원점 기준 유닛)는 편집으로 바뀌는 값
    public sealed class DecorationData : IPlaced
    {
        public DecorationTable Table { get; }
        public Vector2 Position { get; private set; }
        public IPlacedKind Kind => Table;

        public DecorationData(DecorationTable table, Vector2 position)
        {
            Table = table;
            Position = position;
        }

        public void MoveTo(Vector2 position)
        {
            Position = position;
        }
    }
}
