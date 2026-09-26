using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 22: 머리 위 이모지 말풍선 상태(손님·점원·웜뱃 공용). 상태형(seconds 0)은 지우거나 바꿀 때까지, 순간형은 seconds 뒤 저절로 사라진다.
    // 화면은 Row(칸 번호·프레임)와 Elapsed(프레임 넘김)를 읽는다
    public sealed class BubbleState
    {
        private readonly TableSet m_tables;
        private double m_remaining;

        public BubbleTable Row { get; private set; }
        public string Id => Row?.Id;
        public double Elapsed { get; private set; }

        public BubbleState(TableSet tables)
        {
            m_tables = tables;
        }

        // 같은 상태형이면 그대로(프레임이 처음으로 튀지 않게)
        public void Show(string id)
        {
            if (Row != null && Row.Id == id && Row.Seconds == 0d)
            {
                return;
            }

            Row = m_tables.Get<BubbleTable>(id);
            m_remaining = Row.Seconds;
            Elapsed = 0d;
        }

        public void Clear()
        {
            Row = null;
        }

        public void Tick(double dt)
        {
            if (Row == null)
            {
                return;
            }

            Elapsed += dt;

            if (Row.Seconds > 0d)
            {
                m_remaining -= dt;

                if (m_remaining <= 0d)
                {
                    Row = null;
                }
            }
        }
    }
}
