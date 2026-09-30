namespace ZooTycoon.Core
{
    // 설계 25 · 27: 농장 사건
    public static partial class Events
    {
        // 흙 칸을 갈아 밭 칸이 됐다(그림 · 소리)
        public readonly struct Tilled
        {
            public readonly PlotInteractable Plot;

            public Tilled(PlotInteractable plot)
            {
                Plot = plot;
            }
        }

        // 익은 밭을 거뒀다: 창고에 재료 count개(팝업 · 소리)
        public readonly struct Harvested
        {
            public readonly PlotInteractable Plot;
            public readonly string Item;
            public readonly int Count;

            public Harvested(PlotInteractable plot, string item, int count)
            {
                Plot = plot;
                Item = item;
                Count = count;
            }
        }
    }
}
