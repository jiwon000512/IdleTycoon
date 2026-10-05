using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 44: 낚시터(낚시 디펜스). 광장 왼쪽 끝 문과 이 곳 구멍이 이어진다. 굴 속 물길(S자)로 물때마다 물고기 떼가 내려오고(FishingSim),
    // 둑의 말뚝에 꽂은 대가 사거리 안 물고기를 감아 낚는다(창고로). 물때 bossEvery번째는 대물: 낚으면 다음 단계 + 특별한 대 3택 1, 놓치면 같은 단계 물때 1부터.
    // 대는 빈 말뚝에서 코인으로 소환(계열 무작위 · 등급 비중)하고, 같은 계열 · 등급 셋을 합쳐 올린다. 판의 같은 계열 개수 문턱이 그 계열을 세게 한다.
    // 웜뱃은 대를 들고 옮기고, 물가에서 엉덩이 쿵, 붙잡힌 월척에 털썩, 대물을 감는 말뚝 곁에 앉는다(ActionFactory.Fishing.cs). 점원 하나가 오두막에서 합치기 · 오래 버틴 월척을 맡는다.
    // 놓는 사물은 없다(편집 카드 없음). 물고기는 웜뱃이 다른 곳에 있어도 흐른다
    public sealed class FishingArea : WombatArea
    {
        public const string k_Id = "fishing";

        private static readonly IPlacedKind[] s_noKinds = new IPlacedKind[0];

        private readonly List<StakeInteractable> m_stakes = new List<StakeInteractable>();
        private readonly PassageInteractable m_exit;
        // 떼 안에서 나올 차례를 기다리는 물고기(나올 때 · 물고기)
        private readonly List<(double at, Fish fish)> m_queue = new List<(double, Fish)>();
        private readonly Dictionary<string, FishRecord> m_log = new Dictionary<string, FishRecord>();
        private RodTable[] m_choice;
        private StakeInteractable m_carryFrom;
        private StakeInteractable m_sit;
        private double m_clock;
        private double m_waveElapsed;
        private bool m_bossOut;

        public FishingConfigTable Config { get; }
        public FishingLayout Layout { get; }
        public IReadOnlyList<StakeInteractable> Stakes => m_stakes;
        public StreamBankInteractable Bank { get; }
        public FishingHutInteractable Hut { get; }
        // 낚은 대물(뱃속 3택 1이 남은 동안만 사물 목록에)
        public BossCatchInteractable Catch { get; }
        internal FishingSim Sim { get; }
        public IReadOnlyList<Fish> Fish => Sim.Fish;
        // 단계(1부터, 대물을 낚을 때마다 +1) · 물때(1 ~ bossEvery, 시작 전 0) · 이번 물때에 놓친 수 · 소환한 수
        public int Stage { get; private set; } = 1;
        public int Wave { get; private set; }
        public int Missed { get; private set; }
        public int Summons { get; private set; }
        public bool BossOut => m_bossOut;
        // 엉덩이 쿵을 다시 하기까지 남은 초
        public double ThumpLeft { get; private set; }
        // 웜뱃이 든 대(없으면 null)와 그 등급
        public RodTable Carried { get; private set; }
        public int CarriedGrade { get; private set; }
        // 웜뱃이 곁에 앉은 말뚝(앉아 있을 때만)
        public StakeInteractable SitStake => WombatPresent && Wombat.Sitting ? m_sit : null;
        // 대물 뱃속 3택 1(없으면 null)
        public IReadOnlyList<RodTable> Choice => m_choice;
        public double SummonPrice => Config.SummonCost * Math.Pow(Config.SummonGrowth, Summons);
        // 어종 id → 기록(도감 화면은 다음, 기록은 지금부터)
        public IReadOnlyDictionary<string, FishRecord> Log => m_log;
        public override IReadOnlyList<IPlacedKind> ShopKinds => s_noKinds;
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
            Sim = new FishingSim(this, true);
            Sim.Caught += Sim_Caught;
            Sim.Escaped += Sim_Escaped;
            Sim.Hooked += stake => stake.Refresh();
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, PlazaArea.k_Id);
            Bank = new StreamBankInteractable(Row(StreamBankInteractable.k_Id), this);
            Hut = new FishingHutInteractable(Row(FishingHutInteractable.k_Id), this, Layout.HutSpot);
            Catch = new BossCatchInteractable(Row(BossCatchInteractable.k_Id), this, new Vector2((float)Config.CatchX, (float)Config.CatchY));

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

        // 사거리: 대 사거리 × (1 + 다른 말뚝 등대의 비율 합)
        public float RangeOf(StakeInteractable stake)
        {
            double boost = m_stakes.Where(s => s != stake && s.Rod != null && s.Rod.Special == RodTable.k_Lighthouse).Sum(s => s.Rod.Effect);
            return (float)(stake.Rod.Range * (1d + boost));
        }

        // 초당 감는 힘: 대 × 등급 × 계열 문턱 × 크기. 대물이면 도르래탑 배수와 (live) 앉은 말뚝 배수
        internal double ReelOf(StakeInteractable stake, Fish fish, bool live)
        {
            RodTable rod = stake.Rod;
            double reel = rod.Reel * Math.Pow(Config.GradeScale, stake.Grade - 1) * FamilyScale(rod.Family);
            reel *= fish.Big ? rod.BigScale : fish.Size == 0 ? rod.SmallScale : 1d;

            if (fish.Boss)
            {
                reel *= rod.Special == RodTable.k_Pulley ? rod.Effect : 1d;
                reel *= live && SitStake == stake ? Config.SitScale : 1d;
            }

            return reel;
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

        internal bool CanSummon(StakeInteractable stake)
        {
            return stake.Open && stake.Rod == null && Carried == null && Wombat.Worker.Wallet.Coins >= SummonPrice;
        }

        // 빈 말뚝에 대 하나: 계열은 소환에 나오는 것 중 같은 비중, 등급은 gradeWeights 비중
        internal void Summon(StakeInteractable stake)
        {
            if (!CanSummon(stake) || !Wombat.Worker.Wallet.TrySpendCoins(SummonPrice))
            {
                return;
            }

            List<RodTable> families = Tables.GetAll<RodTable>().Where(rod => !rod.IsSpecial && !rod.Locked).ToList();
            RodTable kind = families[Math.Min(families.Count - 1, (int)(Random.NextDouble() * families.Count))];
            Summons++;
            stake.Put(kind, PickGrade());
            Bus.Publish(new Events.RodSummoned(stake));
        }

        private int PickGrade()
        {
            double roll = Random.NextDouble() * Config.GradeWeights.Sum();

            for (int i = 0; i < Config.GradeWeights.Length; i++)
            {
                roll -= Config.GradeWeights[i];

                if (roll < 0d)
                {
                    return i + 1;
                }
            }

            return 1;
        }

        internal bool CanOpen(StakeInteractable stake)
        {
            return !stake.Open && Wombat.Worker.Wallet.Coins >= stake.Cost;
        }

        internal void OpenStake(StakeInteractable stake)
        {
            if (CanOpen(stake) && Wombat.Worker.Wallet.TrySpendCoins(stake.Cost))
            {
                stake.Unlock();
            }
        }

        // 같은 계열 · 등급이 셋(mergeCount) 모였나: 누른 말뚝 + 가까운 순 짝들. 특별한 대 · 마지막 등급 · 월척을 붙잡은 대는 안 된다
        internal bool CanMerge(StakeInteractable stake)
        {
            return Carried == null && MergePartners(stake) != null;
        }

        // 웜뱃 버튼 · 점원 공용(점원은 웜뱃이 대를 들고 있어도 합친다)
        internal void Merge(StakeInteractable stake)
        {
            List<StakeInteractable> partners = MergePartners(stake);

            if (partners == null)
            {
                return;
            }

            foreach (StakeInteractable partner in partners)
            {
                partner.Put(null, 0);
            }

            stake.RaiseGrade();
            Bus.Publish(new Events.RodsMerged(stake, partners.ToArray()));
        }

        internal List<StakeInteractable> MergePartners(StakeInteractable stake)
        {
            if (stake.Rod == null || stake.Rod.IsSpecial || stake.Grade >= Config.GradeWeights.Length || stake.Hooked != null)
            {
                return null;
            }

            List<StakeInteractable> partners = m_stakes
                .Where(s => s != stake && s.Rod == stake.Rod && s.Grade == stake.Grade && s.Hooked == null)
                .OrderBy(s => Vector2.Distance(s.Position, stake.Position))
                .Take(Config.MergeCount - 1)
                .ToList();
            return partners.Count == Config.MergeCount - 1 ? partners : null;
        }

        // 판에 합칠 수 있는 말뚝이 있으면 하나(점원)
        internal StakeInteractable MergeableStake()
        {
            return m_stakes.FirstOrDefault(s => MergePartners(s) != null);
        }

        // 들기(든 것 없을 때, 꽂힌 대) · 놓기(빈 말뚝) · 맞바꾸기(꽂힌 말뚝: 든 대를 꽂고, 원래 대는 들었던 말뚝으로, 그 말뚝이 없으면 손에)
        internal bool CanCarry(StakeInteractable stake)
        {
            return stake.Open && stake.Hooked == null && (Carried != null || stake.Rod != null);
        }

        internal void Carry(StakeInteractable stake)
        {
            if (!CanCarry(stake))
            {
                return;
            }

            if (Carried == null)
            {
                Carried = stake.Rod;
                CarriedGrade = stake.Grade;
                m_carryFrom = stake;
                stake.Put(null, 0);
                return;
            }

            RodTable old = stake.Rod;
            int oldGrade = stake.Grade;
            stake.Put(Carried, CarriedGrade);
            Carried = null;

            if (old == null)
            {
                m_carryFrom = null;
            }
            else if (m_carryFrom != null && m_carryFrom.Rod == null && m_carryFrom != stake)
            {
                m_carryFrom.Put(old, oldGrade);
                m_carryFrom = null;
            }
            else
            {
                Carried = old;
                CarriedGrade = oldGrade;
                m_carryFrom = stake;
            }
        }

        // ---------- 웜뱃 동작 ----------

        // 털썩: 붙잡힌 월척이면 바로 낚고, 대물을 감는 말뚝이면 곁에 앉는다
        internal bool CanHaul(StakeInteractable stake)
        {
            return stake.Hooked != null || stake.Rod != null && !Wombat.Sitting && BossInRange(stake);
        }

        internal void Haul(StakeInteractable stake)
        {
            if (stake.Hooked != null)
            {
                Sim.Land(stake);
                Wombat.Hold(stake.Position, Config.HaulSeconds);
            }
            else if (CanHaul(stake))
            {
                m_sit = stake;
                Wombat.Sit(stake.Position);
            }
        }

        // 점원이 오래 버틴 월척을 거든다(웜뱃 동작 없이)
        internal void ClerkHaul(StakeInteractable stake)
        {
            Sim.Land(stake);
        }

        // 붙잡은 지 trophyHoldSeconds가 지난 월척의 말뚝(점원)
        internal StakeInteractable OverdueTrophy()
        {
            return m_stakes.FirstOrDefault(s => s.Hooked != null && s.Hooked.HookedFor >= Config.TrophyHoldSeconds);
        }

        private bool BossInRange(StakeInteractable stake)
        {
            return Fish.Any(f => f.Boss && Vector2.Distance(Layout.PointAt(f.S), stake.Position) <= RangeOf(stake));
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

        // 빈 말뚝이 있거나 손이 비었나(고를 수 있나)
        public bool CanChoose => m_choice != null && (Carried == null || m_stakes.Any(s => s.Open && s.Rod == null));

        // 대물 뱃속 3택 1에서 고른다: 빈 열린 말뚝에 꽂고, 없으면 웜뱃이 든다. 말뚝이 다 찼고 이미 대를 들고 있으면 고르지 못한다(false, 3택 1은 남는다)
        public bool Choose(int index)
        {
            if (m_choice == null || index < 0 || index >= m_choice.Length)
            {
                return false;
            }

            RodTable rod = m_choice[index];
            StakeInteractable empty = m_stakes.FirstOrDefault(s => s.Open && s.Rod == null);

            if (empty == null && Carried != null)
            {
                return false;
            }

            m_choice = null;
            SyncThings();

            if (empty != null)
            {
                empty.Put(rod, 1);
            }
            else
            {
                Carried = rod;
                CarriedGrade = 1;
                m_carryFrom = null;
            }

            return true;
        }

        // 웜뱃이 나가면 들던 대는 들었던 말뚝으로(그 말뚝이 찼으면 빈 말뚝, 없으면 계속 든다)
        protected override void OnWombatLeft()
        {
            m_sit = null;

            if (Carried == null)
            {
                return;
            }

            StakeInteractable home = m_carryFrom != null && m_carryFrom.Rod == null ? m_carryFrom : m_stakes.FirstOrDefault(s => s.Open && s.Rod == null);

            if (home != null)
            {
                home.Put(Carried, CarriedGrade);
                Carried = null;
                m_carryFrom = null;
            }
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

            if (!m_bossOut)
            {
                m_waveElapsed += dt;

                if (m_waveElapsed >= Config.WaveSeconds)
                {
                    m_waveElapsed = 0d;
                    NextWave();
                }
            }

            Sim.Tick(dt);
        }

        private void NextWave()
        {
            Wave = Wave % Config.BossEvery + 1;
            Missed = 0;

            if (Wave == Config.BossEvery)
            {
                FishTable boss = Tables.Get<FishTable>(Config.BossFish);
                FishSizeData size = boss.Sizes[0];
                Sim.Spawn(new Fish(boss, 0, false, size.Min * StageScale));
                m_bossOut = true;
                Bus.Publish(new Events.WaveStarted(this, true));
                return;
            }

            foreach ((double delay, Fish fish) in MakeSchool(Random))
            {
                m_queue.Add((m_clock + delay, fish));
            }

            Bus.Publish(new Events.WaveStarted(this, false));
        }

        private double StageScale => Math.Pow(Config.WeightGrowth, Stage - 1);

        // 지금 단계의 떼 하나(나올 때까지 초 · 물고기)
        internal List<(double delay, Fish fish)> MakeSchool(IRandom random)
        {
            List<FishTable> kinds = Tables.GetAll<FishTable>().Where(f => !f.Boss && f.Spawn > 0d).ToList();
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
            int count = fish.Trophy ? Config.TrophyCatch : 1;
            Wombat.Worker.Wallet.AddItem(fish.Kind.Item, count);
            Record(fish);
            Bus.Publish(new Events.FishCaught(this, fish, count, stake));

            if (fish.Boss)
            {
                Stage++;
                m_choice = Tables.GetAll<RodTable>().Where(rod => rod.IsSpecial).OrderBy(_ => Random.NextDouble()).Take(3).ToArray();
                EndBoss(true);
                SyncThings();
                Bus.Publish(new Events.RodChoiceReady(this));
            }
        }

        private void Sim_Escaped(Fish fish)
        {
            Bus.Publish(new Events.FishEscaped(this, fish));

            if (fish.Boss)
            {
                EndBoss(false);
            }
            else
            {
                Missed++;
            }
        }

        // 대물이 끝나면 다음 틱에 물때 1이 나온다
        private void EndBoss(bool caught)
        {
            m_bossOut = false;
            Wave = 0;
            m_waveElapsed = Config.WaveSeconds;
            Bus.Publish(new Events.BossResolved(this, caught));
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

        // 지금 판으로 물때 하나를 따로 돌려 낚는 재료(재료 id → 개수). 웜뱃 없이, 월척은 점원이 있을 때만(clerk)
        internal Dictionary<string, double> SimulateWave(bool clerk)
        {
            const double k_Step = 0.1;
            // 미끼 · 소용돌이로 늦어져도 끝나게 넉넉히 기다리는 배수(남은 물고기가 없으면 일찍 끝난다)
            const double k_Slack = 10d;
            Dictionary<string, double> made = new Dictionary<string, double>();
            FishingSim sim = new FishingSim(this, false);
            sim.Caught += (fish, _) => made[fish.Kind.Item] = (made.TryGetValue(fish.Kind.Item, out double n) ? n : 0d) + (fish.Trophy ? Config.TrophyCatch : 1);
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

            if (clerk)
            {
                foreach (Fish fish in sim.Fish.Where(f => f.HookedBy != null))
                {
                    made[fish.Kind.Item] = (made.TryGetValue(fish.Kind.Item, out double n) ? n : 0d) + Config.TrophyCatch;
                }
            }

            return made;
        }

        // ---------- 저장 ----------

        internal RodTable ChoiceOrNull(int i) => m_choice != null && i < m_choice.Length ? m_choice[i] : null;

        internal void Restore(int stage, int summons, IEnumerable<(bool open, RodTable rod, int grade)> stakes, RodTable carried, int carriedGrade,
            IReadOnlyList<RodTable> choice, IReadOnlyDictionary<string, FishRecord> log)
        {
            Stage = Math.Max(1, stage);
            Summons = summons;
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

                m_stakes[i].Put(rod, grade);
                i++;
            }

            Carried = carried;
            CarriedGrade = carried == null ? 0 : Math.Max(1, carriedGrade);
            m_choice = choice != null && choice.Count > 0 ? choice.ToArray() : null;
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

        // 사물 목록: 딴짓 점원 → 똥 → 낚은 대물(있을 때) → 말뚝 → 나가기 → 물가(물가는 말뚝 · 통로에 대상을 양보한다). 오두막은 대상이 아니다
        private void SyncThings()
        {
            Placed.Clear();
            Placed.AddRange(ClerkThings);
            Placed.AddRange(Poops);

            if (m_choice != null)
            {
                Placed.Add(Catch);
            }

            Placed.AddRange(m_stakes);
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
