using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27 · 35: 농장 행동(갈기·심기 버튼·심기 시트·거두기). 굴 파기는 공용 Dig(ActionFactory.cs)
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

        // 설계 35 심기 버튼: 갈아 놓은 빈 밭에서만 뜨고 밭 시트를 연다(화면 몫)
        private sealed class OpenPlant : InteractAction
        {
            public OpenPlant(ActionTable table) : base(table)
            {
            }

            public override bool OpensSheet => true;

            public override bool Accepts(Interactable target)
            {
                return target is PlotInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is PlotInteractable plot && plot.IsTilled && plot.IsEmpty;
            }
        }

        // 설계 35 심기(밭 시트 칩): 연 작물마다 한 칩 + 다음 작물 해금 칩(값 = unlockCost, 사면 열리고 그 밭에 바로 심는다). 오븐의 Bake와 같은 모양.
        // 심을 때 창고에 거름이 있으면 하나 써서 거름 준 밭으로(설계 28)
        private sealed class Plant : SheetAction
        {
            public Plant(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is PlotInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                FarmArea farm = ((PlotInteractable)target).Farm;
                List<SheetOption> options = new List<SheetOption>();

                foreach (CropTable crop in farm.UnlockedCrops)
                {
                    options.Add(new SheetOption(crop.Id, SheetOptionState.Enabled));
                }

                CropTable next = farm.NextCrop;

                if (next != null)
                {
                    options.Add(new SheetOption(next.Id, SheetOption.Afford(worker, next.UnlockCost), next.UnlockCost));
                }

                return options;
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                PlotInteractable plot = (PlotInteractable)target;
                FarmArea farm = plot.Farm;

                if (!plot.IsTilled || !plot.IsEmpty)
                {
                    return false;
                }

                CropTable next = farm.NextCrop;

                if (next != null && next.Id == option)
                {
                    if (!worker.Wallet.TrySpendCoins(next.UnlockCost))
                    {
                        return false;
                    }

                    farm.UnlockCrop(next);
                    Sow(worker, plot, next);
                    return true;
                }

                foreach (CropTable crop in farm.UnlockedCrops)
                {
                    if (crop.Id == option)
                    {
                        Sow(worker, plot, crop);
                        return true;
                    }
                }

                return false;
            }

            private static void Sow(Worker worker, PlotInteractable plot, CropTable crop)
            {
                plot.Plant(crop, worker.Wallet.TrySpendItem(plot.Farm.Config.ManureItem, 1));
            }
        }

        // 거두기(auto): 익은 밭 몸통에 웜뱃 발이 들면 창고로(칸 둘레 여백에서는 안 거둔다). 거두는 것은 웜뱃뿐이라 자리는 밭의 곳에서 읽는다
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
                return target is PlotInteractable plot && plot.IsRipe && plot.IsUnderfoot(plot.Farm.Wombat.Mover.Position);
            }

            public override void Do(Worker worker, Interactable target)
            {
                PlotInteractable plot = (PlotInteractable)target;
                plot.Harvest(worker.Wallet, plot.Farm.Random);
            }
        }
    }
}
