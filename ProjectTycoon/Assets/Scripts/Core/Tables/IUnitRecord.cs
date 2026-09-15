namespace ZooTycoon.Core
{
    // 월드 개체(동물·관광객) 공통 연출 칼럼. 데이터-테이블-규칙 8.1·8.5
    public interface IUnitRecord
    {
        string Sprite { get; }
        string IdleSheet { get; }
        string MoveSheet { get; }
        double FrameRate { get; }
        double Scale { get; }
        double MoveSpeed { get; }
    }
}
