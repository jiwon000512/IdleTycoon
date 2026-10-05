using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 44: 낚시터 행동(모두 버튼). 말뚝: 털썩 → 합치기 → 들기/놓기 → 소환 → 열기(actions 순서 = 버튼 우선), 물가: 엉덩이 쿵.
    // 하는 일은 FishingArea가 갖고, 여기는 사물 종류와 할 수 있나만 본다
    public static partial class ActionFactory
    {
        private sealed class Summon : InteractAction
        {
            public Summon(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is StakeInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is StakeInteractable stake && stake.Fishing.CanSummon(stake);
            }

            public override void Do(Worker worker, Interactable target)
            {
                StakeInteractable stake = (StakeInteractable)target;
                stake.Fishing.Summon(stake);
            }
        }

        private sealed class OpenStake : InteractAction
        {
            public OpenStake(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is StakeInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is StakeInteractable stake && stake.Fishing.CanOpen(stake);
            }

            public override void Do(Worker worker, Interactable target)
            {
                StakeInteractable stake = (StakeInteractable)target;
                stake.Fishing.OpenStake(stake);
            }
        }

        private sealed class CarryRod : InteractAction
        {
            public CarryRod(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is StakeInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is StakeInteractable stake && stake.Fishing.CanCarry(stake);
            }

            public override void Do(Worker worker, Interactable target)
            {
                StakeInteractable stake = (StakeInteractable)target;
                stake.Fishing.Carry(stake);
            }
        }

        private sealed class MergeRods : InteractAction
        {
            public MergeRods(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is StakeInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is StakeInteractable stake && stake.Fishing.CanMerge(stake);
            }

            public override void Do(Worker worker, Interactable target)
            {
                StakeInteractable stake = (StakeInteractable)target;
                stake.Fishing.Merge(stake);
            }
        }

        private sealed class Haul : InteractAction
        {
            public Haul(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is StakeInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is StakeInteractable stake && stake.Fishing.CanHaul(stake);
            }

            public override void Do(Worker worker, Interactable target)
            {
                StakeInteractable stake = (StakeInteractable)target;
                stake.Fishing.Haul(stake);
            }
        }

        // 낚은 대물 시트: 특별한 대 셋이 줄. 말뚝이 다 찼고 이미 대를 들고 있으면 막힘
        private sealed class ChooseRod : SheetAction
        {
            public ChooseRod(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is BossCatchInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                FishingArea fishing = ((BossCatchInteractable)target).Fishing;
                SheetOptionState state = fishing.CanChoose ? SheetOptionState.Enabled : SheetOptionState.Blocked;
                List<SheetOption> options = new List<SheetOption>();

                foreach (RodTable rod in fishing.Choice ?? new RodTable[0])
                {
                    options.Add(new SheetOption(rod.Id, state));
                }

                return options;
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                FishingArea fishing = ((BossCatchInteractable)target).Fishing;

                for (int i = 0; fishing.Choice != null && i < fishing.Choice.Count; i++)
                {
                    if (fishing.Choice[i].Id == option)
                    {
                        return fishing.Choose(i);
                    }
                }

                return false;
            }
        }

        private sealed class Thump : InteractAction
        {
            public Thump(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is StreamBankInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is StreamBankInteractable bank && bank.Fishing.CanThump;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((StreamBankInteractable)target).Fishing.Thump();
            }
        }
    }
}
