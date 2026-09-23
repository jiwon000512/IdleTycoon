using System;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5: 굴의 칸 좌표. Col은 x=0을 경계로 −1·0이 가운데 두 칸, Row는 0이 입구 줄이고 아래로 는다
    public readonly struct Cell : IEquatable<Cell>
    {
        public int Col { get; }
        public int Row { get; }

        public Cell(int col, int row)
        {
            Col = col;
            Row = row;
        }

        public Cell Offset(int dCol, int dRow)
        {
            return new Cell(Col + dCol, Row + dRow);
        }

        public bool Equals(Cell other)
        {
            return Col == other.Col && Row == other.Row;
        }

        public override bool Equals(object obj)
        {
            return obj is Cell other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Col * 397 ^ Row;
        }

        public override string ToString()
        {
            return $"({Col},{Row})";
        }
    }
}
