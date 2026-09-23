using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 09 v0.4 · 데이터-테이블-규칙 8.11: 웜뱃이 다루는 가게 사물 종류. 기준점은 코드(ShopSim), 거리·행동은 여기
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class InteractableRecord
    {
        public string Id { get; set; }
        public double Range { get; set; }
        // actions.json id. 순서 = 버튼 우선순위
        public List<string> Actions { get; set; }
    }
}
