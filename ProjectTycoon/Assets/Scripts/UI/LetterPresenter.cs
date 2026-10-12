using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 59: 편지 카드. 편지가 오면 소식지 틀(FishingNewsView)에 보낸 이 · 글 · 동봉을 띄우고, 확인하면 읽은 편지가 된다(동봉은 Core가 준다).
    // 우편함 타일(소식 줄)은 안 읽은 수를 레드닷으로, 톡하면 안 읽은 첫 편지 또는 마지막 편지를 다시 보여 준다
    public sealed class LetterPresenter : IDisposable
    {
        private const float k_OpenDelay = 0.4f;
        // 도장 그림(Sprites/Letters/<from>)이 없으면 쓰는 임시 그림
        private const string k_StampDir = "Sprites/Letters/";
        private const string k_StampFallback = "Sprites/Quests/draw";

        private readonly FishingNewsView m_view;
        private readonly TopBarView m_topBar;
        private readonly Mailbox m_mailbox;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;
        private Letter m_showing;

        public LetterPresenter(FishingNewsView view, TopBarView topBar, Mailbox mailbox, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_topBar = topBar;
            m_mailbox = mailbox;
            m_tables = tables;
            m_view.Confirmed += View_Confirmed;
            m_topBar.MailClicked += TopBar_MailClicked;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.LetterArrived>(Bus_LetterArrived),
                bus.Subscribe<Events.LetterRead>(_ => RefreshTile()),
            };
            RefreshTile();
            Letter unread = m_mailbox.Unread.FirstOrDefault();

            if (unread != null)
            {
                Show(unread);
            }
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

        private void Bus_LetterArrived(Events.LetterArrived e)
        {
            RefreshTile();

            if (!m_view.IsOpen)
            {
                Show(e.Letter);
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

            string stamp = UnityEngine.Resources.Load<UnityEngine.Sprite>(k_StampDir + row.From) != null ? k_StampDir + row.From : k_StampFallback;
            m_view.Show(new FishingNewsView.Data
            {
                Masthead = m_tables.Text("letter_masthead"),
                Title = clerk != null ? clerk.Name : m_tables.Text("sender_" + row.From),
                Picture = stamp,
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
        }
    }
}
