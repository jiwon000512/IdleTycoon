using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 59: 편지 카드. 편지가 오면 소식지 틀(FishingNewsView)에 보낸 이 · 글 · 동봉을 띄우고, 확인하면 읽은 편지가 된다(동봉은 Core가 준다).
    // 우편함 타일(소식 줄)은 안 읽은 수를 레드닷으로, 톡하면 안 읽은 첫 편지 또는 마지막 편지를 다시 보여 준다.
    // 설계 62: 광장 우체통 앞 읽기 버튼(MailboxTapped)도 같은 카드. 편지가 오면 봉투가 타일로 날아오고(광장에 있으면 월드 우체통이 받는다), 안 읽은 동안 10초마다 타일이 톡
    public sealed class LetterPresenter : IDisposable
    {
        private const float k_OpenDelay = 0.4f;
        // 도장 그림 Sprites/Letters/<from>. 점원 편지는 빈 테(stamp_ring) 위에 그 점원 대기 첫 칸
        private const string k_StampDir = "Sprites/Letters/";
        private const string k_StampRing = "stamp_ring";

        private readonly FishingNewsView m_view;
        private readonly TopBarView m_topBar;
        private readonly Mailbox m_mailbox;
        private readonly Mall m_mall;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;
        private Letter m_showing;

        public LetterPresenter(FishingNewsView view, TopBarView topBar, Mall mall, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_topBar = topBar;
            m_mall = mall;
            m_mailbox = mall.Mailbox;
            m_tables = tables;
            m_view.Confirmed += View_Confirmed;
            m_topBar.MailClicked += TopBar_MailClicked;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.LetterArrived>(Bus_LetterArrived),
                bus.Subscribe<Events.LetterRead>(_ => RefreshTile()),
                bus.Subscribe<Events.MailboxTapped>(_ => TopBar_MailClicked()),
            };
            // 설계 62: 시작할 때도 카드를 띄우지 않는다. 안 읽은 편지는 타일 톡 · 우체통 깃발이 알린다
            RefreshTile();
        }

        public void Dispose()
        {
            m_view.Confirmed -= View_Confirmed;
            m_topBar.MailClicked -= TopBar_MailClicked;

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        // 편지가 왔다: 봉투가 타일로 날아온다(광장이면 월드 우체통이 받으니 타일은 조용히). 카드는 바로 열지 않고 우체통 · 타일에서 읽는다(설계 62)
        private void Bus_LetterArrived(Events.LetterArrived e)
        {
            RefreshTile();

            if (!(m_mall.Active is PlazaArea))
            {
                m_topBar.FlyMailIn();
            }
        }

        private void TopBar_MailClicked()
        {
            Letter letter = m_mailbox.Unread.FirstOrDefault() ?? m_mailbox.Letters.LastOrDefault();

            if (letter != null && !m_view.IsOpen)
            {
                Show(letter);
            }
        }

        // 확인: 읽은 편지로(동봉을 받는다). 안 읽은 편지가 더 있으면 이어서
        private void View_Confirmed()
        {
            Letter letter = m_showing;
            m_showing = null;

            if (letter != null)
            {
                m_mailbox.TryRead(letter);
            }

            // 안 읽은 편지가 더 있으면 닫지 않고 이어서(닫히는 동안은 Open이 무시된다)
            Letter next = m_mailbox.Unread.FirstOrDefault();

            if (next != null)
            {
                Show(next);
            }
            else
            {
                m_view.Close();
            }
        }

        private void Show(Letter letter)
        {
            m_showing = letter;
            m_topBar.SetMailNag(false);
            bool read = m_mailbox.IsRead(letter);
            LetterTable row = letter.Row;
            // 점원 편지: 보낸 이는 점원 이름, 글에는 이름 · 사물 이름(kind_<사물>)
            Clerk clerk = letter.Clerk;
            // ponytail: 동봉은 글 한 줄("코인 100 · 밀 3"). 아이콘 칸은 도장 그림과 함께 아트방 그림이 오면
            List<string> gifts = new List<string>();

            if (row.Coins > 0d)
            {
                gifts.Add(m_tables.Format("letter_gift_coins", BigNumberFormatter.Format(row.Coins)));
            }

            if (row.Item != null && row.ItemCount > 0)
            {
                gifts.Add(m_tables.Format("letter_gift_item", m_tables.Get<ItemTable>(row.Item).Name, row.ItemCount));
            }

            m_view.Show(new FishingNewsView.Data
            {
                Masthead = m_tables.Text("letter_masthead"),
                Title = clerk != null ? clerk.Name : m_tables.Text("sender_" + row.From),
                Picture = k_StampDir + (clerk != null ? k_StampRing : row.From),
                Overlay = clerk?.Look.IdleSheet ?? clerk?.Look.Sprite,
                Line = string.Join("\n", row.Lines.Select(id => clerk != null ? m_tables.Format(id, clerk.Name, m_tables.Text("kind_" + clerk.Thing.Table.Id)) : m_tables.Text(id))),
                BoxLabel = string.Empty,
                Rewards = new List<InfoTile.Data>(),
                Extra = gifts.Count > 0 && !read ? m_tables.Text("letter_box") + " " + string.Join(" · ", gifts) : null,
                Button = m_tables.Text("letter_button"),
            });
            m_view.Open(k_OpenDelay);
        }

        private void RefreshTile()
        {
            m_topBar.ShowMail(m_mailbox.UnreadCount);
            m_topBar.SetMailNag(m_mailbox.UnreadCount > 0 && m_showing == null);
        }
    }
}
