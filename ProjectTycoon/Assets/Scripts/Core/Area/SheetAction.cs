using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 13 v0.5: 시트의 줄을 내놓는 행동(mode sheet). 줄 하나 = 고를 것 하나(빵·업그레이드 등). 줄을 누르면 TryChoose(사는 것도, 굽기처럼 고르기만 하는 것도)
    public abstract class SheetAction : InteractAction
    {
        protected SheetAction(ActionTable table) : base(table)
        {
        }

        public abstract IReadOnlyList<SheetOption> Options(Worker worker, Interactable target);

        public abstract bool TryChoose(Worker worker, Interactable target, string option);
    }
}
