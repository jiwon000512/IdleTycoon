namespace ZooTycoon.Core
{
    // 설계 29: 광장 행동
    public static partial class ActionFactory
    {
        // 석상 앞 버튼: 석상 팝업을 연다
        private sealed class OpenStatue : InteractAction
        {
            public OpenStatue(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is StatueInteractable;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((StatueInteractable)target).Open();
            }
        }

        // 설계 65 둑 치기: 단추를 누를 때마다 한 번(파는 동작이 끝나야 다음 타). 첫 타에 코인이 모자라면 못 친다(값은 빨갛게 보인다)
        private sealed class BreakDam : InteractAction
        {
            public BreakDam(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is DamInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is DamInteractable dam && dam.Left > 0 && !dam.Area.Wombat.Busy && worker.Wallet.Coins >= dam.Cost;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((DamInteractable)target).TryStrike(worker);
            }

            public override double CostOf(Worker worker, Interactable target)
            {
                return target is DamInteractable dam ? dam.Cost : 0d;
            }
        }

        // 설계 31 · 34 행상에게 말 걸기: 행상이 좌판 자리에 선 동안 인사를 듣고 뽑기 팝업
        private sealed class TalkToMerchant : InteractAction
        {
            public TalkToMerchant(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is MerchantInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is MerchantInteractable merchant && merchant.IsOpen;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((MerchantInteractable)target).Talk();
            }
        }
    }
}
