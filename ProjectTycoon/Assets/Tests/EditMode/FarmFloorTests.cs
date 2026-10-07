using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 39 검증: 다 판 층의 계단 자리 → 계단 파기로 아래층 열기 · 계단 굴과 아래로 가는 통로 · 오르내리기 · 층마다 값 · 작물 공유 · 닫힌 층 점원 자리 · 마지막 층. 실제 JSON 값
    public sealed class FarmFloorTests
    {
        private const double k_Dt = 0.02;

        // 난수 1: 일머리 100(딴짓 없음), 덤 · 똥 없음
        private sealed class ConstantRandom : IRandom
        {
            public double NextDouble()
            {
                return 1d;
            }
        }

        private TableSet m_tables;
        private EventBus m_bus;
        private ZooState m_state;
        private Mall m_mall;

        private FarmArea First => m_mall.Farms[0];
        private FarmArea Second => m_mall.Farms[1];

        [SetUp]
        public void Create()
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            IRandom random = new ConstantRandom();
            Wombat wombat = new Wombat(m_tables, m_state);
            BakeryArea shop = new BakeryArea(m_state, m_tables, random, wombat, m_bus);
            PlazaArea plaza = new PlazaArea(m_tables, shop, random, wombat, m_bus);
            m_mall = new Mall(shop, plaza, new FarmArea(m_tables, random, wombat, m_bus), m_bus);
        }

        private bool RunUntil(Func<bool> done, double maxSeconds = 60d)
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

        // 들어온 뒤에는 조이스틱을 놓아야 걷는다
        private void Release()
        {
            m_mall.Wombat.SetInput(Vector2.Zero);
            RunUntil(() => false, 0.1d);
        }

        private void GoTo(FarmArea farm)
        {
            m_bus.Publish(new Events.Passed(m_mall.Active, farm.Id));
            Release();
        }

        private void PushUntil(Vector2 direction, WombatArea to)
        {
            Assert.That(RunUntil(() =>
            {
                m_mall.Wombat.SetInput(direction);
                return m_mall.Active == to;
            }, 5d), Is.True);
            Release();
        }

        private static void DigAll(FarmArea farm)
        {
            while (!farm.IsFull)
            {
                farm.Grid.Dig(farm.Grid.Frontier().First());
            }
        }

        private void SetCoins(double coins)
        {
            m_state.TrySpendCoins(m_state.Coins);
            m_state.AddCoins(coins);
        }

        private void OpenSecond()
        {
            DigAll(First);
            m_state.AddCoins(Second.Floor.OpenCost);
            Assert.That(First.TryChoose(ActionTable.k_DigFloor, First.Things.OfType<StairInteractable>().Single(), null), Is.True);
        }

        // 층은 표 행 순서로 이어지고 1층만 열려 있다. 다 파기 전에는 계단이 없고, 다 파면 맨 아래 가운데 계단 자리가 대상(삽 버튼).
        // 시트 줄 값 = 아래층 openCost, 모자라면 못 열고, 치르면 아래층이 열려 계단 자리 대신 아래로 가는 통로 · 계단 굴(걷는 땅)이 생긴다
        [Test]
        public void Stair_AppearsWhenFloorIsFull_AndDigFloorOpensLower()
        {
            List<Events.FloorOpened> opened = new List<Events.FloorOpened>();
            m_bus.Subscribe<Events.FloorOpened>(e => opened.Add(e));

            Assert.That(m_mall.Farms.Select(farm => farm.Id), Is.EqualTo(m_tables.GetAll<FarmFloorTable>().Select(floor => floor.Id)));
            Assert.That(m_mall.Farms.Select(farm => farm.IsOpen), Is.EqualTo(new[] { true, false, false }));
            Assert.That(First.Things.OfType<StairInteractable>(), Is.Empty);

            DigAll(First);
            StairInteractable stair = First.Things.OfType<StairInteractable>().Single();
            GoTo(First);
            First.Wombat.Mover.Place(First.Layout.StairSpot + new Vector2(0f, 0.35f));
            RunUntil(() => false, k_Dt);
            Assert.That(First.Target, Is.SameAs(stair));
            Assert.That(First.TargetAction.Id, Is.EqualTo(ActionTable.k_OpenDig));

            double cost = Second.Floor.OpenCost;
            Assert.That(First.SheetActions(stair).Single().Options(First.Wombat.Worker, stair)[0].Cost, Is.EqualTo(cost));
            SetCoins(cost - 1d);
            Assert.That(First.TryChoose(ActionTable.k_DigFloor, stair, null), Is.False);
            Assert.That(Second.IsOpen, Is.False);

            SetCoins(cost);
            Assert.That(First.TryChoose(ActionTable.k_DigFloor, stair, null), Is.True);
            Assert.That(Second.IsOpen, Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(0d));
            Assert.That(opened.Single().Floor, Is.SameAs(Second));
            Assert.That(First.Things.OfType<StairInteractable>(), Is.Empty);
            Assert.That(First.Things.OfType<PassageInteractable>().Select(passage => passage.To), Is.EquivalentTo(new[] { PlazaArea.k_Id, Second.Id }));
            Assert.That(First.Layout.Nav.IsWalkable(First.Layout.StairFloor), Is.True);
            Assert.That(First.Layout.Nav.IsWalkable(First.Layout.StairFloor - new Vector2(0f, 0.3f)), Is.True, "계단 굴 안쪽 띠까지 걷는다");
        }

        // 계단 굴 아래로 걸어 들어가면 2층 위 구멍 앞에 서고, 2층 위 구멍으로 올라가면 1층 계단 굴 안에 선다. 2층 위 구멍은 광장이 아니라 1층으로 간다
        [Test]
        public void Stairs_GoDownToLowerFloor_AndBackUp()
        {
            OpenSecond();
            GoTo(First);
            First.Wombat.Mover.Place(First.Layout.StairFloor);

            PushUntil(new Vector2(0f, -1f), Second);
            Assert.That(Second.Wombat.Mover.Position, Is.EqualTo(Second.Layout.HoleFloor));
            Assert.That(Second.Things.OfType<PassageInteractable>().Single().To, Is.EqualTo(First.Id));

            PushUntil(new Vector2(0f, 1f), First);
            Assert.That(First.Wombat.Mover.Position, Is.EqualTo(First.Layout.StairFloor));
        }

        // 아래층은 파기 값이 그 층 digBaseCost부터 다시 시작하고 갈기 값도 그 층 값. 1층에서 연 작물은 2층 밭 시트에도 있다
        [Test]
        public void LowerFloor_HasItsOwnCosts_AndSharesUnlockedCrops()
        {
            OpenSecond();
            Assert.That(Second.Grid.DigCost, Is.EqualTo(Second.Floor.DigBaseCost));

            CropTable next = First.NextCrop;
            m_state.AddCoins(next.UnlockCost);
            Assert.That(First.TryChoose(ActionTable.k_Plant, First.Plots.First(plot => plot.IsTilled && plot.IsEmpty), next.Id), Is.True);
            Assert.That(Second.UnlockedCrops, Does.Contain(next));

            Second.Grid.Dig(Second.Grid.Frontier().First());
            PlotInteractable soil = Second.Plots.Single(plot => !plot.IsTilled);
            GoTo(Second);
            Second.Wombat.Mover.Place(Second.Layout.Cells.CellCenter(soil.Cell));
            SetCoins(Second.Floor.TillCost - 1d);
            RunUntil(() => false, k_Dt);
            Assert.That(Second.TargetAction, Is.Null, "2층 갈기 값이 모자란다");

            SetCoins(Second.Floor.TillCost);
            RunUntil(() => false, k_Dt);
            Assert.That(Second.TryInteract(), Is.True);
            Assert.That(soil.IsTilled, Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(0d));
        }

        // 닫힌 층은 점원 자리가 없고(점원 팝업 탭도 없다), 열면 작업대 자리가 생기고 그 층 점원이 그 층 밭을 심는다
        [Test]
        public void ClosedFloor_HasNoClerkSlot_UntilOpened()
        {
            Assert.That(Second.ClerkSlots, Is.Empty);
            Assert.That(m_mall.Areas.Where(area => area.ClerkSlots.Any()), Is.EqualTo(new WombatArea[] { m_mall.Bakery, First }));

            OpenSecond();
            Assert.That(Second.ClerkSlots, Is.EqualTo(new Interactable[] { Second.Barn }));

            m_state.AddCoins(10000d);
            Candidate candidate = Second.Candidates[0];
            Assert.That(Second.TryHire(candidate, Second.Barn, Second.WageFor(candidate, Second.Barn)), Is.True);
            PlotInteractable[] tilled = Second.Plots.Where(plot => plot.IsTilled).ToArray();
            Assert.That(RunUntil(() => tilled.All(plot => !plot.IsEmpty)), Is.True, "2층 점원이 2층 밭에 심는다");
        }

        // 마지막 층은 다 파도 계단 자리가 없다
        [Test]
        public void LastFloor_HasNoStair()
        {
            FarmArea last = m_mall.Farms.Last();
            DigAll(last);

            Assert.That(last.Lower, Is.Null);
            Assert.That(last.Things.OfType<StairInteractable>(), Is.Empty);
        }
    }
}
