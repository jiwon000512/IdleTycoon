using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 61 지하 호수의 섬들 · 데이터-테이블-규칙 8.34: 낚시터 섬(FishingIslandTable.json 행). 좌표는 낚시터 원점(구멍 위 가운데) 기준 유닛, y 위.
    // index 0이 처음 뭍(구멍 아래), 1부터 차례로 다리(bridge)를 놓아 닿는다. 닿은 섬의 다음 섬은 보이기만 하고(칸이 파임) 다리를 놓아야 걷는다.
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FishingIslandTable : Table<string>
    {
        public int Index { get; set; }
        // 뭍(걷는 네모). 0번은 입구 줄에 잘린다
        public FishingRectData Land { get; set; }
        // 앞 섬에서 이 섬으로 건너는 다리(0번은 null): 네모 · 값 · 다리 위에서 던지면 무는 물고기(없으면 섬 것)
        public FishingBridgeData Bridge { get; set; }
        // 이 섬에서 던지면 무는 물고기(FishTable id)와 비중
        public FishingFishData[] Fish { get; set; } = new FishingFishData[0];
        // 좌대 자리(뭍 가운데에서 dx, 뭍 아랫변 위 seatBack)와 놓는 값
        public FishingSeatData[] Seats { get; set; } = new FishingSeatData[0];
        // 이 섬 좌대 손님이 한 마리 낚을 때 내는 코인
        public double Pay { get; set; }
        // 볼 것: null · shadow(명물 그림자가 섬 아래 물을 오간다) · crate(상자가 떠내려온다)
        public string Event { get; set; }
        // event가 shadow일 때 그림자에 던지면 무는 명물(FishTable id)
        public string Boss { get; set; }
        // 둑 소품(아트방 bank_<이름> · water_<이름>, 발끝 피벗)의 그림 이름과 발끝 자리. 길을 막지 않는 장식
        public FishingDecorData[] Decor { get; set; } = new FishingDecorData[0];
        // 뭍 아랫변에 까는 둑 띠 그림 이름(아트방 bankstrip_<이름>). null이면 공용 bank
        public string Bank { get; set; }

        public const string k_EventShadow = "shadow";
        public const string k_EventCrate = "crate";
    }

    public class FishingRectData
    {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }

        public NavRect ToRect()
        {
            return new NavRect((float)X0, (float)Y0, (float)X1, (float)Y1);
        }
    }

    public sealed class FishingBridgeData : FishingRectData
    {
        public double Cost { get; set; }
        public FishingFishData[] Fish { get; set; } = new FishingFishData[0];
    }

    public sealed class FishingFishData
    {
        public string Id { get; set; }
        public double Chance { get; set; }
    }

    public sealed class FishingSeatData
    {
        public double Dx { get; set; }
        public double Cost { get; set; }
    }

    public sealed class FishingDecorData
    {
        public string Sprite { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
