namespace ZooTycoon.Core
{
    // 설계 16: 도메인 사건은 여기 한 클래스(partial: Area·Bakery·Plaza)에 선언한다. 첫 필드는 발신자.
    // 발행은 일이 일어난 객체가 곳의 EventBus로, 구독은 발신자의 곳으로 거른다. 곳 공통 사건
    public static partial class Events
    {
        // 대상이 바뀌었거나 대상의 버튼 행동이 바뀌었다(다 구웠다·빵을 들었다 등)
        public readonly struct TargetChanged
        {
            public readonly WombatArea Area;

            public TargetChanged(WombatArea area)
            {
                Area = area;
            }
        }

        // 업그레이드를 샀다(사물 종류 id). 화면이 그 종류 사물을 튀기고 값을 다시 그린다
        public readonly struct Upgraded
        {
            public readonly WombatArea Area;
            public readonly string InteractableId;

            public Upgraded(WombatArea area, string interactableId)
            {
                Area = area;
                InteractableId = interactableId;
            }
        }

        // 사물 하나의 상태가 바뀌었다(여러 사물을 보는 시트·소리용. 사물 하나만 보는 그림은 그 사물의 Changed)
        public readonly struct ThingChanged
        {
            public readonly Interactable Thing;

            public ThingChanged(Interactable thing)
            {
                Thing = thing;
            }
        }

        // 웜뱃이 통로(나가기·문)를 지났다. Mall이 다른 곳으로 옮긴다
        public readonly struct Passed
        {
            public readonly WombatArea From;

            public Passed(WombatArea from)
            {
                From = from;
            }
        }

        // 웜뱃이 있는 곳이 바뀌었다
        public readonly struct AreaChanged
        {
            public readonly WombatArea Active;

            public AreaChanged(WombatArea active)
            {
                Active = active;
            }
        }

        public readonly struct CoinsChanged
        {
            public readonly ZooState Wallet;

            public CoinsChanged(ZooState wallet)
            {
                Wallet = wallet;
            }
        }
    }
}
