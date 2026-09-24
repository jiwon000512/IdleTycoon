using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 13: 곳 안의 사물 하나. 표 행(range·actions) + 자기 상태 + 기준점 + 행동을 스스로 가진다.
    // 행동은 행동하는 쪽의 손(Hands)을 받는다(지금은 웜뱃, 나중에 직원도)
    public abstract class Interactable
    {
        public InteractableTable Table { get; }

        // 상태가 바뀌었다(화면이 사물마다 구독)
        public event Action<Interactable> Changed;

        protected Interactable(InteractableTable table)
        {
            Table = table;
        }

        // 기준점까지 거리. range 판정과 가장 가까운 대상 고르기에 쓴다
        public abstract float DistanceTo(Vector2 p);

        // 시간이 흐르는 사물만(오븐 굽기)
        public virtual void Tick(double dt)
        {
        }

        // 지금 할 수 있나. 시트를 여는 행동은 언제나
        public virtual bool CanDo(string actionId, Hands hands)
        {
            return true;
        }

        public virtual void Do(string actionId, Hands hands)
        {
        }

        protected void OnChanged()
        {
            Changed?.Invoke(this);
        }
    }
}
