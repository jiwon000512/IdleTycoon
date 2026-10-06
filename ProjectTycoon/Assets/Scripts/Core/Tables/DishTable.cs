using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 47 · 데이터-테이블-규칙 8.31: 횟집 회 한 접시(DishTable.json 행). 물고기(ItemTable) 하나에 한 접시, 행 순서 = 수조 · 화면 순서
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class DishTable : Table<string>
    {
        public string Name { get; set; }
        // 쓰는 물고기(ItemTable id). 수조가 창고에서 이 물고기를 꺼내 담는다
        public string Fish { get; set; }
        // 접시 그림(Resources/ 기준, 머리 위 · 탁자 위)
        public string Sprite { get; set; }
        public double Price { get; set; }
        // 도마에서 뜨는 초(업그레이드 배수 1에서)
        public double CutSeconds { get; set; }
    }
}
