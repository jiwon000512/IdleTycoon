using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 52 우물 낚시 1회차(사용자 2026-10-07 「디펜스식은 손맛도 재고도 없다」): 광장 왼쪽 끝 문 → 굴 속 강가(방 아래쪽을 가로지르는 강). 웜뱃이 둑에 서서 물 쪽으로 던지면(Cast) 찌가 뜨고,
    // 가짜 입질 몇 번 뒤 물면(Bite) 짧은 틈 안에 낚아채야 걸린다(Strike → Tug). 줄다리기: 물고기가 쉴 때 버튼을 누르고 있으면 줄이 감기고(거리 ↓),
    // 달릴 때 누르고 있으면 긴장이 올라 줄 힘에 닿으면 끊긴다(놓으면 긴장이 내리고 물고기가 줄을 조금 되찾는다). 거리 0이면 낚아 재료 하나가 창고로.
    // 기다림 · 입질 중에 조이스틱을 움직이면 줄을 거둔다(줄다리기 중에는 무시). 어종 뽑기 · 낚싯대 강화 · 우물 깊이 · 알바는 2회차부터. 놓는 사물은 없다(편집 모드 없음)
    public sealed class FishingArea : WombatArea
    {
        public const string k_Id = "fishing";

        private static readonly IPlacedKind[] s_noKinds = new IPlacedKind[0];
        // 한 틱 동안 웜뱃을 우물 쪽으로 세워 두는 초(매 틱 다시 건다)
        private const double k_HoldSeconds = 0.5;
        // 놓치거나 끊기면 잠깐 서 있는 초: 그 사이의 버튼은 던지기가 아니다(늦은 낚아채기가 곧바로 다시 던지지 않게, 사용자 2026-10-07)
        private const double k_EndHoldSeconds = 0.5;

        private readonly PassageInteractable m_exit;
        private readonly Dictionary<string, FishRecord> m_log = new Dictionary<string, FishRecord>();
        // 가짜 입질 시각(던진 뒤 초, 오름차순)
        private readonly List<double> m_nibbles = new List<double>();
        // 기다림: 물기까지 남은 초 · 입질: 낚아챌 틈 남은 초 · 줄다리기: 달리기 또는 쉼이 끝나기까지 남은 초
        private double m_phaseLeft;
        private double m_elapsed;

        public FishingConfigTable Config { get; }
        public FishingLayout Layout { get; }
        public WaterInteractable Water { get; }
        public FishingPhase Phase { get; private set; } = FishingPhase.Idle;
        // 줄에 걸린(또는 물려는) 물고기. 빈 줄이면 null
        public Fish Fish { get; private set; }
        // 줄다리기: 남은 거리(0 = 낚음) · 처음 거리 · 줄 긴장(Config.Rod.Line에 닿으면 끊김) · 물고기가 달리는 중
        public double Distance { get; private set; }
        public double DistanceStart { get; private set; }
        public double Tension { get; private set; }
        public bool Running { get; private set; }
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
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, PlazaArea.k_Id);
            Water = new WaterInteractable(Row(WaterInteractable.k_Id), this);
            SyncThings();
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

        // ---------- 한 판 ----------

        // 던지기: 찌가 뜨고 물기까지 기다린다. 가짜 입질은 그 사이(30~90%)에 0~nibbleMax번
        internal void Cast()
        {
            if (Phase != FishingPhase.Idle)
            {
                return;
            }

            Fish = new Fish(Tables.Get<FishTable>(Config.Fish), Config.Weight);
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
                Land();
                return;
            }

            if (m_phaseLeft <= 0d)
            {
                Running = !Running;
                m_phaseLeft += Running ? Config.RunSeconds : Config.RestSeconds;
                Bus.Publish(new Events.RunChanged(this, Running));
            }
        }

        // 낚았다: 재료 하나 · 기록 · 웜뱃은 털썩 앉았다 일어선다
        private void Land()
        {
            Fish fish = Fish;
            Wombat.Worker.Wallet.AddItem(fish.Kind.Item, 1);
            Record(fish);
            Clear();
            Wombat.Hold(Facing(), Config.LandSeconds);
            Bus.Publish(new Events.FishLanded(this, fish));
        }

        private void End(FishingEnd reason)
        {
            Clear();
            // 거둔 즉시 걸을 수 있게(직전 틱의 Hold를 푼다). 놓침 · 끊김은 잠깐 선다
            Wombat.Hold(Facing(), reason == FishingEnd.Retrieved ? 0d : k_EndHoldSeconds);
            Bus.Publish(new Events.FishingEnded(this, reason));
        }

        private void Clear()
        {
            Phase = FishingPhase.Idle;
            Fish = null;
            Running = false;
            Tension = 0d;
            Distance = 0d;
            m_nibbles.Clear();
        }

        // 웜뱃은 물 쪽을 본 채 제자리에 선다(조이스틱 걷기는 멈춘다)
        private void Hold()
        {
            Wombat.Hold(Facing(), k_HoldSeconds);
        }

        // 웜뱃이 보는 점: 선 자리에서 가장 가까운 물가에서 물 안쪽으로 절반
        private Vector2 Facing()
        {
            return Layout.Deep(Wombat.Mover.Position, 0.5f);
        }

        // 다른 곳으로 나가면 줄을 거둔다
        protected override void OnWombatLeft()
        {
            if (Phase != FishingPhase.Idle)
            {
                End(FishingEnd.Retrieved);
            }
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

        // ---------- 저장 ----------

        internal void Restore(IReadOnlyDictionary<string, FishRecord> log)
        {
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

        // 사물 목록: 딴짓 점원 → 똥 → 물 → 나가기
        private void SyncThings()
        {
            Placed.Clear();
            Placed.AddRange(ClerkThings);
            Placed.AddRange(Poops);
            Placed.Add(Water);
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
