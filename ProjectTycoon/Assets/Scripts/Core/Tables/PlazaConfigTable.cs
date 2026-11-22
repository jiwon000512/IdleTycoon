using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 · 64 · 데이터-테이블-규칙 8.4: 굴 밖 광장 숫자(PlazaConfigTable.json 행, Id = 광장). 놓인 장식은 PlazaDecorTable
    // 설계 64 가지 굴: 광장은 굴길(shaftCols 폭, 아래로 rows줄)과 그 옆 주머니 칸(pockets)을 파서 넓힌다. 처음은 굴길 startRows줄
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class PlazaConfigTable : Table<string>
    {
        public const string k_Main = "main";

        // 굴길 폭(칸, 가운데 정렬) · 줄 상한(0 = 입구 줄) · 처음 판 줄 수 · 파기 값(digBaseCost × digCostGrowth^판 칸 수)
        public int ShaftCols { get; set; }
        public int Rows { get; set; }
        public int StartRows { get; set; }
        public double DigBaseCost { get; set; }
        public double DigCostGrowth { get; set; }
        // 설계 65 냇물 마을: 굴길 가운데 냇물 폭(유닛) · 둑을 치는 횟수
        public double StreamWidth { get; set; }
        public int DamHits { get; set; }
        // 장식 편집은 이 가게(IShop id)가 열린 뒤. null이면 처음부터
        public string DecorAfter { get; set; }
        // 굴길 옆 주머니 칸(문 · 석상 · 행상 자리)
        public PlazaPocketData[] Pockets { get; set; }
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
        // 설계 30: 빈 뒤 다시 빌 때까지 쉬는 초
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
    }

    // 설계 64: 굴길 옆 주머니 칸 하나. use = door(뒷벽에 to 곳의 문) · statue(석상 자리) · merchant(행상 좌판 자리). 자리는 칸 밑변 가운데 조금 위(PlazaLayout).
    // after = 이 가게(IShop id)가 열려야 팔 수 있다(null이면 늘)
    public sealed class PlazaPocketData
    {
        public const string k_Door = "door";
        public const string k_Statue = "statue";
        public const string k_Merchant = "merchant";

        public int Col { get; set; }
        public int Row { get; set; }
        public string Use { get; set; }
        public string To { get; set; }
        public string After { get; set; }

        public Cell Cell => new Cell(Col, Row);
    }
}
