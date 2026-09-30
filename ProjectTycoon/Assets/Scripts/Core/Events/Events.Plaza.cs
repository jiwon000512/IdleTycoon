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

        // 설계 29: 석상 앞 버튼을 눌렀다(팝업)
        public readonly struct StatueOpened
        {
            public readonly StatueInteractable Thing;

            public StatueOpened(StatueInteractable thing)
            {
                Thing = thing;
            }
        }

        // 설계 29: 석상 줄이 바뀌었다. Rolled면 굴렸다(잠그지 않은 줄이 새로), 아니면 잠금만
        public readonly struct StatueChanged
        {
            public readonly Statue Statue;
            public readonly bool Rolled;

            public StatueChanged(Statue statue, bool rolled)
            {
                Statue = statue;
                Rolled = rolled;
            }
        }
    }
}
