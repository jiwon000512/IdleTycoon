using System.Collections.Generic;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 09 v0.4 · 데이터-테이블-규칙 8.11: 웜뱃이 다루는 사물 종류(InteractableTable.json 행). 기준점·행동 코드는 사물 클래스(XXInteractable.k_Id), 거리·행동 목록은 여기
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class InteractableTable : Table<string>
    {
        public double Range { get; set; }
        // ActionTable Id. 순서: manual은 버튼 우선순위, sheet는 시트 줄 순서
        public List<string> Actions { get; set; }
        // 설계 13 v0.6: 업그레이드(upgrade 행동의 데이터). 없으면 null
        public UpgradeInfo Upgrade { get; set; }
    }
}
