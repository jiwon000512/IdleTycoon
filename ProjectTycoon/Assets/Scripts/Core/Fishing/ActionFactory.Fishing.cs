namespace ZooTycoon.Core
{
    // 설계 52 우물 낚시: 물가 행동 셋(빈 줄: 던지기 → 기다림 · 입질: 낚아채기 → 줄다리기: 감기). 하는 일은 FishingArea가 갖고, 여기는 때만 본다
    public static partial class ActionFactory
    {
        private sealed class Cast : InteractAction
        {
            public Cast(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is WaterInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                // 낚은 만세 · 놓친 뒤 잠깐 서 있는 동안(Busy)은 던지지 않는다
                return target is WaterInteractable well && well.Fishing.Phase == FishingPhase.Idle && !well.Fishing.Wombat.Busy;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((WaterInteractable)target).Fishing.Cast();
            }
        }

        // 낚아채기: 물었으면 걸리고(줄다리기), 아직이면 줄을 거둔다
        private sealed class Strike : InteractAction
        {
            public Strike(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is WaterInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is WaterInteractable well && (well.Fishing.Phase == FishingPhase.Waiting || well.Fishing.Phase == FishingPhase.Bite);
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((WaterInteractable)target).Fishing.Strike();
            }
        }

        // 감기: 줄다리기 동안 버튼을 누르고 있는 것이 감기(Wombat.Holding, 화면이 넣는다). 누름 자체는 아무것도 하지 않는다
        private sealed class Reel : InteractAction
        {
            public Reel(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is WaterInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is WaterInteractable well && well.Fishing.Phase == FishingPhase.Tug;
            }

            public override void Do(Worker worker, Interactable target)
            {
            }
        }
    }
}
