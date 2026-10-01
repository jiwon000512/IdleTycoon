using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 31: 광장의 유물 수레(떠돌이 행상의 좌판 자리, 석상처럼 고정). 길을 막는 바닥 사각형과 손님이 들르는 자리가 있고, 값이 없는 종류라 편집 모드에서 집지 않는다.
    // 행동 relic은 행상이 좌판을 연 동안만 팝업을 열고(RelicCartOpened), 뽑기는 광장 난수로 지갑의 유물(Relics)에서 후보를 뽑는다.
    // 설계 32: 행상이 수레를 세운 동안만 그 자리에 있다(그 밖에는 웜뱃이 닿지 않는다)
    public sealed class RelicCartInteractable : Interactable, IPlaced
    {
        public const string k_Id = "relic_cart";

        private readonly IRandom m_random;

        public Vector2 Position { get; }
        public IPlacedKind Kind => Table;
        public Relics Relics => Area.Wombat.Worker.Wallet.Relics;
        public RelicMerchant Merchant { get; }
        public bool IsOpen => Merchant.IsOpen;

        public RelicCartInteractable(InteractableTable table, PlazaArea plaza, Vector2 position, IRandom random, RelicMerchant merchant) : base(table, plaza)
        {
            Position = position;
            m_random = random;
            Merchant = merchant;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Merchant.CartParked ? Vector2.Distance(p, Position) : float.MaxValue;
        }

        public void MoveTo(Vector2 position)
        {
            throw new InvalidOperationException("유물 수레는 옮기지 않는다.");
        }

        public void Open()
        {
            Area.Bus.Publish(new Events.RelicCartOpened(this));
        }

        public bool TryDraw()
        {
            return Relics.TryDraw(m_random);
        }
    }
}
