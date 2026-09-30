using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 · 데이터-테이블-규칙 8.20: 재료(ItemTable.json 행). 창고(ZooState)가 id로 센다. 사고팔지 않는다(재화는 코인 하나)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ItemTable : Table<string>
    {
        public string Name { get; set; }
        // 설계 26: 창고 화면의 설명 한두 줄
        public string Desc { get; set; }
        // 아이콘(Resources/ 기준, 확장자 없음). 상단 바 알약 · 굽기 칩 · 거두기 팝업
        public string Icon { get; set; }
        // 새 게임 창고에 든 개수
        public int Start { get; set; }
    }
}
