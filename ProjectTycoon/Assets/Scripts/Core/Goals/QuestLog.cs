using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 54: 퀘스트 사슬(QuestTable 행 순서). 지금 걸음 하나를 세고, 다 하면 받기로 보상을 받고 다음 걸음으로.
    // 다음 걸음이 상태 종류로 이미 됐으면 넘기고 넘긴 걸음의 보상까지 모아 한 번에 준다(2026-10-08 사용자). 사슬이 끝나면 Current는 null
    public sealed class QuestLog
    {
        private readonly Mall m_mall;
        private readonly EventBus m_bus;
        private readonly Guide m_guide;
        private readonly IReadOnlyList<QuestTable> m_steps;
        private int m_shownIndex = -1;
        private int m_shownCount = -1;

        public int Index { get; private set; }
        // 사건 종류의 진행(이 걸음이 시작된 뒤 센 수)
        public int Progress { get; private set; }
        public QuestTable Current => Index < m_steps.Count ? m_steps[Index] : null;
        // 지금 걸음의 진행. 상태 종류는 지금 상태를 읽고, count에서 멈춘다. 할 수 없는 걸음(유물을 다 모은 뒤 뽑기)은 다 된 것(옛 저장이 막히지 않게, 2026-10-09 리뷰)
        public int Count => Current == null ? 0 : !GoalCounter.IsPossible(Current.Kind, m_mall) ? Current.Count
            : Math.Min(Current.Count, GoalCounter.IsState(Current.Kind) ? GoalCounter.Current(Current.Kind, Current.Param, m_mall) : Progress);
        public bool IsDone => Current != null && Count >= Current.Count;
        // 오늘의 일 상자 단계 = 지금 걸음의 장(사슬이 끝나면 마지막 장)
        public int Chapter => m_steps.Count == 0 ? 0 : (Current ?? m_steps[m_steps.Count - 1]).Chapter;

        public QuestLog(Mall mall, TableSet tables, EventBus bus, Guide guide)
        {
            m_mall = mall;
            m_bus = bus;
            m_guide = guide;
            m_steps = tables.GetAll<QuestTable>();
            GoalCounter.Listen(bus, Counted);
            // 처음 걸음을 이미 보인 것으로 둔다(첫 틱이 「걸음이 바뀌었다」로 길잡이를 끄지 않게)
            m_shownIndex = Index;
            m_shownCount = Count;
        }

        // 매 프레임: 상태 종류는 사건 없이도 바뀌니 다시 본다(바뀌었을 때만 알린다)
        public void Tick()
        {
            Refresh();
        }

        // 가기: 지금 걸음의 대상으로. 메뉴가 대상이면 그 버튼을 반짝이고(웜뱃은 그대로, 2026-10-08 사용자), 아니면 길잡이가 데려간다. 갈 곳이 없으면 false
        public bool Go()
        {
            QuestTable step = Current;

            if (step == null || IsDone)
            {
                return false;
            }

            if (step.Menu != null)
            {
                m_bus.Publish(new Events.MenuPointed(step.Menu));
                return true;
            }

            return m_guide.TryStart(step.Area ?? m_mall.Active.Id, step.Thing, step.Kind, step.Param);
        }

        // 받기: 다 한 걸음의 보상을 받고 다음 걸음으로. 이어서 이미 된 걸음은 넘기며 보상을 모아 한 번에 준다
        public bool TryClaim()
        {
            if (!IsDone)
            {
                return false;
            }

            double coins = 0d;
            Dictionary<string, int> items = new Dictionary<string, int>();

            do
            {
                QuestTable step = Current;
                coins += step.Coins;

                if (step.Item != null && step.ItemCount > 0)
                {
                    items[step.Item] = (items.TryGetValue(step.Item, out int n) ? n : 0) + step.ItemCount;
                }

                Index++;
                Progress = 0;
            }
            while (Current != null && (GoalCounter.IsState(Current.Kind) || !GoalCounter.IsPossible(Current.Kind, m_mall)) && IsDone);

            Rewards.Grant(m_mall.Bakery.Wallet, m_bus, coins, items);
            Refresh();
            return true;
        }

        // 치트: 보상 없이 다음 걸음으로
        public void Skip()
        {
            if (Current == null)
            {
                return;
            }

            Index++;
            Progress = 0;
            Refresh();
        }

        // 설계 43: 저장한 걸음 · 진행. 걸음은 id로 찾는다(표 순서가 바뀌어도 그 걸음, 2026-10-09). id가 없거나(옛 저장 · 사슬 끝) 표에서 빠졌으면 행 번호(표가 줄었으면 끝으로)
        internal void Restore(string id, int index, int progress)
        {
            int found = m_steps.TakeWhile(step => step.Id != id).Count();
            Index = found < m_steps.Count ? found : Math.Max(0, Math.Min(index, m_steps.Count));
            Progress = Math.Max(0, progress);
            Refresh();
        }

        private void Counted(string kind, string param, int count)
        {
            QuestTable step = Current;

            if (step == null || step.Kind != kind || step.Param != null && step.Param != param || IsDone)
            {
                return;
            }

            Progress = Math.Min(step.Count, Progress + count);
            Refresh();
        }

        // 걸음이 다 되거나 바뀌면 길잡이(화살표)를 끈다. 진행이 바뀌었을 때만 알린다
        private void Refresh()
        {
            int count = Count;

            if (IsDone || Index != m_shownIndex)
            {
                m_guide.Stop();
            }

            if (Index == m_shownIndex && count == m_shownCount)
            {
                return;
            }

            m_shownIndex = Index;
            m_shownCount = count;
            m_bus.Publish(new Events.QuestChanged(this));
        }
    }
}
