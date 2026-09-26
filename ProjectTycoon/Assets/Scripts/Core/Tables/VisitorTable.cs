using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 집은 물건(빵 등)을 드는 자리: 앞발에서 위로, 머리 위(날개를 벌린 펭귄 등)
    public enum CarryAt
    {
        Hand,
        Head,
    }

    // 설계 21: 행의 쓰임. customer = 광장 도착 손님 외형(균등 난수), clerk = 점원 외형(후보마다 clerk 행 중 균등 난수)
    public enum VisitorRole
    {
        Customer,
        Clerk,
    }

    // 기획서 4장 · 데이터-테이블-규칙 8.5: 손님·점원 외형(VisitorTable.json 행). 손님이 올 때마다 customer 행 중 하나를 고른다
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class VisitorTable : Table<string>
    {
        public string Name { get; set; }
        public VisitorRole Role { get; set; }
        public string Sprite { get; set; }
        public string IdleSheet { get; set; }
        public string MoveSheet { get; set; }
        public string BackIdleSheet { get; set; }
        public string BackMoveSheet { get; set; }
        // 손님 동선 설계 v0.2: 오른쪽 보는 옆모습(왼쪽은 뒤집기)
        public string SideIdleSheet { get; set; }
        public string SideMoveSheet { get; set; }
        public double IdleFrameRate { get; set; }
        public double MoveFrameRate { get; set; }
        public double Scale { get; set; }
        public CarryAt CarryAt { get; set; }
    }
}
