using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 · 데이터-테이블-규칙 8.14: 광장에 놓인 장식(PlazaDecorTable.json 행, Id = 놓인 자리 번호). 설계 12 꾸미기에서 이어 쓴다
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class PlazaDecorTable : Table<int>
    {
        // DecorationTable Id
        public string Decoration { get; set; }
        // 밑변 가운데, 광장 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
        public double X { get; set; }
        public double Y { get; set; }
    }
}
