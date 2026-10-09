using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace Balance
{
    // 진짜 Core 한 판: GameManager.NewGame과 같은 조립(빵집 · 광장 · 농장 층 · 낚시터 · 횟집 한 Mall), 난수 씨앗 고정, 0.05초 틱.
    // 사건을 세어(this[키]) 판 돈 · 팁 · 월급 · 거둔 재료 · 낚은 물고기 · 횟집 값을 잰다.
    // 손은 봇(웜뱃을 길 따라 걷게 하고 버튼 · 시트를 누르는 반복자)이 맡고, 측정용 배치 · 점원 · 별은 값 없이 바로 넣는다
    internal sealed class Sim
    {
        public const double Dt = 0.05;
        // 시트를 열어 칩을 누르는 사람 손 시간(초)
        public const double Tap = 0.8;

        private static readonly string s_data = Path.Combine(typeof(Sim).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "UnityProject").Value, "Assets", "Resources", "Data");
        private static readonly Vector2[] k_ShelfStands =
        {
            new Vector2(-0.6f, -0.7f), new Vector2(0f, -0.7f), new Vector2(0.6f, -0.7f), new Vector2(-1.1f, 0f), new Vector2(1.1f, 0f), new Vector2(0f, -1f),
        };
        private static readonly Vector2 k_Up = new Vector2(0f, 1f);

        private readonly Dictionary<string, double> m_count = new Dictionary<string, double>();
        private IEnumerator<int> m_brain;

        public TableSet Tables { get; }
        public EventBus Bus { get; } = new EventBus();
        public ZooState State { get; }
        public Mall Mall { get; }
        public IRandom Random { get; }
        public double Time { get; set; }
        public BakeryArea Bakery => Mall.Bakery;
        public FarmArea Farm => Mall.Farm;
        public PlazaArea Plaza => Mall.Plaza;
        public FishingArea Fishing => Mall.Fishing;
        public RestaurantArea Restaurant => Mall.Restaurant;
        public Wombat Wombat => Mall.Wombat;
        public IReadOnlyDictionary<string, double> Counts => m_count;
        public double this[string key] => m_count.TryGetValue(key, out double v) ? v : 0d;
        // 평가 중 정해진 빵(있으면 먼저 굽는다)
        public BreadTable Focus { get; set; }

        // start: 오늘의 일 시계의 0초 시각(없으면 기기 시각)
        public Sim(int seed, Action<TableSet> edit = null, DateTime? start = null)
        {
            Tables = LoadTables();
            edit?.Invoke(Tables);
            State = ZooState.CreateNew(Tables, Bus);
            Random = new SeededRandom(seed);
            Wombat wombat = new Wombat(Tables, State);
            BakeryArea bakery = new BakeryArea(State, Tables, Random, wombat, Bus);
            Mall = new Mall(bakery, new PlazaArea(Tables, bakery, Random, wombat, Bus), new FarmArea(Tables, Random, wombat, Bus), Bus,
                new FishingArea(Tables, Random, wombat, Bus), new RestaurantArea(State, Tables, Random, wombat, Bus),
                start == null ? (Func<DateTime>)null : () => start.Value.AddSeconds(Time));

            Bus.Subscribe<Events.BakeryVisitorPaid>(e => { Add("bakery", e.Coins); Add("served", 1); });
            Bus.Subscribe<Events.Tipped>(e => Add("tips", e.Coins));
            Bus.Subscribe<Events.BakeryVisitorGaveUp>(e => { Add("gaveUp", 1); Add("gaveUp:" + (e.Visitor.Disgusted ? "poop" : e.Visitor.Bread?.Id ?? "none"), 1); });
            Bus.Subscribe<Events.BakeryVisitorArrived>(e => Add("arrived", 1));
            Bus.Subscribe<Events.ClerkPaid>(e => Add("wages", e.Wage));
            Bus.Subscribe<Events.Harvested>(e => Add("harvest:" + e.Crop.Item, e.Count));
            Bus.Subscribe<Events.BonusFound>(e => Add("gem:harvest", e.Count));
            Bus.Subscribe<Events.EvaluationEnded>(e => Add(e.Passed ? "evalPass" : "evalFail", 1));
            Bus.Subscribe<Events.FishLanded>(e => { Add("fish", 1); Add("fish:" + e.Fish.Kind.Item, 1); });
            Bus.Subscribe<Events.FishingEnded>(e => Add("fishEnd:" + e.Reason, 1));
            Bus.Subscribe<Events.DebrisPulled>(e => Add("debris", e.Coins));
            Bus.Subscribe<Events.FishingVisitorCaught>(e => Add("seats", e.Coins));
            Bus.Subscribe<Events.RestaurantVisitorPaid>(e => { Add("restaurant", e.Coins); Add("dishes", 1); });
            Bus.Subscribe<Events.RestaurantVisitorLeft>(e => Add(e.Visitor.Angry ? "restaurantAngry" : "restaurantLeft", 1));
            Bus.Subscribe<Events.PoopsCleaned>(e => Add("manure", e.Count));
            Bus.Subscribe<Events.GoalRewarded>(e => Add("reward", e.Coins));
            // 오늘의 일 후보마다 한 접속에 몇 번 했나(게임과 같은 세기, 대상이 있으면 대상별로도)
            GoalCounter.Listen(Bus, (kind, param, n) =>
            {
                Add("goal:" + kind, n);

                if (param != null)
                {
                    Add("goal:" + kind + ":" + param, n);
                }
            });
        }

        public static TableSet LoadTables()
        {
            return new TableSet(name => File.ReadAllText(Path.Combine(s_data, name + ".json")));
        }

        // 측정용: 별 마일스톤 상한을 걷어 낸다
        public static void LiftCaps(TableSet tables)
        {
            foreach (StarMilestoneTable milestone in tables.GetAll<StarMilestoneTable>())
            {
                milestone.ShelfMax = milestone.OvenMax = milestone.CounterMax = milestone.UpgradeMax = 99;
            }
        }

        public void Add(string key, double value)
        {
            m_count[key] = this[key] + value;
        }

        public void ResetCounts()
        {
            m_count.Clear();
        }

        // 손(봇): 틱마다 Mall보다 먼저 한 걸음
        public void Brain(IEnumerable<int> brain)
        {
            m_brain = brain?.GetEnumerator();
        }

        public void Run(double seconds, Func<bool> until = null)
        {
            double end = Time + seconds;

            while (Time < end - 1e-9)
            {
                if (until != null && until())
                {
                    return;
                }

                m_brain?.MoveNext();
                Mall.Tick(Dt);
                Time += Dt;
            }
        }

        // ---------- 측정용 배치(값 없이) ----------

        public void Unlock(int breads)
        {
            foreach (BreadTable bread in Tables.GetAll<BreadTable>().Take(breads).Skip(Bakery.UnlockedBreads.Count).ToList())
            {
                Bakery.UnlockBread(bread);
            }
        }

        public void AddStars(int n)
        {
            for (int i = 0; i < n; i++)
            {
                State.Stars.Add(BakeryArea.k_Id);
            }
        }

        public void Level(WombatArea area, string id, int level)
        {
            while (area.UpgradeLevel(id) < level)
            {
                area.LevelUp(id);
            }
        }

        public void Plenty(params string[] items)
        {
            foreach (string item in items)
            {
                State.AddItem(item, 100000);
            }
        }

        // 그 곳에 kind 사물이 total개가 될 때까지 놓는다(값은 대 준다). 자리가 없으면 옆 칸을 판다
        public void Place(WombatArea area, BurrowGrid grid, CellMetrics cells, string kind, int total)
        {
            for (int guard = 0; area.CountOf(kind) < total; guard++)
            {
                if (guard > 40)
                {
                    throw new InvalidOperationException($"{area.Id} {kind} {total}개를 놓지 못했다");
                }

                bool placed = false;

                // 입구 구멍 곁(일 자리가 나가기 띠에 닿는 곳)은 피한다
                foreach (Cell cell in grid.Cells.Where(c => c.Row > 0).OrderByDescending(c => c.Row).ThenBy(c => Math.Abs(c.Col)))
                {
                    if (area.TryFindSpot(kind, cells.CellCenter(cell), out Vector2 spot) && Vector2.Distance(spot, area.HoleFloor) > 1.8f)
                    {
                        State.AddCoins(area.PriceOf(kind));

                        if (area.TryBuy(kind, spot))
                        {
                            placed = true;
                            break;
                        }
                    }
                }

                if (!placed)
                {
                    grid.Dig(NextCell(grid));
                }
            }
        }

        // 옆으로 먼저(위 줄부터), 그다음 아래로
        public static Cell NextCell(BurrowGrid grid)
        {
            return grid.Frontier().OrderBy(c => c.Row).ThenBy(c => Math.Abs(c.Col)).First();
        }

        // 농장 층에 갈아 둔 밭 칸이 total개가 될 때까지(파고 간다)
        public void Fields(FarmArea farm, int total)
        {
            while (farm.Plots.Count(p => p.IsTilled) < total)
            {
                PlotInteractable untilled = farm.Plots.FirstOrDefault(p => !p.IsTilled);

                if (untilled != null)
                {
                    untilled.Till();
                    continue;
                }

                farm.Grid.Dig(NextCell(farm.Grid));
            }
        }

        public Clerk Hire(WombatArea area, Interactable thing, int skill, string product = null)
        {
            VisitorTable look = Tables.GetAll<VisitorTable>().First(v => v.Role == VisitorRole.Clerk);
            Candidate candidate = new Candidate("측정", skill, look);
            int wage = area.WageFor(candidate, thing);
            State.AddCoins(wage);

            if (!area.TryHire(candidate, thing, wage))
            {
                throw new InvalidOperationException($"{thing.Table.Id} 점원을 들이지 못했다");
            }

            Clerk clerk = area.ClerkOf(thing);

            if (product != null && !clerk.TrySetProduct(product))
            {
                throw new InvalidOperationException($"{product}를 고르지 못했다");
            }

            return clerk;
        }

        // 빵집 오븐(해금한 빵을 돌아가며) · 계산대마다 점원
        public void StaffBakery(int skill)
        {
            for (int i = 0; i < Bakery.Ovens.Count; i++)
            {
                Hire(Bakery, Bakery.Ovens[i], skill, Bakery.UnlockedBreads[i % Bakery.UnlockedBreads.Count].Id);
            }

            foreach (CounterInteractable counter in Bakery.Counters)
            {
                Hire(Bakery, counter, skill);
            }
        }

        // 웜뱃을 손이 닿지 않는 곳(낚시터 구멍 앞)으로
        public void Park()
        {
            Bus.Publish(new Events.Passed(Mall.Active, FishingArea.k_Id));
        }

        // ---------- 봇 걸음 ----------

        public IEnumerable<int> Wait(double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += Dt)
            {
                yield return 0;
            }
        }

        // 길을 따라 target까지(못 가면 가까운 빈 땅으로). 짧은 동작(Busy) 중이면 끝날 때까지
        public IEnumerable<int> Walk(BurrowNav nav, Vector2 target)
        {
            while (Wombat.Busy)
            {
                yield return 0;
            }

            if (Vector2.Distance(Wombat.Mover.Position, target) > 0.05f && !Wombat.Mover.WalkTo(nav, target, Facing.Down)
                && nav.TryNearestFree(nav.Snap(target), _ => false, 1.5f, out Vector2 free))
            {
                Wombat.Mover.WalkTo(nav, free, Facing.Down);
            }

            for (double t = 0d; (Wombat.Guided || Wombat.Busy) && t < 30d; t += Dt)
            {
                yield return 0;
            }
        }

        // 통로 바닥까지 걸어 띠 쪽으로 밀어 넘어간다(exit · door는 위쪽 띠, 농장 아래층 계단은 dir −1)
        public IEnumerable<int> Pass(BurrowNav nav, Vector2 floor, float dir = 1f)
        {
            WombatArea from = Mall.Active;

            foreach (int _ in Walk(nav, floor))
            {
                yield return 0;
            }

            Wombat.SetInput(k_Up * dir);

            for (double t = 0d; t < 3d && Mall.Active == from; t += Dt)
            {
                yield return 0;
            }

            Wombat.SetInput(Vector2.Zero);

            if (Mall.Active == from)
            {
                Add("stuck:" + from.Id, 1);
            }

            yield return 0;
        }

        // 지금 곳에서 광장을 거쳐 to로(빵집 · 농장 1층 · 낚시터 · 횟집). 이미 거기면 그대로
        public IEnumerable<int> GoTo(string to)
        {
            if (Mall.Active.Id == to)
            {
                yield break;
            }

            if (Mall.Active != Plaza)
            {
                foreach (int _ in Pass(NavOf(Mall.Active), Mall.Active.HoleFloor))
                {
                    yield return 0;
                }
            }

            if (to == PlazaArea.k_Id)
            {
                yield break;
            }

            foreach (int _ in Pass(Plaza.Layout.WombatNav, Plaza.Layout.DoorTo(to).Floor))
            {
                yield return 0;
            }
        }

        public BurrowNav NavOf(WombatArea area)
        {
            switch (area)
            {
                case BakeryArea b: return b.Layout.WombatNav;
                case PlazaArea p: return p.Layout.WombatNav;
                case FarmArea f: return f.Layout.Nav;
                case FishingArea f: return f.Layout.Nav;
                case RestaurantArea r: return r.Layout.WombatNav;
                default: throw new InvalidOperationException(area.Id);
            }
        }

        // ---------- 빵집 손 ----------

        // 든 빵을 받을 진열대(채우기 규칙과 같다: 그 빵 진열대에 자리, 없으면 빈 진열대)
        public ShelfInteractable FillTarget(BreadTable bread)
        {
            if (bread == null)
            {
                return null;
            }

            ShelfInteractable empty = null;

            foreach (ShelfInteractable shelf in Bakery.Shelves)
            {
                if (shelf.HasRoomFor(bread))
                {
                    return shelf;
                }

                if (shelf.Bread == null && empty == null)
                {
                    empty = shelf;
                }
            }

            return empty;
        }

        // 진열대 거리 안의 걷는 땅(점원 자리 왼쪽 아래 → 앞 → 옆 순)
        public static Vector2 ShelfStand(BurrowNav nav, ShelfInteractable shelf)
        {
            foreach (Vector2 offset in k_ShelfStands)
            {
                Vector2 stand = nav.Snap(shelf.Position + offset);

                if (nav.IsWalkable(stand) && shelf.DistanceTo(stand) <= shelf.Table.Range - 0.05f)
                {
                    return stand;
                }
            }

            return nav.TryNearestFree(shelf.Position, _ => false, (float)shelf.Table.Range, out Vector2 free) ? free : shelf.Position;
        }

        public static Vector2 OvenSpot(OvenInteractable oven)
        {
            return Placement.SpotOf(oven, SpotRole.Worker);
        }

        // 구울 빵: 재료가 있고 놓을 진열대 자리가 남은 빵 중 (진열 + 굽는 중 + 든 것) ÷ 비중이 가장 적은 것. 다 찼으면 null
        public BreadTable Wanted()
        {
            if (Focus != null && State.Has(Focus.Ingredients) && FillTarget(Focus) != null)
            {
                return Focus;
            }

            BreadTable best = null;
            double bestScore = double.MaxValue;
            Hands hands = Wombat.Worker.Hands;

            foreach (BreadTable bread in Bakery.UnlockedBreads)
            {
                if (!State.Has(bread.Ingredients) || FillTarget(bread) == null)
                {
                    continue;
                }

                int have = Bakery.StockOf(bread) + Bakery.Ovens.Where(o => o.Bread == bread).Sum(o => o.Ready > 0 ? o.Ready : bread.BatchSize)
                    + (hands.Bread == bread ? hands.Count : 0);
                int room = Bakery.Shelves.Where(s => s.Bread == bread || s.Bread == null).Sum(s => s.Capacity);

                if (have >= room)
                {
                    continue;
                }

                double score = have / (double)bread.Weight;

                if (score < bestScore)
                {
                    best = bread;
                    bestScore = score;
                }
            }

            return best;
        }

        // 점원이 없는 오븐 · 계산대 일 한 가지(채우기 → 똥 치우기 → 꺼내기 → 굽기 → 계산). 할 일이 없으면 아무것도 하지 않고 끝난다
        public IEnumerable<int> BakeryChore(Action<bool> did)
        {
            BakeryArea b = Bakery;
            BurrowNav nav = b.Layout.WombatNav;
            Hands hands = Wombat.Worker.Hands;
            ShelfInteractable shelf = hands.Count > 0 ? FillTarget(hands.Bread) : null;

            if (shelf != null)
            {
                did(true);

                foreach (int _ in Walk(nav, ShelfStand(nav, shelf)))
                {
                    yield return 0;
                }

                yield return 0;
                yield break;
            }

            if (b.Poops.Count > 0)
            {
                did(true);

                foreach (int _ in Clean(b))
                {
                    yield return 0;
                }

                yield break;
            }

            OvenInteractable ready = b.Ovens.FirstOrDefault(o => b.ClerkOf(o) == null && o.Ready > 0 && hands.SpaceFor(o.Bread) > 0 && FillTarget(o.Bread) != null);

            if (ready != null)
            {
                did(true);

                foreach (int _ in Walk(nav, OvenSpot(ready)))
                {
                    yield return 0;
                }

                yield return 0;
                yield break;
            }

            OvenInteractable empty = b.Ovens.FirstOrDefault(o => b.ClerkOf(o) == null && o.IsEmpty);
            BreadTable want = empty != null ? Wanted() : null;

            if (want != null)
            {
                did(true);

                foreach (int _ in Walk(nav, OvenSpot(empty)))
                {
                    yield return 0;
                }

                foreach (int _ in Wait(Tap))
                {
                    yield return 0;
                }

                b.TryChoose(ActionTable.k_Bake, empty, want.Id);
                yield break;
            }

            CounterInteractable counter = b.Counters.Where(c => b.ClerkOf(c) == null && c.Queue.Count > 0).OrderByDescending(c => c.Queue.Count).FirstOrDefault();

            if (counter != null)
            {
                did(true);

                foreach (int _ in Walk(nav, counter.WorkerSpot))
                {
                    yield return 0;
                }

                foreach (int _ in Wait(0.5))
                {
                    yield return 0;
                }

                yield break;
            }

            did(false);
        }

        // 가장 가까운 똥 곁에서 치우기 버튼(곁의 똥 전부 → 거름)
        public IEnumerable<int> Clean(WombatArea area)
        {
            PoopInteractable poop = area.Poops.OrderBy(p => Vector2.Distance(p.Position, Wombat.Mover.Position)).FirstOrDefault();

            if (poop == null)
            {
                yield break;
            }

            foreach (int _ in Walk(NavOf(area), poop.Position))
            {
                yield return 0;
            }

            foreach (int _ in Wait(Tap))
            {
                yield return 0;
            }

            if (!area.TryDo(ActionTable.k_Clean, Wombat.Worker, poop))
            {
                Add("cleanMiss", 1);
            }
        }

        // ---------- 농장 손 ----------

        // 심을 작물: 케이크를 열었고 딸기가 밀의 절반보다 적으면 딸기(열려 있을 때), 아니면 밀
        public string CropFor(FarmArea farm)
        {
            bool cake = Bakery.UnlockedBreads.Any(b => b.Ingredients.Any(i => i.Item == "strawberry"));
            bool berry = farm.UnlockedCrops.Any(c => c.Item == "strawberry");
            return cake && berry && State.Count("strawberry") * 2 < State.Count("wheat") ? "strawberry" : "wheat";
        }

        // 이 층의 익은 밭 거두기 · 빈 밭 심기(가까운 것부터) · 똥 치우기. 곧(wait초 안) 익을 밭이 있으면 한 번만 기다렸다 거둔다
        public IEnumerable<int> TendFarm(FarmArea farm, double wait = 6d)
        {
            BurrowNav nav = farm.Layout.Nav;
            bool waited = false;

            for (int guard = 0; guard < 60; guard++)
            {
                Vector2 at = Wombat.Mover.Position;
                PlotInteractable next = farm.Plots.Where(p => farm.ClerkOf(farm.Barn) == null && (p.IsRipe || p.IsTilled && p.IsEmpty))
                    .OrderBy(p => p.DistanceTo(at)).FirstOrDefault();

                if (next == null && farm.Poops.Count > 0)
                {
                    foreach (int _ in Clean(farm))
                    {
                        yield return 0;
                    }

                    continue;
                }

                if (next == null)
                {
                    PlotInteractable soon = farm.Plots.Where(p => !p.IsEmpty && !p.IsRipe && p.Remaining < wait).OrderBy(p => p.Remaining).FirstOrDefault();

                    if (soon == null || waited)
                    {
                        yield break;
                    }

                    waited = true;

                    foreach (int _ in Walk(nav, nav.Snap(farm.Layout.Cells.CellCenter(soon.Cell))))
                    {
                        yield return 0;
                    }

                    // 발밑 밭은 익는 틱에 바로 거둬 빈 밭이 된다
                    while (!soon.IsRipe && !soon.IsEmpty)
                    {
                        yield return 0;
                    }

                    yield return 0;
                    continue;
                }

                foreach (int _ in Walk(nav, nav.Snap(farm.Layout.Cells.CellCenter(next.Cell))))
                {
                    yield return 0;
                }

                yield return 0;

                if (next.IsTilled && next.IsEmpty)
                {
                    foreach (int _ in Wait(Tap))
                    {
                        yield return 0;
                    }

                    farm.TryChoose(ActionTable.k_Plant, next, CropFor(farm));
                }
            }
        }

        // ---------- 낚시 손 ----------

        // 선 자리에서 던지기 → 물면 reaction초 뒤 낚아채기 → 줄다리기(쉴 때만 감기, 쉼 · 달림이 바뀌면 reelLag초 늦게 따름). seconds 동안
        public IEnumerable<int> Fish(double seconds, double reaction = 0.3, double reelLag = 0.2)
        {
            FishingArea f = Fishing;
            double end = Time + seconds;
            double biteSeen = -1d;
            double runChanged = 0d;
            bool lastRunning = false;

            while (Time < end || f.Phase == FishingPhase.Tug)
            {
                switch (f.Phase)
                {
                    case FishingPhase.Idle:
                        Wombat.SetHolding(false);

                        if (Time < end && !Wombat.Busy && f.TargetAction?.Id == ActionTable.k_Cast)
                        {
                            f.TryInteract();
                        }

                        biteSeen = -1d;
                        break;
                    case FishingPhase.Bite:
                        if (biteSeen < 0d)
                        {
                            biteSeen = Time;
                        }
                        else if (Time - biteSeen >= reaction && f.TargetAction?.Id == ActionTable.k_Strike)
                        {
                            f.TryInteract();
                        }

                        break;
                    case FishingPhase.Tug:
                        if (f.Running != lastRunning)
                        {
                            lastRunning = f.Running;
                            runChanged = Time;
                        }

                        // 사람은 바뀐 것을 reelLag초 늦게 따른다
                        bool rest = Time - runChanged >= reelLag ? !f.Running : f.Running;
                        Wombat.SetHolding(rest);
                        break;
                }

                yield return 0;
            }

            Wombat.SetHolding(false);
        }

        // ---------- 횟집 손 ----------

        // center에서 range 안의 걷는 땅 중 손님 자리(avoid)에서 가장 먼 점(0.2 격자)
        public static Vector2 StandNear(BurrowNav nav, Vector2 center, float range, IReadOnlyList<Vector2> avoid)
        {
            Vector2 best = center;
            float bestScore = float.MinValue;

            for (float dx = -range; dx <= range; dx += 0.2f)
            {
                for (float dy = -range; dy <= range; dy += 0.2f)
                {
                    Vector2 p = nav.Snap(center + new Vector2(dx, dy));

                    if (Vector2.Distance(p, center) > range - 0.1f || !nav.IsWalkable(p))
                    {
                        continue;
                    }

                    float score = avoid.Count == 0 ? -Vector2.Distance(p, center) : avoid.Min(a => Vector2.Distance(a, p));

                    if (score > bestScore)
                    {
                        best = p;
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        // 손맛 횟집: 든 접시 → 그 손님 탁자 · 든 물고기 → 빈 도마 · 놓인 물고기 → 도마 자리에서 뜨기 · 주문 → 수조에서 꺼내기 · 빈 수조 → 채우기
        public IEnumerable<int> Restaurateur()
        {
            RestaurantArea r = Restaurant;
            BurrowNav nav = r.Layout.WombatNav;

            while (true)
            {
                if (Mall.Active != r)
                {
                    foreach (int _ in GoTo(RestaurantArea.k_Id))
                    {
                        yield return 0;
                    }

                    continue;
                }

                Hands hands = Wombat.Worker.Hands;
                RestaurantVisitor order = hands.Order;
                CuttingBoardInteractable cutting = r.Boards.FirstOrDefault(b => b.OnBoard != null);
                TankInteractable fetch = r.Tanks.FirstOrDefault(t => r.NextFetch(t) != null);
                TankInteractable refill = r.Tanks.FirstOrDefault(t => t.CanFill(State));

                if (order != null && order.Cut)
                {
                    foreach (int _ in Walk(nav, StandNear(nav, order.Table.Position, (float)order.Table.Table.Range, r.Layout.Seats(order.Table).Select(seat => seat.Position).ToList())))
                    {
                        yield return 0;
                    }

                    yield return 0;
                }
                else if (order != null || cutting != null)
                {
                    CuttingBoardInteractable board = cutting ?? r.Boards.FirstOrDefault(b => b.OnBoard == null) ?? r.Boards[0];

                    foreach (int _ in Walk(nav, board.WorkerSpot))
                    {
                        yield return 0;
                    }

                    for (double t = 0d; t < 20d && board.OnBoard != null && hands.Empty; t += Dt)
                    {
                        yield return 0;
                    }

                    yield return 0;
                }
                else if (fetch != null || refill != null)
                {
                    TankInteractable tank = fetch ?? refill;

                    foreach (int _ in Walk(nav, StandNear(nav, tank.Position, (float)tank.Table.Range, r.Layout.TankSpots(tank))))
                    {
                        yield return 0;
                    }

                    yield return 0;
                }
                else if (r.Poops.Count > 0)
                {
                    foreach (int _ in Clean(r))
                    {
                        yield return 0;
                    }
                }
                else
                {
                    foreach (int _ in Wait(0.3))
                    {
                        yield return 0;
                    }
                }
            }
        }

        // 둑 위 그 구간 가운데에 선다(물가 0.4 위)
        public IEnumerable<int> ToBank(int stretch)
        {
            FishingLayout layout = Fishing.Layout;
            return Walk(layout.Nav, layout.Nav.Snap(new Vector2(layout.CenterOf(stretch), layout.BankY + 0.4f)));
        }
    }
}
