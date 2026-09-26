using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 18: 자유 배치 규칙(벽 밖·겹침·작업 자리·입구)과 사기·옮기기·보관하기. 빵집 시작 배치(진열대 (−1,1)·오븐 (−1,3)·계산대 가운데)와 광장 시작 장식 위에서
    public sealed class PlacementTests
    {
        private TableSet m_tables;
        private EventBus m_bus;
        private ZooState m_state;
        private BakeryArea m_shop;
        private PlazaArea m_plaza;

        [SetUp]
        public void Create()
        {
            m_tables = TestTables.Load();
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            Wombat wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, new SequenceRandom(new double[100]), wombat, m_bus);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 100).ToArray()), wombat, m_bus);
        }

        [Test]
        public void StartLayout_MatchesOldCellPositions()
        {
            Assert.That(m_shop.Shelves[0].Position, Is.EqualTo(m_shop.Layout.ShelfBase(new Cell(-1, 1))));
            Assert.That(m_shop.Ovens[0].Position, Is.EqualTo(m_shop.Layout.OvenBase(new Cell(-1, 3))));
            Assert.That(m_shop.Counter.Position, Is.EqualTo(m_shop.Layout.CounterBase));
            Assert.That(m_shop.Layout.WombatHome, Is.EqualTo(m_shop.Layout.CounterBase + new Vector2(0f, 0.4f)));
            Assert.That(m_shop.ShopKinds.Select(k => k.Id), Is.EqualTo(new[] { "shelf", "oven", "counter" }));
        }

        [Test]
        public void CanPlace_ReportsWhyNot()
        {
            BakeryLayout layout = m_shop.Layout;

            Assert.That(m_shop.CanPlace("shelf", layout.ShelfBase(new Cell(0, 1))), Is.EqualTo(PlacementCheck.Ok));
            // 벽 밖(안 판 칸)
            Assert.That(m_shop.CanPlace("shelf", layout.ShelfBase(new Cell(1, 1))), Is.EqualTo(PlacementCheck.OutsideFloor));
            // 시작 진열대 위
            Assert.That(m_shop.CanPlace("shelf", layout.ShelfBase(new Cell(-1, 1))), Is.EqualTo(PlacementCheck.Overlaps));
            // 입구 구멍 아래 바닥을 덮음
            Assert.That(m_shop.CanPlace("shelf", layout.HoleFloor + new Vector2(0f, -0.2f)), Is.EqualTo(PlacementCheck.Overlaps));
            // 오븐을 입구 줄 벽에 붙이면 웜뱃 자리(위 1.05)가 벽 안
            Assert.That(m_shop.CanPlace("oven", new Vector2(-1.6875f, -2.3f)), Is.EqualTo(PlacementCheck.NoWorkSpot));
        }

        // 설계 20: 카드 탭 자리 — 시작 진열대 위(겹침)를 주면 가장 가까운 고리의 빈 자리를 찾고, 굴 밖 멀리를 주면 못 찾는다
        [Test]
        public void TryFindSpot_ReturnsNearestFreeSpotOrFalse()
        {
            BakeryArea shop = m_shop;
            Vector2 taken = shop.Shelves[0].Position;
            Assert.That(shop.CanPlace(ShelfInteractable.k_Id, taken), Is.EqualTo(PlacementCheck.Overlaps));
            Assert.That(shop.TryFindSpot(ShelfInteractable.k_Id, taken, out Vector2 spot), Is.True);
            Assert.That(shop.CanPlace(ShelfInteractable.k_Id, spot), Is.EqualTo(PlacementCheck.Ok));
            Assert.That(Vector2.Distance(spot, taken), Is.LessThan(1.6f));

            Assert.That(shop.TryFindSpot(ShelfInteractable.k_Id, spot, out Vector2 same), Is.True);
            Assert.That(same, Is.EqualTo(spot), "놓을 수 있는 자리를 주면 그 자리");

            Assert.That(shop.TryFindSpot(ShelfInteractable.k_Id, new Vector2(30f, 30f), out _), Is.False);
        }

        [Test]
        public void Buy_ChargesGrowingPrice_AndStopsAtMax()
        {
            BakeryLayout layout = m_shop.Layout;
            int layoutChanged = 0;
            m_bus.Subscribe<Events.LayoutChanged>(e => layoutChanged += e.Area == m_shop ? 1 : 0);

            Assert.That(m_shop.PriceOf("shelf"), Is.EqualTo(150d));
            Assert.That(m_shop.TryBuy("shelf", layout.ShelfBase(new Cell(0, 1))), Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(200d));
            Assert.That(m_shop.PriceOf("shelf"), Is.EqualTo(300d));
            Assert.That(m_shop.TryBuy("shelf", layout.ShelfBase(new Cell(0, 3))), Is.False, "코인 부족");
            Assert.That(m_shop.TryBuy("shelf", layout.ShelfBase(new Cell(0, 1))), Is.False, "겹침");

            m_state.AddCoins(10000d);
            Assert.That(m_shop.TryBuy("shelf", layout.ShelfBase(new Cell(0, 3))), Is.True);
            m_shop.Grid.Dig(new Cell(1, 1));
            Assert.That(m_shop.TryBuy("shelf", layout.ShelfBase(new Cell(1, 1))), Is.True);
            Assert.That(m_shop.Shelves.Count, Is.EqualTo(4));
            Assert.That(m_shop.IsMaxed("shelf"), Is.True);
            m_shop.Grid.Dig(new Cell(1, 3));
            Assert.That(m_shop.TryBuy("shelf", layout.ShelfBase(new Cell(1, 3))), Is.False, "최대");
            // 진열대 3 + 파기 2
            Assert.That(layoutChanged, Is.EqualTo(5));
        }

        [Test]
        public void Move_KeepsStock_AndStoreForgetsIt()
        {
            ShelfInteractable shelf = m_shop.Shelves[0];
            shelf.Put(m_tables.Get<BreadTable>("b01"), 3);
            Vector2 to = m_shop.Layout.ShelfBase(new Cell(0, 1));

            Assert.That(m_shop.TryMove(shelf, m_shop.Layout.ShelfBase(new Cell(1, 1))), Is.False, "벽 밖");
            Assert.That(m_shop.TryMove(shelf, to), Is.True);
            Assert.That(shelf.Position, Is.EqualTo(to));
            Assert.That(shelf.Stock, Is.EqualTo(3));
            Assert.That(m_shop.Layout.ShelfSpots(shelf).Count, Is.GreaterThan(0));
            Assert.That(m_shop.ThingAt(to + new Vector2(0f, 0.5f)), Is.SameAs(shelf));

            double coins = m_state.Coins;
            Assert.That(m_shop.TryStore(shelf), Is.True);
            Assert.That(m_shop.Shelves, Is.Empty);
            Assert.That(m_shop.StoredCount("shelf"), Is.EqualTo(1));
            Assert.That(m_shop.CountOf("shelf"), Is.EqualTo(1));
            Assert.That(m_shop.PriceOf("shelf"), Is.EqualTo(150d));
            Assert.That(m_shop.TryPlaceStored("shelf", to), Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins));
            Assert.That(m_shop.StoredCount("shelf"), Is.EqualTo(0));
            Assert.That(m_shop.Shelves[0].Stock, Is.EqualTo(0));
        }

        [Test]
        public void Counter_SecondOneGetsItsOwnQueue_LastOneCannotBeStored()
        {
            m_state.AddCoins(10000d);
            Vector2 at = new Vector2(2.3f, -5.6f);

            Assert.That(m_shop.TryStore(m_shop.Counter), Is.False, "마지막 계산대");
            Assert.That(m_shop.CanPlace("counter", at), Is.EqualTo(PlacementCheck.Ok));
            Assert.That(m_shop.TryBuy("counter", at), Is.True);
            Assert.That(m_shop.Counters.Count, Is.EqualTo(2));

            CounterInteractable second = m_shop.Counters[1];
            Assert.That(m_shop.Layout.QueueSlots(second)[0].Y, Is.LessThan(second.Position.Y));
            Assert.That(m_shop.Layout.QueueSlots(second)[0], Is.Not.EqualTo(m_shop.Layout.QueueSlots(m_shop.Counter)[0]));
            Assert.That(m_shop.Layout.Nav.IsWalkable(second.WorkerSpot), Is.False, "손님은 웜뱃 자리로 못 간다");
            Assert.That(m_shop.Layout.WombatNav.IsWalkable(second.WorkerSpot), Is.True);
            Assert.That(m_shop.ShortestQueue(Vector2.Zero), Is.SameAs(m_shop.Counter));
            Assert.That(m_shop.Layout.WombatHome, Is.EqualTo(m_shop.Counter.WorkerSpot));

            Assert.That(m_shop.TryStore(second), Is.True);
            Assert.That(m_shop.Counters.Count, Is.EqualTo(1));
        }

        [Test]
        public void Plaza_BuysMovesAndStoresDecor()
        {
            m_state.AddCoins(10000d);
            int before = m_plaza.Decor.Count;
            int spotsBefore = m_plaza.Layout.Spots.Count;
            Vector2 at = new Vector2(0f, -4.6f);

            Assert.That(m_plaza.ShopKinds.Count, Is.EqualTo(m_tables.GetAll<DecorationTable>().Count));
            Assert.That(m_plaza.CanPlace("bench_log", new Vector2(0f, -8.2f)), Is.EqualTo(PlacementCheck.Overlaps), "분수 위");
            Assert.That(m_plaza.CanPlace("bench_log", new Vector2(9f, -4.6f)), Is.EqualTo(PlacementCheck.OutsideFloor));
            Assert.That(m_plaza.TryBuy("bench_log", at), Is.True);
            Assert.That(m_plaza.Decor.Count, Is.EqualTo(before + 1));
            Assert.That(m_plaza.Layout.Spots.Count, Is.EqualTo(spotsBefore + 2));

            DecorationData bench = m_plaza.Decor[before];
            Assert.That(m_plaza.TryMove(bench, at + new Vector2(1f, 0f)), Is.True);
            Assert.That(m_plaza.TryStore(bench), Is.True);
            Assert.That(m_plaza.Decor.Count, Is.EqualTo(before));
            Assert.That(m_plaza.StoredCount("bench_log"), Is.EqualTo(1));
            Assert.That(m_plaza.Layout.Spots.Count, Is.EqualTo(spotsBefore));
        }
    }
}
