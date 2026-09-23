namespace ZooTycoon.Core
{
    // 기획서 4장 관광객 · 데이터-테이블-규칙 8.5 (version 2: 뒷모습 시트, 대기·걷기 프레임 속도 분리 · version 5: 옆모습 시트 · version 6: 드는 자리)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class VisitorRecord
    {
        public const string k_CarryHand = "hand";
        public const string k_CarryHead = "head";

        public string Id { get; set; }
        public string Name { get; set; }
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
        public double MoveSpeed { get; set; }
        public double ViewSecondsMin { get; set; }
        public double ViewSecondsMax { get; set; }
        public int Weight { get; set; }
        public int SortOrder { get; set; }
        // 집은 물건(빵 등)을 드는 자리: hand = 앞발에서 위로, head = 머리 위(날개를 벌린 펭귄 등)
        public string CarryAt { get; set; }
    }
}
