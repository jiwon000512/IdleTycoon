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
