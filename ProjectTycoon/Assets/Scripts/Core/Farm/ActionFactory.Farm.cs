namespace ZooTycoon.Core
{
    // 설계 25: 농장 행동(심기·거두기). 밭 사기는 편집 모드(WombatArea.Placement)
    public static partial class ActionFactory
    {
        // 심기(버튼): 빈 밭에 농장 작물을
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
                return target is PlotInteractable plot && plot.IsEmpty;
            }

            public override void Do(Worker worker, Interactable target)
            {
                PlotInteractable plot = (PlotInteractable)target;
                plot.Plant(plot.Farm.Crop);
            }
        }

        // 거두기(auto): 익은 밭 곁을 지나가면 창고로
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
