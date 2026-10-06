using System.Collections.Generic;
using System.Linq;

namespace ZooTycoon.Core
{
    // 설계 44 · 46 · 49: 낚시터 행동. 말뚝: 털썩 → 대 사기(시트) → 열기(actions 순서 = 버튼 우선), 물가: 엉덩이 쿵, 미끼 노점: 업그레이드 둘(시트 줄). 합치기 · 옮기기는 낚시판 보기(FishingArea.TryMoveRod).
    // 하는 일은 FishingArea가 갖고, 여기는 사물 종류와 할 수 있나만 본다
    public static partial class ActionFactory
    {
        // 설계 45: 막다른 끝 앞에서 물길을 한 칸 더 판다(값은 월드 값 표식)
        private sealed class DigStream : InteractAction
        {
            public DigStream(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is StreamEndInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is StreamEndInteractable end && end.Fishing.CanDigStream;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((StreamEndInteractable)target).Fishing.DigStream();
            }
        }

        // 설계 46 대 사기 버튼: 열린 말뚝(비었든 꽂혔든, 월척을 붙잡지 않은 동안)에서 대 사기 시트를 연다(화면 몫)
        private sealed class OpenSummon : InteractAction
        {
            public OpenSummon(ActionTable table) : base(table)
            {
            }

            public override bool OpensSheet => true;

            public override bool Accepts(Interactable target)
            {
                return target is StakeInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is StakeInteractable stake && stake.Fishing.CanSummon(stake);
            }
        }

        // 설계 46 · 49 대 사기(시트 칩): 열린 계열마다 한 칩, 값은 공통. 고르면 사서 꽂는다(단 뽑기)
        private sealed class Summon : SheetAction
        {
            public Summon(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is StakeInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                StakeInteractable stake = (StakeInteractable)target;
                FishingArea fishing = stake.Fishing;
                List<SheetOption> options = new List<SheetOption>();

                foreach (RodTable rod in fishing.ShopRods)
                {
                    double cost = fishing.SummonPrice;
                    options.Add(new SheetOption(rod.Id, fishing.CanSummon(stake) ? SheetOption.Afford(worker, cost) : SheetOptionState.Blocked, cost));
                }

                return options;
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                StakeInteractable stake = (StakeInteractable)target;
                RodTable rod = stake.Fishing.ShopRods.FirstOrDefault(r => r.Id == option);
                return rod != null && stake.Fishing.Summon(stake, rod);
            }
        }

        // 설계 46 미끼 노점 업그레이드(시트 줄): 표 순서로 한 줄씩, 단계 · 효과 전후 · 값(최대면 Max)
        private sealed class HutUpgrade : SheetAction
        {
            public HutUpgrade(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is FishingHutInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                FishingArea fishing = ((FishingHutInteractable)target).Fishing;
                List<SheetOption> options = new List<SheetOption>();

                foreach (FishingUpgradeData upgrade in fishing.Config.HutUpgrades)
                {
                    int level = fishing.LevelOf(upgrade.Id);
                    double cost = fishing.UpgradePrice(upgrade.Id);
                    bool maxed = level >= upgrade.MaxLevel;
                    SheetOptionState state = maxed ? SheetOptionState.Max : SheetOption.Afford(worker, cost);
                    options.Add(new SheetOption(upgrade.Id, state, cost, level, EffectAt(upgrade, level), EffectAt(upgrade, maxed ? level : level + 1)));
                }

                return options;
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                FishingArea fishing = ((FishingHutInteractable)target).Fishing;
                return fishing.LevelOf(option) < fishing.Upgrade(option).MaxLevel && fishing.BuyUpgrade(option);
            }

            // 문구에 넣을 효과 값(단계 0 = 0): 불빛 = 사거리 +%, 소용돌이 = 1분에 되돌리는 번
            private static double EffectAt(FishingUpgradeData upgrade, int level)
            {
                if (upgrade.Id == FishingUpgradeData.k_Light)
                {
                    return System.Math.Round(upgrade.EffectPerLevel * level * 100d);
                }

                return level > 0 ? System.Math.Round(60d / (upgrade.EffectBase - upgrade.EffectPerLevel * (level - 1))) : 0d;
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
