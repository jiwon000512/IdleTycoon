using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 52 우물 낚시 1회차(사용자 2026-10-07 「디펜스식은 손맛도 재고도 없다」): 광장 왼쪽 끝 문 → 굴 속 강가(방 아래쪽을 가로지르는 강). 웜뱃이 둑에 서서 물 쪽으로 던지면(Cast) 찌가 뜨고,
    // 가짜 입질 몇 번 뒤 물면(Bite) 짧은 틈 안에 낚아채야 걸린다(Strike → Tug). 줄다리기: 물고기가 쉴 때 버튼을 누르고 있으면 줄이 감기고(거리 ↓),
    // 달릴 때 누르고 있으면 긴장이 올라 줄 힘에 닿으면 끊긴다(놓으면 긴장이 내리고 물고기가 줄을 조금 되찾는다). 거리 0이면 낚아 재료 하나가 창고로.
    // 기다림 · 입질 중에 조이스틱을 움직이면 줄을 거둔다(줄다리기 중에는 무시).
    // 설계 53 넓히기(사용자 2026-10-07 「한 공간이 이어지게 · 돌을 뚫어 새 낚시터 · 손님도 낚는다 · 깊이는 댐 허물기」): 낚시터는 구간(FishingStretchTable)을 양옆으로 이어 붙인 한 굴이다.
    // 열린 구간 끝 바위를 코인으로 뚫으면 다음 구간 땅이 열리고(강바닥은 마름), 물속 잔해 댐을 줄다리기로 한 조각씩 끌어내 다 빼면 그 구간에 물이 차 그 구간의 물고기가 문다.
    // 좌대를 놓으면 낚시터는 광장 손님을 받는 가게가 되고(IShop), 손님은 좌대에 앉아 낚으며 코인을 낸다
    public sealed class FishingArea : WombatArea, IShop
    {
        public const string k_Id = "fishing";

        private static readonly IPlacedKind[] s_noKinds = new IPlacedKind[0];
        // 한 틱 동안 웜뱃을 우물 쪽으로 세워 두는 초(매 틱 다시 건다)
        private const double k_HoldSeconds = 0.5;
        // 놓치거나 끊기면 잠깐 서 있는 초: 그 사이의 버튼은 던지기가 아니다(늦은 낚아채기가 곧바로 다시 던지지 않게, 사용자 2026-10-07)
        private const double k_EndHoldSeconds = 0.5;
        // 잔해 한 조각을 끌어낸 뒤 서 있는 초(조각이 둑으로 날아오는 동안)
        private const double k_DebrisHoldSeconds = 0.4;

        private readonly PassageInteractable m_exit;
        private readonly Dictionary<string, FishRecord> m_log = new Dictionary<string, FishRecord>();
        // 가짜 입질 시각(던진 뒤 초, 오름차순)
        private readonly List<double> m_nibbles = new List<double>();
        private readonly Dictionary<int, FishingStretchTable> m_stretches = new Dictionary<int, FishingStretchTable>();
        private readonly HashSet<int> m_opened = new HashSet<int> { 0 };
        private readonly HashSet<int> m_flooded = new HashSet<int> { 0 };
        // 댐에서 뺀 조각(구간 → 수, 물이 차면 지운다)
        private readonly Dictionary<int, int> m_pulled = new Dictionary<int, int>();
        private readonly List<SeatInteractable> m_seats = new List<SeatInteractable>();
        // 구간마다 한 번 만든 바위 · 댐(사물 목록을 다시 짜도 같은 객체라 대상이 흔들리지 않는다)
        private readonly Dictionary<int, RockInteractable> m_rocks = new Dictionary<int, RockInteractable>();
        private readonly Dictionary<int, DamInteractable> m_dams = new Dictionary<int, DamInteractable>();
        private readonly List<FishingVisitor> m_visitors = new List<FishingVisitor>();
        private int m_nextVisitorId;
        // 기다림: 물기까지 남은 초 · 입질: 낚아챌 틈 남은 초 · 줄다리기: 달리기 또는 쉼이 끝나기까지 남은 초
        private double m_phaseLeft;
        private double m_elapsed;

        public FishingConfigTable Config { get; }
        public FishingLayout Layout { get; }
        public WaterInteractable Water { get; }
        public FishingPhase Phase { get; private set; } = FishingPhase.Idle;
        // 줄에 걸린(또는 물려는) 물고기. 빈 줄이거나 잔해를 끄는 중이면 null
        public Fish Fish { get; private set; }
        // 설계 53: 끌어내는 중인 댐(줄다리기 상대가 잔해). 아니면 null
        public DamInteractable Debris { get; private set; }
        // 줄다리기: 남은 거리(0 = 낚음) · 처음 거리 · 줄 긴장(Config.Rod.Line에 닿으면 끊김) · 물고기가 달리는 중
        public double Distance { get; private set; }
        public double DistanceStart { get; private set; }
        public double Tension { get; private set; }
        public bool Running { get; private set; }
        // 어종 id → 기록(도감 화면은 다음, 기록은 지금부터)
        public IReadOnlyDictionary<string, FishRecord> Log => m_log;
        // 설계 53: 열린 구간 · 물이 찬 구간(index) · 놓은 좌대 · 좌대 손님
        public IReadOnlyCollection<int> Opened => m_opened;
        public IReadOnlyCollection<int> Flooded => m_flooded;
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

            foreach (FishingStretchTable stretch in tables.GetAll<FishingStretchTable>())
            {
                m_stretches[stretch.Index] = stretch;
            }

            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, PlazaArea.k_Id);
            Water = new WaterInteractable(Row(WaterInteractable.k_Id), this);
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

        // 그 구간 댐에서 뺀 조각
        public int PulledFrom(int stretch)
        {
            m_pulled.TryGetValue(stretch, out int pulled);
            return pulled;
        }

        // ---------- 한 판 ----------

        // 던지기: 웜뱃이 선 구간의 물고기 하나를 비중으로 고르고 무게는 크기 표에서. 찌가 뜨고 물기까지 기다린다. 가짜 입질은 그 사이(30~90%)에 0~nibbleMax번
        internal void Cast()
        {
            if (Phase != FishingPhase.Idle)
            {
                return;
            }

            Fish = PickFish(StretchAt(Layout.Shore(Wombat.Mover.Position).X));
            m_phaseLeft = Config.BiteWaitMin + Random.NextDouble() * (Config.BiteWaitMax - Config.BiteWaitMin);
            m_elapsed = 0d;
            m_nibbles.Clear();
            int nibbles = (int)Math.Floor(Random.NextDouble() * (Config.NibbleMax + 1));

            for (int i = 0; i < nibbles; i++)
            {
                m_nibbles.Add(m_phaseLeft * (0.3 + 0.6 * Random.NextDouble()));
            }

            m_nibbles.Sort();
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

        // 설계 53 끌어내기: 댐 잔해 한 조각에 줄을 걸어 바로 줄다리기(입질 없음). 잔해는 무겁고 오래 쉬며 가끔 걸린다(달리기)
        internal void PullDebris(DamInteractable dam)
        {
            if (Phase != FishingPhase.Idle)
            {
                return;
            }

            Debris = dam;
            Fish = null;
            Phase = FishingPhase.Tug;
            DistanceStart = Distance = Config.DebrisWeight * Config.DistancePerKg;
            Tension = 0d;
            Running = false;
            m_phaseLeft = Config.DebrisRestSeconds;
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
            // 잔해에 건 줄은 조이스틱을 움직이면 놓고 바로 걷는다(안 그러면 다 감기 전엔 아무것도 못 한다, 사용자 2026-10-07). 물고기 줄다리기는 조이스틱을 무시한다
            if (Debris != null && Wombat.Input != Vector2.Zero)
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
                if (Debris != null)
                {
                    LandDebris();
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
                m_phaseLeft += Running ? Config.RunSeconds : Debris != null ? Config.DebrisRestSeconds : Config.RestSeconds;
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

        // 설계 53: 잔해 한 조각을 끌어냈다(코인 조금). 마지막 조각이면 댐이 무너져 그 구간에 물이 찬다
        private void LandDebris()
        {
            DamInteractable dam = Debris;
            int stretch = dam.Stretch.Index;
            m_pulled[stretch] = PulledFrom(stretch) + 1;
            Wombat.Worker.Wallet.AddCoins(Config.DebrisCoins);
            Clear();
            Wombat.Hold(dam.Position, k_DebrisHoldSeconds);
            Bus.Publish(new Events.DebrisPulled(this, dam, Config.DebrisCoins));

            if (dam.PiecesLeft <= 0)
            {
                Flood(stretch);
            }
        }

        private void Flood(int stretch)
        {
            m_pulled.Remove(stretch);
            m_flooded.Add(stretch);
            Rebuild();
            Bus.Publish(new Events.DamBroken(this, stretch));
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
            Debris = null;
            Running = false;
            Tension = 0d;
            Distance = 0d;
            m_nibbles.Clear();
        }

        // 웜뱃은 물 쪽(잔해를 끄는 중이면 댐 쪽)을 본 채 제자리에 선다(조이스틱 걷기는 멈춘다)
        private void Hold()
        {
            Wombat.Hold(Facing(), k_HoldSeconds);
        }

        // 웜뱃이 보는 점: 선 자리에서 가장 가까운 물가에서 물 안쪽으로 절반(잔해를 끄는 중이면 댐)
        private Vector2 Facing()
        {
            return Debris != null ? Debris.Position : Layout.Deep(Wombat.Mover.Position, 0.5f);
        }

        // 다른 곳으로 나가면 줄을 거둔다
        protected override void OnWombatLeft()
        {
            if (Phase != FishingPhase.Idle)
            {
                End(FishingEnd.Retrieved);
            }
        }

        private FishingStretchTable StretchAt(float x)
        {
            return m_stretches.TryGetValue(Layout.StretchOf(x), out FishingStretchTable stretch) ? stretch : m_stretches[0];
        }

        // 그 구간 물고기 중 하나(chance 비중) · 크기(chance 비중) · 무게(그 크기 범위 안 난수)
        private Fish PickFish(FishingStretchTable stretch)
        {
            FishTable kind = Tables.Get<FishTable>(Pick(stretch.Fish, fish => fish.Chance).Id);
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

        // ---------- 설계 53 넓히기 ----------

        // 바위 뚫기(시트 줄): 값을 치르고 그 구간 땅을 연다. 웜뱃은 바위 쪽을 보고 판다(굴 파기 동작)
        internal bool TryOpen(Worker worker, RockInteractable rock)
        {
            if (m_opened.Contains(rock.Stretch.Index) || !worker.Wallet.TrySpendCoins(rock.Stretch.RockCost))
            {
                return false;
            }

            Wombat.Dig(rock.Position);
            Open(rock.Stretch.Index);
            return true;
        }

        private void Open(int stretch)
        {
            m_opened.Add(stretch);
            Rebuild();
            Bus.Publish(new Events.StretchOpened(this, stretch));
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

        // 손님이 한 마리 낚았다: 그 구간 물고기 하나(보이기만) · 코인
        internal void CustomerCatch(FishingVisitor visitor)
        {
            FishingStretchTable stretch = visitor.Seat.Stretch;
            FishTable kind = Tables.Get<FishTable>(Pick(stretch.Fish, fish => fish.Chance).Id);
            double coins = Math.Round(stretch.Pay * Wombat.Worker.Wallet.Scale(BlessingTable.k_Price));
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

        // 치트: 다음 바위를 값 없이 뚫는다(오른쪽 먼저) · 지금 댐을 다 허문다 · 다음 좌대를 값 없이 놓는다. 한 일이 없으면 false
        public bool OpenNextFree()
        {
            RockInteractable rock = Placed.OfType<RockInteractable>().OrderByDescending(r => r.Stretch.Index).FirstOrDefault();

            if (rock == null)
            {
                return false;
            }

            Open(rock.Stretch.Index);
            return true;
        }

        public bool BreakDamNow()
        {
            DamInteractable dam = Placed.OfType<DamInteractable>().FirstOrDefault();

            if (dam == null)
            {
                return false;
            }

            Flood(dam.Stretch.Index);
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

        // 열린 구간 · 물로 굴을 다시 만들고, 좌대 자리(물이 찬 구간만 — 손님은 그 구간 물고기를 낚는다)를 맞추고, 사물 목록을 다시 짠다. 끼인 웜뱃 · 걷던 손님은 새 길로
        private void Rebuild()
        {
            Layout.Rebuild(m_opened, m_flooded);

            foreach (int stretch in m_flooded)
            {
                if (m_seats.Any(seat => seat.Stretch.Index == stretch))
                {
                    continue;
                }

                for (int i = 0; i < m_stretches[stretch].Seats.Length; i++)
                {
                    m_seats.Add(new SeatInteractable(Row(SeatInteractable.k_Id), this, m_stretches[stretch], i));
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

        // 설계 53: 열린 · 물 찬 구간(구간 0에서 끊김 없이 이어진 것만, 물이 찼는데 안 열린 구간은 버린다), 댐에서 뺀 조각, 놓은 좌대 [구간, 번호]
        internal void RestoreStretches(IEnumerable<int> opened, IEnumerable<int> flooded, IEnumerable<int[]> pulled, IEnumerable<int[]> seats)
        {
            m_opened.UnionWith(Connected(opened.Where(m_stretches.ContainsKey)));
            m_flooded.UnionWith(Connected(flooded.Where(m_opened.Contains)));

            foreach (int[] pair in pulled.Where(p => p.Length == 2 && m_opened.Contains(p[0]) && !m_flooded.Contains(p[0])))
            {
                m_pulled[pair[0]] = Math.Min(pair[1], m_stretches[pair[0]].DamPieces - 1);
            }

            Rebuild();

            foreach (int[] pair in seats.Where(p => p.Length == 2))
            {
                SeatInteractable seat = m_seats.FirstOrDefault(s => s.Stretch.Index == pair[0] && s.Index == pair[1]);

                if (seat != null)
                {
                    seat.Placed = true;
                }
            }

            SyncThings();
        }

        // 구간 0에서 양쪽으로 끊김 없이 이어진 index만
        private static IEnumerable<int> Connected(IEnumerable<int> stretches)
        {
            HashSet<int> set = new HashSet<int>(stretches) { 0 };
            List<int> connected = new List<int> { 0 };

            for (int side = -1; side <= 1; side += 2)
            {
                for (int s = side; set.Contains(s); s += side)
                {
                    connected.Add(s);
                }
            }

            return connected;
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

        // 사물 목록: 딴짓 점원 → 똥 → 댐 → 물 → 바위 → 놓지 않은 좌대 → 나가기.
        // 바위는 열린 구간 양 끝 바깥 구간이 표에 있으면, 댐은 열렸지만 물이 안 찬 구간 중 안쪽 구간에 물이 찬 것
        private void SyncThings()
        {
            Placed.Clear();
            Placed.AddRange(ClerkThings);
            Placed.AddRange(Poops);

            foreach (int stretch in m_opened.Where(s => !m_flooded.Contains(s) && m_flooded.Contains(s - Math.Sign(s))))
            {
                if (!m_dams.TryGetValue(stretch, out DamInteractable dam))
                {
                    dam = new DamInteractable(Row(DamInteractable.k_Id), this, m_stretches[stretch]);
                    m_dams[stretch] = dam;
                }

                Placed.Add(dam);
            }

            Placed.Add(Water);

            foreach (int stretch in new[] { m_opened.Min() - 1, m_opened.Max() + 1 })
            {
                if (!m_stretches.TryGetValue(stretch, out FishingStretchTable row))
                {
                    continue;
                }

                if (!m_rocks.TryGetValue(stretch, out RockInteractable rock))
                {
                    rock = new RockInteractable(Row(RockInteractable.k_Id), this, row);
                    m_rocks[stretch] = rock;
                }

                Placed.Add(rock);
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
