using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 44: 낚시터(낚시 디펜스). 광장 왼쪽 끝 문과 이 곳 구멍이 이어진다. 굴 속 물길(S자)로 물때마다 물고기 떼가 내려오고(FishingSim),
    // 둑의 말뚝에 꽂은 대가 사거리 안 물고기를 감아 낚는다(창고로). 물때는 waveSeconds마다 떼 하나.
    // 설계 46: 말뚝 시트에서 웜뱃이 계열을 골라 사고 단은 뽑기(tierWeights). 꽂힌 말뚝에서 다시 사면 옛 대는 사라진다.
    // 미끼 노점(오두막)에서 업그레이드 둘(불빛 사거리 · 소용돌이 되돌리기)을 코인으로 올린다. 판의 같은 계열 개수 문턱이 그 계열을 세게 한다.
    // 설계 49 물때 디펜스: 단계는 사지 않는다. wavesPerStage번째 물때 떼 끝에 대물이 나오고, 낚으면 단계 +1(새 계열이 열리고 한 마리 재료가 는다), 놓치면 그대로, 둘 다 물때는 1부터.
    // 별 대신 종류(RodTable 행): 같은 종류 둘을 합치면 그 계열의 윗 종류. 옮기기 · 바꾸기 · 합치기는 낚시판 보기(편집 모드)에서 끌어서(TryMoveRod)
    // 웜뱃은 물가에서 엉덩이 쿵, 붙잡힌 월척에 털썩(ActionFactory.Fishing.cs). 점원 하나가 오두막에서 기다리다 붙잡힌 월척을 바로 건진다(2026-10-06 사용자).
    // 놓는 사물은 없다(편집 카드 없음). 물고기는 웜뱃이 다른 곳에 있어도 흐른다.
    // 설계 45: 물길은 처음 절반쯤만 파여 있고 끝은 막혀 있다(끝에 닿은 물고기는 빠져나가 놓침). 막다른 끝 앞에서 코인으로 정해진 길을 한 칸씩 더 판다. 대는 말뚝 위쪽만 본다
    public sealed class FishingArea : WombatArea
    {
        public const string k_Id = "fishing";

        private static readonly IPlacedKind[] s_noKinds = new IPlacedKind[0];

        private readonly List<StakeInteractable> m_stakes = new List<StakeInteractable>();
        private readonly PassageInteractable m_exit;
        // 떼 안에서 나올 차례를 기다리는 물고기(나올 때 · 물고기)
        private readonly List<(double at, Fish fish)> m_queue = new List<(double, Fish)>();
        private readonly Dictionary<string, FishRecord> m_log = new Dictionary<string, FishRecord>();
        // 미끼 노점 업그레이드 id → 단계(없으면 0)
        private readonly Dictionary<string, int> m_upgrades = new Dictionary<string, int>();
        private double m_clock;
        private double m_waveElapsed;
        // 나왔거나 나올 차례를 기다리는 대물(낚이거나 빠져나갈 때까지 다음 떼는 오지 않는다)
        private Fish m_boss;

        public FishingConfigTable Config { get; }
        public FishingLayout Layout { get; }
        public IReadOnlyList<StakeInteractable> Stakes => m_stakes;
        public StreamBankInteractable Bank { get; }
        // 미끼 노점: 점원 자리이자 웜뱃이 단계를 올리는 곳
        public FishingHutInteractable Hut { get; }
        // 설계 45: 막다른 끝 앞 땅(파기 버튼). 판 횟수 · 다음 파기 값 · 끝까지 팠나
        public StreamEndInteractable StreamEnd { get; }
        public int Dug { get; private set; }
        public double DigPrice => Config.DigCost * Math.Pow(Config.DigGrowth, Dug);
        public bool StreamFull => Layout.Length >= Layout.PlanLength - 1e-3f;
        internal FishingSim Sim { get; }
        public IReadOnlyList<Fish> Fish => Sim.Fish;
        // 단계(1부터, 대물을 낚으면 오른다) · 이 단계의 몇 번째 물때인가(1 ~ wavesPerStage, 마지막이 대물) · 산 대 수(값이 오른다)
        public int Stage { get; private set; } = 1;
        public int Wave { get; private set; }
        public bool BossWave => m_boss != null;
        // 한 마리 재료 개수(단계 보람)
        public int Yield => 1 + (Stage - 1) / Config.YieldEvery;
        public int Summons { get; private set; }
        // 엉덩이 쿵을 다시 하기까지 남은 초
        public double ThumpLeft { get; private set; }
        public double SummonPrice => Config.SummonCost * Math.Pow(Config.SummonGrowth, Summons);
        public IReadOnlyDictionary<string, int> Upgrades => m_upgrades;
        // 소용돌이: 되돌리는 간격(0 = 없음)
        public double WhirlInterval
        {
            get
            {
                FishingUpgradeData whirl = Upgrade(FishingUpgradeData.k_Whirl);
                int level = LevelOf(whirl.Id);
                return level > 0 ? whirl.EffectBase - whirl.EffectPerLevel * (level - 1) : 0d;
            }
        }
        // 어종 id → 기록(도감 화면은 다음, 기록은 지금부터)
        public IReadOnlyDictionary<string, FishRecord> Log => m_log;
        public override IReadOnlyList<IPlacedKind> ShopKinds => s_noKinds;
        // 설계 49: 놓는 사물은 없지만 편집 모드가 낚시판 보기다
        public override bool Editable => true;
        public override string Id => k_Id;

        protected override BurrowNav WombatNav => Layout.Nav;
        protected override Vector2 Entrance => Layout.HoleFloor;
        protected override BurrowShape.Result Shape => Layout.Shape;

        protected override IEnumerable<IPlaced> PlacedThings
        {
            get { yield break; }
        }

        public FishingArea(TableSet tables, IRandom random, Wombat wombat, EventBus bus) : base(tables, random, wombat, bus)
        {
            Config = tables.Get<FishingConfigTable>(FishingConfigTable.k_Main);
            Layout = new FishingLayout(tables);
            Sim = new FishingSim(this);
            Sim.Caught += Sim_Caught;
            Sim.Escaped += Sim_Escaped;
            Sim.Hooked += stake => stake.Refresh();
            Sim.Whirled += (fish, from) => Bus.Publish(new Events.FishWhirled(this, fish, from));
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, PlazaArea.k_Id);
            Bank = new StreamBankInteractable(Row(StreamBankInteractable.k_Id), this);
            Hut = new FishingHutInteractable(Row(FishingHutInteractable.k_Id), this, Layout.HutSpot);
            StreamEnd = new StreamEndInteractable(Row(StreamEndInteractable.k_Id), this);

            for (int i = 0; i < Config.Stakes.Length; i++)
            {
                FishingStakeData stake = Config.Stakes[i];
                m_stakes.Add(new StakeInteractable(Row(StakeInteractable.k_Id), this, i, new Vector2((float)stake.X, (float)stake.Y), stake.Cost));
            }

            m_waveElapsed = Config.WaveSeconds;
            SyncThings();
        }

        public override IEnumerable<Interactable> ClerkSlots
        {
            get { yield return Hut; }
        }

        internal override Interactable FixedClerkSlot => Hut;

        internal override bool IsPassage(Vector2 p)
        {
            return Vector2.DistanceSquared(p, Layout.HoleFloor) < 1e-4f;
        }

        // ---------- 대 ----------

        // 설계 45: 대는 말뚝 위쪽 반원만 본다(줄이 땅을 가로지르지 않게). 감기 · 미끼 · 소용돌이가 다 이 규칙
        internal bool InReach(StakeInteractable stake, Vector2 p, float range)
        {
            return p.Y >= stake.Position.Y && Vector2.Distance(p, stake.Position) <= range;
        }

        // 사거리: 대 사거리 × (1 + 불빛 업그레이드)
        public float RangeOf(StakeInteractable stake)
        {
            return (float)(stake.Rod.Range * (1d + Upgrade(FishingUpgradeData.k_Light).EffectPerLevel * LevelOf(FishingUpgradeData.k_Light)));
        }

        // 초당 감는 힘: 종류 × 계열 문턱 × 크기 × 큰 놈 잡이(월척 · 대물)
        internal double ReelOf(StakeInteractable stake, Fish fish)
        {
            RodTable rod = stake.Rod;
            double reel = rod.Reel * FamilyScale(rod.Family);
            reel *= fish.Big ? rod.BigScale : fish.Size == 0 ? rod.SmallScale : 1d;
            return rod.BigGame > 0d && (fish.Trophy || fish.Boss) ? reel * rod.BigGame : reel;
        }

        // 판의 같은 계열 개수가 넘은 가장 큰 문턱의 배수(없으면 1)
        public double FamilyScale(string family)
        {
            int count = m_stakes.Count(s => s.Rod != null && s.Rod.Family == family);
            double scale = 1d;

            for (int i = 0; i < Config.FamilySteps.Length; i++)
            {
                if (count >= Config.FamilySteps[i])
                {
                    scale = Config.FamilyScales[i];
                }
            }

            return scale;
        }

        // 설계 49: 시트에 나오는 계열(첫 종류, 지금 단계에 열린 것만, 표 순서)
        public IEnumerable<RodTable> ShopRods => Tables.GetAll<RodTable>().Where(rod => rod.Tier == 1 && rod.UnlockStage <= Stage);

        // 그 계열의 그 단 종류(없으면 null)
        public RodTable KindOf(string family, int tier)
        {
            return Tables.GetAll<RodTable>().FirstOrDefault(rod => rod.Family == family && rod.Tier == tier);
        }

        // 같은 종류 둘을 합치면 되는 윗 종류(맨 윗 단이면 null)
        public RodTable NextOf(RodTable rod)
        {
            return KindOf(rod.Family, rod.Tier + 1);
        }

        // 열린 말뚝이면 비었든 꽂혔든 산다(월척을 붙잡은 동안은 못 바꾼다)
        internal bool CanSummon(StakeInteractable stake)
        {
            return stake.Open && stake.Hooked == null;
        }

        // 고른 계열의 대를 산다(단 뽑기: 가끔 윗 종류가 바로 나온다). 꽂혀 있던 대는 사라진다
        internal bool Summon(StakeInteractable stake, RodTable rod)
        {
            if (!CanSummon(stake) || !ShopRods.Contains(rod) || !Wombat.Worker.Wallet.TrySpendCoins(SummonPrice))
            {
                return false;
            }

            Summons++;
            stake.Put(KindOf(rod.Family, PickTier()) ?? rod);
            Bus.Publish(new Events.RodSummoned(stake));
            return true;
        }

        private int PickTier()
        {
            double roll = Random.NextDouble() * Config.TierWeights.Sum();

            for (int i = 0; i < Config.TierWeights.Length; i++)
            {
                roll -= Config.TierWeights[i];

                if (roll < 0d)
                {
                    return i + 1;
                }
            }

            return 1;
        }

        // 설계 49 낚시판 보기: from의 대를 to에 놓는다. 빈 말뚝이면 옮기고, 같은 종류면 합쳐 윗 종류가 되고(from은 빈다), 다른 종류면 자리를 바꾼다.
        // 월척을 붙잡은 대 · 잠긴 말뚝은 안 된다
        public bool TryMoveRod(StakeInteractable from, StakeInteractable to)
        {
            if (from == to || from.Rod == null || !to.Open || from.Hooked != null || to.Hooked != null)
            {
                return false;
            }

            RodTable moved = from.Rod;
            RodTable merged = to.Rod == moved ? NextOf(moved) : null;

            if (to.Rod == moved && merged == null)
            {
                return false;
            }

            from.Put(merged != null ? null : to.Rod);
            to.Put(merged ?? moved);

            if (merged != null)
            {
                Bus.Publish(new Events.RodMerged(to, from));
            }

            return true;
        }

        // p에서 radius 안의 가장 가까운 말뚝(낚시판 보기의 손가락, 없으면 null)
        public StakeInteractable StakeAt(Vector2 p, float radius)
        {
            StakeInteractable best = m_stakes.OrderBy(s => Vector2.Distance(s.Position, p)).First();
            return Vector2.Distance(best.Position, p) <= radius ? best : null;
        }

        internal bool CanOpen(StakeInteractable stake)
        {
            return !stake.Open && Wombat.Worker.Wallet.Coins >= stake.Cost;
        }

        internal void OpenStake(StakeInteractable stake)
        {
            TryOpenStake(stake);
        }

        // 잠긴 말뚝을 값을 치르고 연다(말뚝 앞 버튼 · 낚시판 보기의 탭)
        public bool TryOpenStake(StakeInteractable stake)
        {
            if (!CanOpen(stake) || !Wombat.Worker.Wallet.TrySpendCoins(stake.Cost))
            {
                return false;
            }

            stake.Unlock();
            return true;
        }

        // ---------- 웜뱃 동작 ----------

        // 털썩: 붙잡힌 월척을 바로 낚는다
        internal bool CanHaul(StakeInteractable stake)
        {
            return stake.Hooked != null;
        }

        internal void Haul(StakeInteractable stake)
        {
            // 먼저 세워야 낚은 사건(화면)이 웜뱃 털썩인 줄 안다
            if (CanHaul(stake))
            {
                Wombat.Hold(stake.Position, Config.HaulSeconds);
                Sim.Land(stake);
            }
        }

        // 점원이 붙잡힌 월척을 건진다(웜뱃 동작 없이)
        internal void ClerkHaul(StakeInteractable stake)
        {
            Sim.Land(stake);
        }

        // 점원이 갈 말뚝: 가장 오래 붙잡힌 월척부터(번호 순이면 뒤 말뚝이 굶는다). 없으면 null
        internal StakeInteractable HookedStake()
        {
            return m_stakes.Where(s => s.Hooked != null).OrderByDescending(s => s.Hooked.HookedFor).FirstOrDefault();
        }

        internal bool CanDigStream => !StreamFull && Wombat.Worker.Wallet.Coins >= DigPrice;

        // 설계 45: 정해진 길을 digStep 더 판다. 웜뱃은 판 쪽을 보고 파는 동작, 새 물에 서 있었으면 가까운 땅으로 비킨다
        internal void DigStream()
        {
            if (!CanDigStream || !Wombat.Worker.Wallet.TrySpendCoins(DigPrice))
            {
                return;
            }

            float from = Layout.Length;
            Wombat.Dig(StreamEnd.Position);
            Dug++;
            ApplyDug();
            Bus.Publish(new Events.StreamDug(this, from, Layout.Length));
        }

        private void ApplyDug()
        {
            Layout.SetDug((float)(Config.DugStart + Config.DigStep * Dug));

            if (WombatPresent && !Layout.Nav.IsWalkable(Layout.Nav.Snap(Wombat.Mover.Position))
                && Layout.Nav.TryNearestFree(Wombat.Mover.Position, _ => false, (float)Config.StreamWidth * 2f, out Vector2 free))
            {
                Wombat.Mover.Place(free);
            }

            SyncThings();
        }

        internal bool CanThump => ThumpLeft <= 0d;

        // 엉덩이 쿵: 웜뱃 둘레 물고기가 멈추고, 웜뱃은 물 쪽을 보고 잠깐 제자리
        internal void Thump()
        {
            if (!CanThump)
            {
                return;
            }

            Vector2 at = Wombat.Mover.Position;
            Sim.Stun(at, (float)Config.ThumpRadius, Config.ThumpStun);
            ThumpLeft = Config.ThumpCooldown;
            Wombat.Hold(Layout.NearestOnStream(at), Config.ThumpSeconds);
            Bus.Publish(new Events.Thumped(this, at));
        }

        public FishingUpgradeData Upgrade(string id) => Config.HutUpgrades.First(u => u.Id == id);

        public int LevelOf(string id) => m_upgrades.TryGetValue(id, out int level) ? level : 0;

        // 다음 단계 값(최대면 무한)
        public double UpgradePrice(string id)
        {
            FishingUpgradeData upgrade = Upgrade(id);
            int level = LevelOf(id);
            return level >= upgrade.MaxLevel ? double.PositiveInfinity : upgrade.BaseCost * Math.Pow(upgrade.CostGrowth, level);
        }

        // 설계 46 미끼 노점 업그레이드 한 단계
        internal bool BuyUpgrade(string id)
        {
            if (!Wombat.Worker.Wallet.TrySpendCoins(UpgradePrice(id)))
            {
                return false;
            }

            m_upgrades[id] = LevelOf(id) + 1;
            Bus.Publish(new Events.Upgraded(this, FishingHutInteractable.k_Id));
            return true;
        }

        // ---------- 물때 ----------

        protected override void TickArea(double dt)
        {
            ThumpLeft = Math.Max(0d, ThumpLeft - dt);
            m_clock += dt;

            while (m_queue.Count > 0 && m_queue[0].at <= m_clock)
            {
                Sim.Spawn(m_queue[0].fish);
                m_queue.RemoveAt(0);
            }

            // 대물이 낚이거나 빠져나갈 때까지 다음 떼는 오지 않는다
            if (m_boss == null)
            {
                m_waveElapsed += dt;
            }

            if (m_waveElapsed >= Config.WaveSeconds)
            {
                m_waveElapsed = 0d;
                NextWave();
            }

            Sim.Tick(dt);
        }

        // 다음 물때 떼. wavesPerStage번째면 떼 끝에 대물이 따라 나온다
        private void NextWave()
        {
            Wave++;
            List<(double delay, Fish fish)> school = MakeSchool(Random);

            foreach ((double delay, Fish fish) in school)
            {
                m_queue.Add((m_clock + delay, fish));
            }

            Bus.Publish(new Events.WaveStarted(this));

            if (Wave >= Config.WavesPerStage)
            {
                FishingBossData boss = Config.Boss;
                m_boss = new Fish(Tables.Get<FishTable>(boss.Fish), 2, false, boss.Weight * StageScale, true, boss.SpeedScale);
                m_queue.Add((m_clock + school.Count * Config.SpawnGap, m_boss));
                Bus.Publish(new Events.BossSpawned(this));
            }
        }

        // 치트 · 검증: 단계를 정하고 물때를 1부터 · 다음 틱에 대물 물때를 연다(이미 대물이 있으면 그대로)
        public void SetStageNow(int stage)
        {
            Stage = Math.Max(1, stage);
            Wave = 0;
        }

        public void CallBossNow()
        {
            if (m_boss == null)
            {
                Wave = Config.WavesPerStage - 1;
                m_waveElapsed = Config.WaveSeconds;
            }
        }

        private double StageScale => Math.Pow(Config.WeightGrowth, Stage - 1);

        // 지금 단계의 떼 하나(나올 때까지 초 · 물고기)
        internal List<(double delay, Fish fish)> MakeSchool(IRandom random)
        {
            List<FishTable> kinds = Tables.GetAll<FishTable>().Where(f => f.Spawn > 0d).ToList();
            double total = kinds.Sum(f => f.Spawn);
            int count = Config.SchoolBase + (int)Math.Floor((Stage - 1) * Config.SchoolPerStage);
            List<(double, Fish)> school = new List<(double, Fish)>();

            for (int i = 0; i < count; i++)
            {
                FishTable kind = Pick(kinds, f => f.Spawn, total, random);
                FishSizeData[] sizes = kind.Sizes;
                int size = PickIndex(sizes.Select(s => s.Chance).ToArray(), random);
                double weight = (sizes[size].Min + random.NextDouble() * (sizes[size].Max - sizes[size].Min)) * StageScale;
                bool trophy = random.NextDouble() < Config.TrophyChance;
                school.Add((i * Config.SpawnGap, new Fish(kind, size, trophy, trophy ? weight * Config.TrophyWeightScale : weight)));
            }

            return school;
        }

        private static FishTable Pick(List<FishTable> kinds, Func<FishTable, double> weight, double total, IRandom random)
        {
            double roll = random.NextDouble() * total;

            foreach (FishTable kind in kinds)
            {
                roll -= weight(kind);

                if (roll < 0d)
                {
                    return kind;
                }
            }

            return kinds[kinds.Count - 1];
        }

        private static int PickIndex(double[] weights, IRandom random)
        {
            double roll = random.NextDouble() * weights.Sum();

            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];

                if (roll < 0d)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        private void Sim_Caught(Fish fish, StakeInteractable stake)
        {
            int count = CatchCount(fish);
            Wombat.Worker.Wallet.AddItem(fish.Kind.Item, count);

            if (!fish.Boss)
            {
                Record(fish);
            }
            Bus.Publish(new Events.FishCaught(this, fish, count, stake));

            // 설계 49: 대물을 낚으면 다음 단계(물때는 1부터)
            if (fish == m_boss)
            {
                m_boss = null;
                Wave = 0;
                Stage++;
                Bus.Publish(new Events.StageRaised(this));
            }
        }

        private void Sim_Escaped(Fish fish)
        {
            Bus.Publish(new Events.FishEscaped(this, fish));

            if (fish == m_boss)
            {
                m_boss = null;
                Wave = 0;
                Bus.Publish(new Events.BossEscaped(this));
            }
        }

        // 낚은 물고기 한 마리의 재료 개수: (대물 · 월척 · 보통) × 단계 보람 + 그 자리를 사거리에 둔 덤 대(꿀떡밥)
        private int CatchCount(Fish fish)
        {
            int count = (fish.Boss ? Config.Boss.Catch : fish.Trophy ? Config.TrophyCatch : 1) * Yield;
            Vector2 at = Layout.PointAt(fish.S);
            return count + m_stakes.Where(s => s.Rod != null && s.Rod.Bonus > 0 && InReach(s, at, RangeOf(s))).Sum(s => s.Rod.Bonus);
        }

        private void Record(Fish fish)
        {
            if (!m_log.TryGetValue(fish.Kind.Id, out FishRecord record))
            {
                record = new FishRecord();
                m_log[fish.Kind.Id] = record;
            }

            record.Caught++;
            record.Sizes |= 1 << fish.Size;
            record.Best = Math.Max(record.Best, fish.Weight);
        }

        // ---------- 오프라인 ----------

        // 지금 판으로 물때 하나를 따로 돌려 낚는 재료(재료 id → 개수). 웜뱃 없이, 월척은 점원이 있을 때만(clerk) 걸리는 순간 건진다(걷는 시간은 뺀 어림).
        // 대물은 나오지 않는다(오프라인은 단계가 오르지 않는다)
        internal Dictionary<string, double> SimulateWave(bool clerk)
        {
            const double k_Step = 0.1;
            // 미끼 · 소용돌이로 늦어져도 끝나게 넉넉히 기다리는 배수(남은 물고기가 없으면 일찍 끝난다)
            const double k_Slack = 10d;
            Dictionary<string, double> made = new Dictionary<string, double>();
            FishingSim sim = new FishingSim(this);
            sim.Caught += (fish, _) => made[fish.Kind.Item] = (made.TryGetValue(fish.Kind.Item, out double n) ? n : 0d) + CatchCount(fish);

            if (clerk)
            {
                sim.Hooked += sim.Land;
            }

            List<(double delay, Fish fish)> school = MakeSchool(new SeededRandom(Stage));
            double slowest = school.Count == 0 ? 0d : school.Min(f => f.fish.Kind.Speed);
            double end = school.Count == 0 ? 0d : school[school.Count - 1].delay + Layout.Length / slowest * k_Slack;
            int next = 0;

            for (double t = 0d; t <= end && (next < school.Count || sim.Fish.Any(f => f.HookedBy == null)); t += k_Step)
            {
                while (next < school.Count && school[next].delay <= t)
                {
                    sim.Spawn(school[next].fish);
                    next++;
                }

                sim.Tick(k_Step);
            }

            return made;
        }

        // ---------- 저장 ----------

        // grade: 옛 저장(설계 46)의 별 등급. 2 이상이면 그 계열의 그 단 종류로 옮긴다
        internal void Restore(int stage, int summons, IReadOnlyDictionary<string, int> upgrades, int dug, IEnumerable<(bool open, RodTable rod, int grade)> stakes, IReadOnlyDictionary<string, FishRecord> log)
        {
            Stage = Math.Max(1, stage);
            Summons = summons;

            m_upgrades.Clear();

            foreach (FishingUpgradeData upgrade in Config.HutUpgrades)
            {
                if (upgrades != null && upgrades.TryGetValue(upgrade.Id, out int level) && level > 0)
                {
                    m_upgrades[upgrade.Id] = Math.Min(level, upgrade.MaxLevel);
                }
            }

            Dug = Math.Max(0, dug);
            ApplyDug();
            int i = 0;

            foreach ((bool open, RodTable rod, int grade) in stakes)
            {
                if (i >= m_stakes.Count)
                {
                    break;
                }

                if (open && !m_stakes[i].Open)
                {
                    m_stakes[i].Unlock();
                }

                m_stakes[i].Put(rod != null && grade > 1 ? KindOf(rod.Family, grade) ?? rod : rod);
                i++;
            }

            SyncThings();

            foreach (KeyValuePair<string, FishRecord> pair in log)
            {
                m_log[pair.Key] = pair.Value;
            }
        }

        // ---------- 사물 목록 ----------

        protected override IPlacedKind KindOf(string kindId)
        {
            throw new InvalidOperationException("낚시터에는 놓는 사물이 없다.");
        }

        protected override IPlaced Create(IPlacedKind kind, Vector2 at)
        {
            throw new InvalidOperationException("낚시터에는 놓는 사물이 없다.");
        }

        protected override void Destroy(IPlaced thing)
        {
            throw new InvalidOperationException("낚시터에는 놓는 사물이 없다.");
        }

        protected override void OnPlacementChanged()
        {
            SyncThings();
        }

        protected override void OnPoopsChanged()
        {
            SyncThings();
        }

        protected override void OnClerkThingsChanged()
        {
            SyncThings();
        }

        // 사물 목록: 딴짓 점원 → 똥 → 말뚝 → 막다른 끝(더 팔 수 있을 때) → 미끼 노점 → 나가기 → 물가(물가는 말뚝 · 통로에 대상을 양보한다)
        private void SyncThings()
        {
            Placed.Clear();
            Placed.AddRange(ClerkThings);
            Placed.AddRange(Poops);
            Placed.AddRange(m_stakes);

            if (!StreamFull)
            {
                Placed.Add(StreamEnd);
            }

            Placed.Add(Hut);
            Placed.Add(m_exit);
            Placed.Add(Bank);
        }

        private InteractableTable Row(string interactableId)
        {
            return Tables.Get<InteractableTable>(interactableId);
        }
    }

    // 설계 44: 어종 하나의 기록(도감 화면은 다음). Sizes = 본 크기 비트(1 작은 · 2 보통 · 4 큰)
    public sealed class FishRecord
    {
        public int Caught;
        public int Sizes;
        public double Best;
    }
}
