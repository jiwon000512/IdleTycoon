using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 13: 곳 안의 사물 하나. 표 행(range·actions·upgrade) + 자기 상태 + 기준점 + 시간 흐름.
    // v0.5: 사물에 하는 일은 행동 객체(ActionFactory)가 하고, 사물은 상태와 그 상태를 바꾸는 메서드만 가진다
    public abstract class Interactable
    {
        public InteractableTable Table { get; }
        // 이 사물이 놓인 곳
        public WombatArea Area { get; }
        // v0.6: 업그레이드 단계(사물 종류 공통, 곳이 센다). 업그레이드가 없으면 0
        public int UpgradeLevel => Area.UpgradeLevel(Table.Id);

        // 상태가 바뀌었다. 사물 하나만 보는 그림은 이 C# event를, 여러 사물을 보는 쪽은 버스의 ThingChanged를 듣는다(설계 16)
        public event Action<Interactable> Changed;

        protected Interactable(InteractableTable table, WombatArea area)
        {
            Table = table;
            Area = area;
        }

        // 기준점까지 거리. range 판정과 가장 가까운 대상 고르기에 쓴다
        public abstract float DistanceTo(Vector2 p);

        // 설계 27 QA: 대상 고르기 순서(작을수록 먼저, 같으면 가까운 것). 밟고 서는 사물(밭)은 1이라 곁에 서는 사물(흙 칸·통로)에 양보한다
        public virtual int TargetPriority => 0;

        // 시간이 흐르는 사물만(오븐 굽기·계산대 계산)
        public virtual void Tick(double dt)
        {
        }

        // 설계 54: 길잡이(「가기」)가 웜뱃을 데려갈 기준점(from = 웜뱃 자리). 그 둘레 가장 가까운 걷는 점에 선다. 놓는 사물은 일하는 · 손님 자리를 쓰니 여기는 그 밖의 사물만, null이면 데려가지 않는다
        public virtual Vector2? GuidePoint(Vector2 from)
        {
            return null;
        }

        // v0.6: 그 업그레이드 단계가 이 사물에서 무슨 값인가. 기본은 배수 1 + 효과(굽기·계산 속도), 진열대는 용량
        public virtual double UpgradeValue(int level)
        {
            return 1d + Table.Upgrade.EffectPerLevel * level;
        }

        protected void OnChanged()
        {
            Changed?.Invoke(this);
            Area.Bus.Publish(new Events.ThingChanged(this));
        }
    }
}
