using System;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2 5장: 행동 트리 틀. 마디가 진행 상태를 가지므로 트리 하나를 한 손님만 쓴다
    public enum BtStatus
    {
        Running,
        Success,
        Failure,
    }

    public abstract class BtNode<T>
    {
        public abstract BtStatus Tick(T context, double dt);

        public virtual void Reset()
        {
        }
    }

    // 순서(→): 자식을 차례로. 하나라도 실패하면 실패, 모두 성공하면 성공
    public sealed class BtSequence<T> : BtNode<T>
    {
        private readonly BtNode<T>[] m_children;
        private int m_index;

        public BtSequence(params BtNode<T>[] children)
        {
            m_children = children;
        }

        public override BtStatus Tick(T context, double dt)
        {
            while (m_index < m_children.Length)
            {
                BtStatus status = m_children[m_index].Tick(context, dt);

                if (status == BtStatus.Running)
                {
                    return BtStatus.Running;
                }

                if (status == BtStatus.Failure)
                {
                    Reset();
                    return BtStatus.Failure;
                }

                m_index++;
            }

            Reset();
            return BtStatus.Success;
        }

        public override void Reset()
        {
            m_index = 0;

            foreach (BtNode<T> child in m_children)
            {
                child.Reset();
            }
        }
    }

    // 선택(?): 자식을 차례로 해 본다. 하나라도 성공하면 성공, 모두 실패하면 실패
    public sealed class BtSelector<T> : BtNode<T>
    {
        private readonly BtNode<T>[] m_children;
        private int m_index;

        public BtSelector(params BtNode<T>[] children)
        {
            m_children = children;
        }

        public override BtStatus Tick(T context, double dt)
        {
            while (m_index < m_children.Length)
            {
                BtStatus status = m_children[m_index].Tick(context, dt);

                if (status == BtStatus.Running)
                {
                    return BtStatus.Running;
                }

                if (status == BtStatus.Success)
                {
                    Reset();
                    return BtStatus.Success;
                }

                m_index++;
            }

            Reset();
            return BtStatus.Failure;
        }

        public override void Reset()
        {
            m_index = 0;

            foreach (BtNode<T> child in m_children)
            {
                child.Reset();
            }
        }
    }

    // 반복(↻): 조건이 참인 동안 자식이 성공할 때까지 다시 한다. 자식이 실패했을 때 조건이 거짓이면 실패
    public sealed class BtRepeat<T> : BtNode<T>
    {
        private readonly BtNode<T> m_child;
        private readonly Func<T, bool> m_while;

        public BtRepeat(Func<T, bool> whileCondition, BtNode<T> child)
        {
            m_while = whileCondition;
            m_child = child;
        }

        public override BtStatus Tick(T context, double dt)
        {
            BtStatus status = m_child.Tick(context, dt);

            if (status != BtStatus.Failure)
            {
                return status;
            }

            m_child.Reset();
            return m_while(context) ? BtStatus.Running : BtStatus.Failure;
        }

        public override void Reset()
        {
            m_child.Reset();
        }
    }

    // 잎 행동. start는 처음 틱에 한 번(false면 바로 실패), tick은 진행 상태를 돌려준다
    public sealed class BtAction<T> : BtNode<T>
    {
        private readonly Func<T, bool> m_start;
        private readonly Func<T, double, BtStatus> m_tick;
        private bool m_started;

        public BtAction(Func<T, bool> start, Func<T, double, BtStatus> tick)
        {
            m_start = start;
            m_tick = tick;
        }

        public override BtStatus Tick(T context, double dt)
        {
            if (!m_started)
            {
                m_started = true;

                if (m_start != null && !m_start(context))
                {
                    m_started = false;
                    return BtStatus.Failure;
                }
            }

            BtStatus status = m_tick(context, dt);

            if (status != BtStatus.Running)
            {
                m_started = false;
            }

            return status;
        }

        public override void Reset()
        {
            m_started = false;
        }
    }
}
