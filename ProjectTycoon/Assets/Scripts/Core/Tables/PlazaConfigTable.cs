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
        // 설계 29: 웜뱃 석상 자리(분수 자리, 밑변 가운데). 설계 30: 빈 뒤 다시 빌 때까지 쉬는 초
        public double StatueX { get; set; }
        public double StatueY { get; set; }
        public double BlessingCooldown { get; set; }
        // 설계 31: 뽑을 때 내는 재료와 개수 · 칸 수
        public string RelicItem { get; set; }
        public int RelicCost { get; set; }
        public int RelicSlots { get; set; }
        // 설계 31 떠돌이 행상: 처음 오기까지 · 오는 간격(오는 때부터 다음 오는 때까지) · 머무는 초 · 외형(VisitorTable 역할 merchant)
        public double MerchantFirst { get; set; }
        public double MerchantEvery { get; set; }
        public double MerchantStay { get; set; }
        public string MerchantLook { get; set; }
        // 설계 34: 행상이 서는 좌판 자리(발 위치, 수레 없이 행상만)
        public double MerchantX { get; set; }
        public double MerchantY { get; set; }
    }
}
