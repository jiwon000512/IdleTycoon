namespace ZooTycoon.Core
{
    // 월드 개체(동물·관광객) 공통 연출 칼럼. 데이터-테이블-규칙 8.1·8.5
    public interface IUnitRecord
    {
        string Sprite { get; }
        string IdleSheet { get; }
        string MoveSheet { get; }
        string BackIdleSheet { get; }
        string BackMoveSheet { get; }
        double IdleFrameRate { get; }
        double MoveFrameRate { get; }
        double Scale { get; }
        double MoveSpeed { get; }
    }
}
