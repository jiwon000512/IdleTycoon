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
    }
}
