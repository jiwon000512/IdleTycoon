namespace ZooTycoon.Core
{
    // 설계 16: 빵집 사건. 손님 사건은 곳마다 따로 둔다(곳마다 도착 행동이 달라질 수 있다)
    public static partial class Events
    {
        // 굴을 팠거나 사물(진열대·오븐·계산대·장식)이 놓이고 옮겨지고 치워졌다(설계 18: 광장도 낸다)
        public readonly struct LayoutChanged
        {
            public readonly WombatArea Area;

            public LayoutChanged(WombatArea area)
            {
                Area = area;
            }
        }

        // 칸 하나를 팠다(배치 갱신·파기 연출)
        public readonly struct Dug
        {
            public readonly BurrowGrid Grid;
            public readonly Cell Cell;

            public Dug(BurrowGrid grid, Cell cell)
            {
                Grid = grid;
                Cell = cell;
            }
        }

        public readonly struct BakeryVisitorArrived
        {
            public readonly BakeryVisitor Visitor;

            public BakeryVisitorArrived(BakeryVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        // 구멍으로 나갔다(계산을 마쳤거나 화나서)
        public readonly struct BakeryVisitorLeft
        {
            public readonly BakeryVisitor Visitor;

            public BakeryVisitorLeft(BakeryVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        public readonly struct BakeryVisitorPicked
        {
            public readonly BakeryVisitor Visitor;

            public BakeryVisitorPicked(BakeryVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        public readonly struct BakeryVisitorPaid
        {
            public readonly BakeryVisitor Visitor;
            public readonly double Coins;

            public BakeryVisitorPaid(BakeryVisitor visitor, double coins)
            {
                Visitor = visitor;
                Coins = coins;
            }
        }

        // 빵을 못 찾고 포기했다(효과음)
        public readonly struct BakeryVisitorGaveUp
        {
            public readonly BakeryVisitor Visitor;

            public BakeryVisitorGaveUp(BakeryVisitor visitor)
            {
                Visitor = visitor;
            }
        }
    }
}
