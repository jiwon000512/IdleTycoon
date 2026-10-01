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

        // 웜뱃이 통로(나가기·문)를 지났다. Mall이 가는 곳(To, 곳 id)으로 옮긴다
        public readonly struct Passed
        {
            public readonly WombatArea From;
            public readonly string To;

            public Passed(WombatArea from, string to)
            {
                From = from;
                To = to;
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

        // 설계 22: 대화의 줄 하나. speaker 객체(Clerk) 머리 위에 seconds 동안 글자 말풍선
        public readonly struct DialogueLine
        {
            public readonly Dialogue Dialogue;
            public readonly DialogueSpeaker Speaker;
            public readonly object SpeakerObject;
            public readonly string TextId;
            public readonly double Seconds;

            public DialogueLine(Dialogue dialogue, DialogueSpeaker speaker, object speakerObject, string textId, double seconds)
            {
                Dialogue = dialogue;
                Speaker = speaker;
                SpeakerObject = speakerObject;
                TextId = textId;
                Seconds = seconds;
            }
        }

        public readonly struct DialogueEnded
        {
            public readonly Dialogue Dialogue;

            public DialogueEnded(Dialogue dialogue)
            {
                Dialogue = dialogue;
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

        // 설계 25: 창고 재료가 바뀌었다(거두기 · 굽기)
        public readonly struct ItemsChanged
        {
            public readonly ZooState Wallet;

            public ItemsChanged(ZooState wallet)
            {
                Wallet = wallet;
            }
        }

        // ---------- 설계 24 · 37 똥(곳 공용) ----------

        // 웜뱃이 똥을 떨궜다(그림 · 흙먼지 · 소리)
        public readonly struct PoopDropped
        {
            public readonly PoopInteractable Poop;

            public PoopDropped(PoopInteractable poop)
            {
                Poop = poop;
            }
        }

        // 똥이 치워졌다(치우기 버튼 · 사물 밑에 깔림). 그림을 지운다. Coins: 설계 31 거름 국자로 받은 코인(없으면 0)
        public readonly struct PoopCleaned
        {
            public readonly PoopInteractable Poop;
            public readonly double Coins;

            public PoopCleaned(PoopInteractable poop, double coins)
            {
                Poop = poop;
                Coins = coins;
            }
        }

        // 설계 37: 치우기 버튼으로 똥 Count개를 치워 거름(Item)이 창고에 들었다(「+N」 팝업 · 창고 버튼). 곳마다 한 번
        public readonly struct PoopsCleaned
        {
            public readonly WombatArea Area;
            public readonly string Item;
            public readonly int Count;

            public PoopsCleaned(WombatArea area, string item, int count)
            {
                Area = area;
                Item = item;
                Count = count;
            }
        }
    }
}
