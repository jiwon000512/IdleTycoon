using System.Collections.Generic;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 09 v0.4 · 데이터-테이블-규칙 8.11: 웜뱃이 다루는 사물 종류(InteractableTable.json 행). 기준점·행동 코드는 사물 클래스(XXInteractable.k_Id), 거리·행동 목록은 여기.
    // 설계 18: 놓을 수 있는 사물(진열대·오븐·계산대)은 바닥 사각형·자리·가격도 여기(IPlacedKind). 통로·파기는 price가 없다
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class InteractableTable : Table<string>, IPlacedKind
    {
        private static readonly SpotOffset[] s_noSpots = new SpotOffset[0];

        public double Range { get; set; }
        // ActionTable Id. 순서: manual은 버튼 우선순위, sheet는 시트 줄 순서
        public List<string> Actions { get; set; }
        // 설계 13 v0.6: 업그레이드(upgrade 행동의 데이터). 없으면 null
        public UpgradeInfo Upgrade { get; set; }
        // 설계 18: 발밑 막는 자리(밑변 가운데에서 좌우·위, 유닛)와 손님·웜뱃 자리, 사는 값. 놓을 수 없는 사물은 0·null
        public double HalfWidth { get; set; }
        public double Depth { get; set; }
        public List<SpotOffset> Spots { get; set; }
        public PriceInfo Price { get; set; }

        // 카드 그림은 화면이 종류별로 갖는다(Sprites/World는 Resources가 아니다)
        string IPlacedKind.Icon => null;
        IReadOnlyList<SpotOffset> IPlacedKind.Spots => Spots ?? (IReadOnlyList<SpotOffset>)s_noSpots;
    }
}
