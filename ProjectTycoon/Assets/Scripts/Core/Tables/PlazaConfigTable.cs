using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 · 데이터-테이블-규칙 8.4: 굴 밖 광장 숫자(PlazaConfigTable.json 행, Id = 광장). 놓인 장식은 PlazaDecorTable
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class PlazaConfigTable : Table<string>
    {
        public const string k_Main = "main";

        // 칸 수(한 칸은 ConfigTable의 cellWidth × cellHeight, 첫 줄은 entranceHeight)
        public int Cols { get; set; }
        public int Rows { get; set; }
        public double ArrivalSeconds { get; set; }
        public int MaxVisitors { get; set; }
        // 들를 곳에서 머무는 초, 빵집 가기 전 들르는 곳 수
        public double VisitSecondsMin { get; set; }
        public double VisitSecondsMax { get; set; }
        public int VisitsMin { get; set; }
        public int VisitsMax { get; set; }
        // 가게 없이 구경만 하고 떠나는 비율, 들를 곳에서 ♥를 띄우는 비율
        public double BrowseChance { get; set; }
        public double EmoteChance { get; set; }
    }
}
