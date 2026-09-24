using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11: 걸어 다니는 동물 한 마리를 그리는 데 필요한 것(빵집 손님·광장 손님이 같은 그림을 쓴다)
    public interface IWalker
    {
        VisitorTable Look { get; }
        Vector2 Position { get; }
        // 비켜 걷기로 화면에만 더하는 옆 걸음(광장 손님은 0)
        Vector2 Sidestep { get; }
        Facing Facing { get; }
        bool Moving { get; }
        // 톡 뛰어 나오기(Entering)·들어가기(Exiting)·두리번(Looking)을 그림이 구분한다
        CustomerPhase Phase { get; }
        double HopProgress { get; }
    }
}
