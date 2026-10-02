using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 39: 계단 자리(층 맨 아래 줄 밑변 가운데 = 가운데 통로 끝). 층을 다 팠고 아래층이 닫혀 있을 때만 농장 사물 목록에 든다.
    // 삽 버튼(open_dig) → 시트 줄(dig_floor)에서 아래층 openCost를 치르면 계단 굴이 파이고 아래로 가는 통로가 대신 선다
    public sealed class StairInteractable : Interactable
    {
        public const string k_Id = "stair";

        public FarmArea Farm { get; }

        public StairInteractable(InteractableTable table, FarmArea farm) : base(table, farm)
        {
            Farm = farm;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Farm.Layout.StairSpot);
        }
    }
}
