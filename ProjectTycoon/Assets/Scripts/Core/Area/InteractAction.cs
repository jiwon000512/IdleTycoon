namespace ZooTycoon.Core
{
    // 설계 13 v0.5: 사물에 하는 일 하나 = ActionTable 행 + 코드(ActionFactory가 id로 만든다).
    // 받을 수 있는 사물인지는 Accepts(종류·능력), 지금 할 수 있는지는 CanDo(상태). 어느 사물에 붙는지는 InteractableTable.actions
    public abstract class InteractAction
    {
        public ActionTable Table { get; }
        // 화면이 시트를 여는 행동(open·open_dig)
        public virtual bool OpensSheet => false;

        protected InteractAction(ActionTable table)
        {
            Table = table;
        }

        public abstract bool Accepts(Interactable target);

        public virtual bool CanDo(Worker worker, Interactable target)
        {
            return Accepts(target);
        }

        public virtual void Do(Worker worker, Interactable target)
        {
        }
    }
}
