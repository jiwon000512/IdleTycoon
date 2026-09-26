using System.Collections.Generic;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 · 설계 18 · 데이터-테이블-규칙 8.13: 광장 장식 종류(DecorationTable.json 행). 시작 배치는 PlazaDecorTable, 그 뒤는 편집 모드에서 사고 옮긴다
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class DecorationTable : Table<string>, IPlacedKind
    {
        // Resources/ 기준 경로(확장자 없음), 피벗 아래 가운데
        public string Sprite { get; set; }
        // 발밑 막는 자리(유닛): 밑변 가운데에서 좌우 halfWidth, 위로 depth
        public double HalfWidth { get; set; }
        public double Depth { get; set; }
        // 움직이는 그림(분수 물결): <sprite>_0 ~ _(frames−1)을 frameRate로 돈다. 0이면 sprite 한 장
        public int Frames { get; set; }
        public double FrameRate { get; set; }
        // 손님이 들르는 자리(밑변 기준 유닛)와 거기서 보는 방향(role은 customer)
        public List<SpotOffset> Spots { get; set; }
        // 설계 18: 사는 값(growth 1 = 고정)
        public PriceInfo Price { get; set; }

        string IPlacedKind.Icon => Sprite;
        IReadOnlyList<SpotOffset> IPlacedKind.Spots => Spots;
    }
}
