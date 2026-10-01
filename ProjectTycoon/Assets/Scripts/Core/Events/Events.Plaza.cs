namespace ZooTycoon.Core
{
    // 설계 16: 광장 사건
    public static partial class Events
    {
        public readonly struct PlazaVisitorArrived
        {
            public readonly PlazaVisitor Visitor;

            public PlazaVisitorArrived(PlazaVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        // 빵집 문이나 계단으로 들어갔다
        public readonly struct PlazaVisitorLeft
        {
            public readonly PlazaVisitor Visitor;

            public PlazaVisitorLeft(PlazaVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        // 설계 29: 석상 앞 버튼을 눌렀다(팝업)
        public readonly struct StatueOpened
        {
            public readonly StatueInteractable Thing;

            public StatueOpened(StatueInteractable thing)
            {
                Thing = thing;
            }
        }

        // 설계 31: 유물 수레 앞 버튼을 눌렀다(팝업)
        public readonly struct RelicCartOpened
        {
            public readonly RelicCartInteractable Thing;

            public RelicCartOpened(RelicCartInteractable thing)
            {
                Thing = thing;
            }
        }

        // 설계 31: 떠돌이 행상의 단계가 바뀌었다(Merchant.Phase)
        public readonly struct MerchantChanged
        {
            public readonly RelicMerchant Merchant;

            public MerchantChanged(RelicMerchant merchant)
            {
                Merchant = merchant;
            }
        }

        // 설계 31: 유물이 바뀌었다. Change = 뽑음 · 고름 · 끼움/뺌, Relic = 고르거나 끼우거나 뺀 유물(뽑음이면 null)
        public readonly struct RelicsChanged
        {
            public readonly Relics Relics;
            public readonly RelicChange Change;
            public readonly RelicTable Relic;

            public RelicsChanged(Relics relics, RelicChange change, RelicTable relic)
            {
                Relics = relics;
                Change = change;
                Relic = relic;
            }
        }

        // 설계 30: 석상 축복이 바뀌었다. Prayed면 빌어서 새 축복이 걸렸다, 아니면 축복이 풀렸거나 쉬는 시간이 끝났다
        public readonly struct BlessingChanged
        {
            public readonly Blessing Blessing;
            public readonly bool Prayed;

            public BlessingChanged(Blessing blessing, bool prayed)
            {
                Blessing = blessing;
                Prayed = prayed;
            }
        }
    }
}
