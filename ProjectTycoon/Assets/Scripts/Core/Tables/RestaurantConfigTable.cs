using System.Collections.Generic;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 47 · 데이터-테이블-규칙 8.32: 횟집 시뮬 숫자(RestaurantConfigTable.json 행 하나). 전부 시작값
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class RestaurantConfigTable : Table<string>
    {
        public const string k_Main = "restaurant";

        public int MaxCustomers { get; set; }
        // 수조 앞에서 물고기 · 빈 자리를 기다리는 초. 다 쓰면 화나서 나간다
        public double PatienceSeconds { get; set; }
        // 수조에서 물고기를 고르는 초
        public double PickSeconds { get; set; }
        // 접시가 온 뒤 먹는 초. 다 먹으면 탁자에서 값을 낸다
        public double EatSeconds { get; set; }
        public int TankCapacity { get; set; }
        public double DigBaseCost { get; set; }
        public double DigCostGrowth { get; set; }
        // 빵집 별이 openStar 이상이면 광장 문 앞에서 openCost로 연다
        public int OpenStar { get; set; }
        public double OpenCost { get; set; }
        // 새 게임 시작 배치(사물 종류 · 밑변 가운데, 가게 원점 기준 유닛)
        public List<StartThingData> Start { get; set; }
    }

    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class StartThingData
    {
        public string Kind { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
