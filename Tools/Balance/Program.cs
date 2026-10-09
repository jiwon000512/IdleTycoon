using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZooTycoon.Core;

namespace Balance
{
    // 밸런스 측정(2026-10-08 밸런스방): 진짜 Core를 Unity 밖에서 돌려 곳마다 속도를 잰다. 결과는 출력 폴더 rates.json(명령마다 한 칸)에 쌓는다.
    // 사용: dotnet run -c Release --project Tools/Balance -- <명령|all> [출력 폴더(기본 임시 폴더 wombat-balance)]
    // 명령: solo(손맛 빵집 + 농장) · clerks(점원 자동 · 오프라인 공식) · farm(농장 점원) · eval(별 평가 연달아) · fish(손 낚시 · 잔해) · seats(좌대 손님) ·
    // restaurant(손맛 횟집) · week(「보통 플레이」 한 주 타임라인, Player.cs) · weeks(씨앗 2~5) · checks(문제 A/B) ·
    // guide(퀘스트 사슬을 따라가는 손, 씨앗 1~5) · guide2(「빵집 별 5」를 새 맛 앞으로) · guide3(guide2 + 「좌대 놓기」를 바위 앞으로) · all
    internal static class Program
    {
        private static readonly Dictionary<string, Func<object>> s_jobs = new Dictionary<string, Func<object>>
        {
            ["solo"] = Measure.Solo,
            ["clerks"] = Measure.Clerks,
            ["farm"] = Measure.FarmClerk,
            ["eval"] = Measure.Eval,
            ["fish"] = Measure.Fish,
            ["seats"] = Measure.Seats,
            ["restaurant"] = Measure.Restaurant,
            ["week"] = () => Week.Run(),
            ["weeks"] = () => Enumerable.Range(2, 4).Select(seed => Week.Run(7, seed)).ToList(),
            ["checks"] = Measure.Checks,
            ["guide"] = () => Enumerable.Range(1, 5).Select(seed => Week.Run(7, seed, true)).ToList(),
            ["guide2"] = () => Enumerable.Range(1, 5).Select(seed => Week.Run(7, seed, true, 1)).ToList(),
            ["guide3"] = () => Enumerable.Range(1, 5).Select(seed => Week.Run(7, seed, true, 2)).ToList(),
        };

        private static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            string cmd = args.Length > 0 ? args[0] : "all";
            string outDir = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "wombat-balance");
            Directory.CreateDirectory(outDir);
            string file = Path.Combine(outDir, "rates.json");
            JObject all = File.Exists(file) ? JObject.Parse(File.ReadAllText(file)) : new JObject();

            // quest<이름> [출력 폴더] [시안 JSON]: 진짜 퀘스트 사슬 · 오늘의 일을 따라가는 한 주(씨앗 1~5). 시안이 없으면 보상 0(바탕선).
            // 이름에 save가 있으면 저축 손(걸음 값을 남기며 벌기), 아니면 보통 플레이를 하며 알약을 따르는 손
            if (cmd.StartsWith("quest"))
            {
                Action<GameKit.Tables.TableSet> edit = Proposal.Load(args.Length > 2 ? args[2] : null);
                bool invest = !cmd.Contains("save");
                // [씨앗 첫 번호] [씨앗 수]: 판을 여러 프로세스로 나눠 돌릴 때
                int first = args.Length > 3 ? int.Parse(args[3]) : 1;
                int count = args.Length > 4 ? int.Parse(args[4]) : 5;
                all[cmd] = JToken.FromObject(Enumerable.Range(first, count).Select(seed => Week.Run(7, seed, quests: true, edit: edit, invest: invest)).ToList());
                File.WriteAllText(file, all.ToString(Formatting.Indented));
                return;
            }

            foreach (KeyValuePair<string, Func<object>> job in s_jobs.Where(j => cmd == "all" || cmd == j.Key))
            {
                Stopwatch watch = Stopwatch.StartNew();
                all[job.Key] = JToken.FromObject(job.Value());
                File.WriteAllText(file, all.ToString(Formatting.Indented));
                Console.WriteLine($"{job.Key}: {watch.Elapsed.TotalSeconds:F0}초");
                Console.WriteLine(all[job.Key].ToString(Formatting.None));
            }
        }
    }

    // 퀘스트 · 오늘의 일 값 시안을 표에 덮는다(파일은 그대로, 이 판의 TableSet만).
    // { "quests": { "q01": { "coins", "count", "item", "itemCount" } }, "missions": { "m_sell": { "count", "weight", "points", "area", "needs" } },
    //   "chestMinutes": [5, 10, 15], "chestPoints": [20, 60, 100], "perMinute": { "1": 100, ... }, "config": { "missionsPerDay": 5 } }. 없으면 퀘스트 · 상자 코인 0
    internal static class Proposal
    {
        public static Action<GameKit.Tables.TableSet> Load(string path)
        {
            JObject p = path == null ? null : JObject.Parse(File.ReadAllText(path));
            // "zero": true면 적지 않은 퀘스트 · 상자 코인은 0(순서만 바꿔 볼 때)
            bool zero = p == null || p["zero"]?.Value<bool>() == true;

            return tables =>
            {
                // 순서 바꿔 보기: "order": [id ...](빠진 행은 뒤에 그대로), "chapters": { id: 장 }
                if (p?["order"] is JArray order)
                {
                    List<QuestTable> rows = (List<QuestTable>)tables.GetAll<QuestTable>();
                    List<string> ids = order.Select(t => t.Value<string>()).ToList();
                    List<QuestTable> sorted = ids.Select(id => rows.First(r => r.Id == id)).Concat(rows.Where(r => !ids.Contains(r.Id))).ToList();
                    rows.Clear();
                    rows.AddRange(sorted);
                }

                foreach (QuestTable q in tables.GetAll<QuestTable>())
                {
                    q.Chapter = p?["chapters"]?[q.Id]?.Value<int>() ?? q.Chapter;
                    JToken v = p?["quests"]?[q.Id];
                    q.Coins = v?["coins"]?.Value<double>() ?? (zero ? 0d : q.Coins);
                    q.Count = v?["count"]?.Value<int>() ?? q.Count;

                    if (v?["item"] != null)
                    {
                        q.Item = v["item"].Type == JTokenType.Null ? null : v["item"].Value<string>();
                        q.ItemCount = v["itemCount"]?.Value<int>() ?? 0;
                    }
                }

                foreach (MissionTable m in tables.GetAll<MissionTable>())
                {
                    JToken v = p?["missions"]?[m.Id];

                    if (v == null)
                    {
                        continue;
                    }

                    m.Count = v["count"]?.Value<int>() ?? m.Count;
                    m.Weight = v["weight"]?.Value<double>() ?? m.Weight;
                    m.Points = v["points"]?.Value<int>() ?? m.Points;
                    m.Area = v["area"] == null ? m.Area : v["area"].Type == JTokenType.Null ? null : v["area"].Value<string>();
                    m.Needs = v["needs"] == null ? m.Needs : v["needs"].Type == JTokenType.Null ? null : v["needs"].Value<string>();
                }

                foreach (MissionChestTable c in tables.GetAll<MissionChestTable>())
                {
                    c.Minutes = p?["chestMinutes"]?[c.Chest - 1]?.Value<double>() ?? (zero ? 0d : c.Minutes);
                    c.Points = p?["chestPoints"]?[c.Chest - 1]?.Value<int>() ?? c.Points;
                    c.CoinsPerMinute = p?["perMinute"]?[c.Chapter.ToString()]?.Value<double>() ?? c.CoinsPerMinute;
                }

                foreach (JProperty kv in (p?["config"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
                {
                    tables.Get<ConfigTable>(kv.Name).Value = kv.Value.Value<double>();
                }
            };
        }
    }

    // 진행 단계 배치(빵 · 진열대 · 오븐 · 계산대 · 업그레이드 단계 · 별)
    internal sealed class Stage
    {
        public string Name;
        public int Breads, Shelves, Ovens, Counters, Upgrade, Stars;

        public static readonly Stage[] All =
        {
            new Stage { Name = "S0 시작", Breads = 1, Shelves = 1, Ovens = 1, Counters = 1, Upgrade = 0, Stars = 0 },
            new Stage { Name = "S1 크루아상", Breads = 2, Shelves = 2, Ovens = 2, Counters = 1, Upgrade = 2, Stars = 2 },
            new Stage { Name = "S2 케이크", Breads = 3, Shelves = 3, Ovens = 3, Counters = 2, Upgrade = 6, Stars = 4 },
            new Stage { Name = "S3 다 산", Breads = 3, Shelves = 4, Ovens = 4, Counters = 3, Upgrade = 10, Stars = 10 },
        };

        public void Apply(Sim sim)
        {
            BakeryArea b = sim.Bakery;
            sim.Unlock(Breads);
            sim.AddStars(Stars);
            sim.Place(b, b.Grid, b.Layout.Cells, ShelfInteractable.k_Id, Shelves);
            sim.Place(b, b.Grid, b.Layout.Cells, OvenInteractable.k_Id, Ovens);
            sim.Place(b, b.Grid, b.Layout.Cells, CounterInteractable.k_Id, Counters);

            foreach (string id in new[] { ShelfInteractable.k_Id, OvenInteractable.k_Id, CounterInteractable.k_Id })
            {
                sim.Level(b, id, Upgrade);
            }
        }
    }

    internal static class Measure
    {
        private const int k_Seeds = 3;

        private static double PerMin(Sim sim, string key, double seconds)
        {
            return Math.Round(sim[key] / seconds * 60d, 2);
        }

        // 손맛(웜뱃 혼자): 단계 배치에서 빵집 일 + 밀이 모자라면 농장 다녀오기. 밀은 시작 창고 그대로(S0) · 넉넉(나머지)
        public static object Solo()
        {
            List<object> rows = new List<object>();

            foreach (Stage stage in Stage.All)
            {
                foreach (int plots in new[] { 2, 6 })
                {
                    Dictionary<string, double> sum = new Dictionary<string, double>();
                    const double seconds = 1200d;

                    for (int seed = 1; seed <= k_Seeds; seed++)
                    {
                        Sim sim = new Sim(seed, Sim.LiftCaps);
                        stage.Apply(sim);
                        sim.Fields(sim.Farm, plots);

                        if (stage.Breads >= 3)
                        {
                            sim.Farm.UnlockCrop(sim.Tables.GetAll<CropTable>()[1]);
                        }

                        sim.Brain(SoloBrain(sim));
                        sim.Run(seconds);

                        foreach (KeyValuePair<string, double> pair in sim.Counts)
                        {
                            sum[pair.Key] = (sum.TryGetValue(pair.Key, out double v) ? v : 0d) + pair.Value / k_Seeds;
                        }
                    }

                    rows.Add(new
                    {
                        stage = stage.Name,
                        plots,
                        coinsPerMin = Math.Round((Get(sum, "bakery") + Get(sum, "tips")) / seconds * 60d, 1),
                        servedPerMin = Math.Round(Get(sum, "served") / seconds * 60d, 2),
                        arrivedPerMin = Math.Round(Get(sum, "arrived") / seconds * 60d, 2),
                        gaveUpPerMin = Math.Round(Get(sum, "gaveUp") / seconds * 60d, 2),
                        wheatPerMin = Math.Round(Get(sum, "harvest:wheat") / seconds * 60d, 2),
                        farmShare = Math.Round(Get(sum, "farmSeconds") / seconds, 2),
                        gemsPerMin = Math.Round(Get(sum, "gem:harvest") / seconds * 60d, 3),
                        manurePerMin = Math.Round(Get(sum, "manure") / seconds * 60d, 2),
                    });
                }
            }

            return rows;
        }

        private static double Get(Dictionary<string, double> sum, string key)
        {
            return sum.TryGetValue(key, out double v) ? v : 0d;
        }

        // 웜뱃 혼자: 빵집 일이 없으면 밀 · 딸기가 모자랄 때 농장에 다녀온다(거두고 심기). 걸린 시간은 farmSeconds
        private static IEnumerable<int> SoloBrain(Sim sim)
        {
            while (true)
            {
                bool did = false;

                foreach (int _ in sim.GoTo(BakeryArea.k_Id))
                {
                    yield return 0;
                }

                foreach (int _ in sim.BakeryChore(d => did = d))
                {
                    yield return 0;
                }

                if (did)
                {
                    continue;
                }

                if (NeedsFarm(sim) && sim.Bakery.Counters.All(c => c.Queue.Count <= 1))
                {
                    double start = sim.Time;

                    foreach (int _ in sim.GoTo(FarmArea.k_Id))
                    {
                        yield return 0;
                    }

                    foreach (int _ in sim.TendFarm(sim.Farm))
                    {
                        yield return 0;
                    }

                    foreach (int _ in sim.GoTo(BakeryArea.k_Id))
                    {
                        yield return 0;
                    }

                    sim.Add("farmSeconds", sim.Time - start);
                    sim.Add("farmTrips", 1);
                    continue;
                }

                // 할 일이 없으면 계산대 자리
                CounterInteractable counter = sim.Bakery.Counter;

                foreach (int _ in sim.Walk(sim.Bakery.Layout.WombatNav, counter.WorkerSpot))
                {
                    yield return 0;
                }

                foreach (int _ in sim.Wait(0.5))
                {
                    yield return 0;
                }
            }
        }

        // 밀이 두 판(가장 많이 드는 빵) 아래이거나 케이크용 딸기가 모자라고, 농장에 거둘 · 심을 밭이 있다
        private static bool NeedsFarm(Sim sim)
        {
            int wheat = sim.Bakery.UnlockedBreads.Max(b => b.Ingredients.Where(i => i.Item == "wheat").Sum(i => i.Count)) * 2;
            bool cake = sim.Bakery.UnlockedBreads.Any(b => b.Ingredients.Any(i => i.Item == "strawberry"));
            bool low = sim.State.Count("wheat") < wheat || cake && sim.State.Count("strawberry") < 4;
            return low && sim.Farm.Plots.Any(p => p.IsRipe || p.IsTilled && p.IsEmpty);
        }

        // 점원 자동(손 놓음): 단계 배치마다 일머리 30 · 60 · 100, 씨앗 셋 × 20분 → 분당 판매(팁 포함) · 월급, 같은 배치의 오프라인 공식(Offline.Settle 20분)
        public static object Clerks()
        {
            List<object> rows = new List<object>();
            const double seconds = 1200d;

            foreach (Stage stage in Stage.All)
            {
                foreach (int skill in new[] { 30, 60, 100 })
                {
                    double sales = 0d, wages = 0d, served = 0d, gaveUp = 0d;

                    for (int seed = 1; seed <= k_Seeds; seed++)
                    {
                        Sim sim = Staffed(stage, skill, seed);
                        sim.Run(60d);
                        sim.ResetCounts();
                        sim.Run(seconds);
                        sales += sim["bakery"] + sim["tips"];
                        wages += sim["wages"];
                        served += sim["served"];
                        gaveUp += sim["gaveUp"];
                    }

                    Sim model = Staffed(stage, skill, 1);
                    OfflineReport report = Offline.Settle(model.State, model.Mall, model.Tables, seconds);
                    double perMin = 60d / seconds / k_Seeds;
                    rows.Add(new
                    {
                        stage = stage.Name,
                        skill,
                        salesPerMin = Math.Round(sales * perMin, 1),
                        wagesPerMin = Math.Round(wages * perMin, 1),
                        servedPerMin = Math.Round(served * perMin, 2),
                        gaveUpPerMin = Math.Round(gaveUp * perMin, 2),
                        offlineSalesPerMin = Math.Round(report.Sales / seconds * 60d, 1),
                        offlineWagesPerMin = Math.Round(report.Wages / seconds * 60d, 1),
                    });
                }
            }

            return rows;
        }

        // 농장 점원(손 놓음): 1층 밭 2 · 6 · 12칸 × 일머리 30 · 60 · 100, 밀만 → 분당 밀 · 반짝돌, 같은 배치의 오프라인 공식
        public static object FarmClerk()
        {
            List<object> rows = new List<object>();
            const double seconds = 1200d;

            foreach (int plots in new[] { 2, 6, 12 })
            {
                foreach (int skill in new[] { 30, 60, 100 })
                {
                    double wheat = 0d, gems = 0d, wages = 0d;

                    for (int seed = 1; seed <= k_Seeds; seed++)
                    {
                        Sim sim = Farmed(plots, skill, seed);
                        sim.Run(60d);
                        sim.ResetCounts();
                        sim.Run(seconds);
                        wheat += sim["harvest:wheat"];
                        gems += sim["gem:harvest"];
                        wages += sim["wages"];
                    }

                    Sim model = Farmed(plots, skill, 1);
                    OfflineReport report = Offline.Settle(model.State, model.Mall, model.Tables, seconds);
                    double perMin = 60d / seconds / k_Seeds;
                    rows.Add(new
                    {
                        plots,
                        skill,
                        wheatPerMin = Math.Round(wheat * perMin, 1),
                        gemsPerMin = Math.Round(gems * perMin, 2),
                        wagesPerMin = Math.Round(wages * perMin, 1),
                        offlineWheatPerMin = Math.Round((report.Items.TryGetValue("wheat", out int w) ? w : 0) / seconds * 60d, 1),
                        offlineGemsPerMin = Math.Round((report.Items.TryGetValue("gem", out int g) ? g : 0) / seconds * 60d, 2),
                    });
                }
            }

            return rows;
        }

        private static Sim Farmed(int plots, int skill, int seed)
        {
            Sim sim = new Sim(seed);
            sim.Fields(sim.Farm, plots);
            sim.Hire(sim.Farm, sim.Farm.Barn, skill, "wheat");
            sim.Park();
            return sim;
        }

        // 별 평가 연달아(손맛, 재료 넉넉): 단계 배치에서 진열대가 반 넘게 차면 평가판에서 부른다. 45분 또는 ★20까지 → 별마다 걸린 초 · 통과
        public static object Eval()
        {
            List<object> rows = new List<object>();

            foreach (Stage stage in Stage.All.Take(3))
            {
                for (int seed = 1; seed <= 2; seed++)
                {
                    Sim sim = new Sim(seed, Sim.LiftCaps);
                    stage.Apply(sim);
                    sim.State.Stars.Restore(new Dictionary<string, int> { [BakeryArea.k_Id] = 0 });
                    sim.Plenty("wheat", "strawberry");
                    List<object> log = new List<object>();
                    double started = 0d;
                    sim.Bus.Subscribe<Events.EvaluationStarted>(e => started = sim.Time);
                    sim.Bus.Subscribe<Events.EvaluationEnded>(e => log.Add(new
                    {
                        star = e.Evaluation.NextStar - (e.Passed ? 1 : 0),
                        passed = e.Passed,
                        at = Math.Round(sim.Time),
                        seconds = Math.Round(sim.Time - started, 1),
                        goals = string.Join(" ", e.Evaluation.Goals.Select(g => $"{g.Kind}{g.Bread?.Id}:{g.Progress}/{g.Target}")),
                    }));
                    sim.Brain(EvalBrain(sim));
                    sim.Run(2700d, () => sim.State.Stars.Count(BakeryArea.k_Id) >= 20);
                    rows.Add(new { stage = stage.Name, seed, stars = sim.State.Stars.Count(BakeryArea.k_Id), minutes = Math.Round(sim.Time / 60d, 1), log });
                }
            }

            return rows;
        }

        private static IEnumerable<int> EvalBrain(Sim sim)
        {
            BakeryArea b = sim.Bakery;
            BurrowNav nav = b.Layout.WombatNav;
            Vector2 board = Sim.StandNear(nav, new Vector2((float)b.Evaluation.Config.BoardX, (float)b.Evaluation.Config.BoardY), 0.8f, new List<Vector2>());

            while (true)
            {
                bool did = false;

                foreach (int _ in sim.BakeryChore(d => did = d))
                {
                    yield return 0;
                }

                if (did)
                {
                    continue;
                }

                if (b.Evaluation.CanStart && b.Shelves.Sum(s => s.Stock) * 2 >= b.Shelves.Sum(s => s.Capacity))
                {
                    foreach (int _ in sim.Walk(nav, board))
                    {
                        yield return 0;
                    }

                    foreach (int _ in sim.Wait(Sim.Tap))
                    {
                        yield return 0;
                    }

                    b.Evaluation.TryStart();
                    continue;
                }

                foreach (int _ in sim.Walk(nav, b.Counter.WorkerSpot))
                {
                    yield return 0;
                }

                foreach (int _ in sim.Wait(0.5))
                {
                    yield return 0;
                }
            }
        }

        // 손 낚시(반응 0.3초 · 감기 바꿈 0.2초 늦게): 구간 처음 · 하류 1 · 상류 1에서 20분 → 분당 마리 · 어종 · 놓침 · 끊김. 잔해 한 조각 초
        public static object Fish()
        {
            List<object> rows = new List<object>();
            const double seconds = 1200d;

            foreach (int stretch in new[] { 0, 1, -1 })
            {
                Dictionary<string, double> sum = new Dictionary<string, double>();

                for (int seed = 1; seed <= k_Seeds; seed++)
                {
                    Sim sim = new Sim(seed);
                    OpenTo(sim, stretch);
                    sim.Brain(FishBrain(sim, stretch, seconds));
                    sim.Run(seconds + 120d);

                    foreach (KeyValuePair<string, double> pair in sim.Counts)
                    {
                        sum[pair.Key] = Get(sum, pair.Key) + pair.Value / k_Seeds;
                    }
                }

                rows.Add(new
                {
                    stretch,
                    fishPerMin = Math.Round(Get(sum, "fish") / seconds * 60d, 2),
                    minnow = Math.Round(Get(sum, "fish:fish_minnow") / seconds * 60d, 2),
                    crucian = Math.Round(Get(sum, "fish:fish_crucian") / seconds * 60d, 2),
                    catfish = Math.Round(Get(sum, "fish:fish_catfish") / seconds * 60d, 2),
                    missedPerMin = Math.Round(Get(sum, "fishEnd:Missed") / seconds * 60d, 2),
                    snappedPerMin = Math.Round(Get(sum, "fishEnd:Snapped") / seconds * 60d, 2),
                });
            }

            // 잔해: 하류 1 바위만 뚫고 댐 앞 둑에서 조각을 다 끌어낸다
            Sim debris = new Sim(1);
            debris.Fishing.OpenNextFree();
            double start = 0d;
            debris.Brain(DebrisBrain(debris, () => start = debris.Time));
            debris.Run(600d, () => debris.Fishing.Flooded.Contains(1));
            rows.Add(new { debrisPieces = debris["debris"] / debris.Tables.Get<FishingConfigTable>(FishingConfigTable.k_Main).DebrisCoins, seconds = Math.Round(debris.Time - start, 1) });
            return rows;
        }

        // 구간까지 바위를 값 없이 뚫고 댐을 허문다(측정용)
        private static void OpenTo(Sim sim, int stretch)
        {
            while (!sim.Fishing.Flooded.Contains(stretch))
            {
                if (!sim.Fishing.BreakDamNow())
                {
                    sim.Fishing.OpenNextFree();
                }
            }
        }

        private static IEnumerable<int> FishBrain(Sim sim, int stretch, double seconds)
        {
            foreach (int _ in sim.GoTo(FishingArea.k_Id))
            {
                yield return 0;
            }

            foreach (int _ in sim.ToBank(stretch))
            {
                yield return 0;
            }

            sim.ResetCounts();
            sim.Time = 0d;

            foreach (int _ in sim.Fish(seconds))
            {
                yield return 0;
            }

            while (true)
            {
                yield return 0;
            }
        }

        private static IEnumerable<int> DebrisBrain(Sim sim, Action started)
        {
            FishingArea f = sim.Fishing;

            foreach (int _ in sim.GoTo(FishingArea.k_Id))
            {
                yield return 0;
            }

            foreach (int _ in sim.Walk(f.Layout.Nav, f.Layout.Nav.Snap(new Vector2(f.Layout.InnerEdge(1) + 0.4f, f.Layout.BankY + 0.4f))))
            {
                yield return 0;
            }

            started();
            double runChanged = 0d;
            bool lastRunning = false;

            while (true)
            {
                if (f.Phase == FishingPhase.Idle && !sim.Wombat.Busy && f.TargetAction?.Id == ActionTable.k_PullDebris)
                {
                    f.TryInteract();
                }
                else if (f.Phase == FishingPhase.Tug)
                {
                    if (f.Running != lastRunning)
                    {
                        lastRunning = f.Running;
                        runChanged = sim.Time;
                    }

                    sim.Wombat.SetHolding(sim.Time - runChanged >= 0.2 ? !f.Running : f.Running);
                }

                yield return 0;
            }
        }

        // 좌대 손님: 좌대 1 · 2 · 4 · 6개(처음 → 하류 → 상류, 물은 값 없이 채움). 빵집은 일머리 100 점원(광장 흐름이 막히지 않게), 웜뱃은 농장에
        public static object Seats()
        {
            List<object> rows = new List<object>();
            const double seconds = 1200d;

            foreach (int seats in new[] { 1, 2, 4, 6 })
            {
                double coins = 0d, served = 0d;

                for (int seed = 1; seed <= k_Seeds; seed++)
                {
                    Sim sim = new Sim(seed, Sim.LiftCaps);
                    Stage.All[0].Apply(sim);
                    sim.Plenty("wheat");
                    sim.StaffBakery(100);
                    OpenTo(sim, seats > 4 ? -1 : seats > 2 ? 1 : 0);

                    for (int i = 0; i < seats; i++)
                    {
                        sim.Fishing.PlaceSeatFree();
                    }

                    sim.Bus.Publish(new Events.Passed(sim.Mall.Active, FarmArea.k_Id));
                    sim.Run(60d);
                    sim.ResetCounts();
                    sim.Run(seconds);
                    coins += sim["seats"];
                    served += sim["served"];
                }

                double perMin = 60d / seconds / k_Seeds;
                rows.Add(new { seats, seatCoinsPerMin = Math.Round(coins * perMin, 1), bakeryServedPerMin = Math.Round(served * perMin, 2) });
            }

            return rows;
        }

        // 손맛 횟집(★5 · 열기 값 대 줌, 물고기 넉넉): 시작 배치 · 늘린 배치 × 물고기 섞임(처음 구간 피라미 5 : 붕어 3 · 셋 다). 빵집은 일머리 100 점원
        public static object Restaurant()
        {
            List<object> rows = new List<object>();
            const double seconds = 1200d;

            foreach (bool expanded in new[] { false, true })
            {
                foreach (bool allFish in new[] { false, true })
                {
                    double coins = 0d, dishes = 0d, angry = 0d, served = 0d;

                    for (int seed = 1; seed <= k_Seeds; seed++)
                    {
                        Sim sim = new Sim(seed, Sim.LiftCaps);
                        Stage.All[0].Apply(sim);
                        sim.Plenty("wheat");
                        sim.StaffBakery(100);
                        sim.AddStars(5);
                        sim.State.AddCoins(sim.Restaurant.Config.OpenCost);
                        sim.Restaurant.TryOpen(sim.Wombat.Worker);
                        sim.State.AddItem("fish_minnow", 5000);
                        sim.State.AddItem("fish_crucian", 3000);

                        if (allFish)
                        {
                            sim.State.AddItem("fish_catfish", 3000);
                        }

                        if (expanded)
                        {
                            RestaurantArea r = sim.Restaurant;
                            sim.Place(r, r.Grid, r.Layout.Cells, TankInteractable.k_Id, 2);
                            sim.Place(r, r.Grid, r.Layout.Cells, CuttingBoardInteractable.k_Id, 2);
                            sim.Place(r, r.Grid, r.Layout.Cells, DiningTableInteractable.k_Id, 4);
                            sim.Level(r, TankInteractable.k_Id, 2);
                            sim.Level(r, CuttingBoardInteractable.k_Id, 4);
                        }

                        sim.Brain(sim.Restaurateur());
                        sim.Run(60d);
                        sim.ResetCounts();
                        sim.Run(seconds);
                        coins += sim["restaurant"];
                        dishes += sim["dishes"];
                        angry += sim["restaurantAngry"];
                        served += sim["served"];
                    }

                    double perMin = 60d / seconds / k_Seeds;
                    rows.Add(new
                    {
                        expanded,
                        allFish,
                        coinsPerMin = Math.Round(coins * perMin, 1),
                        dishesPerMin = Math.Round(dishes * perMin, 2),
                        angryPerMin = Math.Round(angry * perMin, 2),
                        bakeryServedPerMin = Math.Round(served * perMin, 2),
                    });
                }
            }

            return rows;
        }

        // 눈에 띈 문제 A/B 확인: ① 케이크 오븐 점원 + 딸기 0이면 오프라인 빵집이 멈추나(시뮬과 비교) ② 물고기 없는 횟집을 열면 빵집 손님이 주나
        public static object Checks()
        {
            List<object> rows = new List<object>();
            const double seconds = 3600d;

            foreach (int berries in new[] { 0, 1000 })
            {
                Sim model = Staffed(Stage.All[2], 60, 1);
                model.State.TrySpendItem("strawberry", model.State.Count("strawberry") - berries);
                OfflineReport report = Offline.Settle(model.State, model.Mall, model.Tables, seconds);
                Sim sim = Staffed(Stage.All[2], 60, 1);
                sim.State.TrySpendItem("strawberry", sim.State.Count("strawberry") - berries);
                sim.Run(seconds);
                rows.Add(new { check = "offlineCake", berries, offlineSalesPerMin = Math.Round(report.Sales / 60d, 1), simSalesPerMin = Math.Round((sim["bakery"] + sim["tips"]) / 60d, 1) });
            }

            foreach ((bool restaurant, int seats) in new[] { (false, 0), (true, 0), (false, 4), (true, 4) })
            {
                double arrived = 0d, plaza = 0d;

                for (int seed = 1; seed <= k_Seeds; seed++)
                {
                    Sim sim = Staffed(Stage.All[0], 100, seed);

                    if (seats > 0)
                    {
                        OpenTo(sim, 1);

                        for (int i = 0; i < seats; i++)
                        {
                            sim.Fishing.PlaceSeatFree();
                        }
                    }

                    if (restaurant)
                    {
                        sim.AddStars(5);
                        sim.State.AddCoins(sim.Restaurant.Config.OpenCost);
                        sim.Restaurant.TryOpen(sim.Wombat.Worker);
                    }

                    sim.Run(60d);
                    sim.ResetCounts();

                    for (int i = 0; i < 120; i++)
                    {
                        sim.Run(10d);
                        plaza += sim.Plaza.Visitors.Count / 120d / k_Seeds;
                    }

                    arrived += sim["arrived"] / k_Seeds;
                }

                rows.Add(new { check = "emptyRestaurant", restaurant, seats, bakeryArrivedPerMin = Math.Round(arrived / 20d, 2), plazaVisitors = Math.Round(plaza, 1) });
            }

            return rows;
        }

        private static Sim Staffed(Stage stage, int skill, int seed)
        {
            Sim sim = new Sim(seed, Sim.LiftCaps);
            stage.Apply(sim);
            sim.Plenty("wheat", "strawberry");
            sim.StaffBakery(skill);
            sim.Park();
            return sim;
        }
    }
}
