using System.Linq;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 59 ⓒ 검증: 점원의 부탁 — 셋째 월급에 1단계 사물의 점원이 편지(보낸 이 = 점원) · 올려 주면 일머리 + · 넘기면 기분(딴짓 ×) · 점원마다 하나 · 저장
    public sealed class ClerkRequestTests
    {
        private const double k_Dt = 0.05;

        private static ClerkConfigTable Config(TestGame game)
        {
            return game.Tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
        }

        // 월급날을 n번 돌린다(월급은 넉넉히, 1초 틱으로 한 주기씩)
        private static void Paydays(TestGame game, int count)
        {
            game.State.AddCoins(1_000_000d);
            double period = Config(game).WagePeriodSeconds;

            for (int i = 0; i < count; i++)
            {
                double before = game.Mall.Payroll.UntilPayday;

                for (double t = 0d; t < period; t += 1d)
                {
                    game.Mall.Tick(1d);
                }

                Assert.That(game.Mall.Payroll.UntilPayday, Is.EqualTo(before).Within(1e-6), "한 주기에 월급날 한 번");
            }
        }

        [Test]
        public void ThirdPayday_ClerkOnLevelOneThing_SendsUpgradeLetter_OncePerClerk()
        {
            TestGame game = new TestGame();
            Clerk clerk = game.Hire(game.Bakery, game.Bakery.Ovens[0], 50);
            ClerkRequests requests = game.Mall.Requests;
            int before = game.Mall.Mailbox.Letters.Count;

            Paydays(game, Config(game).RequestAfterPaydays - 1);
            Assert.That(requests.Pending, Is.Empty, "아직 부탁 없음");

            Paydays(game, 1);
            ClerkRequests.Request request = requests.Of(clerk);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Kind, Is.EqualTo(ClerkRequests.k_Upgrade));
            Letter letter = game.Mall.Mailbox.Letters.Last();
            Assert.That(game.Mall.Mailbox.Letters.Count, Is.EqualTo(before + 1));
            Assert.That(letter.Clerk, Is.SameAs(clerk), "보낸 이는 점원");
            Assert.That(letter.Id, Is.EqualTo(ClerkRequests.k_Upgrade));
            Assert.That(letter.Key, Is.EqualTo("r_upgrade@bakery:0"));

            Paydays(game, 2);
            Assert.That(requests.Pending.Count, Is.EqualTo(1), "점원마다 한 번에 하나");
        }

        [Test]
        public void UpgradeInTime_SkillGrows_MissingIt_MoodDrops()
        {
            TestGame game = new TestGame();
            Clerk clerk = game.Hire(game.Bakery, game.Bakery.Ovens[0], 50);
            Paydays(game, Config(game).RequestAfterPaydays);
            Assert.That(game.Mall.Requests.Of(clerk), Is.Not.Null);

            game.State.AddCoins(1_000_000d);
            Assert.That(game.Bakery.TryChoose(ActionTable.k_Upgrade, game.Bakery.Ovens[0], null), Is.True);
            game.Mall.Tick(k_Dt);

            Assert.That(game.Mall.Requests.Of(clerk), Is.Null, "들어줬다");
            Assert.That(clerk.Skill, Is.EqualTo(50 + Config(game).RequestSkill));
            Assert.That(clerk.Mood, Is.EqualTo(1d));
            Assert.That(clerk.Bubble.Id, Is.EqualTo(BubbleTable.k_Heart));

            // 계산 점원은 넘긴다
            Clerk counter = game.Hire(game.Bakery, game.Bakery.Counter, 50);
            Paydays(game, Config(game).RequestAfterPaydays);
            Assert.That(game.Mall.Requests.Of(counter), Is.Not.Null);

            for (double t = 0d; t <= Config(game).RequestSeconds; t += 1d)
            {
                game.Mall.Tick(1d);
            }

            Assert.That(counter.Skill, Is.EqualTo(50));
            // 넘긴 뒤 다음 월급날에 다시 부탁한다(새 부탁은 시간이 가득). 앞 편지는 빠지고 새 편지 한 통
            Paydays(game, 1);
            Assert.That(game.Mall.Requests.Of(counter), Is.Not.Null, "다시 부탁");
            Assert.That(game.Mall.Requests.Of(counter).Left, Is.GreaterThan(0d).And.LessThanOrEqualTo(Config(game).RequestSeconds));
            Assert.That(game.Mall.Mailbox.Letters.Count(letter => letter.Clerk == counter), Is.EqualTo(1), "같은 점원의 옛 부탁 편지는 빠진다");
            Assert.That(game.Mall.Mailbox.Unread.Count(letter => letter.Clerk == counter), Is.EqualTo(1));
            Assert.That(counter.Mood, Is.EqualTo(Config(game).RequestSulk), "넘기면 딴짓 ×");
        }

        // 이미 올린 사물의 점원 · 해고된 점원은 부탁하지 않는다
        [Test]
        public void UpgradedThing_NoRequest_FiredClerk_RequestDropped()
        {
            TestGame game = new TestGame();
            game.State.AddCoins(1_000_000d);
            Assert.That(game.Bakery.TryChoose(ActionTable.k_Upgrade, game.Bakery.Ovens[0], null), Is.True);
            Clerk clerk = game.Hire(game.Bakery, game.Bakery.Ovens[0], 50);
            Paydays(game, Config(game).RequestAfterPaydays + 1);
            Assert.That(game.Mall.Requests.Pending, Is.Empty);

            Clerk counter = game.Hire(game.Bakery, game.Bakery.Counter, 50);
            Paydays(game, Config(game).RequestAfterPaydays);
            Assert.That(game.Mall.Requests.Of(counter), Is.Not.Null);
            game.Bakery.Fire(counter, FireReason.Fired);
            game.Mall.Tick(k_Dt);
            Assert.That(game.Mall.Requests.Pending, Is.Empty);
        }

        [Test]
        public void Save_KeepsRequestLetterAndMood()
        {
            TestGame game = new TestGame();
            Clerk clerk = game.Hire(game.Bakery, game.Bakery.Ovens[0], 50);
            Clerk counter = game.Hire(game.Bakery, game.Bakery.Counter, 50);
            Paydays(game, Config(game).RequestAfterPaydays);
            game.Mall.Tick(10d);
            game.Mall.Mailbox.TryRead(game.Mall.Mailbox.Letters.Single(letter => letter.Clerk == counter));
            double left = game.Mall.Requests.Of(clerk).Left;
            SaveData data = GameSave.Capture(game.State, game.Mall);
            Assert.That(data.Requests.Count, Is.EqualTo(2));
            Assert.That(data.Mail.Arrived, Does.Contain("r_upgrade@bakery:0"));

            TestGame loaded = new TestGame();
            GameSave.Apply(data, loaded.State, loaded.Mall, loaded.Tables);
            Clerk loadedClerk = loaded.Bakery.ClerkOf(loaded.Bakery.Ovens[0]);
            Clerk loadedCounter = loaded.Bakery.ClerkOf(loaded.Bakery.Counter);
            Assert.That(loaded.Mall.Requests.Of(loadedClerk).Left, Is.EqualTo(left));
            Assert.That(loaded.Mall.Requests.Of(loadedCounter), Is.Not.Null);
            Assert.That(loaded.Mall.Mailbox.Letters.Count(letter => letter.Clerk != null), Is.EqualTo(2));
            Assert.That(loaded.Mall.Mailbox.Letters.Single(letter => letter.Clerk == loadedCounter).Id, Is.EqualTo(ClerkRequests.k_Upgrade));
            Assert.That(loaded.Mall.Mailbox.Unread.Count(letter => letter.Clerk != null), Is.EqualTo(1), "읽은 것은 읽은 채");

            // 기분 저장: 넘긴 점원
            for (double t = 0d; t <= Config(loaded).RequestSeconds; t += 1d)
            {
                loaded.Mall.Tick(1d);
            }

            Assert.That(loadedClerk.Mood, Is.EqualTo(Config(loaded).RequestSulk));
            data = GameSave.Capture(loaded.State, loaded.Mall);
            TestGame again = new TestGame();
            GameSave.Apply(data, again.State, again.Mall, again.Tables);
            Assert.That(again.Bakery.ClerkOf(again.Bakery.Ovens[0]).Mood, Is.EqualTo(Config(again).RequestSulk));
        }
    }
}
