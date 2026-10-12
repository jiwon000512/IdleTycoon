using System.Linq;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 59 검증: 우편함 편지(걸음이 시작되면 한 통 · 읽으면 동봉 · 읽기가 곧 첫 걸음) · 새 게임은 광장에서 빵집 문이 닫힌 채(밀 5개로 연다) · 저장
    public sealed class LetterTests
    {
        private const double k_Dt = 0.05;

        [Test]
        public void NewGame_FirstLetterArrives_ReadingItFinishesTheFirstStep()
        {
            TestGame game = new TestGame();
            Mailbox mailbox = game.Mall.Mailbox;
            LetterTable first = game.Tables.Get<LetterTable>("l01");
            Assert.That(mailbox.Letters, Is.EqualTo(new[] { first }), "첫 걸음(편지 읽기)이 시작되며 큰이모 편지가 온다");
            Assert.That(mailbox.UnreadCount, Is.EqualTo(1));
            Assert.That(game.Mall.Quests.IsDone, Is.False);
            double coins = game.State.Coins;

            Assert.That(mailbox.TryRead(first), Is.True);

            Assert.That(game.State.Coins, Is.EqualTo(coins + first.Coins), "동봉 코인");
            Assert.That(mailbox.UnreadCount, Is.EqualTo(0));
            Assert.That(game.Mall.Quests.IsDone, Is.True, "읽으면 첫 걸음이 끝난다");
            Assert.That(mailbox.TryRead(first), Is.False, "두 번은 없다");
            Assert.That(game.Mall.Quests.TryClaim(), Is.True);
            Assert.That(game.Mall.Quests.Current.Id, Is.EqualTo("q04"));
        }

        // 빵집 열기 걸음이 시작되면 마을 회관 편지, 굽기 걸음이 시작되면 밀 셋이 든 편지
        [Test]
        public void Letters_ArriveWhenTheirStepStarts_OnceEach()
        {
            TestGame game = new TestGame();
            Mailbox mailbox = game.Mall.Mailbox;
            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.QuestId = "q28";
            GameSave.Apply(data, game.State, game.Mall, game.Tables);
            Assert.That(mailbox.Letters.Select(letter => letter.Id), Does.Contain("l02"));
            int count = mailbox.Letters.Count;
            game.Mall.Tick(k_Dt);
            Assert.That(mailbox.Letters.Count, Is.EqualTo(count), "같은 편지는 한 번만");

            data = GameSave.Capture(game.State, game.Mall);
            data.QuestId = "q01";
            GameSave.Apply(data, game.State, game.Mall, game.Tables);
            LetterTable bake = game.Tables.Get<LetterTable>("l03");
            Assert.That(mailbox.Unread, Does.Contain(bake));
            int wheat = game.State.Count("wheat");
            Assert.That(mailbox.TryRead(bake), Is.True);
            Assert.That(game.State.Count("wheat"), Is.EqualTo(wheat + bake.ItemCount), "동봉 밀");
        }

        // 새 게임: 광장에서 시작, 빵집 문은 닫혀 있고 밀 5개로 연다(광장 문 앞 시트). 열리면 손님을 받는다
        [Test]
        public void NewGame_StartsInPlaza_BakeryGateOpensWithWheat()
        {
            TestGame game = new TestGame();
            Mall mall = game.Mall;
            Assert.That(mall.Active, Is.SameAs(game.Bakery), "테스트 기본은 빵집");
            mall.StartNew();

            Assert.That(mall.Active, Is.SameAs(mall.Plaza), "새 게임은 광장");
            Assert.That(game.Bakery.GateOpen, Is.False);
            Assert.That(game.Bakery.IsOpen, Is.False, "닫힌 빵집은 손님을 받지 않는다");
            Assert.That(game.Bakery.CanAdmit, Is.False);
            Assert.That(game.Bakery.WombatPresent, Is.False, "웜뱃은 광장에만");
            ShopGateInteractable gate = mall.Plaza.Gates.Single(g => g.Shop == (IGated)game.Bakery);
            Assert.That(mall.Plaza.Things.OfType<PassageInteractable>().Single(p => p.To == BakeryArea.k_Id).IsOpen, Is.False);
            Assert.That(GoalCounter.IsAreaOpen(BakeryArea.k_Id, mall), Is.False);

            game.State.TrySpendItem("wheat", game.State.Count("wheat"));
            Assert.That(mall.Plaza.SheetActions(gate).Single().Options(mall.Wombat.Worker, gate)[0].State, Is.EqualTo(SheetOptionState.Poor), "밀이 없으면 못 연다");
            Assert.That(mall.Plaza.TryChoose(ActionTable.k_OpenShop, gate, null), Is.False);
            game.State.AddItem("wheat", game.Bakery.GateCount);
            Assert.That(mall.Plaza.TryChoose(ActionTable.k_OpenShop, gate, null), Is.True, "밀 5개로 연다");
            mall.Tick(k_Dt);

            Assert.That(game.State.Count("wheat"), Is.EqualTo(0));
            Assert.That(game.Bakery.GateOpen, Is.True);
            Assert.That(game.Bakery.IsOpen, Is.True);
            Assert.That(mall.Plaza.Gates.Any(g => g.Shop == (IGated)game.Bakery), Is.False, "열리면 문 앞 사물이 빠진다");
        }

        // 저장: 있던 곳 · 빵집 문 · 온 편지 · 읽은 편지. 옛 저장(우편함 없음)은 빵집에서 · 문 열림 · 편지는 지금 걸음 것만
        [Test]
        public void Save_KeepsAreaGateAndLetters_OldSaveOpensBakery()
        {
            TestGame game = new TestGame();
            game.Mall.StartNew();
            game.Mall.Mailbox.TryRead(game.Mall.Mailbox.Unread.First());
            SaveData data = GameSave.Capture(game.State, game.Mall);
            Assert.That(data.Area, Is.EqualTo(PlazaArea.k_Id));

            // 실제 흐름: 새 게임(광장 · 닫힌 빵집) 위에 저장을 덮는다
            TestGame loaded = new TestGame();
            loaded.Mall.StartNew();
            GameSave.Apply(data, loaded.State, loaded.Mall, loaded.Tables);
            Assert.That(loaded.Mall.Active, Is.SameAs(loaded.Mall.Plaza));
            Assert.That(loaded.Bakery.WombatPresent, Is.False, "빵집에는 웜뱃이 없다");
            Assert.That(loaded.Mall.Plaza.WombatPresent, Is.True);
            Assert.That(loaded.Bakery.GateOpen, Is.False);
            Assert.That(loaded.Mall.Mailbox.Letters.Select(l => l.Id), Is.EqualTo(new[] { "l01" }));
            Assert.That(loaded.Mall.Mailbox.UnreadCount, Is.EqualTo(0), "읽은 채");

            data.Mail = null;
            data.Area = null;
            TestGame old = new TestGame();
            old.Mall.StartNew();
            GameSave.Apply(data, old.State, old.Mall, old.Tables);
            Assert.That(old.Mall.Active, Is.SameAs(old.Bakery));
            Assert.That(old.Mall.Plaza.WombatPresent, Is.False);
            Assert.That(old.Bakery.GateOpen, Is.True, "옛 저장은 빵집 문 열림");
            Assert.That(old.Mall.Mailbox.UnreadCount, Is.EqualTo(1), "옛 저장도 지금 걸음의 편지는 온다");
        }

        // 광장 손님 소문: 그 걸음의 소문 행이 있으면 들어온 손님이 확률로 한 줄(말하는 이는 손님)
        [Test]
        public void Rumor_PlazaVisitorSpeaksTheStepsRumor()
        {
            TestGame game = new TestGame();
            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.QuestId = "q06";
            GameSave.Apply(data, game.State, game.Mall, game.Tables);
            int rumors = 0;
            game.Bus.Subscribe<Events.DialogueLine>(e =>
            {
                if (e.SpeakerObject is PlazaVisitor && e.Speaker == DialogueSpeaker.Visitor)
                {
                    rumors++;
                }
            });
            game.Bus.Publish(new Events.Passed(game.Bakery, PlazaArea.k_Id));

            for (double t = 0d; t < 120d && rumors == 0; t += k_Dt)
            {
                game.Mall.Tick(k_Dt);
            }

            Assert.That(rumors, Is.GreaterThan(0), "손님이 빵집 소문을 말한다");
        }
    }
}
