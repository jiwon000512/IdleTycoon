using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 29: 광장의 웜뱃 석상(분수 자리에 고정). 길을 막는 바닥 사각형과 손님이 들르는 자리가 있고, 값이 없는 종류라 편집 모드에서 집지 않는다.
    // 행동 statue는 팝업을 열고(StatueOpened), 빌기는 광장 난수로 축복(지갑의 Blessing, 설계 30)을 뽑는다
    public sealed class StatueInteractable : Interactable, IPlaced
    {
        public const string k_Id = "statue";

        private readonly IRandom m_random;

        public Vector2 Position { get; }
        public IPlacedKind Kind => Table;
        public Blessing Blessing => Area.Wombat.Worker.Wallet.Blessing;

        public StatueInteractable(InteractableTable table, PlazaArea plaza, Vector2 position, IRandom random) : base(table, plaza)
        {
            Position = position;
            m_random = random;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return Position;
        }

        public void MoveTo(Vector2 position)
        {
            throw new InvalidOperationException("석상은 옮기지 않는다.");
        }

        public void Open()
        {
            Area.Bus.Publish(new Events.StatueOpened(this));
        }

        public bool TryPray()
        {
            return Blessing.TryPray(m_random);
        }
    }
}
