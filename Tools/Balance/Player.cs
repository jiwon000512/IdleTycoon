using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Tables;
using Newtonsoft.Json;
using ZooTycoon.Core;

namespace Balance
{
    // 「보통 플레이」 봇: 새 게임부터 접속(세션)마다 진짜 Core를 돌리고, 접속 사이는 게임처럼 저장 → 새 게임에 덮기 → 오프라인 정산(Offline.Settle).
    // 정책(가정): 살 수 있는 것 중 가장 싼 것을 바로 산다(다음 두 번 월급만큼은 남긴다). 점원은 후보 셋 중 일머리가 가장 높은 웜뱃을 그냥 고용.
    // 장식 · 새 후보 보기는 사지 않고 빵집 굴은 놓을 자리가 없을 때만 판다. 평가는 쉬는 시간이 끝나고 진열대가 반 넘게 차 있으면 부른다.
    // 석상은 빌 수 있으면 빌고, 행상이 있으면 반짝돌을 다 쓴다. 점원 없는 오븐 · 계산대가 있으면 빵집 일, 밀이 모자라면 농장, 횟집을 열었으면 낚시 → 횟집
    internal sealed class Player
    {
        public sealed class Goal
        {
            public string Name;
            public double Cost;
            // 사고 나서도 남겨 둘 코인(기본은 다음 두 번 월급)
            public double Reserve = -1d;
            // 점원 빈자리(LeaveAt 뒤에는 퀘스트 걸음 값을 모으는 중에도 채운다)
            public bool Staff;
            public Func<IEnumerable<int>> Buy;
        }

        private readonly Sim m_sim;
        private readonly Action<string, double> m_log;

        public Player(Sim sim, Action<string, double> log)
        {
            m_sim = sim;
            m_log = log;
        }

        // 알약이 아직 알려 주지 않은 것(석상 · 행상 · 평가 · 낚시터)은 하지 않는 새 플레이어(FollowQuests가 정한다). 기본은 다 안다
        public Func<string, string, bool> Knows { get; set; } = (kind, param) => true;
        // 이 시각(sim 초)부터는 끄기 전 가게 단속: 걸음 값을 모으는 중이어도 빈 점원 자리를 채운다(월급 다섯 번 몫만 남김, 2026-10-09 밸런스방)
        public double LeaveAt { get; set; } = double.MaxValue;

        private BakeryArea Bakery => m_sim.Bakery;
        private ZooState State => m_sim.State;
        private Worker Worker => m_sim.Wombat.Worker;
        private double Wages => m_sim.Mall.Payroll.Clerks.Sum(c => c.Wage);

        // ---------- 살 것 ----------

        public IEnumerable<Goal> Goals()
        {
            BakeryArea b = Bakery;
            BreadTable bread = b.NextBread;

            if (bread != null)
            {
                yield return new Goal
                {
                    Name = "빵 " + bread.Name,
                    Cost = bread.UnlockCost,
                    Buy = () => AtOven(() =>
                    {
                        bool done = b.TryChoose(ActionTable.k_Bake, b.Ovens[0], bread.Id);
                        Assign(bread);
                        return done;
                    }),
                };
            }

            foreach (string kind in new[] { ShelfInteractable.k_Id, OvenInteractable.k_Id, CounterInteractable.k_Id })
            {
                if (!b.IsMaxed(kind) && !b.IsCapped(kind))
                {
                    yield return new Goal { Name = "빵집 " + Kind(kind), Cost = b.PriceOf(kind), Buy = () => Place(b, b.Grid, b.Layout.Cells, kind) };
                }

                Interactable thing = b.Things.FirstOrDefault(t => t.Table.Id == kind);
                UpgradeInfo up = thing?.Table.Upgrade;

                if (up != null && thing.UpgradeLevel < up.MaxLevel && thing.UpgradeLevel < State.Stars.Cap(b.Id, Stars.k_Upgrade))
                {
                    yield return new Goal { Name = $"{up.Name} {thing.UpgradeLevel + 1}", Cost = up.Cost(thing.UpgradeLevel), Buy = () => Upgrade(b, thing) };
                }
            }

            foreach (FarmArea farm in m_sim.Mall.Farms.Where(f => f.IsOpen))
            {
                if (!farm.IsFull)
                {
                    yield return new Goal { Name = $"농장{farm.Number}층 밭", Cost = farm.Grid.DigCost + farm.Floor.TillCost, Buy = () => Field(farm) };
                }
                else if (farm.Lower != null && !farm.Lower.IsOpen)
                {
                    yield return new Goal { Name = $"농장{farm.Number + 1}층 열기", Cost = farm.Lower.Floor.OpenCost, Buy = () => Floor(farm) };
                }
            }

            CropTable crop = m_sim.Farm.NextCrop;

            if (crop != null)
            {
                yield return new Goal { Name = "작물 " + crop.Id, Cost = crop.UnlockCost, Buy = () => Crop(crop) };
            }

            // 점원은 알약이 「점원 뽑기」를 알려 준 뒤부터
            foreach (WombatArea area in new WombatArea[] { b }.Concat(m_sim.Mall.Farms.Where(f => f.IsOpen)).Where(_ => Knows(GoalCounter.k_Hire, null)))
            {
                foreach (Interactable slot in area.ClerkSlots.Where(s => area.CanStaff(s) && area.ClerkOf(s) == null))
                {
                    Candidate best = area.Candidates.OrderByDescending(c => c.Skill).First();
                    int wage = area.WageFor(best, slot);
                    // 고용은 다음 다섯 번 월급(새 점원 포함)이 남을 때만
                    yield return new Goal { Name = $"점원 {area.Id} {Kind(slot.Table.Id)}", Cost = wage, Reserve = (Wages + wage) * 5d, Staff = true, Buy = () => Hire(area, slot) };
                }
            }

            FishingArea f = m_sim.Fishing;

            if (!Knows(GoalCounter.k_Visit, FishingArea.k_Id))
            {
                yield break;
            }

            foreach (DamInteractable dam in f.Things.OfType<DamInteractable>())
            {
                yield return new Goal { Name = $"댐 {dam.Stretch.Id}", Cost = 0d, Buy = () => Dam(dam) };
            }

            foreach (RockInteractable rock in f.Things.OfType<RockInteractable>())
            {
                yield return new Goal { Name = $"바위 {rock.Stretch.Id}", Cost = rock.Stretch.RockCost, Buy = () => Rock(rock) };
            }

            foreach (SeatInteractable seat in f.Seats.Where(s => !s.Placed))
            {
                yield return new Goal { Name = $"좌대 {seat.Stretch.Id}", Cost = seat.Row.Cost, Buy = () => Seat(seat) };
            }

            RestaurantArea r = m_sim.Restaurant;

            if (!r.IsOpen && !r.StarLocked)
            {
                yield return new Goal { Name = "횟집 열기", Cost = r.Config.OpenCost, Buy = () => OpenRestaurant() };
            }

            if (r.IsOpen)
            {
                foreach (string kind in new[] { TankInteractable.k_Id, CuttingBoardInteractable.k_Id, DiningTableInteractable.k_Id })
                {
                    if (!r.IsMaxed(kind))
                    {
                        yield return new Goal { Name = "횟집 " + Kind(kind), Cost = r.PriceOf(kind), Buy = () => Place(r, r.Grid, r.Layout.Cells, kind) };
                    }

                    Interactable thing = r.Things.FirstOrDefault(t => t.Table.Id == kind);
                    UpgradeInfo up = thing?.Table.Upgrade;

                    if (up != null && thing.UpgradeLevel < up.MaxLevel)
                    {
                        yield return new Goal { Name = $"{up.Name} {thing.UpgradeLevel + 1}", Cost = up.Cost(thing.UpgradeLevel), Buy = () => Upgrade(r, thing) };
                    }
                }
            }
        }

        private static string Kind(string id)
        {
            switch (id)
            {
                case ShelfInteractable.k_Id: return "진열대";
                case OvenInteractable.k_Id: return "오븐";
                case CounterInteractable.k_Id: return "계산대";
                case TankInteractable.k_Id: return "수조";
                case CuttingBoardInteractable.k_Id: return "도마";
                case DiningTableInteractable.k_Id: return "탁자";
                case BarnInteractable.k_Id: return "작업대";
                default: return id;
            }
        }

        // 가장 싼 것(월급 두 번 몫은 남긴다)
        public Goal NextGoal(out Goal cheapest)
        {
            cheapest = Goals().OrderBy(g => g.Cost).FirstOrDefault();
            return cheapest != null && State.Coins - cheapest.Cost >= (cheapest.Reserve >= 0d ? cheapest.Reserve : Wages * 2d) ? cheapest : null;
        }

        private IEnumerable<int> AtOven(Func<bool> choose)
        {
            foreach (int _ in m_sim.GoTo(BakeryArea.k_Id))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Walk(Bakery.Layout.WombatNav, Sim.OvenSpot(Bakery.Ovens[0])))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap))
            {
                yield return 0;
            }

            choose();
        }

        private IEnumerable<int> Upgrade(WombatArea area, Interactable thing)
        {
            foreach (int _ in m_sim.GoTo(area.Id))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Walk(m_sim.NavOf(area), StandAt(area, thing)))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap))
            {
                yield return 0;
            }

            area.TryChoose(ActionTable.k_Upgrade, thing, null);
        }

        private Vector2 StandAt(WombatArea area, Interactable thing)
        {
            switch (thing)
            {
                case ShelfInteractable shelf: return Sim.ShelfStand(m_sim.NavOf(area), shelf);
                case OvenInteractable oven: return Sim.OvenSpot(oven);
                case CounterInteractable counter: return counter.WorkerSpot;
                case CuttingBoardInteractable board: return board.WorkerSpot;
                case IPlaced placed: return Sim.StandNear(m_sim.NavOf(area), placed.Position, (float)thing.Table.Range, new List<Vector2>());
                default: return m_sim.Wombat.Mover.Position;
            }
        }

        // 편집 모드: 곳 안이면 어디서나 상점 카드 → 놓기. 자리가 없으면 굴을 한 칸 판다(값을 치른다)
        private IEnumerable<int> Place(WombatArea area, BurrowGrid grid, CellMetrics cells, string kind)
        {
            foreach (int _ in m_sim.GoTo(area.Id))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap * 2d))
            {
                yield return 0;
            }

            for (int tries = 0; tries < 6; tries++)
            {
                foreach (Cell cell in grid.Cells.Where(c => c.Row > 0).OrderByDescending(c => c.Row).ThenBy(c => Math.Abs(c.Col)))
                {
                    if (area.TryFindSpot(kind, cells.CellCenter(cell), out Vector2 spot) && Vector2.Distance(spot, area.HoleFloor) > 1.8f && area.TryBuy(kind, spot))
                    {
                        yield break;
                    }
                }

                double dig = grid.DigCost;

                if (State.Coins < dig + area.PriceOf(kind) || !State.TrySpendCoins(dig))
                {
                    yield break;
                }

                grid.Dig(Sim.NextCell(grid));
                m_log($"{area.Id} 굴 파기", dig);
            }
        }

        private IEnumerable<int> Field(FarmArea farm)
        {
            foreach (int _ in ToFloor(farm))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap * 2d))
            {
                yield return 0;
            }

            double dig = farm.Grid.DigCost;

            if (!State.TrySpendCoins(dig))
            {
                yield break;
            }

            Cell cell = Sim.NextCell(farm.Grid);
            farm.Grid.Dig(cell);
            PlotInteractable plot = farm.Plots.First(p => p.Cell.Equals(cell));

            if (State.TrySpendCoins(farm.Floor.TillCost))
            {
                plot.Till();
            }
        }

        // 1층부터 계단으로 내려간다
        private IEnumerable<int> ToFloor(FarmArea farm)
        {
            if (m_sim.Mall.Active is FarmArea here && here.Number > farm.Number)
            {
                foreach (int _ in m_sim.GoTo(FarmArea.k_Id))
                {
                    yield return 0;
                }
            }

            foreach (int _ in m_sim.GoTo(FarmArea.k_Id))
            {
                yield return 0;
            }

            while (m_sim.Mall.Active is FarmArea at && at.Number < farm.Number)
            {
                foreach (int _ in m_sim.Pass(at.Layout.Nav, at.Layout.StairFloor, -1f))
                {
                    yield return 0;
                }

                if (m_sim.Mall.Active == at)
                {
                    yield break;
                }
            }
        }

        private IEnumerable<int> Floor(FarmArea farm)
        {
            foreach (int _ in ToFloor(farm))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Walk(farm.Layout.Nav, farm.Layout.StairFloor))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap))
            {
                yield return 0;
            }

            farm.TryOpenLower(Worker);
        }

        private IEnumerable<int> Crop(CropTable crop)
        {
            FarmArea farm = m_sim.Farm;
            PlotInteractable plot = farm.Plots.FirstOrDefault(p => p.IsTilled && p.IsEmpty);

            foreach (int _ in m_sim.GoTo(FarmArea.k_Id))
            {
                yield return 0;
            }

            // 빈 밭이 없으면 익은 밭을 거둬 비운다
            plot = plot ?? farm.Plots.FirstOrDefault(p => p.IsRipe);

            if (plot == null)
            {
                yield break;
            }

            foreach (int _ in m_sim.Walk(farm.Layout.Nav, farm.Layout.Nav.Snap(farm.Layout.Cells.CellCenter(plot.Cell))))
            {
                yield return 0;
            }

            yield return 0;

            foreach (int _ in m_sim.Wait(Sim.Tap))
            {
                yield return 0;
            }

            farm.TryChoose(ActionTable.k_Plant, plot, crop.Id);
        }

        // 점원 팝업은 어디서나: 후보 셋 중 가장 높은 일머리를 그냥 고용. 40 아래면 새 후보 보기를 한 번 누른다
        private IEnumerable<int> Hire(WombatArea area, Interactable slot)
        {
            foreach (int _ in m_sim.Wait(Sim.Tap * 2d))
            {
                yield return 0;
            }

            if (area.Candidates.Max(c => c.Skill) < 40 && State.Coins >= area.ClerkConfig.RefreshCost * 2d && area.TryRefreshCandidates())
            {
                m_log("새 후보 보기", area.ClerkConfig.RefreshCost);
            }

            Candidate best = area.Candidates.OrderByDescending(c => c.Skill).First();

            if (area.TryHire(best, slot, area.WageFor(best, slot)))
            {
                m_sim.Add("hireSkill", best.Skill);
                m_sim.Add("hires", 1);
            }
        }

        // 새 빵을 열면 그 빵을 굽는 점원이 없을 때 점원이 가장 많이 굽는 빵의 오븐 점원 하나를 새 빵으로 바꾼다(칩 한 번)
        private void Assign(BreadTable bread)
        {
            List<Clerk> bakers = Bakery.Ovens.Select(o => Bakery.ClerkOf(o)).Where(c => c != null).ToList();

            if (bakers.Count > 1 && bakers.All(c => c.Product != bread.Id))
            {
                bakers.GroupBy(c => c.Product).OrderByDescending(g => g.Count()).First().First().TrySetProduct(bread.Id);
            }
        }

        private IEnumerable<int> Rock(RockInteractable rock)
        {
            FishingArea f = m_sim.Fishing;

            foreach (int _ in m_sim.GoTo(FishingArea.k_Id))
            {
                yield return 0;
            }

            int side = rock.Side;
            Vector2 stand = f.Layout.Nav.Snap(new Vector2(f.Layout.InnerEdge(rock.Stretch.Index) - side * 0.5f, f.Layout.BankY + 0.4f));

            foreach (int _ in m_sim.Walk(f.Layout.Nav, stand))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap))
            {
                yield return 0;
            }

            f.TryChoose(ActionTable.k_BreakRock, rock, null);
        }

        private IEnumerable<int> Dam(DamInteractable dam)
        {
            FishingArea f = m_sim.Fishing;

            foreach (int _ in m_sim.GoTo(FishingArea.k_Id))
            {
                yield return 0;
            }

            int side = Math.Sign(dam.Stretch.Index);
            Vector2 stand = f.Layout.Nav.Snap(new Vector2(f.Layout.InnerEdge(dam.Stretch.Index) - side * 0.4f, f.Layout.BankY + 0.4f));

            foreach (int _ in m_sim.Walk(f.Layout.Nav, stand))
            {
                yield return 0;
            }

            double runChanged = 0d;
            bool lastRunning = false;

            for (double t = 0d; t < 180d && f.Things.Contains(dam); t += Sim.Dt)
            {
                if (f.Phase == FishingPhase.Idle && !m_sim.Wombat.Busy && f.TargetAction?.Id == ActionTable.k_PullDebris)
                {
                    f.TryInteract();
                }
                else if (f.Phase == FishingPhase.Tug)
                {
                    if (f.Running != lastRunning)
                    {
                        lastRunning = f.Running;
                        runChanged = m_sim.Time;
                    }

                    m_sim.Wombat.SetHolding(m_sim.Time - runChanged >= 0.2 ? !f.Running : f.Running);
                }

                yield return 0;
            }

            m_sim.Wombat.SetHolding(false);
        }

        private IEnumerable<int> Seat(SeatInteractable seat)
        {
            FishingArea f = m_sim.Fishing;

            foreach (int _ in m_sim.GoTo(FishingArea.k_Id))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Walk(f.Layout.Nav, seat.Position))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap))
            {
                yield return 0;
            }

            f.TryChoose(ActionTable.k_PlaceSeat, seat, null);
        }

        private IEnumerable<int> OpenRestaurant()
        {
            foreach (int _ in m_sim.GoTo(PlazaArea.k_Id))
            {
                yield return 0;
            }

            ShopGateInteractable gate = m_sim.Plaza.Gates.FirstOrDefault();

            if (gate == null)
            {
                yield break;
            }

            foreach (int _ in m_sim.Walk(m_sim.Plaza.Layout.WombatNav, gate.Floor))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap))
            {
                yield return 0;
            }

            m_sim.Plaza.TryChoose(ActionTable.k_OpenShop, gate, null);
        }

        // ---------- 한 접속 ----------

        public IEnumerable<int> Play()
        {
            while (true)
            {
                foreach (int _ in PlayOnce())
                {
                    yield return 0;
                }
            }
        }

        // 보통 플레이 한 가지(든 빵 → 살 것 → 석상 · 행상 → 평가 → 농장 → 횟집 → 빵집 일). keep: 사고 나서도 남길 코인(퀘스트 걸음 값)
        private IEnumerable<int> PlayOnce(double keep = 0d)
        {
            BakeryArea b = Bakery;
            Crops();

            // 1. 든 빵부터 내려놓는다
            if (Worker.Hands.Count > 0 && m_sim.FillTarget(Worker.Hands.Bread) != null)
            {
                foreach (int _ in BakeryWork())
                {
                    yield return 0;
                }

                yield break;
            }

            // 2. 살 것
            Goal goal = NextGoal(out _);

            if (goal != null && State.Coins - goal.Cost >= (goal.Staff && m_sim.Time >= LeaveAt ? 0d : keep))
            {
                double before = State.Coins;

                foreach (int _ in goal.Buy())
                {
                    yield return 0;
                }

                double spent = before - State.Coins;

                // 걷는 동안 번 돈이 섞이므로 값의 절반 넘게 줄었으면 산 것으로 본다
                if (spent > goal.Cost * 0.5 || goal.Cost == 0d && !Goals().Any(g => g.Name == goal.Name))
                {
                    m_log(goal.Name, goal.Cost);
                }
                else
                {
                    m_sim.Add("buyFail:" + goal.Name, 1);
                    foreach (int _ in m_sim.Wait(5d))
                    {
                        yield return 0;
                    }
                }

                yield break;
            }

            // 3. 석상 · 행상
            bool merchant = m_sim.Plaza.Merchant.IsOpen && State.Count(State.Relics.Item) >= State.Relics.Cost && !State.Relics.Complete && Knows(GoalCounter.k_Draw, null);
            bool pray = State.Blessing.CanPray && Knows(GoalCounter.k_Bless, null);

            if (pray || merchant)
            {
                foreach (int _ in Plaza(merchant, pray))
                {
                    yield return 0;
                }

                yield break;
            }

            // 4. 평가
            if (Knows(GoalCounter.k_Star, null) && CanJudge())
            {
                foreach (int _ in Evaluate())
                {
                    yield return 0;
                }

                yield break;
            }

            // 5. 빵집 일(점원 없는 자리)
            bool understaffed = b.Ovens.Any(o => b.ClerkOf(o) == null) || b.Counters.Any(c => b.ClerkOf(c) == null);

            // 6. 농장(점원이 없을 때 밀 · 딸기가 모자라면)
            if (m_sim.Farm.ClerkOf(m_sim.Farm.Barn) == null && NeedsFarm())
            {
                foreach (int _ in FarmTrip())
                {
                    yield return 0;
                }

                yield break;
            }

            // 7. 횟집: 열었고 빵집에 점원이 다 있으면 낚시 → 횟집
            RestaurantArea r = m_sim.Restaurant;

            if (r.IsOpen && !understaffed)
            {
                int fish = State.Count("fish_minnow") + State.Count("fish_crucian") + State.Count("fish_catfish") + r.Tanks.Sum(t => t.Stock);

                foreach (int _ in fish >= 6 ? Restaurant(120d) : Fishing(180d))
                {
                    yield return 0;
                }

                yield break;
            }

            foreach (int _ in BakeryWork())
            {
                yield return 0;
            }
        }

        // ---------- 퀘스트 따라가기(가이드 · 퀘스트 1회차 사슬, 2026-10-08 리드 확정) ----------

        // 사슬 한 걸음: 이미 했나(상태 · 지난 사건으로) · 지금 할 수 있나 · 하는 법 · 못 할 때 기다리는 까닭
        public sealed class Step
        {
            public string Chapter;
            public string Name;
            public Func<bool> Done;
            public Func<bool> Ready = () => true;
            public Func<string> Need = () => "";
            // 이 걸음에 드는 코인(없으면 0). 기다리는 동안 다른 것을 사도 이만큼은 남긴다
            public Func<double> Cost = () => 0d;
            public Func<IEnumerable<int>> Act;
        }

        // 접속을 넘어 남는 사슬 진행(다음 걸음 번호 · 시작했나 · 기다린 초(까닭별) · 지난 사건 표시)
        public sealed class Progress
        {
            public int Index;
            public bool Begun;
            public Dictionary<string, double> Waits = new Dictionary<string, double>();
            public readonly HashSet<string> Seen = new HashSet<string>();
        }

        // 비교 안: 0 = 리드 초안 그대로 · 1 = 「빵집 별 5」를 새 맛 앞(광장 끝)으로 · 2 = 1 + 「좌대 놓기」를 바위 뚫기 앞으로
        public int Order { get; set; }

        // 리드 1회차 초안 순서(빵집 → 농장 → 가게 키우기 → 광장 → 새 맛 → 낚시 → 횟집)를 Order대로 옮긴다
        public List<Step> Chain(Progress p)
        {
            List<Step> chain = DraftChain(p);

            if (Order >= 1)
            {
                MoveBefore(chain, "빵집 별 5", chain.First(st => st.Chapter == "새 맛").Name);
            }

            if (Order >= 2)
            {
                MoveBefore(chain, "좌대 놓기", "바위 뚫기");
            }

            return chain;
        }

        private static void MoveBefore(List<Step> chain, string name, string before)
        {
            Step step = chain.First(st => st.Name == name);
            chain.Remove(step);
            chain.Insert(chain.FindIndex(st => st.Name == before), step);
        }

        private List<Step> DraftChain(Progress p)
        {
            BakeryArea b = Bakery;
            FarmArea farm = m_sim.Farm;
            FishingArea f = m_sim.Fishing;
            RestaurantArea r = m_sim.Restaurant;
            // 코인 걸음은 다음 두 번 월급을 남기고 산다(보통 플레이와 같은 규칙)
            Func<double, bool> afford = cost => State.Coins >= cost + Wages * 2d;
            Func<double, string> coins = cost => afford(cost) ? "" : "코인";
            Func<bool> stocked = () => b.Shelves.Sum(s => s.Stock) * 2 >= b.Shelves.Sum(s => s.Capacity);
            Func<string> evalNeed = () => !b.Evaluation.CanStart ? "평가 쉼(실패 뒤 3분)" : "진열대 채우기";
            int fishCount() => State.Count("fish_minnow") + State.Count("fish_crucian") + State.Count("fish_catfish") + r.Tanks.Sum(t => t.Stock);
            string cheapKind() => b.PriceOf(ShelfInteractable.k_Id) <= b.PriceOf(OvenInteractable.k_Id) ? ShelfInteractable.k_Id : OvenInteractable.k_Id;

            return new List<Step>
            {
                new Step { Chapter = "빵집", Name = "식빵 굽기", Done = () => b.Ovens.Any(o => o.LastBread != null),
                    Ready = () => b.Ovens[0].IsEmpty && State.Has(b.UnlockedBreads[0].Ingredients), Need = () => "오븐 비우기",
                    Act = () => AtOven(() => b.TryChoose(ActionTable.k_Bake, b.Ovens[0], b.UnlockedBreads[0].Id)) },
                new Step { Chapter = "빵집", Name = "진열대 채우기", Done = () => b.Shelves.Any(s => s.Stock > 0) || p.Seen.Contains("served"), Act = BakeryWork },
                new Step { Chapter = "빵집", Name = "손님 계산하기", Done = () => p.Seen.Contains("served"), Act = () => AtCounter(3d) },
                new Step { Chapter = "농장", Name = "농장 가기", Done = () => p.Seen.Contains("farm"), Act = () => m_sim.GoTo(FarmArea.k_Id) },
                new Step { Chapter = "농장", Name = "밭 갈기(먼저 파기)", Done = () => farm.Plots.Count(pl => pl.IsTilled) > farm.Floor.StartFields,
                    Ready = () => !farm.IsFull && afford(farm.Grid.DigCost + farm.Floor.TillCost), Need = () => coins(farm.Grid.DigCost + farm.Floor.TillCost),
                    Cost = () => farm.Grid.DigCost + farm.Floor.TillCost,
                    Act = () => Field(farm) },
                new Step { Chapter = "농장", Name = "밀 심기", Done = () => farm.Plots.Any(pl => !pl.IsEmpty) || p.Seen.Contains("harvest"),
                    Ready = () => farm.Plots.Any(pl => pl.IsTilled && pl.IsEmpty), Need = () => "빈 밭", Act = () => PlantOne(farm) },
                new Step { Chapter = "농장", Name = "밀 거두기", Done = () => p.Seen.Contains("harvest"),
                    Ready = () => farm.Plots.Any(pl => !pl.IsEmpty), Need = () => "심은 밭", Act = () => HarvestOne(farm) },
                new Step { Chapter = "가게 키우기", Name = "빵집 굴 넓히기", Done = () => b.Grid.Cells.Count > 8,
                    Ready = () => afford(b.Grid.DigCost), Need = () => coins(b.Grid.DigCost), Cost = () => b.Grid.DigCost, Act = DigBakery },
                new Step { Chapter = "가게 키우기", Name = "오븐 · 진열대 하나 더", Done = () => b.Shelves.Count > 1 || b.Ovens.Count > 1,
                    Ready = () => afford(b.PriceOf(cheapKind())), Need = () => coins(b.PriceOf(cheapKind())), Cost = () => b.PriceOf(cheapKind()),
                    Act = () => Place(b, b.Grid, b.Layout.Cells, cheapKind()) },
                new Step { Chapter = "가게 키우기", Name = "점원 뽑기", Done = () => p.Seen.Contains("hire"),
                    Ready = () => afford(b.ClerkConfig.RefreshCost + 60d), Need = () => coins(b.ClerkConfig.RefreshCost + 60d), Cost = () => b.ClerkConfig.RefreshCost + 60d,
                    Act = () => Hire(b, b.Counter) },
                new Step { Chapter = "광장", Name = "별 평가 받기", Done = () => State.Stars.Count(b.Id) >= 1,
                    Ready = () => b.Evaluation.CanStart && stocked(), Need = evalNeed, Act = Evaluate },
                new Step { Chapter = "광장", Name = "석상에 빌기", Done = () => p.Seen.Contains("pray"),
                    Ready = () => State.Blessing.CanPray, Need = () => "석상 쉼", Act = () => Plaza(false) },
                new Step { Chapter = "광장", Name = "행상에게 유물 뽑기", Done = () => State.Relics.All.Sum(rl => State.Relics.Stars(rl)) > 0,
                    Ready = () => m_sim.Plaza.Merchant.IsOpen && State.Count(State.Relics.Item) >= State.Relics.Cost,
                    Need = () => State.Count(State.Relics.Item) < State.Relics.Cost ? "반짝돌" : "행상 기다림", Act = () => Plaza(true) },
                new Step { Chapter = "광장", Name = "똥 치우기(거름)", Done = () => p.Seen.Contains("clean"),
                    Ready = () => b.Poops.Count > 0 || m_sim.Mall.Active.Poops.Count > 0, Need = () => "똥",
                    Act = () => m_sim.Clean(m_sim.Mall.Active.Poops.Count > 0 ? m_sim.Mall.Active : b) },
                new Step { Chapter = "새 맛", Name = "크루아상 열기", Done = () => b.UnlockedBreads.Count >= 2,
                    Ready = () => afford(b.NextBread.UnlockCost), Need = () => coins(b.NextBread.UnlockCost), Cost = () => b.NextBread.UnlockCost,
                    Act = () => AtOven(() => b.TryChoose(ActionTable.k_Bake, b.Ovens[0], b.NextBread.Id)) },
                new Step { Chapter = "새 맛", Name = "딸기 열기 · 심기", Done = () => farm.UnlockedCrops.Count >= 2,
                    Ready = () => afford(farm.NextCrop.UnlockCost) && farm.Plots.Any(pl => pl.IsTilled && pl.IsEmpty || pl.IsRipe),
                    Need = () => coins(farm.NextCrop.UnlockCost) == "" ? "빈 밭" : "코인", Cost = () => farm.NextCrop.UnlockCost, Act = () => Crop(farm.NextCrop) },
                new Step { Chapter = "새 맛", Name = "케이크 열기", Done = () => b.UnlockedBreads.Count >= 3,
                    Ready = () => afford(b.NextBread.UnlockCost), Need = () => coins(b.NextBread.UnlockCost), Cost = () => b.NextBread.UnlockCost,
                    Act = () => AtOven(() => b.TryChoose(ActionTable.k_Bake, b.Ovens[0], b.NextBread.Id)) },
                new Step { Chapter = "낚시", Name = "낚시터 가기", Done = () => p.Seen.Contains("fishing"), Act = () => m_sim.GoTo(FishingArea.k_Id) },
                new Step { Chapter = "낚시", Name = "물고기 낚기", Done = () => p.Seen.Contains("fish"), Act = () => FishOne() },
                new Step { Chapter = "낚시", Name = "바위 뚫기", Done = () => f.Opened.Count >= 2,
                    Ready = () => afford(f.Things.OfType<RockInteractable>().Min(rk => rk.Stretch.RockCost)),
                    Need = () => "코인", Cost = () => f.Things.OfType<RockInteractable>().Min(rk => rk.Stretch.RockCost), Act = () => Rock(f.Things.OfType<RockInteractable>().OrderBy(rk => rk.Stretch.RockCost).First()) },
                new Step { Chapter = "낚시", Name = "댐 허물기", Done = () => f.Flooded.Count >= 2, Act = () => Dam(f.Things.OfType<DamInteractable>().First()) },
                new Step { Chapter = "낚시", Name = "좌대 놓기", Done = () => f.Seats.Any(s => s.Placed),
                    Ready = () => afford(f.Seats.Where(s => !s.Placed).Min(s => s.Row.Cost)), Need = () => "코인", Cost = () => f.Seats.Where(s => !s.Placed).Min(s => s.Row.Cost),
                    Act = () => Seat(f.Seats.Where(s => !s.Placed).OrderBy(s => s.Row.Cost).First()) },
                new Step { Chapter = "횟집", Name = "빵집 별 5", Done = () => State.Stars.Count(b.Id) >= 5,
                    Ready = () => b.Evaluation.CanStart && stocked(), Need = evalNeed, Act = Evaluate },
                new Step { Chapter = "횟집", Name = "횟집 열기", Done = () => r.IsOpen,
                    Ready = () => !r.StarLocked && afford(r.Config.OpenCost), Need = () => r.StarLocked ? "빵집 ★5" : coins(r.Config.OpenCost),
                    Cost = () => r.Config.OpenCost, Act = OpenRestaurant },
                new Step { Chapter = "횟집", Name = "회 팔기", Done = () => p.Seen.Contains("dish"),
                    Act = () => fishCount() > 0 ? Restaurant(120d) : Fishing(90d) },
            };
        }

        // 사슬을 차례로: 이미 한 걸음은 건너뛰고(skip), 아직 못 하면 빵집 · 농장 일로 벌며 기다린다(까닭별 초를 센다). 사슬이 끝나면 보통 플레이(Play).
        // log: "begin"(처음 손댐) · "end"(해냄) · "skip"(가 보니 이미 됨)
        public IEnumerable<int> Guided(Progress p, Action<string, Step> log)
        {
            List<Step> chain = Chain(p);

            while (p.Index < chain.Count)
            {
                Step step = chain[p.Index];

                if (step.Done())
                {
                    log(p.Begun ? "end" : "skip", step);
                    p.Index++;
                    p.Begun = false;
                    p.Waits = new Dictionary<string, double>();
                    continue;
                }

                if (!p.Begun)
                {
                    p.Begun = true;
                    log("begin", step);
                }

                if (step.Ready())
                {
                    foreach (int _ in step.Act())
                    {
                        yield return 0;
                    }

                    // 해 봤는데 그대로면 한숨 돌린다(같은 틱에 되풀이하지 않게)
                    if (!step.Done())
                    {
                        foreach (int _ in m_sim.Wait(1d))
                        {
                            yield return 0;
                        }
                    }

                    continue;
                }

                double t0 = m_sim.Time;
                string need = step.Need();

                // 기다리는 동안 남는 코인으로 가장 싼 다른 것을 산다(이 걸음 값 + 월급 두 번은 남긴다)
                Goal extra = NextGoal(out _);
                double keep = step.Cost() + Wages * 2d;

                if (extra != null && State.Coins - extra.Cost >= Math.Max(keep, extra.Reserve))
                {
                    double before = State.Coins;

                    foreach (int _ in extra.Buy())
                    {
                        yield return 0;
                    }

                    if (before - State.Coins > extra.Cost * 0.5 || extra.Cost == 0d && !Goals().Any(g => g.Name == extra.Name))
                    {
                        m_log(extra.Name, extra.Cost);
                    }
                }
                else
                {
                    foreach (int _ in Earn())
                    {
                        yield return 0;
                    }
                }

                p.Waits[need] = (p.Waits.TryGetValue(need, out double w) ? w : 0d) + m_sim.Time - t0;
            }

            foreach (int _ in Play())
            {
                yield return 0;
            }
        }

        // ---------- 진짜 사슬 따라가기(설계 54 QuestLog: QuestTable 행 순서 · 세는 법 · 받기 · 건너뛰기 그대로) ----------

        // 지금 걸음을 하고, 다 되면 받는다(보상은 표 값이라 시안은 Sim 표를 고쳐 잰다). 못 하면 Guided처럼 벌거나 남는 코인으로 산다. 사슬이 끝나면 보통 플레이
        // log: "begin" · "end" · "skip"(받기가 이미 된 걸음을 넘김)
        // invest: 못 하는 동안 보통 플레이 한 가지(가장 싼 것 사기 · 석상 · 평가 · 농장 · 빵집 일). 아니면 걸음 값을 남기며 번다(저축 손)
        public IEnumerable<int> FollowQuests(Progress p, Action<string, QuestTable> log, bool invest = true)
        {
            QuestLog quests = m_sim.Mall.Quests;
            IReadOnlyList<QuestTable> rows = m_sim.Tables.GetAll<QuestTable>();
            Knows = (kind, param) => rows.Take(quests.Index + 1).Any(r => r.Kind == kind && (param == null || r.Param == param));

            while (quests.Current != null)
            {
                QuestTable row = quests.Current;

                if (quests.IsDone)
                {
                    log(p.Begun ? "end" : "skip", row);
                    int from = quests.Index;
                    double before = State.Coins;
                    quests.TryClaim();
                    m_sim.Add("questCoins", State.Coins - before);

                    for (int i = from + 1; i < quests.Index; i++)
                    {
                        log("skip", rows[i]);
                    }

                    p.Begun = false;
                    p.Waits = new Dictionary<string, double>();
                    continue;
                }

                if (!p.Begun)
                {
                    p.Begun = true;
                    log("begin", row);
                }

                Step step = StepFor(row);

                if (step.Ready())
                {
                    foreach (int _ in step.Act())
                    {
                        yield return 0;
                    }

                    if (!quests.IsDone)
                    {
                        foreach (int _ in m_sim.Wait(1d))
                        {
                            yield return 0;
                        }
                    }

                    continue;
                }

                double t0 = m_sim.Time;
                string need = step.Need();
                Goal extra = NextGoal(out _);
                double keep = step.Cost() + Wages * 2d;

                if (invest)
                {
                    foreach (int _ in PlayOnce(keep))
                    {
                        yield return 0;
                    }
                }
                else if (extra != null && State.Coins - extra.Cost >= Math.Max(keep, extra.Reserve))
                {
                    double before = State.Coins;

                    foreach (int _ in extra.Buy())
                    {
                        yield return 0;
                    }

                    if (before - State.Coins > extra.Cost * 0.5 || extra.Cost == 0d && !Goals().Any(g => g.Name == extra.Name))
                    {
                        m_log(extra.Name, extra.Cost);
                    }
                }
                else
                {
                    foreach (int _ in Earn())
                    {
                        yield return 0;
                    }
                }

                p.Waits[need] = (p.Waits.TryGetValue(need, out double w) ? w : 0d) + m_sim.Time - t0;
            }

            foreach (int _ in Play())
            {
                yield return 0;
            }
        }

        // 걸음 한 줄(kind · param)을 하는 법. 다 됐나는 QuestLog가 센다
        public Step StepFor(QuestTable q)
        {
            BakeryArea b = Bakery;
            FarmArea farm = m_sim.Farm;
            FishingArea f = m_sim.Fishing;
            RestaurantArea r = m_sim.Restaurant;
            Func<double, bool> afford = cost => State.Coins >= cost + Wages * 2d;
            Func<double, string> coins = cost => afford(cost) ? "" : "코인";
            int fishCount() => State.Count("fish_minnow") + State.Count("fish_crucian") + State.Count("fish_catfish") + r.Tanks.Sum(t => t.Stock);
            Step step = new Step { Chapter = q.Chapter.ToString(), Name = q.Id, Act = BakeryWork };

            switch (q.Kind)
            {
                case GoalCounter.k_Bake:
                    BreadTable bread = m_sim.Tables.Get<BreadTable>(q.Param ?? b.UnlockedBreads[0].Id);
                    step.Ready = () => b.Ovens[0].IsEmpty && State.Has(bread.Ingredients);
                    step.Need = () => "오븐 비우기";
                    step.Act = () => AtOven(() => b.TryChoose(ActionTable.k_Bake, b.Ovens[0], bread.Id));
                    break;
                case GoalCounter.k_Visit:
                    step.Act = () => m_sim.GoTo(q.Param);
                    break;
                case GoalCounter.k_Plant:
                    step.Ready = () => farm.Plots.Any(pl => pl.IsTilled);
                    step.Need = () => "밭";
                    step.Act = () => farm.Plots.Any(pl => pl.IsTilled && pl.IsEmpty) ? PlantOne(farm, q.Param ?? "wheat") : HarvestOne(farm);
                    break;
                case GoalCounter.k_Harvest:
                    step.Ready = () => farm.Plots.Any(pl => pl.IsTilled);
                    step.Need = () => "밭";
                    step.Act = () => farm.Plots.Any(pl => !pl.IsEmpty) ? HarvestOne(farm) : PlantOne(farm, q.Param ?? "wheat");
                    break;
                case GoalCounter.k_Dig:
                    BurrowGrid grid = q.Area == BakeryArea.k_Id ? b.Grid : farm.Grid;
                    step.Ready = () => afford(grid.DigCost) && (grid != farm.Grid || !farm.IsFull);
                    step.Need = () => coins(grid.DigCost);
                    step.Cost = () => grid.DigCost;
                    step.Act = () => grid == b.Grid ? DigBakery() : DigFarm(farm);
                    break;
                case GoalCounter.k_Till:
                    step.Ready = () => farm.Plots.Any(pl => !pl.IsTilled) && afford(farm.Floor.TillCost);
                    step.Need = () => farm.Plots.Any(pl => !pl.IsTilled) ? coins(farm.Floor.TillCost) : "갈 칸";
                    step.Cost = () => farm.Floor.TillCost;
                    step.Act = () => TillOne(farm);
                    break;
                case GoalCounter.k_Place:
                    step.Ready = () => !b.IsMaxed(q.Param) && !b.IsCapped(q.Param) && afford(b.PriceOf(q.Param));
                    step.Need = () => b.IsCapped(q.Param) ? "별 상한" : coins(b.PriceOf(q.Param));
                    step.Cost = () => b.PriceOf(q.Param);
                    step.Act = () => Place(b, b.Grid, b.Layout.Cells, q.Param);
                    break;
                case GoalCounter.k_Hire:
                    step.Ready = () => afford(b.ClerkConfig.RefreshCost + 60d);
                    step.Need = () => coins(b.ClerkConfig.RefreshCost + 60d);
                    step.Cost = () => b.ClerkConfig.RefreshCost + 60d;
                    step.Act = () => Hire(b, b.Counter);
                    break;
                case GoalCounter.k_Star:
                case GoalCounter.k_Evaluate:
                    step.Ready = CanJudge;
                    step.Need = () => !b.Evaluation.CanStart ? "평가 쉼(실패 뒤 3분)" : "진열대 채우기";
                    step.Act = Evaluate;
                    break;
                case GoalCounter.k_Bless:
                    step.Ready = () => State.Blessing.CanPray;
                    step.Need = () => "석상 쉼";
                    step.Act = () => Plaza(false);
                    break;
                case GoalCounter.k_Draw:
                    step.Ready = () => m_sim.Plaza.Merchant.IsOpen && State.Count(State.Relics.Item) >= State.Relics.Cost;
                    step.Need = () => State.Count(State.Relics.Item) < State.Relics.Cost ? "반짝돌" : "행상 기다림";
                    step.Act = () => Plaza(true);
                    break;
                case GoalCounter.k_Clean:
                    // 지금 곳 → 빵집 · 광장 · 농장 1층 똥을 치우러 간다. 똥이 없으면 사람처럼 빵집을 오가며 걷는다(걸어야 싼다)
                    step.Act = () =>
                    {
                        WombatArea dirty = m_sim.Mall.Active.Poops.Count > 0 ? m_sim.Mall.Active
                            : new WombatArea[] { b, m_sim.Plaza, farm }.FirstOrDefault(a => a.Poops.Count > 0);
                        return dirty == null ? Stroll() : m_sim.GoTo(dirty.Id).Concat(m_sim.Clean(dirty));
                    };
                    break;
                case GoalCounter.k_Unlock:
                    if (b.Tables.GetAll<BreadTable>().Any(br => br.Id == q.Param))
                    {
                        BreadTable next = m_sim.Tables.Get<BreadTable>(q.Param);
                        step.Ready = () => b.NextBread == next && afford(next.UnlockCost);
                        step.Need = () => b.NextBread != next ? "앞 빵" : coins(next.UnlockCost);
                        step.Cost = () => next.UnlockCost;
                        step.Act = () => AtOven(() =>
                        {
                            bool done = b.TryChoose(ActionTable.k_Bake, b.Ovens[0], next.Id);
                            Assign(next);
                            return done;
                        });
                    }
                    else
                    {
                        CropTable crop = m_sim.Tables.Get<CropTable>(q.Param);
                        step.Ready = () => farm.NextCrop == crop && afford(crop.UnlockCost) && farm.Plots.Any(pl => pl.IsTilled && pl.IsEmpty || pl.IsRipe);
                        step.Need = () => farm.NextCrop != crop ? "앞 작물" : coins(crop.UnlockCost) == "" ? "빈 밭" : "코인";
                        step.Cost = () => crop.UnlockCost;
                        step.Act = () => Crop(crop);
                    }

                    break;
                case GoalCounter.k_Catch:
                    step.Act = FishOne;
                    break;
                case GoalCounter.k_Open:
                    RockInteractable rock() => f.Things.OfType<RockInteractable>().FirstOrDefault(rk => rk.Stretch.Id == q.Param);

                    if (q.Param == r.Id)
                    {
                        step.Ready = () => !r.StarLocked && afford(r.Config.OpenCost);
                        step.Need = () => r.StarLocked ? "빵집 ★5" : coins(r.Config.OpenCost);
                        step.Cost = () => r.Config.OpenCost;
                        step.Act = OpenRestaurant;
                    }
                    else
                    {
                        step.Ready = () => rock() != null && afford(rock().Stretch.RockCost);
                        step.Need = () => coins(rock()?.Stretch.RockCost ?? 0d);
                        step.Cost = () => rock()?.Stretch.RockCost ?? 0d;
                        step.Act = () => Rock(rock());
                    }

                    break;
                case GoalCounter.k_Fill:
                    DamInteractable dam() => f.Things.OfType<DamInteractable>().FirstOrDefault(d => d.Stretch.Id == q.Param);
                    step.Ready = () => dam() != null;
                    step.Need = () => "댐";
                    step.Act = () => Dam(dam());
                    break;
                case GoalCounter.k_Seat:
                    step.Ready = () => f.Seats.Any(s => !s.Placed) && afford(f.Seats.Where(s => !s.Placed).Min(s => s.Row.Cost));
                    step.Need = () => "코인";
                    step.Cost = () => f.Seats.Where(s => !s.Placed).Select(s => s.Row.Cost).DefaultIfEmpty(0d).Min();
                    step.Act = () => Seat(f.Seats.Where(s => !s.Placed).OrderBy(s => s.Row.Cost).First());
                    break;
                case GoalCounter.k_Serve:
                    step.Ready = () => r.IsOpen;
                    step.Need = () => "횟집";
                    step.Act = () => fishCount() > 0 ? Restaurant(120d) : Fishing(90d);
                    break;
            }

            return step;
        }

        private IEnumerable<int> DigFarm(FarmArea farm)
        {
            foreach (int _ in ToFloor(farm))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap * 2d))
            {
                yield return 0;
            }

            if (!farm.IsFull && State.TrySpendCoins(farm.Grid.DigCost))
            {
                farm.Grid.Dig(Sim.NextCell(farm.Grid));
            }
        }

        private IEnumerable<int> TillOne(FarmArea farm)
        {
            foreach (int _ in ToFloor(farm))
            {
                yield return 0;
            }

            PlotInteractable plot = farm.Plots.FirstOrDefault(pl => !pl.IsTilled);

            if (plot == null)
            {
                yield break;
            }

            foreach (int _ in m_sim.Walk(farm.Layout.Nav, farm.Layout.Nav.Snap(farm.Layout.Cells.CellCenter(plot.Cell))))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap))
            {
                yield return 0;
            }

            if (State.TrySpendCoins(farm.Floor.TillCost))
            {
                plot.Till();
            }
        }

        // 기다리는 동안 버는 일: 밀이 모자라면 농장, 아니면 빵집 일 하나
        private IEnumerable<int> Earn()
        {
            return m_sim.Farm.ClerkOf(m_sim.Farm.Barn) == null && NeedsFarm() ? FarmTrip() : BakeryWork();
        }

        private IEnumerable<int> Stroll()
        {
            foreach (int _ in m_sim.GoTo(BakeryArea.k_Id))
            {
                yield return 0;
            }

            foreach (Vector2 spot in new[] { Sim.OvenSpot(Bakery.Ovens[0]), Bakery.Counter.WorkerSpot })
            {
                foreach (int _ in m_sim.Walk(Bakery.Layout.WombatNav, spot))
                {
                    yield return 0;
                }
            }
        }

        private IEnumerable<int> AtCounter(double seconds)
        {
            foreach (int _ in m_sim.GoTo(BakeryArea.k_Id))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Walk(Bakery.Layout.WombatNav, Bakery.Counter.WorkerSpot))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(seconds))
            {
                yield return 0;
            }
        }

        private IEnumerable<int> PlantOne(FarmArea farm, string crop = "wheat")
        {
            foreach (int _ in m_sim.GoTo(FarmArea.k_Id))
            {
                yield return 0;
            }

            PlotInteractable plot = farm.Plots.FirstOrDefault(pl => pl.IsTilled && pl.IsEmpty);

            if (plot == null)
            {
                yield break;
            }

            foreach (int _ in m_sim.Walk(farm.Layout.Nav, farm.Layout.Nav.Snap(farm.Layout.Cells.CellCenter(plot.Cell))))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap))
            {
                yield return 0;
            }

            farm.TryChoose(ActionTable.k_Plant, plot, crop);
        }

        // 심은 밭 곁에 서서 익기를 기다린다(밟고 서 있으면 익는 틱에 거둔다)
        private IEnumerable<int> HarvestOne(FarmArea farm)
        {
            foreach (int _ in m_sim.GoTo(FarmArea.k_Id))
            {
                yield return 0;
            }

            PlotInteractable plot = farm.Plots.Where(pl => !pl.IsEmpty).OrderBy(pl => pl.Remaining).FirstOrDefault();

            if (plot == null)
            {
                yield break;
            }

            foreach (int _ in m_sim.Walk(farm.Layout.Nav, farm.Layout.Nav.Snap(farm.Layout.Cells.CellCenter(plot.Cell))))
            {
                yield return 0;
            }

            for (double t = 0d; t < 70d && !plot.IsEmpty; t += Sim.Dt)
            {
                yield return 0;
            }
        }

        private IEnumerable<int> DigBakery()
        {
            foreach (int _ in m_sim.GoTo(BakeryArea.k_Id))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap * 2d))
            {
                yield return 0;
            }

            double cost = Bakery.Grid.DigCost;

            if (State.TrySpendCoins(cost))
            {
                Bakery.Grid.Dig(Sim.NextCell(Bakery.Grid));
            }
        }

        private IEnumerable<int> FishOne()
        {
            foreach (int _ in m_sim.GoTo(FishingArea.k_Id))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.ToBank(0))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Fish(20d))
            {
                yield return 0;
            }
        }

        // 케이크를 열었으면 1층 농장 점원이 딸기가 모자랄 때 딸기, 넉넉하면 밀을 심게 한다(칩 한 번)
        private void Crops()
        {
            Clerk clerk = m_sim.Farm.ClerkOf(m_sim.Farm.Barn);

            if (clerk == null || !Bakery.UnlockedBreads.Any(br => br.Ingredients.Any(i => i.Item == "strawberry")))
            {
                return;
            }

            string want = State.Count("strawberry") < 8 ? "strawberry" : "wheat";

            if (clerk.Product != want)
            {
                clerk.TrySetProduct(want);
            }
        }

        // 평가를 부를 만한가: 쉬는 시간이 끝났고 진열대가 반 넘게 찼고, 정해진 빵이 진열대에 둘 넘게 있다.
        // 정해진 빵을 굽는 오븐(점원 또는 빈 오븐)이 없으면 점원 하나를 그 빵으로 바꾼다(평가 팝업이 조건을 보여 준다)
        private bool CanJudge()
        {
            BakeryArea b = Bakery;
            List<BreadTable> breads = b.Evaluation.NextGoals.Where(g => g.Kind == Evaluation.GoalKind.Sell).Select(g => g.Bread).ToList();

            foreach (BreadTable bread in breads)
            {
                List<Clerk> bakers = b.Ovens.Select(o => b.ClerkOf(o)).Where(c => c != null).ToList();

                if (bakers.Count > 0 && bakers.Count == b.Ovens.Count && bakers.All(c => c.Product != bread.Id))
                {
                    bakers.GroupBy(c => c.Product).OrderByDescending(g => g.Count()).First().First().TrySetProduct(bread.Id);
                }
            }

            return b.Evaluation.CanStart && b.Shelves.Sum(s => s.Stock) * 2 >= b.Shelves.Sum(s => s.Capacity) && breads.All(br => b.StockOf(br) >= 2);
        }

        private bool NeedsFarm()
        {
            int wheat = Bakery.UnlockedBreads.Max(br => br.Ingredients.Where(i => i.Item == "wheat").Sum(i => i.Count)) * 2;
            bool cake = Bakery.UnlockedBreads.Any(br => br.Ingredients.Any(i => i.Item == "strawberry")) && m_sim.Farm.UnlockedCrops.Count > 1;
            bool low = State.Count("wheat") < wheat || cake && State.Count("strawberry") < 4;
            return low && m_sim.Farm.Plots.Any(p => p.IsRipe || p.IsTilled && p.IsEmpty);
        }

        // 빵집에서 일 하나(없으면 계산대 자리 · 딴짓 점원 깨우기)
        private IEnumerable<int> BakeryWork()
        {
            BakeryArea b = Bakery;

            foreach (int _ in m_sim.GoTo(BakeryArea.k_Id))
            {
                yield return 0;
            }

            bool did = false;

            foreach (int _ in m_sim.BakeryChore(d => did = d))
            {
                yield return 0;
            }

            if (did)
            {
                yield break;
            }

            ClerkInteractable idle = b.Things.OfType<ClerkInteractable>().FirstOrDefault(c => c.Clerk.Idling);

            if (idle != null)
            {
                foreach (int _ in m_sim.Walk(b.Layout.WombatNav, idle.Clerk.Position))
                {
                    yield return 0;
                }

                b.TryDo(ActionTable.k_Wake, Worker, idle);
                m_sim.Add("wake", 1);
                yield break;
            }

            foreach (int _ in m_sim.Walk(b.Layout.WombatNav, b.Counter.WorkerSpot))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(0.5))
            {
                yield return 0;
            }
        }

        private IEnumerable<int> FarmTrip()
        {
            double start = m_sim.Time;

            foreach (int _ in m_sim.GoTo(FarmArea.k_Id))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.TendFarm(m_sim.Farm))
            {
                yield return 0;
            }

            m_sim.Add("farmSeconds", m_sim.Time - start);
        }

        private IEnumerable<int> Plaza(bool merchant, bool pray = true)
        {
            PlazaArea p = m_sim.Plaza;

            foreach (int _ in m_sim.GoTo(PlazaArea.k_Id))
            {
                yield return 0;
            }

            if (pray && State.Blessing.CanPray)
            {
                foreach (int _ in m_sim.Walk(p.Layout.WombatNav, Sim.StandNear(p.Layout.WombatNav, p.Statue.Position, (float)p.Statue.Table.Range, new List<Vector2>())))
                {
                    yield return 0;
                }

                foreach (int _ in m_sim.Wait(Sim.Tap * 2d))
                {
                    yield return 0;
                }

                if (p.Statue.TryPray())
                {
                    m_sim.Add("pray", 1);
                    m_sim.Add("pray:" + State.Blessing.Active.Id, 1);
                }
            }

            if (merchant && p.Merchant.IsOpen)
            {
                foreach (int _ in m_sim.Walk(p.Layout.WombatNav, p.Merchant.Figure.Position))
                {
                    yield return 0;
                }

                while (p.MerchantThing.TryDraw())
                {
                    m_sim.Add("relicDraw", 1);

                    foreach (int _ in m_sim.Wait(Sim.Tap * 2d))
                    {
                        yield return 0;
                    }
                }
            }
        }

        private IEnumerable<int> Evaluate()
        {
            BakeryArea b = Bakery;
            BurrowNav nav = b.Layout.WombatNav;

            foreach (int _ in m_sim.GoTo(BakeryArea.k_Id))
            {
                yield return 0;
            }

            Vector2 board = Sim.StandNear(nav, new Vector2((float)b.Evaluation.Config.BoardX, (float)b.Evaluation.Config.BoardY), 0.8f, new List<Vector2>());

            foreach (int _ in m_sim.Walk(nav, board))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Wait(Sim.Tap * 2d))
            {
                yield return 0;
            }

            if (!b.Evaluation.TryStart())
            {
                yield break;
            }

            while (b.Evaluation.Running)
            {
                m_sim.Focus = b.Evaluation.Goals.FirstOrDefault(g => g.Kind == Evaluation.GoalKind.Sell && !g.Done)?.Bread;
                bool did = false;
                ClerkInteractable idle = b.Things.OfType<ClerkInteractable>().FirstOrDefault(c => c.Clerk.Idling);

                if (idle != null)
                {
                    foreach (int _ in m_sim.Walk(nav, idle.Clerk.Position))
                    {
                        yield return 0;
                    }

                    b.TryDo(ActionTable.k_Wake, Worker, idle);
                    m_sim.Add("wake", 1);
                    continue;
                }

                foreach (int _ in m_sim.BakeryChore(d => did = d))
                {
                    yield return 0;
                }

                if (!did)
                {
                    foreach (int _ in m_sim.Walk(nav, b.Counter.WorkerSpot))
                    {
                        yield return 0;
                    }

                    foreach (int _ in m_sim.Wait(0.5))
                    {
                        yield return 0;
                    }
                }
            }

            m_sim.Focus = null;
        }

        private IEnumerable<int> Fishing(double seconds)
        {
            FishingArea f = m_sim.Fishing;
            int stretch = f.Flooded.OrderBy(s => Math.Abs(s)).Last();
            double start = m_sim.Time;

            foreach (int _ in m_sim.GoTo(FishingArea.k_Id))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.ToBank(stretch))
            {
                yield return 0;
            }

            foreach (int _ in m_sim.Fish(seconds))
            {
                yield return 0;
            }

            m_sim.Add("fishSeconds", m_sim.Time - start);
        }

        private IEnumerable<int> Restaurant(double seconds)
        {
            double start = m_sim.Time;
            double end = m_sim.Time + seconds;
            IEnumerator<int> serve = m_sim.Restaurateur().GetEnumerator();

            while (m_sim.Time < end && (State.Count("fish_minnow") + State.Count("fish_crucian") + State.Count("fish_catfish") + m_sim.Restaurant.Tanks.Sum(t => t.Stock) > 0 || !Worker.Hands.Empty))
            {
                serve.MoveNext();
                yield return 0;
            }

            m_sim.Add("restaurantSeconds", m_sim.Time - start);
        }
    }

    // 한 주 「보통 플레이」: 접속 시간표대로 Player를 돌리고 접속 사이는 저장 → 새 게임에 덮기 → 오프라인 정산
    internal static class Week
    {
        // (시작 분, 길이 분). 첫날 9시 첫 접속 60분 + 12 · 15 · 18 · 21시 10분, 둘째 날부터 8 · 12 · 15 · 18 · 21시 10분
        public static List<(double start, double minutes)> Sessions(int days)
        {
            List<(double, double)> list = new List<(double, double)> { (9 * 60d, 60d) };

            foreach (int hour in new[] { 12, 15, 18, 21 })
            {
                list.Add((hour * 60d, 10d));
            }

            for (int day = 1; day < days; day++)
            {
                foreach (int hour in new[] { 8, 12, 15, 18, 21 })
                {
                    list.Add((day * 1440d + hour * 60d, 10d));
                }
            }

            return list;
        }

        // 오늘의 일 시계: 첫날 0시(월요일). 접속 시작 분을 더해 Sim에 준다
        private static readonly DateTime s_day1 = new DateTime(2026, 10, 12);

        // guided: 퀘스트 사슬을 따라가는 손(Player.Guided). 사슬 걸음마다 시작 · 끝(벽시계 분 · 접속 분) · 기다린 까닭을 steps에 남긴다.
        // quests: 진짜 사슬(Mall.Quests)을 따라가며 받고, 오늘의 일은 다 되면 받고 상자를 연다(보상 값은 edit로 고친 표). 하루마다 미션 · 점수 · 상자를 sessions에 남긴다
        public static object Run(int days = 7, int seed = 1, bool guided = false, int order = 0, bool quests = false, Action<TableSet> edit = null, bool invest = true)
        {
            List<object> purchases = new List<object>();
            List<object> sessions = new List<object>();
            List<object> coinsLine = new List<object>();
            List<object> steps = new List<object>();
            List<object> chests = new List<object>();
            Player.Progress progress = new Player.Progress();
            (double clock, double online) begun = (0d, 0d);
            string save = null;
            double clock = 0d;
            double onlineBefore = 0d;
            int index = 0;

            foreach ((double start, double minutes) in Sessions(days))
            {
                Sim sim = new Sim(seed * 1000 + index, edit, s_day1.AddMinutes(start));
                OfflineReport offline = null;

                if (save != null)
                {
                    GameSave.Apply(JsonConvert.DeserializeObject<SaveData>(save), sim.State, sim.Mall, sim.Tables);
                    offline = Offline.Settle(sim.State, sim.Mall, sim.Tables, (start - clock) * 60d);
                }

                double online = 0d;
                double coins0 = sim.State.Coins;
                int stars0 = sim.State.Stars.Count(BakeryArea.k_Id);
                int gems0 = sim.State.Count("gem");
                Player player = new Player(sim, (name, cost) =>
                {
                    purchases.Add(new { min = Math.Round(start + online / 60d, 1), session = index, name, cost = Math.Round(cost), coins = Math.Round(sim.State.Coins) });
                });
                if (quests)
                {
                    player.LeaveAt = (minutes - 1d) * 60d;
                    int session = index;
                    double sessionStart = start, sessionOnline = onlineBefore;
                    sim.Brain(player.FollowQuests(progress, (kind, row) =>
                    {
                        (double c, double o) now = (sessionStart + sim.Time / 60d, sessionOnline + sim.Time / 60d);

                        if (kind == "begin")
                        {
                            begun = now;
                            return;
                        }

                        (double c, double o) from = kind == "skip" ? now : begun;
                        steps.Add(new
                        {
                            id = row.Id,
                            chapter = row.Chapter,
                            kind = row.Kind,
                            param = row.Param,
                            count = row.Count,
                            reward = row.Coins,
                            skipped = kind == "skip",
                            startClock = Math.Round(from.c - 540d, 2),
                            endClock = Math.Round(now.c - 540d, 2),
                            startOnline = Math.Round(from.o, 2),
                            endOnline = Math.Round(now.o, 2),
                            onlineMinutes = Math.Round(now.o - from.o, 2),
                            session,
                            waits = progress.Waits.ToDictionary(w => w.Key, w => Math.Round(w.Value / 60d, 2)),
                            coins = Math.Round(sim.State.Coins),
                            cost = Math.Round(player.StepFor(row).Cost()),
                        });
                    }, invest));
                }
                else if (guided)
                {
                    player.Order = order;
                    sim.Bus.Subscribe<Events.BakeryVisitorPaid>(e => progress.Seen.Add("served"));
                    sim.Bus.Subscribe<Events.AreaChanged>(e => progress.Seen.Add(e.Active is FarmArea ? "farm" : e.Active.Id));
                    sim.Bus.Subscribe<Events.Harvested>(e => progress.Seen.Add("harvest"));
                    sim.Bus.Subscribe<Events.ClerkHired>(e => progress.Seen.Add("hire"));
                    sim.Bus.Subscribe<Events.BlessingChanged>(e => { if (e.Prayed) progress.Seen.Add("pray"); });
                    sim.Bus.Subscribe<Events.PoopsCleaned>(e => progress.Seen.Add("clean"));
                    sim.Bus.Subscribe<Events.FishLanded>(e => progress.Seen.Add("fish"));
                    sim.Bus.Subscribe<Events.RestaurantVisitorPaid>(e => progress.Seen.Add("dish"));
                    int session = index;
                    double sessionStart = start, sessionOnline = onlineBefore;
                    sim.Brain(player.Guided(progress, (kind, step) =>
                    {
                        (double c, double o) now = (sessionStart + sim.Time / 60d, sessionOnline + sim.Time / 60d);

                        if (kind == "begin")
                        {
                            begun = now;
                            return;
                        }

                        (double c, double o) from = kind == "skip" ? now : begun;
                        steps.Add(new
                        {
                            index = progress.Index,
                            chapter = step.Chapter,
                            name = step.Name,
                            skipped = kind == "skip",
                            startClock = Math.Round(from.c - 540d, 2),
                            endClock = Math.Round(now.c - 540d, 2),
                            startOnline = Math.Round(from.o, 2),
                            endOnline = Math.Round(now.o, 2),
                            onlineMinutes = Math.Round(now.o - from.o, 2),
                            session,
                            waits = progress.Waits.ToDictionary(w => w.Key, w => Math.Round(w.Value / 60d, 2)),
                            coins = Math.Round(sim.State.Coins),
                        });
                    }));
                }
                else
                {
                    sim.Brain(player.Play());
                }

                Action<string> mark = name => purchases.Add(new { min = Math.Round(start + sim.Time / 60d, 1), session = index, name, cost = 0d, coins = Math.Round(sim.State.Coins) });
                sim.Bus.Subscribe<Events.StarsChanged>(e => mark("★" + e.Count));
                sim.Bus.Subscribe<Events.ClerkFired>(e => mark("해고 " + e.Clerk.Home.Id + " " + e.Reason));
                sim.Bus.Subscribe<Events.RelicsChanged>(e =>
                {
                    if (sim.State.Relics.Complete)
                    {
                        mark("유물 다 모음");
                    }
                });

                DailyBoard board = sim.Mall.Missions;

                for (double t = 0d; t < minutes * 60d; t += 30d)
                {
                    sim.Run(30d);
                    online = sim.Time;

                    if (quests)
                    {
                        double before = sim.State.Coins;

                        for (int i = 0; i < board.Missions.Count; i++)
                        {
                            board.TryClaim(i);
                        }

                        foreach (MissionChestTable chest in board.Chests)
                        {
                            double was = sim.State.Coins;

                            if (board.TryOpen(chest.Chest))
                            {
                                player.NextGoal(out Player.Goal goal);
                                chests.Add(new { min = Math.Round(start + sim.Time / 60d, 1), day = (int)(start / 1440d) + 1, chest = chest.Chest, chapter = chest.Chapter, coins = Math.Round(sim.State.Coins - was), next = goal?.Name, nextCost = goal == null ? 0d : Math.Round(goal.Cost) });
                            }
                        }

                        sim.Add("chestCoins", sim.State.Coins - before);
                    }

                    player.NextGoal(out Player.Goal next);
                    coinsLine.Add(new { min = Math.Round(start + online / 60d, 1), coins = Math.Round(sim.State.Coins), next = next?.Name, nextCost = next == null ? 0d : Math.Round(next.Cost), stars = sim.State.Stars.Count(BakeryArea.k_Id), gems = sim.State.Count("gem"), wheat = sim.State.Count("wheat"), berry = sim.State.Count("strawberry"), fish = sim.State.Count("fish_minnow") + sim.State.Count("fish_crucian") + sim.State.Count("fish_catfish"), clerks = sim.Mall.Payroll.Clerks.Count(),
                        earned = Math.Round(sim["bakery"] + sim["tips"] + sim["restaurant"] + sim["seats"] + sim["debris"]), wages = Math.Round(sim["wages"]), quest = Math.Round(sim["questCoins"]), chest = Math.Round(sim["chestCoins"]), chapter = sim.Mall.Quests.Chapter, step = sim.Mall.Quests.Index });
                }

                sessions.Add(new
                {
                    index,
                    day = (int)(start / 1440d) + 1,
                    startMin = start,
                    minutes,
                    offlineMin = offline == null ? 0d : Math.Round(offline.Seconds / 60d),
                    offlineCoins = offline == null ? 0d : Math.Round(offline.Coins),
                    offlineSales = offline == null ? 0d : Math.Round(offline.Sales),
                    offlineWages = offline == null ? 0d : Math.Round(offline.Wages),
                    offlineItems = offline?.Items,
                    coinsStart = Math.Round(coins0),
                    coinsEnd = Math.Round(sim.State.Coins),
                    stars = $"{stars0}→{sim.State.Stars.Count(BakeryArea.k_Id)}",
                    gems = $"{gems0}→{sim.State.Count("gem")}",
                    relics = sim.State.Relics.All.Sum(r => sim.State.Relics.Stars(r)),
                    counts = sim.Counts.ToDictionary(c => c.Key, c => Math.Round(c.Value, 1)),
                    chapter = sim.Mall.Quests.Chapter,
                    missionDay = board.Day,
                    missions = board.Missions.Select(m => new { id = m.Row.Id, m.Progress, m.Row.Count, m.Claimed }).ToList(),
                    points = board.Points,
                    opened = board.Opened.ToList(),
                });

                Console.WriteLine($"세션 {index} {start / 60d:F1}시 코인 {coins0:F0}→{sim.State.Coins:F0} 별 {stars0}→{sim.State.Stars.Count(BakeryArea.k_Id)} 오프라인 {offline?.Coins:F0} 산 것 {purchases.Count}");
                save = JsonConvert.SerializeObject(GameSave.Capture(sim.State, sim.Mall));
                clock = start + minutes;
                onlineBefore += minutes;
                index++;
            }

            return new { purchases, sessions, coins = coinsLine, steps, chests };
        }
    }
}
