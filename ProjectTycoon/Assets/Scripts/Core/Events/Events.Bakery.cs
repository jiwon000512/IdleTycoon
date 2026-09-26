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

        // ---------- 설계 21 점원 ----------

        // 점원을 고용했다(구멍에서 톡 나와 자리로 간다)
        public readonly struct ClerkHired
        {
            public readonly Clerk Clerk;

            public ClerkHired(Clerk clerk)
            {
                Clerk = clerk;
            }
        }

        // 점원이 그만둔다(말풍선 → 구멍으로 걸어 나간다). 자리는 바로 빈다
        public readonly struct ClerkFired
        {
            public readonly Clerk Clerk;
            public readonly FireReason Reason;

            public ClerkFired(Clerk clerk, FireReason reason)
            {
                Clerk = clerk;
                Reason = reason;
            }
        }

        // 그만둔 점원이 구멍으로 사라졌다(그림 삭제)
        public readonly struct ClerkLeft
        {
            public readonly Clerk Clerk;

            public ClerkLeft(Clerk clerk)
            {
                Clerk = clerk;
            }
        }

        // 설계 22: 웜뱃이 딴짓하던 점원을 깨웠다(톡 튀기·효과음 자리)
        public readonly struct ClerkWoke
        {
            public readonly Clerk Clerk;

            public ClerkWoke(Clerk clerk)
            {
                Clerk = clerk;
            }
        }

        // 설계 22 외출: 점원이 구멍으로 나갔다(광장이 그림을 세운다) · 돌아오라고 했다(광장 그림이 문으로) · 광장 그림이 문으로 들어왔다(구멍에서 나온다)
        public readonly struct ClerkWentOut
        {
            public readonly Clerk Clerk;

            public ClerkWentOut(Clerk clerk)
            {
                Clerk = clerk;
            }
        }

        public readonly struct ClerkReturning
        {
            public readonly Clerk Clerk;

            public ClerkReturning(Clerk clerk)
            {
                Clerk = clerk;
            }
        }

        public readonly struct ClerkCameBack
        {
            public readonly Clerk Clerk;

            public ClerkCameBack(Clerk clerk)
            {
                Clerk = clerk;
            }
        }

        // 대기 후보가 바뀌었다(고용·새 후보 보기)
        public readonly struct CandidatesChanged
        {
            public readonly BakeryArea Bakery;

            public CandidatesChanged(BakeryArea bakery)
            {
                Bakery = bakery;
            }
        }
    }
}
