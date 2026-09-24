using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 기획서 6.2~6.4 · 데이터-테이블-규칙 8.4 (version 11: 동물·관광객 수 섹션 삭제, version 15: 동물원 레벨 틱 income 삭제, version 17: plaza)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class GameConfig
    {
        public StartConfig Start { get; set; }
        public OfflineConfig Offline { get; set; }
        public ShopConfig Shop { get; set; }
        public PlazaConfig Plaza { get; set; }

        public sealed class StartConfig
        {
            public int Coins { get; set; }
        }

        public sealed class OfflineConfig
        {
            public int MaxSeconds { get; set; }
        }

        // 설계 08 v0.5: 빵집 시뮬 시간(초)·수량의 시작값
        public sealed class ShopConfig
        {
            public int MaxCustomers { get; set; }
            // 손님 동선 설계 v0.2(version 14): 빈 진열대 앞 두리번 시간 합계. 다 쓰면 「!!」로 나간다
            public double PatienceSeconds { get; set; }
            // 한 진열대에서 두리번하는 최대 시간. 지나면 다른 빵을 찾아간다
            public double LookSeconds { get; set; }
            // 구멍에서 톡 뛰어 나오기·들어가기, 빵 집기
            public double HopSeconds { get; set; }
            public double PickSeconds { get; set; }
            public double CheckoutSeconds { get; set; }
            public int ShelfCapacity { get; set; }
            public int OvenCount { get; set; }
            // 굴 격자 설계 v0.5(version 13): 칸 크기(유닛)·입구 줄 높이·걷는 속도(유닛/초, 손님·웜뱃. 걷는 시간 = A* 길 ÷ 이 값)·파기 비용
            public double CellWidth { get; set; }
            public double CellHeight { get; set; }
            public double EntranceHeight { get; set; }
            public double WalkSpeed { get; set; }
            public double DigBaseCost { get; set; }
            public double DigCostGrowth { get; set; }
            // 설계 09(version 15): 조이스틱 웜뱃 속도(유닛/초), 머리에 이는 빵 수. 사물 거리는 interactables.json(version 16)
            public double WombatSpeed { get; set; }
            public int CarryCapacity { get; set; }
        }

        // 설계 11(version 17): 굴 밖 광장. 손님 도착은 빵집이 아니라 광장 계단에서(shop.arrivalSeconds를 옮김)
        public sealed class PlazaConfig
        {
            // 칸 수(한 칸 = shop.cellWidth × cellHeight, 첫 줄은 entranceHeight)
            public int Cols { get; set; }
            public int Rows { get; set; }
            public double ArrivalSeconds { get; set; }
            public int MaxVisitors { get; set; }
            // 들를 곳에서 머무는 초, 들르는 곳 수(빵집 가기 전)
            public double VisitSecondsMin { get; set; }
            public double VisitSecondsMax { get; set; }
            public int VisitsMin { get; set; }
            public int VisitsMax { get; set; }
            // 가게 없이 구경만 하고 떠나는 비율, 들를 곳에서 ♥를 띄우는 비율
            public double BrowseChance { get; set; }
            public double EmoteChance { get; set; }
            // 놓인 장식(decorations.json id, 밑변 가운데 광장 좌표 유닛)
            public List<PlacedDecor> Decor { get; set; }
        }

        public sealed class PlacedDecor
        {
            public string Id { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }
    }
}
