using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 62: 광장 계단 옆 우체통(PlazaConfigTable mailboxX · Y에 고정). 석상처럼 길을 막고 편집에서 집지 않는다.
    // 편지함(Mailbox)에 안 읽은 편지가 있으면 깃발이 올라가고(HasMail), 행동 read_mail은 MailboxTapped를 내 UI가 편지 카드를 연다
    public sealed class MailboxInteractable : Interactable, IPlaced
    {
        public const string k_Id = "mailbox";

        public Vector2 Position { get; }
        public IPlacedKind Kind => Table;
        // Mall이 편지함을 만든 뒤 잇는다
        public Mailbox Mailbox { get; private set; }
        public bool HasMail => Mailbox != null && Mailbox.UnreadCount > 0;

        public MailboxInteractable(InteractableTable table, PlazaArea plaza, Vector2 position) : base(table, plaza)
        {
            Position = position;
        }

        internal void Bind(Mailbox mailbox)
        {
            Mailbox = mailbox;
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
            throw new InvalidOperationException("우체통은 옮기지 않는다.");
        }

        public void Open()
        {
            Area.Bus.Publish(new Events.MailboxTapped(this));
        }
    }
}
