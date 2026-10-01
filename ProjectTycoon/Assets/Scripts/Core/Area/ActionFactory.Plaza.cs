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

        // 설계 31 유물 수레 앞 버튼: 행상이 좌판을 연 동안 유물 팝업을 연다
        private sealed class OpenRelicCart : InteractAction
        {
            public OpenRelicCart(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is RelicCartInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is RelicCartInteractable cart && cart.IsOpen;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((RelicCartInteractable)target).Open();
            }
        }
    }
}
