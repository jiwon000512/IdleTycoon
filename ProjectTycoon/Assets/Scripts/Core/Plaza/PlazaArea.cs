using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 3장 · 설계 13 · 리뷰 R2 · 설계 18: 광장. 지상 계단으로 손님이 오고(도착 타이머), 빵집에서 나간 손님은 문에서 다시 나온다.
    // 손님 한 명의 할 일은 PlazaVisitor, 여기는 손님 목록과 들를 곳 자리표. 사물은 곳으로 가는 문(설계 25 · 44, 들어가기). 장식은 자유 배치(WombatArea.Placement)
    public sealed class PlazaArea : WombatArea
    {
        public const string k_Id = "plaza";

        private readonly PlazaConfigTable m_config;
        private readonly IRandom m_random;
        private readonly List<PlazaVisitor> m_visitors = new List<PlazaVisitor>();
        private readonly List<DecorationData> m_decor = new List<DecorationData>();
        private readonly List<IPlacedKind> m_shopKinds = new List<IPlacedKind>();
        private readonly HashSet<int> m_takenSpots = new HashSet<int>();
        // 설계 21: 손님 외형(role customer)만
        private readonly List<VisitorTable> m_looks = new List<VisitorTable>();
        // 설계 22 외출: 점원의 광장 그림과 그것을 감싼 사물(웜뱃이 광장에서 깨운다)
        private readonly Dictionary<Clerk, PlazaVisitor> m_clerkFigures = new Dictionary<Clerk, PlazaVisitor>();
        private readonly Dictionary<Clerk, ClerkInteractable> m_clerkThings = new Dictionary<Clerk, ClerkInteractable>();
        private readonly float m_questionRange;
        private double m_arrivalElapsed;
        private int m_nextVisitorId;

        public PlazaLayout Layout { get; }
        public BakeryArea Bakery { get; }
        public IReadOnlyList<PlazaVisitor> Visitors => m_visitors;
        public IReadOnlyList<DecorationData> Decor => m_decor;
        public override IReadOnlyList<IPlacedKind> ShopKinds => m_shopKinds;
        public override string Id => k_Id;

        protected override BurrowNav WombatNav => Layout.WombatNav;
        // 손님이 드나드는 빵집 문
        public PlazaDoor BakeryDoor => Layout.DoorTo(BakeryArea.k_Id);
        protected override Vector2 Entrance => BakeryDoor.Floor;
        protected override IEnumerable<Vector2> Doors => Layout.Doors.Select(door => door.Floor);

        // 행상 자리도 웜뱃이 곁에 서 있다고 영영 기다리지 않는다(설계 34)
        internal override bool IsPassage(Vector2 p)
        {
            return Layout.Doors.Any(door => Vector2.DistanceSquared(p, door.Floor) < 1e-4f) || Vector2.DistanceSquared(p, Layout.StairsFloor) < 1e-4f
                || Vector2.DistanceSquared(p, new Vector2((float)m_config.MerchantX, (float)m_config.MerchantY)) < 1e-4f;
        }

        // 설계 25 · 44: 온 곳의 문 앞에 선다
        protected override Vector2 EntranceFrom(WombatArea from)
        {
            return from == null ? Entrance : DoorOf(from).Floor;
        }
        protected override BurrowShape.Result Shape => Layout.Shape;
        // 설계 29: 석상도 길을 막고 겹침 판정에 들지만, 값이 없는 종류라 편집에서 집지 않는다(WombatArea.ThingAt)
        protected override IEnumerable<IPlaced> PlacedThings => m_decor.Cast<IPlaced>().Append(Statue);
        public StatueInteractable Statue { get; }
        // 설계 31 · 34: 떠돌이 행상(일정 · 걷는 그림)과 그 행상에게 말 거는 사물
        public RelicMerchant Merchant { get; }
        public MerchantInteractable MerchantThing { get; }
        // 손님이 계단으로 오는 간격(손님 축복 · 풍경이면 짧아진다)
        // 설계 40: 빵집 평가 중에는 손님(맛 평가단)이 rush배로 몰려온다
        private double ArrivalSeconds => m_config.ArrivalSeconds / Wombat.Worker.Wallet.Scale(BlessingTable.k_Visitors)
            / (Bakery.Evaluation.Running ? Bakery.Evaluation.Config.Rush : 1d);

        // 첫 손님은 첫 틱에 온다. 웜뱃은 빵집에서 시작한다. 시작 장식은 PlazaDecorTable
        public PlazaArea(TableSet tables, BakeryArea bakery, IRandom random, Wombat wombat, EventBus bus) : base(tables, random, wombat, bus)
        {
            m_config = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_random = random;
            Bakery = bakery;
            Layout = new PlazaLayout(tables);

            foreach (PlazaDoor door in Layout.Doors)
            {
                Placed.Add(new PassageInteractable(tables.Get<InteractableTable>(PassageInteractable.k_Door), this, door.Floor, door.To));
            }

            Statue = new StatueInteractable(tables.Get<InteractableTable>(StatueInteractable.k_Id), this, new Vector2((float)m_config.StatueX, (float)m_config.StatueY), random);
            Placed.Add(Statue);
            Merchant = new RelicMerchant(this, tables);
            MerchantThing = new MerchantInteractable(tables.Get<InteractableTable>(MerchantInteractable.k_Id), this, random, Merchant);
            Placed.Add(MerchantThing);
            m_arrivalElapsed = m_config.ArrivalSeconds;
            m_questionRange = (float)tables.Get<InteractableTable>(ClerkInteractable.k_Id).Range;
            bus.Subscribe<Events.BakeryVisitorLeft>(Bus_BakeryVisitorLeft);
            bus.Subscribe<Events.ClerkWentOut>(Bus_ClerkWentOut);
            bus.Subscribe<Events.ClerkReturning>(Bus_ClerkReturning);
            bus.Subscribe<Events.ClerkFired>(Bus_ClerkFired);

            foreach (DecorationTable row in tables.GetAll<DecorationTable>())
            {
                m_shopKinds.Add(row);
            }

            foreach (VisitorTable look in tables.GetAll<VisitorTable>())
            {
                if (look.Role == VisitorRole.Customer)
                {
                    m_looks.Add(look);
                }
            }

            foreach (PlazaDecorTable placed in tables.GetAll<PlazaDecorTable>())
            {
                m_decor.Add(new DecorationData(tables.Get<DecorationTable>(placed.Decoration), new Vector2((float)placed.X, (float)placed.Y)));
            }

            Layout.Rebuild(PlacedThings);
        }

        // 들를 곳에서 머무는 초 · ♥를 띄울지
        internal double VisitSeconds()
        {
            return m_config.VisitSecondsMin + m_random.NextDouble() * (m_config.VisitSecondsMax - m_config.VisitSecondsMin);
        }

        internal bool RollEmote()
        {
            return m_random.NextDouble() < m_config.EmoteChance;
        }

        // 설계 40: 빵집 별 × admirePerStar 확률로 감탄 ✨(과시). 별이 없으면 난수를 굴리지 않는다
        internal bool RollAdmire()
        {
            StarConfigTable stars = Wombat.Worker.Wallet.Stars.Config(BakeryArea.k_Id);
            double chance = stars == null ? 0d : Math.Min(1d, stars.AdmirePerStar * Wombat.Worker.Wallet.Stars.Count(BakeryArea.k_Id));
            return chance > 0d && m_random.NextDouble() < chance;
        }

        // 빈 들를 곳 하나를 잡는다(난수 자리부터 돌며). 설계 37: 똥 둘레 안 자리는 찬 자리. 다 찼으면 false
        internal bool TryTakeSpot(out int index)
        {
            int count = Layout.Spots.Count;
            index = count == 0 ? -1 : RandomIndex(count);

            for (int tried = 0; tried < count; tried++, index = (index + 1) % count)
            {
                if (!m_takenSpots.Contains(index) && !NearPoop(Layout.Spots[index].Position))
                {
                    m_takenSpots.Add(index);
                    return true;
                }
            }

            index = -1;
            return false;
        }

        internal void ReleaseSpot(int index)
        {
            m_takenSpots.Remove(index);
        }

        // 매 프레임(웜뱃 다음). 순서: 도착 → 손님 → 외출 점원 말풍선 → 행상
        protected override void TickArea(double dt)
        {
            TickArrival(dt);
            TickVisitors(dt);
            TickClerkFigures();
            Merchant.Tick(dt);
        }

        // 설계 31 · 34: 행상이 계단에서 톡 나와 좌판 자리(merchantX · Y)로 걷는다
        internal PlazaVisitor SpawnMerchant(VisitorTable look)
        {
            Vector2 stall = new Vector2((float)m_config.MerchantX, (float)m_config.MerchantY);
            PlazaVisitor merchant = new PlazaVisitor(++m_nextVisitorId, look, this, Layout.StairsInside, Layout.StairsFloor, 0, false, null, stall);
            Spawn(merchant);
            return merchant;
        }

        // 외출한 점원의 광장 그림: 웜뱃이 가까우면 「?」와 웜뱃 쪽 보기, 아니면 딴짓 말풍선(점원의 말풍선 상태를 같이 쓴다)
        private void TickClerkFigures()
        {
            foreach (KeyValuePair<Clerk, PlazaVisitor> pair in m_clerkFigures)
            {
                Clerk clerk = pair.Key;
                PlazaVisitor figure = pair.Value;

                if (!clerk.Idling || clerk.Leaving)
                {
                    continue;
                }

                if (WombatPresent && Vector2.Distance(Wombat.Mover.Position, figure.Position) <= m_questionRange)
                {
                    if (clerk.Bubble.Id != BubbleTable.k_Question)
                    {
                        figure.Hold(clerk.Home.ClerkConfig.QuestionHoldSeconds);
                    }

                    clerk.Bubble.Show(BubbleTable.k_Question);

                    if (!figure.Moving || figure.Held)
                    {
                        figure.Mover.Facing = Mover.FacingOf(Wombat.Mover.Position - figure.Position);
                    }
                }
                else
                {
                    clerk.Bubble.Show(clerk.IdleBubbleId);
                }
            }
        }

        // 설계 38 · 44: 그 곳(점원이 사는 곳)으로 가는 광장 문. 손님은 빵집 문
        internal PlazaDoor DoorOf(WombatArea home)
        {
            return Layout.DoorTo(home.PlazaGate);
        }

        // 외출한 점원(빵집 · 농장)이 그 곳의 문에서 나온다
        private void Bus_ClerkWentOut(Events.ClerkWentOut e)
        {
            Clerk clerk = e.Clerk;
            PlazaDoor door = DoorOf(clerk.Home);
            PlazaVisitor figure = new PlazaVisitor(++m_nextVisitorId, clerk.Look, this, door.Inside, door.Floor, int.MaxValue, false, clerk);
            m_clerkFigures[clerk] = figure;
            ClerkInteractable thing = new ClerkInteractable(Tables.Get<InteractableTable>(ClerkInteractable.k_Id), clerk, this, () => figure.Position);
            m_clerkThings[clerk] = thing;
            Placed.Insert(0, thing);
            Spawn(figure);
        }

        // 돌아가는 그림은 더 깨울 수 없다: 사물을 뺀다
        private void Bus_ClerkReturning(Events.ClerkReturning e)
        {
            if (m_clerkFigures.TryGetValue(e.Clerk, out PlazaVisitor figure))
            {
                figure.ReturnToDoor();
            }

            RemoveClerkThing(e.Clerk);
        }

        private void Bus_ClerkFired(Events.ClerkFired e)
        {
            if (m_clerkFigures.TryGetValue(e.Clerk, out PlazaVisitor figure))
            {
                figure.Dismiss();
            }
        }

        private void ForgetFigure(PlazaVisitor visitor)
        {
            if (visitor.Clerk == null)
            {
                return;
            }

            m_clerkFigures.Remove(visitor.Clerk);
            RemoveClerkThing(visitor.Clerk);
        }

        private void RemoveClerkThing(Clerk clerk)
        {
            if (m_clerkThings.TryGetValue(clerk, out ClerkInteractable thing))
            {
                Placed.Remove(thing);
                m_clerkThings.Remove(clerk);
            }
        }

        protected override IPlacedKind KindOf(string kindId)
        {
            return Tables.Get<DecorationTable>(kindId);
        }

        protected override IPlaced Create(IPlacedKind kind, Vector2 at)
        {
            DecorationData placed = new DecorationData((DecorationTable)kind, at);
            m_decor.Add(placed);
            return placed;
        }

        protected override void Destroy(IPlaced thing)
        {
            m_decor.Remove((DecorationData)thing);
        }

        // 들를 곳 번호가 바뀌므로 손님은 들르던 곳을 놓고 다음으로, 걷던 손님은 새 땅에서 길을 다시 찾는다. 새 사물이 웜뱃 발밑이면 비켜 선다
        protected override void OnPlacementChanged()
        {
            Layout.Rebuild(PlacedThings);
            ClearBuriedPoops();
            SyncPoops();
            m_takenSpots.Clear();

            foreach (PlazaVisitor visitor in m_visitors)
            {
                visitor.Relayout();
            }

            Unstick();
            Bus.Publish(new Events.LayoutChanged(this));
        }

        // 설계 37: 똥은 사물 목록 앞에 들고 손님 땅에 둘레를 건다. 걷는 손님은 새 길을 찾고, 들를 곳에 닿을 수 없으면 건너뛴다
        protected override void OnPoopsChanged()
        {
            SyncPoops();

            foreach (PlazaVisitor visitor in m_visitors)
            {
                visitor.Repath();
            }
        }

        private void SyncPoops()
        {
            Placed.RemoveAll(thing => thing is PoopInteractable);
            Placed.InsertRange(0, Poops);
            ApplyPoopObstacles(Layout.Nav);
        }

        private void TickArrival(double dt)
        {
            m_arrivalElapsed = Math.Min(m_arrivalElapsed + dt, ArrivalSeconds);

            if (m_arrivalElapsed < ArrivalSeconds || m_visitors.Count >= m_config.MaxVisitors)
            {
                return;
            }

            m_arrivalElapsed = 0d;
            VisitorTable look = m_looks[RandomIndex(m_looks.Count)];
            int visits = m_config.VisitsMin + RandomIndex(m_config.VisitsMax - m_config.VisitsMin + 1);
            bool wantsShop = m_random.NextDouble() >= m_config.BrowseChance;
            Spawn(new PlazaVisitor(++m_nextVisitorId, look, this, Layout.StairsInside, Layout.StairsFloor, visits, wantsShop));
        }

        // 빵집에서 나간 손님은 문에서 톡 나와 0 ~ visitsMax곳 들르고 계단으로. 화나서 나갔으면 곧장
        private void Bus_BakeryVisitorLeft(Events.BakeryVisitorLeft e)
        {
            if (e.Visitor.Bakery != Bakery)
            {
                return;
            }

            BakeryVisitor customer = e.Visitor;
            int visits = customer.Angry ? 0 : RandomIndex(m_config.VisitsMax + 1);
            Spawn(new PlazaVisitor(++m_nextVisitorId, customer.Look, this, BakeryDoor.Inside, BakeryDoor.Floor, visits, false));
        }

        private void Spawn(PlazaVisitor visitor)
        {
            m_visitors.Add(visitor);
            Bus.Publish(new Events.PlazaVisitorArrived(visitor));
        }

        private void TickVisitors(double dt)
        {
            for (int i = 0; i < m_visitors.Count; i++)
            {
                PlazaVisitor visitor = m_visitors[i];

                if (visitor.Tick(dt))
                {
                    continue;
                }

                m_visitors.RemoveAt(i);
                i--;
                ForgetFigure(visitor);
                Bus.Publish(new Events.PlazaVisitorLeft(visitor));
            }
        }

        // [0, count)
        private int RandomIndex(int count)
        {
            return Math.Min(count - 1, (int)(m_random.NextDouble() * count));
        }
    }
}
