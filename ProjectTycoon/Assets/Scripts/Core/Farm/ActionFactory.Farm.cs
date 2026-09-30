namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27: 농장 행동(갈기·심기·거두기). 굴 파기는 공용 Dig(ActionFactory.cs)
    public static partial class ActionFactory
    {
        // 설계 27 갈기(버튼): 판 흙 칸을 코인(FarmConfigTable tillCost)으로 밭 칸으로. 코인이 모자라면 버튼이 꺼진다(편집 카드 · 시트 줄과 같은 규칙)
        private sealed class Till : InteractAction
        {
            public Till(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is PlotInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is PlotInteractable plot && !plot.IsTilled && worker.Wallet.Coins >= plot.Farm.Config.TillCost;
            }

            public override void Do(Worker worker, Interactable target)
            {
                PlotInteractable plot = (PlotInteractable)target;

                if (worker.Wallet.TrySpendCoins(plot.Farm.Config.TillCost))
                {
                    plot.Till();
                }
            }
        }

        // 심기(버튼): 갈아 놓은 빈 밭에 농장 작물을
        private sealed class Plant : InteractAction
        {
            public Plant(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is PlotInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is PlotInteractable plot && plot.IsTilled && plot.IsEmpty;
            }

            public override void Do(Worker worker, Interactable target)
            {
                PlotInteractable plot = (PlotInteractable)target;
                plot.Plant(plot.Farm.Crop);
            }
        }

        // 거두기(auto): 익은 밭 칸에 발이 들면 창고로
        private sealed class Harvest : InteractAction
        {
            public Harvest(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is PlotInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is PlotInteractable plot && plot.IsRipe;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((PlotInteractable)target).Harvest(worker.Wallet);
            }
        }
    }
}
