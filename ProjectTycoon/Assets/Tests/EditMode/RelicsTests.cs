using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 31 검증: 반짝돌로 뽑으면 서로 다른 후보 셋, 모자라거나 후보가 남았으면 못 뽑음, ★3은 후보에서 빠짐, 고르면 별 · 빈 칸에 끼움,
    // 칸이 차면 모음에만, 끼운 것만 값, 축복과 곱, 규칙 넷(주판 · 국자 · 바구니 · 시계는 ClerkTests), 광장 수레(표 검사는 TableValidatorTests). 실제 JSON 값.
    // 설계 32: 행상 단계(펼침 · 접음), 수레는 세운 동안만 길을 막고 그 자리에 선 웜뱃은 비켜 선다
    public sealed class RelicsTests
    {
        private const double k_Dt = 0.02;

        private TableSet m_tables;
        private EventBus m_bus;
        private ZooState m_state;
        private Wombat m_wombat;
        private BakeryArea m_shop;
        private PlazaArea m_plaza;
        private FarmArea m_farm;
        private Mall m_mall;

        private Relics Relics => m_state.Relics;
        private PlazaConfigTable Config => m_tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
        private int Gems => m_state.Count(Relics.Item);

        [SetUp]
        public void Create()
        {
            Build(null);
        }

        // plaza: 광장 숫자를 곳을 만들기 전에 고친다(행상 시간표 등)
        private void Build(System.Action<PlazaConfigTable> plaza)
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            plaza?.Invoke(m_tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main));
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            m_wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, new SequenceRandom(new double[2000]), m_wombat, m_bus);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 4000).ToArray()), m_wombat, m_bus);
            m_farm = new FarmArea(m_tables, new SequenceRandom(Enumerable.Repeat(0.99, 200).ToArray()), m_wombat, m_bus);
            m_mall = new Mall(m_shop, m_plaza, m_farm, m_bus);
        }

        // 그 유물을 첫 후보로 뽑아 고른다(비중이 모두 같다고 보고 후보 순서의 가운데 난수)
        internal static void Grant(ZooState state, string id)
        {
            Relics relics = state.Relics;
            List<RelicTable> pool = relics.All.Where(r => relics.Stars(r) < r.Values.Length).ToList();
            double roll = (pool.FindIndex(r => r.Id == id) + 0.5d) / pool.Count;
            Assert.That(relics.TryDraw(new SequenceRandom(roll, 0.5d, 0.5d)), Is.True);
            Assert.That(relics.Offer[0].Id, Is.EqualTo(id));
            relics.Choose(0);
        }

        private RelicTable Relic(string id)
        {
            return m_tables.Get<RelicTable>(id);
        }

        [Test]
        public void Draw_SpendsGems_AndOffersThreeDifferent()
        {
            int gems = Gems;
            int drawn = 0;
            m_bus.Subscribe<Events.RelicsChanged>(e => drawn += e.Change == RelicChange.Drawn ? 1 : 0);

            Assert.That(Relics.TryDraw(new SequenceRandom(0.1d, 0.1d, 0.1d)), Is.True);

            Assert.That(Gems, Is.EqualTo(gems - Config.RelicCost));
            Assert.That(Relics.Offer.Count, Is.EqualTo(Relics.k_Choices));
            Assert.That(Relics.Offer.Distinct().Count(), Is.EqualTo(Relics.k_Choices));
            Assert.That(drawn, Is.EqualTo(1));
        }

        [Test]
        public void Draw_FailsWithoutGems_OrWhileOfferIsOpen()
        {
            Assert.That(Relics.TryDraw(new SequenceRandom(0.1d, 0.1d, 0.1d)), Is.True);
            int gems = Gems;

            Assert.That(Relics.TryDraw(new SequenceRandom(0.1d, 0.1d, 0.1d)), Is.False);
            Assert.That(Gems, Is.EqualTo(gems));

            Relics.Choose(0);
            m_state.TrySpendItem(Relics.Item, Gems - (Config.RelicCost - 1));
            Assert.That(Relics.TryDraw(new SequenceRandom(0.1d, 0.1d, 0.1d)), Is.False);
            Assert.That(Relics.HasOffer, Is.False);
        }

        // 새 유물은 ★1로 빈 칸에 들고, 또 고르면 ★2(칸은 그대로 하나)
        [Test]
        public void Choose_GivesStar_AndEquipsIntoFreeSlot_ThenRaisesStar()
        {
            RelicTable bellows = Relic("bellows");

            Grant(m_state, "bellows");
            Assert.That(Relics.Stars(bellows), Is.EqualTo(1));
            Assert.That(Relics.Slots[0], Is.EqualTo(bellows));
            Assert.That(Relics.HasOffer, Is.False);

            Grant(m_state, "bellows");
            Assert.That(Relics.Stars(bellows), Is.EqualTo(2));
            Assert.That(Relics.Slots.Count(r => r == bellows), Is.EqualTo(1));
            Assert.That(Relics.Value(BlessingTable.k_Bake), Is.EqualTo(bellows.Values[1]));
        }

        // ★3이 된 유물은 후보에 안 뜨고, 남은 후보가 둘이면 둘만, 다 모으면 뽑기가 없다
        [Test]
        public void MaxedRelics_LeaveTheOffer_UntilComplete()
        {
            foreach (RelicTable relic in Relics.All.Skip(2))
            {
                for (int star = 0; star < relic.Values.Length; star++)
                {
                    Grant(m_state, relic.Id);
                }
            }

            Assert.That(Relics.TryDraw(new SequenceRandom(0.1d, 0.1d)), Is.True);
            Assert.That(Relics.Offer, Is.EquivalentTo(Relics.All.Take(2)));
            Relics.Choose(0);

            foreach (RelicTable relic in Relics.All.Take(2))
            {
                while (Relics.Stars(relic) < relic.Values.Length)
                {
                    Grant(m_state, relic.Id);
                }
            }

            Assert.That(Relics.Complete, Is.True);
            Assert.That(Relics.TryDraw(new SequenceRandom(0.1d)), Is.False);
        }

        // 칸이 차면 새로 고른 유물은 모음에만 들고, 하나를 빼면 끼울 수 있다. 끼운 것만 값이 난다
        [Test]
        public void FullSlots_KeepNewRelicInCollection_UntilOneIsRemoved()
        {
            Grant(m_state, "bellows");
            Grant(m_state, "bell");
            Grant(m_state, "chime");
            RelicTable can = Relic("watering_can");

            Grant(m_state, can.Id);
            Assert.That(Relics.IsEquipped(can), Is.False);
            Assert.That(Relics.Value(BlessingTable.k_Grow), Is.EqualTo(0d));
            Assert.That(Relics.TryEquip(can), Is.False);

            Relics.Unequip(Relic("bell"));
            Assert.That(Relics.Value(BlessingTable.k_Checkout), Is.EqualTo(0d));
            Assert.That(Relics.TryEquip(can), Is.True);
            Assert.That(Relics.Value(BlessingTable.k_Grow), Is.EqualTo(can.Values[0]));
            Assert.That(Relics.TryEquip(Relic("abacus")), Is.False);
        }

        // 축복과 유물이 같은 효과면 곱한다
        [Test]
        public void Scale_MultipliesBlessingAndRelic()
        {
            Grant(m_state, "bellows");
            BlessingTable bake = m_tables.Get<BlessingTable>(BlessingTable.k_Bake);
            List<BlessingTable> blessings = m_tables.GetAll<BlessingTable>().ToList();
            double roll = (blessings.IndexOf(bake) + 0.5d) / blessings.Count;
            Assert.That(m_state.Blessing.TryPray(new SequenceRandom(roll)), Is.True);

            Assert.That(m_state.Scale(BlessingTable.k_Bake), Is.EqualTo((1d + bake.Value) * (1d + Relic("bellows").Values[0])).Within(1e-9));
            Assert.That(m_state.Scale(BlessingTable.k_Grow), Is.EqualTo(1d));
        }

        // 낡은 주판: 끼운 동안 N번째 계산마다 true, 빼면 세지 않는다
        [Test]
        public void Abacus_DoublesEveryNthSale()
        {
            Assert.That(Relics.CountSale(), Is.False);
            Grant(m_state, "abacus");
            int every = (int)Relic("abacus").Values[0];

            for (int round = 0; round < 2; round++)
            {
                for (int i = 1; i < every; i++)
                {
                    Assert.That(Relics.CountSale(), Is.False);
                }

                Assert.That(Relics.CountSale(), Is.True);
            }
        }

        // 거름 국자: 치운 똥 하나마다 코인(거름도 그대로)
        [Test]
        public void Scoop_CleaningPoopGivesCoins()
        {
            Grant(m_state, "scoop");
            Vector2 at = new Vector2(2f, -3f);
            m_wombat.Mover.Place(at);
            m_shop.TryDropPoop(at + new Vector2(0.3f, 0f));
            m_shop.TryDropPoop(at + new Vector2(-0.4f, 0f));
            m_mall.Tick(k_Dt);
            double coins = m_state.Coins;
            List<double> popped = new List<double>();
            m_bus.Subscribe<Events.PoopCleaned>(e => popped.Add(e.Coins));

            Assert.That(m_shop.TargetAction.Id, Is.EqualTo(ActionTable.k_Clean));
            Assert.That(m_shop.TryInteract(), Is.True);

            Assert.That(m_state.Coins, Is.EqualTo(coins + 2 * Relic("scoop").Values[0]));
            Assert.That(popped, Is.EqualTo(new[] { Relic("scoop").Values[0], Relic("scoop").Values[0] }));
        }

        // 이삭 바구니: 거둘 때 그 확률로 작물 +1(덤 난수 다음 난수)
        [Test]
        public void Basket_HarvestSometimesGivesOneMore()
        {
            PlotInteractable plot = m_farm.Plots.First(p => p.IsTilled);
            string wheat = m_farm.Crop.Item;
            plot.Plant(m_farm.Crop, false);
            int before = m_state.Count(wheat);
            plot.Harvest(m_state, new SequenceRandom(0.99d));
            Assert.That(m_state.Count(wheat), Is.EqualTo(before + m_farm.Crop.Yield));

            Grant(m_state, "basket");
            double chance = Relic("basket").Values[0];
            plot.Plant(m_farm.Crop, false);
            before = m_state.Count(wheat);
            plot.Harvest(m_state, new SequenceRandom(0.99d, chance * 0.5d));
            Assert.That(m_state.Count(wheat), Is.EqualTo(before + m_farm.Crop.Yield + 1));

            plot.Plant(m_farm.Crop, false);
            before = m_state.Count(wheat);
            plot.Harvest(m_state, new SequenceRandom(0.99d, (chance + 1d) * 0.5d));
            Assert.That(m_state.Count(wheat), Is.EqualTo(before + m_farm.Crop.Yield));
        }

        // 풍경: 손님 간격이 ÷ (1 + 값)
        [Test]
        public void Chime_CustomersArriveSooner()
        {
            Grant(m_state, "chime");
            int arrived = 0;
            m_bus.Subscribe<Events.PlazaVisitorArrived>(_ => arrived++);
            double boosted = Config.ArrivalSeconds / (1d + Relic("chime").Values[0]);

            for (double t = 0d; t < k_Dt + (boosted + Config.ArrivalSeconds) * 0.5d; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }

            Assert.That(arrived, Is.EqualTo(2));
        }

        private bool RunUntil(System.Func<bool> done, double maxSeconds)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return true;
                }

                m_mall.Tick(k_Dt);
            }

            return done();
        }

        // 좌판 자리는 석상 오른쪽, 편집에서 집히지 않고 장식도 못 놓는다. 행상이 없으면 빈 바닥이라 지나갈 수 있고 웜뱃이 닿지 않는다
        [Test]
        public void RelicCart_WhileAway_IsOpenFloorButStaysReserved()
        {
            RelicCartInteractable cart = m_plaza.RelicCart;
            Assert.That(cart.Position, Is.EqualTo(new Vector2((float)Config.RelicCartX, (float)Config.RelicCartY)));
            Assert.That(m_plaza.ThingAt(cart.Position + new Vector2(0f, 0.2f)), Is.Null);
            Assert.That(m_plaza.CanPlace(m_tables.GetAll<DecorationTable>()[0].Id, cart.Position), Is.Not.EqualTo(PlacementCheck.Ok));

            m_bus.Publish(new Events.Passed(m_shop, PlazaArea.k_Id));
            m_wombat.Mover.Place(cart.Position + new Vector2(0f, -0.7f));
            m_mall.Tick(k_Dt);

            Assert.That(m_plaza.Merchant.Phase, Is.EqualTo(MerchantPhase.Away));
            Assert.That(m_plaza.Layout.Nav.IsWalkable(CartFloor), Is.True);
            Assert.That(m_plaza.Target, Is.Not.EqualTo(cart));
        }

        // 수레 바닥 사각형 안의 한 점(기준점 바로 위)
        private Vector2 CartFloor => m_plaza.RelicCart.Position + new Vector2(0f, 0.1f);

        // 설계 32: 수레를 세울 때 그 자리에 선 웜뱃은 가까운 바닥으로 비켜 선다. 웜뱃은 수레 바닥 가운데 안쪽(행상이 서는 손잡이 · 좌판 자리에서 손님 비켜 가기 둘레 밖)에 둔다
        [Test]
        public void Merchant_ParkingCart_MovesWombatOff()
        {
            Build(c =>
            {
                c.MaxVisitors = 0;
                c.MerchantFirst = 1d;
            });
            Vector2 cartEnd = m_plaza.RelicCart.Position + new Vector2(0f, 0.3f);
            m_bus.Publish(new Events.Passed(m_shop, PlazaArea.k_Id));
            m_wombat.Mover.Place(cartEnd);

            Assert.That(RunUntil(() => m_plaza.Merchant.Phase == MerchantPhase.Unpacking, 40d), Is.True);
            Assert.That(m_plaza.Layout.Nav.IsWalkable(cartEnd), Is.False);
            Assert.That(m_wombat.Mover.Position, Is.Not.EqualTo(cartEnd));
            Assert.That(m_plaza.Layout.Nav.IsWalkable(m_wombat.Mover.Position), Is.True);
        }

        // 행상: 처음 시간이 되면 계단에서 내려와 좌판 자리에 서고, 수레를 세워 펼친 뒤 머무는 동안 버튼으로 팝업, 시간이 다 되면 접고
        // 손잡이 자리(수레 오른쪽)로 가 잡은 뒤 계단으로 나간다(설계 33). 수레는 세운 동안만 길을 막는다. 뽑은 후보는 떠나도 남고, 다음은 오는 때부터 merchantEvery 뒤에 온다
        [Test]
        public void Merchant_ComesOpensAndLeaves_OnSchedule()
        {
            Build(c =>
            {
                c.MaxVisitors = 0;
                c.MerchantFirst = 1d;
                c.MerchantStay = 5d;
                c.MerchantEvery = 60d;
            });
            RelicCartInteractable cart = m_plaza.RelicCart;
            List<MerchantPhase> phases = new List<MerchantPhase>();
            m_bus.Subscribe<Events.MerchantChanged>(e => phases.Add(e.Merchant.Phase));
            int opened = 0;
            m_bus.Subscribe<Events.RelicCartOpened>(e => opened += e.Thing == cart ? 1 : 0);
            m_bus.Publish(new Events.Passed(m_shop, PlazaArea.k_Id));
            m_wombat.Mover.Place(cart.Position + new Vector2(0f, -0.7f));

            Assert.That(RunUntil(() => m_plaza.Merchant.Phase == MerchantPhase.Unpacking, 40d), Is.True);
            Assert.That(m_plaza.Merchant.OpenLeft, Is.EqualTo(Config.MerchantStay));
            Assert.That(m_plaza.Layout.Nav.IsWalkable(CartFloor), Is.False);
            Assert.That(m_plaza.TargetAction, Is.Null);

            Assert.That(RunUntil(() => m_plaza.Merchant.IsOpen, 40d), Is.True);
            Assert.That(phases, Is.EqualTo(new[] { MerchantPhase.Coming, MerchantPhase.Unpacking, MerchantPhase.Open }));
            Assert.That(Vector2.Distance(m_plaza.Merchant.Figure.Position, Placement.SpotOf(cart, SpotRole.Worker)), Is.LessThan(0.3f));
            Assert.That(m_plaza.TargetAction.Id, Is.EqualTo(ActionTable.k_Relic));
            Assert.That(m_plaza.TryInteract(), Is.True);
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(cart.TryDraw(), Is.True);

            Assert.That(RunUntil(() => m_plaza.Merchant.Phase == MerchantPhase.Hitching, 40d), Is.True);
            Assert.That(m_plaza.Layout.Nav.IsWalkable(CartFloor), Is.False);
            Assert.That(RunUntil(() => m_plaza.Merchant.Phase == MerchantPhase.Leaving, 40d), Is.True);
            Vector2 stand = Placement.SpotOf(cart, SpotRole.Worker);
            Vector2 handle = new Vector2(2f * cart.Position.X - stand.X, cart.Position.Y);
            Assert.That(Vector2.Distance(m_plaza.Merchant.Figure.Position, handle), Is.LessThan(0.3f));
            Assert.That(m_plaza.Layout.Nav.IsWalkable(CartFloor), Is.True);

            Assert.That(RunUntil(() => m_plaza.Merchant.Phase == MerchantPhase.Away, 40d), Is.True);
            Assert.That(phases, Is.EqualTo(new[]
            {
                MerchantPhase.Coming, MerchantPhase.Unpacking, MerchantPhase.Open, MerchantPhase.Packing, MerchantPhase.Hitching, MerchantPhase.Leaving, MerchantPhase.Away,
            }));
            Assert.That(m_plaza.Visitors, Is.Empty);
            Assert.That(m_plaza.TargetAction, Is.Null);
            Assert.That(Relics.HasOffer, Is.True);
            Assert.That(m_plaza.Merchant.UntilNext, Is.GreaterThan(0d).And.LessThan(60d));

            Assert.That(RunUntil(() => m_plaza.Merchant.Phase == MerchantPhase.Coming, 60d), Is.True);
        }
    }
}
