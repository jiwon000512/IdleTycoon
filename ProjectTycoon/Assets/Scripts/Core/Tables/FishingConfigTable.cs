using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 52 우물 낚시 · 데이터-테이블-규칙 8.28: 낚시터(굴 속 강가) 숫자(FishingConfigTable.json 행, id main). 좌표는 낚시터 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FishingConfigTable : Table<string>
    {
        public const string k_Main = "main";

        // 방 칸 수(광장처럼 파지 않는 고정 방)
        public int Cols { get; set; }
        public int Rows { get; set; }
        // 강(물 사각형, 유닛): 방 아래쪽을 가로지른다. 물은 걸을 수 없고 웜뱃은 둑에서 물 쪽을 보고 던진다
        public FishingWaterData Water { get; set; }
        // 입질: 던진 뒤 물기까지 초(사이에서 난수) · 그 전 가짜 입질 최대 횟수 · 물고 나서 낚아챌 수 있는 초
        public double BiteWaitMin { get; set; }
        public double BiteWaitMax { get; set; }
        public int NibbleMax { get; set; }
        public double BiteWindow { get; set; }
        // 1회차: 어종 하나(FishTable id) · 무게(kg) 고정. 어종 뽑기는 2회차
        public string Fish { get; set; }
        public double Weight { get; set; }
        // 줄다리기: 거리 = 무게 × distancePerKg(0이 되면 낚음) · 달리기 초 · 쉼 초 · 달릴 때 놓으면 물고기가 되찾는 거리/초 · 달릴 때 감으면 오르는 긴장/초 · 놓으면 내리는 긴장/초
        public double DistancePerKg { get; set; }
        public double RunSeconds { get; set; }
        public double RestSeconds { get; set; }
        public double RunPull { get; set; }
        public double TensionRise { get; set; }
        public double TensionFall { get; set; }
        // 낚싯대(1회차는 하나): 쉴 때 감는 거리/초 · 줄 힘(긴장이 이 값에 닿으면 끊김)
        public FishingRodData Rod { get; set; }
        // 낚은 뒤 웜뱃이 털썩 앉아 있는 초
        public double LandSeconds { get; set; }
        // 둑 소품(아트방 bank_<이름>, 발끝 피벗): 그림 이름과 발끝 자리. 길을 막지 않는 장식
        public FishingDecorData[] Decor { get; set; } = new FishingDecorData[0];
    }

    public sealed class FishingDecorData
    {
        public string Sprite { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public sealed class FishingWaterData
    {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
    }

    public sealed class FishingRodData
    {
        public double Reel { get; set; }
        public double Line { get; set; }
    }
}
