using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 52 우물 낚시: 물가 행동 셋(빈 줄: 던지기 → 기다림 · 입질: 낚아채기 → 줄다리기: 감기). 하는 일은 FishingArea가 갖고, 여기는 때만 본다.
    // 설계 61 섬들: 다리 놓기(시트) · 상자 끌어내기(버튼) · 좌대 놓기(시트)
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
                return target is WaterInteractable water && water.Fishing.Phase == FishingPhase.Idle && !water.Fishing.Wombat.Busy;
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
                return target is WaterInteractable water && (water.Fishing.Phase == FishingPhase.Waiting || water.Fishing.Phase == FishingPhase.Bite);
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((WaterInteractable)target).Fishing.Strike();
            }
        }

        // 감기: 줄다리기 동안 버튼을 누르고 있는 것이 감기(Wombat.Holding, 화면이 넣는다). 누름 자체는 아무것도 하지 않는다.
        // 설계 61: 상자 줄다리기 동안 대상은 상자라 상자에서도 감기 버튼이 뜬다
        private sealed class Reel : InteractAction
        {
            public Reel(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is WaterInteractable || target is CrateInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                FishingArea fishing = target is WaterInteractable water ? water.Fishing : target is CrateInteractable crate ? crate.Fishing : null;
                return fishing != null && fishing.Phase == FishingPhase.Tug;
            }

            public override void Do(Worker worker, Interactable target)
            {
            }
        }

        // 설계 61 다리 놓기(시트 줄): 표의 값을 치르고 그 섬에 닿는다
        private sealed class PlaceBridge : SheetAction
        {
            public PlaceBridge(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is BridgeInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                double cost = ((BridgeInteractable)target).Island.Bridge.Cost;
                return new[] { new SheetOption(null, SheetOption.Afford(worker, cost), cost) };
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                BridgeInteractable bridge = (BridgeInteractable)target;
                return bridge.Fishing.TryBridge(worker, bridge);
            }
        }

        // 설계 61 끌어내기: 빈 줄이고 서 있지 않을 때(만세 · 놓친 뒤 잠깐 서기 동안은 안 됨) 떠내려온 상자에 줄을 건다
        private sealed class PullDebris : InteractAction
        {
            public PullDebris(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is CrateInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is CrateInteractable crate && crate.Fishing.Phase == FishingPhase.Idle && !crate.Fishing.Wombat.Busy;
            }

            public override void Do(Worker worker, Interactable target)
            {
                CrateInteractable crate = (CrateInteractable)target;
                crate.Fishing.PullCrate(crate);
            }
        }

        // 설계 53 좌대 놓기(시트 줄)
        private sealed class PlaceSeat : SheetAction
        {
            public PlaceSeat(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is SeatInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                double cost = ((SeatInteractable)target).Row.Cost;
                return new[] { new SheetOption(null, SheetOption.Afford(worker, cost), cost) };
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                SeatInteractable seat = (SeatInteractable)target;
                return seat.Fishing.TryPlaceSeat(worker, seat);
            }
        }
    }
}
