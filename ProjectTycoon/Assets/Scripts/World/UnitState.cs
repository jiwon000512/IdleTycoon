namespace ZooTycoon.World
{
    // Unit의 상태 하나. Enter에서 연출을 시작하고 Update에서 전이 조건을 본다
    public abstract class UnitState
    {
        public abstract void Enter();

        public abstract void Update(float deltaTime);
    }
}
