using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 13 v0.5: 시트의 줄을 내놓는 행동(mode sheet). 줄 하나 = 고를 것 하나(빵·업그레이드 등). 줄을 누르면 TryBuy
    public abstract class SheetAction : InteractAction
    {
        protected SheetAction(ActionTable table) : base(table)
        {
        }

        public abstract IReadOnlyList<SheetOption> Options(Worker worker, Interactable target);

        public abstract bool TryBuy(Worker worker, Interactable target, string option);
    }
}
