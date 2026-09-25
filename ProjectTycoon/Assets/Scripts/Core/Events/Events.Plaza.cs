namespace ZooTycoon.Core
{
    // 설계 16: 광장 사건
    public static partial class Events
    {
        public readonly struct PlazaVisitorArrived
        {
            public readonly PlazaVisitor Visitor;

            public PlazaVisitorArrived(PlazaVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        // 빵집 문이나 계단으로 들어갔다
        public readonly struct PlazaVisitorLeft
        {
            public readonly PlazaVisitor Visitor;

            public PlazaVisitorLeft(PlazaVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        // 들를 곳에 멈춰 ♥를 띄운다
        public readonly struct PlazaVisitorEmoted
        {
            public readonly PlazaVisitor Visitor;

            public PlazaVisitorEmoted(PlazaVisitor visitor)
            {
                Visitor = visitor;
            }
        }
    }
}
