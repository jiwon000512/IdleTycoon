using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 52 우물 낚시 1회차(사용자 2026-10-07 「디펜스식은 손맛도 재고도 없다」): 광장 왼쪽 끝 문 → 굴 속 물가. 웜뱃이 물가에 서서 물 쪽으로 던지면(Cast) 찌가 뜨고,
    // 가짜 입질 몇 번 뒤 물면(Bite) 짧은 틈 안에 낚아채야 걸린다(Strike → Tug). 줄다리기: 물고기가 쉴 때 버튼을 누르고 있으면 줄이 감기고(거리 ↓),
    // 달릴 때 누르고 있으면 긴장이 올라 줄 힘에 닿으면 끊긴다(놓으면 긴장이 내리고 물고기가 줄을 조금 되찾는다). 거리 0이면 낚아 재료 하나가 창고로.
    // 기다림 · 입질 중에 조이스틱을 움직이면 줄을 거둔다(줄다리기 중에는 무시).
    // 설계 61 지하 호수의 섬들(사용자 2026-10-14 「작게 시작해 넓히는 구조 · 맵 변주 · 전제 C」): 낚시터는 호수 위 섬들(FishingIslandTable)이다. 처음은 구멍 아래 작은 뭍(0)과
    // 보이기만 하는 다음 섬. 다리 자리에 코인을 내면 다리가 놓여 그 섬에 닿고(걷는 땅 · 좌대 · 물고기) 그다음 섬이 보인다. 섬마다 볼 것: 그림자(명물이 뭍 아래를 오가고,
    // 찌가 가까우면 바로 문다) · 상자(떠내려와 「끌어내기」 줄다리기로 끌어내면 코인). 좌대를 놓으면 낚시터는 광장 손님을 받는 가게가 되고(IShop), 손님은 좌대에 앉아 낚으며 코인을 낸다
    public sealed class FishingArea : WombatArea, IShop
    {
        public const string k_Id = "fishing";

        private static readonly IPlacedKind[] s_noKinds = new IPlacedKind[0];
        // 한 틱 동안 웜뱃을 물 쪽으로 세워 두는 초(매 틱 다시 건다)
        private const double k_HoldSeconds = 0.5;
        // 놓치거나 끊기면 잠깐 서 있는 초: 그 사이의 버튼은 던지기가 아니다(늦은 낚아채기가 곧바로 다시 던지지 않게, 사용자 2026-10-07)
        private const double k_EndHoldSeconds = 0.5;
        // 상자를 끌어낸 뒤 서 있는 초(상자가 둑으로 날아오는 동안)
        private const double k_CrateHoldSeconds = 0.4;
        // 그림자가 뭍 양 끝 밖으로 더 오가는 폭 · 상자가 뭍 왼쪽 끝을 지나 사라지는 거리(그림자 · 상자 깊이는 표 shadowDepth · crateDepth)
        private const float k_ShadowOverhang = 1.0f;
        private const float k_CrateExit = 0.6f;

        private readonly PassageInteractable m_exit;
        private readonly Dictionary<string, FishRecord> m_log = new Dictionary<string, FishRecord>();
        // 가짜 입질 시각(던진 뒤 초, 오름차순)
        private readonly List<double> m_nibbles = new List<double>();
        private readonly HashSet<int> m_reached = new HashSet<int> { 0 };
        private readonly HashSet<int> m_visible = new HashSet<int> { 0 };
        private readonly List<SeatInteractable> m_seats = new List<SeatInteractable>();
        // 섬마다 한 번 만든 다리 자리(사물 목록을 다시 짜도 같은 객체라 대상이 흔들리지 않는다)
        private readonly Dictionary<int, BridgeInteractable> m_bridges = new Dictionary<int, BridgeInteractable>();
        private readonly List<FishingVisitor> m_visitors = new List<FishingVisitor>();
        private int m_nextVisitorId;
        // 기다림: 물기까지 남은 초 · 입질: 낚아챌 틈 남은 초 · 줄다리기: 달리기 또는 쉼이 끝나기까지 남은 초
        private double m_phaseLeft;
        private double m_elapsed;
        // 그림자: 나타나기까지 · 사라지기까지 남은 초 · 헤엄 방향. 상자: 떠내려오기까지 남은 초
        private double m_shadowIn;
        private double m_shadowLeft;
        private float m_shadowDir = 1f;
        private double m_crateIn;

        public FishingConfigTable Config { get; }
        public FishingLayout Layout { get; }
        public WaterInteractable Water { get; }
        public FishingPhase Phase { get; private set; } = FishingPhase.Idle;
        // 줄에 걸린(또는 물려는) 물고기. 빈 줄이거나 상자를 끄는 중이면 null
        public Fish Fish { get; private set; }
        // 설계 61: 지금 떠내려오는 상자(없으면 null) · 끌어내는 중인 상자(줄다리기 상대, 아니면 null)
        public CrateInteractable Crate { get; private set; }
        public CrateInteractable Pulling { get; private set; }
        // 설계 61: 명물 그림자(그림자 섬에 닿은 뒤 shadowEvery마다 shadowSeconds 동안 뭍 아래 물을 오간다)
        public bool ShadowVisible { get; private set; }
        public Vector2 ShadowPosition { get; private set; }
        // 줄다리기: 남은 거리(0 = 낚음) · 처음 거리 · 줄 긴장(Config.Rod.Line에 닿으면 끊김) · 물고기가 달리는 중
        public double Distance { get; private set; }
        public double DistanceStart { get; private set; }
        public double Tension { get; private set; }
        public bool Running { get; private set; }
        // 어종 id → 기록(도감 화면은 다음, 기록은 지금부터)
        public IReadOnlyDictionary<string, FishRecord> Log => m_log;
        // 설계 61: 닿은 섬 · 보이는 섬(index) · 놓은 좌대 · 좌대 손님
        public IReadOnlyCollection<int> Reached => m_reached;
        public IReadOnlyCollection<int> Visible => m_visible;
        public IReadOnlyList<SeatInteractable> Seats => m_seats;
        public IReadOnlyList<FishingVisitor> Visitors => m_visitors;
        public override IReadOnlyList<IPlacedKind> ShopKinds => s_noKinds;
        public override string Id => k_Id;

        // IShop: 좌대를 하나라도 놓으면 광장 손님이 온다. 빈 좌대가 있어야 받는다
        public bool IsOpen => m_seats.Any(seat => seat.Placed);
        public bool CanAdmit => FreeSeat() != null;

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
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, PlazaArea.k_Id);
            Water = new WaterInteractable(Row(WaterInteractable.k_Id), this);
            m_shadowIn = Config.ShadowEvery;
            m_crateIn = Config.CrateEvery;
            Reveal();
            Rebuild();
        }

        // 1회차: 점원 자리 없음(알바는 다음 회차)
        public override IEnumerable<Interactable> ClerkSlots
        {
            get { yield break; }
        }

        internal override bool IsPassage(Vector2 p)
        {
            return Vector2.DistanceSquared(p, Layout.HoleFloor) < 1e-4f;
        }

        // 닿은 섬 중 그 볼 것이 있는 섬(없으면 null)
        public FishingIslandTable EventIsland(string eventId)
        {
            return m_reached.Select(Layout.Island).FirstOrDefault(island => island.Event == eventId);
        }

        // 찌가 뜨는 자리(물가에서 물 안쪽으로 깊이의 bobberIn)
        public Vector2 BobberPoint()
        {
            return Layout.Deep(Wombat.Mover.Position, (float)Config.BobberIn);
        }

        // ---------- 한 판 ----------

        // 던지기: 웜뱃이 선 섬(다리 위면 다리)의 물고기 하나를 비중으로 고르고 무게는 크기 표에서. 찌가 뜨고 물기까지 기다린다. 가짜 입질은 그 사이(30~90%)에 0~nibbleMax번.
        // 설계 61: 그림자가 보이고 찌 자리가 shadowReach 안이면 명물이 가장 짧은 기다림 뒤 바로 문다(입질 없음). 그림자는 들어가고 다음 때를 기다린다
        internal void Cast()
        {
            if (Phase != FishingPhase.Idle)
            {
                return;
            }

            m_nibbles.Clear();
            m_elapsed = 0d;
            FishingIslandTable island = Layout.IslandAt(Wombat.Mover.Position, out bool onBridge);

            if (ShadowVisible && Vector2.Distance(BobberPoint(), ShadowPosition) <= (float)Config.ShadowReach)
            {
                Fish = PickFish(Tables.Get<FishTable>(EventIsland(FishingIslandTable.k_EventShadow).Boss));
                m_phaseLeft = Config.BiteWaitMin;
                HideShadow();
            }
            else
            {
                IReadOnlyList<FishingFishData> pool = onBridge && island.Bridge.Fish.Length > 0 ? island.Bridge.Fish : island.Fish;
                Fish = PickFish(Tables.Get<FishTable>(Pick(pool, fish => fish.Chance).Id));
                m_phaseLeft = Config.BiteWaitMin + Random.NextDouble() * (Config.BiteWaitMax - Config.BiteWaitMin);
                int nibbles = (int)Math.Floor(Random.NextDouble() * (Config.NibbleMax + 1));

                for (int i = 0; i < nibbles; i++)
                {
                    m_nibbles.Add(m_phaseLeft * (0.3 + 0.6 * Random.NextDouble()));
                }

                m_nibbles.Sort();
            }

            Phase = FishingPhase.Waiting;
            Hold();
            Bus.Publish(new Events.CastThrown(this));
        }

        // 낚아채기: 물었을 때면 걸리고(줄다리기), 아직이면 줄을 거둔다
        internal void Strike()
        {
            if (Phase == FishingPhase.Bite)
            {
                Phase = FishingPhase.Tug;
                DistanceStart = Distance = Fish.Weight * Config.DistancePerKg;
                Tension = 0d;
                Running = false;
                m_phaseLeft = Config.RestSeconds;
                Bus.Publish(new Events.Hooked(this, Fish));
            }
            else if (Phase == FishingPhase.Waiting)
            {
                End(FishingEnd.Retrieved);
            }
        }

        // 설계 61 끌어내기: 떠내려온 상자에 줄을 걸어 바로 줄다리기(입질 없음). 상자는 무겁고 오래 쉬며 가끔 걸린다(달리기)
        internal void PullCrate(CrateInteractable crate)
        {
            if (Phase != FishingPhase.Idle || crate != Crate)
            {
                return;
            }

            Pulling = crate;
            Fish = null;
            Phase = FishingPhase.Tug;
            DistanceStart = Distance = Config.CrateWeight * Config.DistancePerKg;
            Tension = 0d;
            Running = false;
            m_phaseLeft = Config.CrateRestSeconds;
            Hold();
            Bus.Publish(new Events.CastThrown(this));
            Bus.Publish(new Events.Hooked(this, null));
        }

        // 치트 · 검증: 기다리던 줄을 지금 물게 한다
        public void BiteNow()
        {
            if (Phase == FishingPhase.Waiting)
            {
                m_phaseLeft = 0d;
            }
        }

        protected override void TickArea(double dt)
        {
            switch (Phase)
            {
                case FishingPhase.Waiting:
                    TickWaiting(dt);
                    break;
                case FishingPhase.Bite:
                    TickBite(dt);
                    break;
                case FishingPhase.Tug:
                    TickTug(dt);
                    break;
            }

            TickShadow(dt);
            TickCrate(dt);
            TickVisitors(dt);
        }

        private void TickWaiting(double dt)
        {
            if (Wombat.Input != Vector2.Zero)
            {
                End(FishingEnd.Retrieved);
                return;
            }

            Hold();
            m_elapsed += dt;
            m_phaseLeft -= dt;

            while (m_nibbles.Count > 0 && m_nibbles[0] <= m_elapsed)
            {
                m_nibbles.RemoveAt(0);
                Bus.Publish(new Events.Nibbled(this));
            }

            if (m_phaseLeft <= 0d)
            {
                Phase = FishingPhase.Bite;
                m_phaseLeft += Config.BiteWindow;
                // 물었다: 웜뱃 머리 위 「!」(낚아챌 틈을 알린다)
                Wombat.Bubble.Show(BubbleTable.k_Alert);
                Bus.Publish(new Events.Bitten(this));
            }
        }

        private void TickBite(double dt)
        {
            if (Wombat.Input != Vector2.Zero)
            {
                End(FishingEnd.Retrieved);
                return;
            }

            Hold();
            m_phaseLeft -= dt;

            if (m_phaseLeft <= 0d)
            {
                End(FishingEnd.Missed);
            }
        }

        // 줄다리기: 달릴 때 누르면 긴장 ↑(놓으면 긴장 ↓ · 물고기가 줄을 되찾음), 쉴 때 누르면 거리 ↓(긴장은 늘 내린다)
        private void TickTug(double dt)
        {
            // 상자에 건 줄은 조이스틱을 움직이면 놓고 바로 걷는다(안 그러면 다 감기 전엔 아무것도 못 한다, 사용자 2026-10-07). 물고기 줄다리기는 조이스틱을 무시한다
            if (Pulling != null && Wombat.Input != Vector2.Zero)
            {
                End(FishingEnd.Retrieved);
                return;
            }

            Hold();
            bool holding = Wombat.Holding;
            m_phaseLeft -= dt;

            if (Running)
            {
                if (holding)
                {
                    Tension += Config.TensionRise * dt;
                }
                else
                {
                    Tension = Math.Max(0d, Tension - Config.TensionFall * dt);
                    Distance = Math.Min(DistanceStart, Distance + Config.RunPull * dt);
                }
            }
            else
            {
                Tension = Math.Max(0d, Tension - Config.TensionFall * dt);

                if (holding)
                {
                    Distance -= Config.Rod.Reel * dt;
                }
            }

            if (Tension >= Config.Rod.Line)
            {
                End(FishingEnd.Snapped);
                return;
            }

            if (Distance <= 0d)
            {
                if (Pulling != null)
                {
                    LandCrate();
                }
                else
                {
                    Land();
                }

                return;
            }

            if (m_phaseLeft <= 0d)
            {
                Running = !Running;
                m_phaseLeft += Running ? Config.RunSeconds : Pulling != null ? Config.CrateRestSeconds : Config.RestSeconds;
                Bus.Publish(new Events.RunChanged(this, Running));
            }
        }

        // 낚았다: 재료 하나 · 기록 · 웜뱃은 물고기를 들고 만세(landSeconds 동안 선다)
        private void Land()
        {
            Fish fish = Fish;
            Wombat.Worker.Wallet.AddItem(fish.Kind.Item, 1);
            Record(fish);
            Clear();
            Wombat.Hold(Facing(), Config.LandSeconds);
            Bus.Publish(new Events.FishLanded(this, fish));
        }

        // 설계 61: 상자를 끌어냈다(코인). 상자는 사라지고 다음 상자를 기다린다
        private void LandCrate()
        {
            CrateInteractable crate = Pulling;
            Wombat.Worker.Wallet.AddCoins(Config.CrateCoins);
            Clear();
            RemoveCrate();
            Wombat.Hold(crate.Position, k_CrateHoldSeconds);
            Bus.Publish(new Events.CratePulled(this, crate, Config.CrateCoins));
        }

        private void End(FishingEnd reason)
        {
            Clear();
            // 거둔 즉시 걸을 수 있게(직전 틱의 Hold를 푼다). 놓침 · 끊김은 잠깐 서서 「!!」
            Wombat.Hold(Facing(), reason == FishingEnd.Retrieved ? 0d : k_EndHoldSeconds);

            if (reason != FishingEnd.Retrieved)
            {
                Wombat.Bubble.Show(BubbleTable.k_Angry);
            }

            Bus.Publish(new Events.FishingEnded(this, reason));
        }

        private void Clear()
        {
            Phase = FishingPhase.Idle;
            Fish = null;
            Pulling = null;
            Running = false;
            Tension = 0d;
            Distance = 0d;
            m_nibbles.Clear();
        }

        // 웜뱃은 물 쪽(상자를 끄는 중이면 상자 쪽)을 본 채 제자리에 선다(조이스틱 걷기는 멈춘다)
        private void Hold()
        {
            Wombat.Hold(Facing(), k_HoldSeconds);
        }

        // 웜뱃이 보는 점: 선 자리에서 가장 가까운 물가에서 물 안쪽으로 절반(상자를 끄는 중이면 상자)
        private Vector2 Facing()
        {
            return Pulling != null ? Pulling.Position : Layout.Deep(Wombat.Mover.Position, 0.5f);
        }

        // 다른 곳으로 나가면 줄을 거둔다
        protected override void OnWombatLeft()
        {
            if (Phase != FishingPhase.Idle)
            {
                End(FishingEnd.Retrieved);
            }
        }

        // 그 어종의 크기(chance 비중) · 무게(그 크기 범위 안 난수)
        private Fish PickFish(FishTable kind)
        {
            FishSizeData size = Pick(kind.Sizes, s => s.Chance);
            return new Fish(kind, size.Min + Random.NextDouble() * (size.Max - size.Min));
        }

        private T Pick<T>(IReadOnlyList<T> items, Func<T, double> chance)
        {
            double roll = Random.NextDouble() * items.Sum(chance);

            foreach (T item in items)
            {
                roll -= chance(item);

                if (roll < 0d)
                {
                    return item;
                }
            }

            return items[items.Count - 1];
        }

        private void Record(Fish fish)
        {
            if (!m_log.TryGetValue(fish.Kind.Id, out FishRecord record))
            {
                record = new FishRecord();
                m_log[fish.Kind.Id] = record;
            }

            record.Caught++;
            record.Best = Math.Max(record.Best, fish.Weight);
        }

        // ---------- 설계 61 볼 것 ----------

        // 그림자: 그림자 섬에 닿은 뒤 shadowEvery마다 나타나 뭍 아래 물을 양 끝 밖 overhang까지 오가고 shadowSeconds 뒤 들어간다
        private void TickShadow(double dt)
        {
            FishingIslandTable island = EventIsland(FishingIslandTable.k_EventShadow);

            if (island == null)
            {
                return;
            }

            NavRect land = island.Land.ToRect();
            float left = land.XMin - k_ShadowOverhang;
            float right = land.XMax + k_ShadowOverhang;

            if (!ShadowVisible)
            {
                m_shadowIn -= dt;

                if (m_shadowIn <= 0d)
                {
                    ShadowVisible = true;
                    m_shadowLeft = Config.ShadowSeconds;
                    m_shadowDir = 1f;
                    ShadowPosition = new Vector2(left, land.YMin - (float)Config.ShadowDepth);
                    Bus.Publish(new Events.ShadowShown(this, island));
                }

                return;
            }

            float x = ShadowPosition.X + m_shadowDir * (float)(Config.ShadowSpeed * dt);

            if (x > right || x < left)
            {
                m_shadowDir = -m_shadowDir;
                x = Math.Clamp(x, left, right);
            }

            ShadowPosition = new Vector2(x, ShadowPosition.Y);
            m_shadowLeft -= dt;

            if (m_shadowLeft <= 0d)
            {
                HideShadow();
            }
        }

        private void HideShadow()
        {
            ShadowVisible = false;
            m_shadowIn = Config.ShadowEvery;
        }

        // 상자: 상자 섬에 닿은 뒤 crateEvery마다 뭍 오른쪽 끝 아래에 나타나 crateSpeed로 왼쪽으로 떠내려가고, crateSeconds가 다 되거나 뭍 왼쪽 끝을 지나면 사라진다. 끄는 중에는 멈춘다
        private void TickCrate(double dt)
        {
            FishingIslandTable island = EventIsland(FishingIslandTable.k_EventCrate);

            if (island == null)
            {
                return;
            }

            if (Crate == null)
            {
                m_crateIn -= dt;

                if (m_crateIn <= 0d)
                {
                    Crate = new CrateInteractable(Row(CrateInteractable.k_Id), this, island);
                    SyncThings();
                    Bus.Publish(new Events.CrateDrifted(this, Crate));
                }

                return;
            }

            if (Pulling == Crate)
            {
                return;
            }

            Crate.Position = new Vector2(Crate.Position.X - (float)(Config.CrateSpeed * dt), Crate.Position.Y);
            Crate.Left -= dt;

            if (Crate.Left <= 0d || Crate.Position.X < (float)island.Land.X0 - k_CrateExit)
            {
                RemoveCrate();
            }
        }

        private void RemoveCrate()
        {
            Crate = null;
            m_crateIn = Config.CrateEvery;
            SyncThings();
        }

        // 치트: 그림자 · 상자를 지금 부른다(그 섬에 닿지 않았으면 false)
        public bool ShowShadowNow()
        {
            if (EventIsland(FishingIslandTable.k_EventShadow) == null)
            {
                return false;
            }

            HideShadow();
            m_shadowIn = 0d;
            return true;
        }

        public bool SpawnCrateNow()
        {
            if (EventIsland(FishingIslandTable.k_EventCrate) == null)
            {
                return false;
            }

            if (Crate != null && Pulling != Crate)
            {
                RemoveCrate();
            }

            m_crateIn = 0d;
            return true;
        }

        // ---------- 설계 61 넓히기 ----------

        // 다리 놓기(시트 줄): 값을 치르고 그 섬에 닿는다. 웜뱃은 다리 쪽을 보고 두드린다(굴 파기 동작)
        internal bool TryBridge(Worker worker, BridgeInteractable bridge)
        {
            if (m_reached.Contains(bridge.Island.Index) || !worker.Wallet.TrySpendCoins(bridge.Island.Bridge.Cost))
            {
                return false;
            }

            Wombat.Dig(bridge.Position);
            Reach(bridge.Island.Index);
            return true;
        }

        private void Reach(int island)
        {
            m_reached.Add(island);
            Reveal();
            Rebuild();
            Bus.Publish(new Events.IslandReached(this, island));
        }

        // 닿은 섬의 다음 섬이 보인다(칸이 파여 뭍이 그려지지만 다리를 놓아야 걷는다)
        private void Reveal()
        {
            m_visible.UnionWith(m_reached);
            int next = m_reached.Max() + 1;

            if (Layout.HasIsland(next))
            {
                m_visible.Add(next);
            }
        }

        // 좌대 놓기(시트 줄)
        internal bool TryPlaceSeat(Worker worker, SeatInteractable seat)
        {
            if (seat.Placed || !worker.Wallet.TrySpendCoins(seat.Row.Cost))
            {
                return false;
            }

            seat.Placed = true;
            SyncThings();
            Bus.Publish(new Events.SeatPlaced(this, seat));
            return true;
        }

        // 손님이 한 마리 낚았다: 그 섬 물고기 하나(보이기만) · 코인
        internal void CustomerCatch(FishingVisitor visitor)
        {
            FishingIslandTable island = visitor.Seat.Island;
            FishTable kind = Tables.Get<FishTable>(Pick(island.Fish, fish => fish.Chance).Id);
            double coins = Math.Round(island.Pay * Wombat.Worker.Wallet.Scale(BlessingTable.k_Price));
            Wombat.Worker.Wallet.AddCoins(coins);
            Bus.Publish(new Events.FishingVisitorCaught(visitor, kind, coins));
        }

        public void Admit(VisitorTable look)
        {
            SeatInteractable seat = FreeSeat();

            if (seat == null)
            {
                return;
            }

            FishingVisitor visitor = new FishingVisitor(++m_nextVisitorId, look, this, seat);
            m_visitors.Add(visitor);
            Bus.Publish(new Events.FishingVisitorArrived(visitor));
        }

        // 놓았고 아무 손님도 잡지 않은 좌대(구멍에서 가까운 순)
        private SeatInteractable FreeSeat()
        {
            return m_seats.Where(seat => seat.Placed && m_visitors.All(visitor => visitor.Seat != seat))
                .OrderBy(seat => Vector2.Distance(seat.Position, Layout.HoleFloor)).FirstOrDefault();
        }

        private void TickVisitors(double dt)
        {
            for (int i = 0; i < m_visitors.Count; i++)
            {
                FishingVisitor visitor = m_visitors[i];

                if (visitor.Tick(dt))
                {
                    continue;
                }

                m_visitors.RemoveAt(i);
                i--;
                Bus.Publish(new Events.FishingVisitorLeft(visitor));
            }

            Crowd.Sidestep(m_visitors, dt);
        }

        // 치트: 다음 다리를 값 없이 놓는다 · 다음 좌대를 값 없이 놓는다. 한 일이 없으면 false
        public bool ReachNextFree()
        {
            int next = m_reached.Max() + 1;

            if (!Layout.HasIsland(next))
            {
                return false;
            }

            Reach(next);
            return true;
        }

        public bool PlaceSeatFree()
        {
            SeatInteractable seat = m_seats.FirstOrDefault(s => !s.Placed);

            if (seat == null)
            {
                return false;
            }

            seat.Placed = true;
            SyncThings();
            Bus.Publish(new Events.SeatPlaced(this, seat));
            return true;
        }

        // 보이는 · 닿은 섬으로 굴을 다시 만들고, 좌대 자리(닿은 섬만 — 손님은 그 섬 물고기를 낚는다)를 맞추고, 사물 목록을 다시 짠다. 끼인 웜뱃 · 걷던 손님은 새 길로
        private void Rebuild()
        {
            Layout.Rebuild(m_visible, m_reached);

            foreach (int index in m_reached.OrderBy(i => i))
            {
                if (m_seats.Any(seat => seat.Island.Index == index))
                {
                    continue;
                }

                FishingIslandTable island = Layout.Island(index);

                for (int i = 0; i < island.Seats.Length; i++)
                {
                    m_seats.Add(new SeatInteractable(Row(SeatInteractable.k_Id), this, island, i));
                }
            }

            SyncThings();
            Unstick();

            foreach (FishingVisitor visitor in m_visitors)
            {
                visitor.Relayout();
            }
        }

        // ---------- 저장 ----------

        internal void Restore(IReadOnlyDictionary<string, FishRecord> log)
        {
            foreach (KeyValuePair<string, FishRecord> pair in log)
            {
                m_log[pair.Key] = pair.Value;
            }
        }

        // 설계 61: 닿은 섬(0에서 빈틈없이 이어진 것만), 놓은 좌대 [섬, 번호]
        internal void RestoreIslands(IEnumerable<int> reached, IEnumerable<int[]> seats)
        {
            HashSet<int> set = new HashSet<int>(reached);

            for (int index = 1; set.Contains(index) && Layout.HasIsland(index); index++)
            {
                m_reached.Add(index);
            }

            Reveal();
            Rebuild();

            foreach (int[] pair in seats.Where(p => p.Length == 2))
            {
                SeatInteractable seat = m_seats.FirstOrDefault(s => s.Island.Index == pair[0] && s.Index == pair[1]);

                if (seat != null)
                {
                    seat.Placed = true;
                }
            }

            SyncThings();
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

        // 사물 목록: 딴짓 점원 → 똥 → 상자 → 물 → 다음 섬 다리 자리 → 놓지 않은 좌대 → 나가기
        private void SyncThings()
        {
            Placed.Clear();
            Placed.AddRange(ClerkThings);
            Placed.AddRange(Poops);

            if (Crate != null)
            {
                Placed.Add(Crate);
            }

            Placed.Add(Water);
            int next = m_reached.Max() + 1;

            if (Layout.HasIsland(next))
            {
                if (!m_bridges.TryGetValue(next, out BridgeInteractable bridge))
                {
                    bridge = new BridgeInteractable(Row(BridgeInteractable.k_Id), this, Layout.Island(next));
                    m_bridges[next] = bridge;
                }

                Placed.Add(bridge);
            }

            Placed.AddRange(m_seats.Where(seat => !seat.Placed));
            Placed.Add(m_exit);
        }

        private InteractableTable Row(string interactableId)
        {
            return Tables.Get<InteractableTable>(interactableId);
        }
    }

    // 빈 줄 → 던져서 기다림 → 물었다(낚아챌 틈) → 줄다리기
    public enum FishingPhase
    {
        Idle,
        Waiting,
        Bite,
        Tug,
    }

    // 줄이 빈 줄로 돌아간 까닭: 거둠(조이스틱 · 일찍 낚아챔 · 곳을 나감) · 놓침(틈을 넘김) · 끊김(긴장)
    public enum FishingEnd
    {
        Retrieved,
        Missed,
        Snapped,
    }
}
