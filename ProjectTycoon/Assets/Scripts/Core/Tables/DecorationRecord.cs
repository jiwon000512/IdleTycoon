using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 11 · 데이터-테이블-규칙 8.13: 광장 장식 종류. 놓인 자리는 game_config.plaza.decor
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class DecorationRecord
    {
        public string Id { get; set; }
        // Resources/ 기준 경로(확장자 없음), 피벗 아래 가운데
        public string Sprite { get; set; }
        // 발밑 막는 자리(유닛): 밑변 가운데에서 좌우 halfWidth, 위로 depth
        public double HalfWidth { get; set; }
        public double Depth { get; set; }
        // 손님이 들르는 자리(밑변 기준 유닛)와 거기서 보는 방향
        public List<DecorationSpot> Spots { get; set; }
    }

    public sealed class DecorationSpot
    {
        public double Dx { get; set; }
        public double Dy { get; set; }
        // up · down · left · right
        public string Face { get; set; }
    }
}
