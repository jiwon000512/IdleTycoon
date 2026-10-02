namespace ZooTycoon.Core
{
    // 설계 25 · 27 · 39: 농장 사건
    public static partial class Events
    {
        // 설계 39: 아래층이 열렸다(위층 계단 굴 · 점원 팝업 탭 · 소리). Floor = 열린 층, 계단은 Floor.Upper에
        public readonly struct FloorOpened
        {
            public readonly FarmArea Floor;

            public FloorOpened(FarmArea floor)
            {
                Floor = floor;
            }
        }

        // 흙 칸을 갈아 밭 칸이 됐다(그림 · 소리)
        public readonly struct Tilled
        {
            public readonly PlotInteractable Plot;

            public Tilled(PlotInteractable plot)
            {
                Plot = plot;
            }
        }

        // 익은 밭을 거뒀다: 창고에 그 작물의 재료 count개(팝업 · 소리). 설계 35: 작물이 여럿이라 거둔 작물을 싣는다(덤 작물 판정은 그 작물의 yield와 비교)
        public readonly struct Harvested
        {
            public readonly PlotInteractable Plot;
            public readonly CropTable Crop;
            public readonly int Count;

            public Harvested(PlotInteractable plot, CropTable crop, int count)
            {
                Plot = plot;
                Crop = crop;
                Count = count;
            }
        }

        // 설계 28: 거둘 때 덤(bonusItem)이 나왔다(팝업 · 반짝 알갱이 · 소리)
        public readonly struct BonusFound
        {
            public readonly PlotInteractable Plot;
            public readonly string Item;
            public readonly int Count;

            public BonusFound(PlotInteractable plot, string item, int count)
            {
                Plot = plot;
                Item = item;
                Count = count;
            }
        }
    }
}
