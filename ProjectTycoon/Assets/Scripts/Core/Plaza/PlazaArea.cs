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
    // 설계 64 가지 굴: 광장도 빵집처럼 판다(BurrowGrid · DigSet). 굴길 열은 아래로, 주머니 칸(PlazaConfigTable pockets)은 정해진 자리만.
    // 문은 주머니 뒷벽이라 주머니를 파야 닿고, 석상 · 행상은 그 주머니를 판 뒤에만 사물이 된다. 장식 편집은 decorAfter 가게가 열린 뒤
    public sealed class PlazaArea : WombatArea
    {
        public const string k_Id = "plaza";

        private readonly PlazaConfigTable m_config;
        private readonly IRandom m_random;
        // 설계 59 손님 소문: 지금 퀘스트 걸음 id(Mall이 건다). DialogueTable rumor_<걸음 id>가 있으면 광장에 들어온 손님이 확률로 한 줄
        internal Func<string> StepId { get; set; }
        private const double k_RumorChance = 0.5;
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
        // 설계 47: 손님이 가는 가게(빵집 · 횟집). 연 가게를 번갈아 보낸다. 닫힌 가게 문 앞 표식
        private readonly List<IShop> m_shops = new List<IShop>();
        private readonly List<ShopGateInteractable> m_gates = new List<ShopGateInteractable>();
        private int m_nextShop;
        // 설계 64: 파기 사물과 석상 · 행상 주머니 칸
        private readonly DigSet m_digs;
        private readonly Cell m_statueCell;
        private readonly Cell m_merchantCell;
        // 마지막으로 본 연 가게 수(바뀌면 after 주머니가 열리거나 장식 편집이 열린다). 첫 틱에도 한 번 맞춘다(저장 복원 뒤)
        private int m_openShops = -1;

        public PlazaLayout Layout { get; }
        public BurrowGrid Grid { get; }
        public BakeryArea Bakery { get; }
        public IReadOnlyList<PlazaVisitor> Visitors => m_visitors;
        public IReadOnlyList<IShop> Shops => m_shops;
        // 닫힌 가게 문 앞 표식(화면이 값 · 별 표식을 띄운다)
        public IReadOnlyList<ShopGateInteractable> Gates => m_gates;
        public IReadOnlyList<DecorationData> Decor => m_decor;
        public override IReadOnlyList<IPlacedKind> ShopKinds => m_shopKinds;
        public override string Id => k_Id;
        // 설계 64: 석상 · 행상 주머니를 팠나, 장식 편집이 열렸나(decorAfter 가게)
        public bool StatueOpen => Grid.Contains(m_statueCell);
        public bool MerchantOpen => Grid.Contains(m_merchantCell);
        public override bool Editable => ShopOpen(m_config.DecorAfter);

        protected override BurrowNav WombatNav => Layout.WombatNav;
        // 손님이 드나드는 빵집 문
        public PlazaDoor BakeryDoor => Layout.DoorTo(BakeryArea.k_Id);
        // 설계 64: 새 게임 · 저장에서 돌아올 때는 계단 밑에 선다(문은 주머니를 파야 닿는다)
        protected override Vector2 Entrance => Layout.StairsFloor;
        protected override IEnumerable<Vector2> Doors => Layout.Doors.Select(door => door.Floor);

        // 행상 자리도 웜뱃이 곁에 서 있다고 영영 기다리지 않는다(설계 34)
        internal override bool IsPassage(Vector2 p)
        {
            return Layout.Doors.Any(door => Vector2.DistanceSquared(p, door.Floor) < 1e-4f) || Vector2.DistanceSquared(p, Layout.StairsFloor) < 1e-4f
                || Vector2.DistanceSquared(p, Layout.MerchantStall) < 1e-4f;
        }

        // 설계 25 · 44: 온 곳의 문 앞에 선다
        protected override Vector2 EntranceFrom(WombatArea from)
        {
            return from == null ? Entrance : DoorOf(from).Floor;
        }
        protected override BurrowShape.Result Shape => Layout.Shape;
        // 설계 29: 석상도 길을 막고 겹침 판정에 들지만, 값이 없는 종류라 편집에서 집지 않는다(WombatArea.ThingAt). 주머니를 파기 전엔 없는 것
        protected override IEnumerable<IPlaced> PlacedThings => StatueOpen ? m_decor.Cast<IPlaced>().Append(Statue) : m_decor;
        public StatueInteractable Statue { get; }
        // 설계 31 · 34: 떠돌이 행상(일정 · 걷는 그림)과 그 행상에게 말 거는 사물
        public RelicMerchant Merchant { get; }
        public MerchantInteractable MerchantThing { get; }
        // 손님이 계단으로 오는 간격(손님 축복 · 풍경이면 짧아진다)
        // 설계 40: 빵집 평가 중에는 손님(맛 평가단)이 rush배로 몰려온다
        // 설계 47: 가게마다 손님이 따로 온다(간격을 연 가게 수로 나눈다)
        private double ArrivalSeconds => m_config.ArrivalSeconds / Wombat.Worker.Wallet.Scale(BlessingTable.k_Visitors)
            / (Bakery.Evaluation.Running ? Bakery.Evaluation.Config.Rush : 1d) / Math.Max(1, m_shops.Count(shop => shop.IsOpen));

        // 첫 손님은 첫 틱에 온다. 시작 장식은 PlazaDecorTable. 시작 굴은 굴길 startRows줄
        public PlazaArea(TableSet tables, BakeryArea bakery, IRandom random, Wombat wombat, EventBus bus) : base(tables, random, wombat, bus)
        {
            m_config = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_random = random;
            Bakery = bakery;
            m_shops.Add(bakery);
            Grid = new BurrowGrid(BurrowGrid.Columns(m_config.ShaftCols, m_config.StartRows), m_config.DigBaseCost, m_config.DigCostGrowth,
                new CellBounds(m_config.ShaftCols + 2, m_config.Rows), bus, Allowed);
            Layout = new PlazaLayout(tables, m_config, Grid.Cells);
            m_digs = new DigSet(tables.Get<InteractableTable>(DigInteractable.k_Id), Grid, Layout.Cells.CellRect, this);

            foreach (PlazaPocketData pocket in m_config.Pockets)
            {
                switch (pocket.Use)
                {
                    case PlazaPocketData.k_Statue:
                        m_statueCell = pocket.Cell;
                        break;
                    case PlazaPocketData.k_Merchant:
                        m_merchantCell = pocket.Cell;
                        break;
                }
            }

            foreach (PlazaDoor door in Layout.Doors)
            {
                Placed.Add(new PassageInteractable(tables.Get<InteractableTable>(PassageInteractable.k_Door), this, door.Floor, door.To));
            }

            Statue = new StatueInteractable(tables.Get<InteractableTable>(StatueInteractable.k_Id), this, Layout.StatueBase, random);
            Merchant = new RelicMerchant(this, tables);
            MerchantThing = new MerchantInteractable(tables.Get<InteractableTable>(MerchantInteractable.k_Id), this, random, Merchant);
            m_arrivalElapsed = m_config.ArrivalSeconds;
            m_questionRange = (float)tables.Get<InteractableTable>(ClerkInteractable.k_Id).Range;
            bus.Subscribe<Events.BakeryVisitorLeft>(Bus_BakeryVisitorLeft);
            bus.Subscribe<Events.RestaurantVisitorLeft>(Bus_RestaurantVisitorLeft);
            bus.Subscribe<Events.FishingVisitorLeft>(Bus_FishingVisitorLeft);
            bus.Subscribe<Events.ClerkWentOut>(Bus_ClerkWentOut);
            bus.Subscribe<Events.ClerkReturning>(Bus_ClerkReturning);
            bus.Subscribe<Events.ClerkFired>(Bus_ClerkFired);
            bus.Subscribe<Events.Dug>(Bus_Dug);

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

            SyncDigs();
            Layout.Rebuild(Grid.Cells, PlacedThings);
        }

        // 설계 64: 팔 수 있는 칸 = 굴길 열 · 주머니 칸(after 가게가 열렸을 때)
        private bool Allowed(Cell cell)
        {
            int first = -m_config.ShaftCols / 2;

            if (cell.Col >= first && cell.Col < first + m_config.ShaftCols)
            {
                return true;
            }

            PlazaPocketData pocket = m_config.Pockets.FirstOrDefault(p => p.Col == cell.Col && p.Row == cell.Row);
            return pocket != null && ShopOpen(pocket.After);
        }

        // 설계 64 튜토리얼 파기 걸음: 그 주머니(문 갈 곳 id 또는 statue · merchant)까지 더 팔 칸 수. 팠으면 0, 아직 못 파는 주머니면 int.MaxValue
        public int DigsTo(string pocket)
        {
            Dictionary<Cell, int> left = DigsLeft(pocket, out Cell target);

            if (Grid.Contains(target))
            {
                return 0;
            }

            int best = int.MaxValue;

            foreach (Cell cell in Grid.Frontier())
            {
                if (left.TryGetValue(cell, out int n) && n < best)
                {
                    best = n;
                }
            }

            return best;
        }

        // 그 칸을 파면 주머니에 한 칸 가까워지나(길잡이가 가리킬 칸)
        public bool OnWayTo(string pocket, Cell cell)
        {
            return DigsLeft(pocket, out _).TryGetValue(cell, out int n) && n == DigsTo(pocket);
        }

        // 안 판 칸마다 거기서 주머니까지 팔 칸 수(그 칸 · 주머니 포함). 주머니에서 거꾸로 넓혀 간다
        private Dictionary<Cell, int> DigsLeft(string pocket, out Cell target)
        {
            target = m_config.Pockets.First(p => (p.To ?? p.Use) == pocket).Cell;
            Dictionary<Cell, int> left = new Dictionary<Cell, int>();

            if (!Diggable(target))
            {
                return left;
            }

            Queue<Cell> queue = new Queue<Cell>();
            left[target] = 1;
            queue.Enqueue(target);

            while (queue.Count > 0)
            {
                Cell cell = queue.Dequeue();

                foreach (Cell next in new[] { cell.Offset(1, 0), cell.Offset(-1, 0), cell.Offset(0, 1), cell.Offset(0, -1) })
                {
                    if (!left.ContainsKey(next) && Diggable(next))
                    {
                        left[next] = left[cell] + 1;
                        queue.Enqueue(next);
                    }
                }
            }

            return left;
        }

        // 아직 안 판, 언젠가 팔 수 있는 칸
        private bool Diggable(Cell cell)
        {
            return cell.Row > BurrowGrid.k_EntranceRow && Grid.Bounds.Contains(cell) && Allowed(cell) && !Grid.Contains(cell);
        }

        // 그 가게(IShop id)가 열렸나. null이면 조건 없음
        private bool ShopOpen(string shopId)
        {
            return shopId == null || m_shops.Any(shop => shop.Id == shopId && shop.IsOpen);
        }

        // 설계 64: 가게가 열리면(횟집 문 · 저장 복원) 주머니 프론티어 · 장식 편집이 바뀐다 — 파기 사물을 다시 맞추고 LayoutChanged로 알린다
        private void SyncOpenShops()
        {
            int open = m_shops.Count(shop => shop.IsOpen);

            if (open == m_openShops)
            {
                return;
            }

            m_openShops = open;
            SyncDigs();
            Bus.Publish(new Events.LayoutChanged(this));
        }

        public override DigInteractable DigAt(Vector2 p)
        {
            return m_digs.At(p);
        }

        // 파기 사물은 목록 끝에(문 · 표식 뒤)
        private void SyncDigs()
        {
            Placed.RemoveAll(thing => thing is DigInteractable);
            m_digs.Sync();
            Placed.AddRange(m_digs.Things);
        }

        // 설계 64: 칸을 팠다 — 굴 · 땅 다시, 석상 · 행상 주머니면 그 사물이 선다
        private void Bus_Dug(Events.Dug e)
        {
            if (e.Grid != Grid)
            {
                return;
            }

            if (e.Cell.Equals(m_statueCell))
            {
                Placed.Add(Statue);
            }

            if (e.Cell.Equals(m_merchantCell))
            {
                Placed.Add(MerchantThing);
            }

            SyncDigs();
            OnPlacementChanged();
        }

        // 들를 곳에서 머무는 초 · ♥를 띄울지
        // 설계 59: 들어온 손님이 지금 걸음의 소문을 말한다(있으면 · 확률)
        internal void Rumor(PlazaVisitor visitor)
        {
            string step = StepId?.Invoke();
            DialogueTable rumor = step == null ? null : Tables.GetAll<DialogueTable>().FirstOrDefault(row => row.Id == "rumor_" + step);

            // 소문 행이 없는 걸음에서는 난수를 쓰지 않는다(오프라인 정산 대조가 같은 난수 순서를 전제)
            if (rumor == null || rumor.Lines.Count == 0 || m_random.NextDouble() >= k_RumorChance)
            {
                return;
            }

            DialogueLineData line = rumor.Lines[0];
            string text = line.Texts[(int)(m_random.NextDouble() * line.Texts.Count) % line.Texts.Count];
            Bus.Publish(new Events.DialogueLine(null, DialogueSpeaker.Visitor, visitor, text, rumor.LineSeconds));
        }

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

        // 매 프레임(웜뱃 다음). 순서: 도착 → 손님 → 외출 점원 말풍선 → 행상(좌판 주머니를 판 뒤부터)
        protected override void TickArea(double dt)
        {
            SyncGates();
            SyncOpenShops();
            TickArrival(dt);
            TickVisitors(dt);
            TickClerkFigures();

            if (MerchantOpen)
            {
                Merchant.Tick(dt);
            }
        }

        // 설계 31 · 34: 행상이 계단에서 톡 나와 좌판 자리(행상 주머니)로 걷는다
        internal PlazaVisitor SpawnMerchant(VisitorTable look)
        {
            PlazaVisitor merchant = new PlazaVisitor(++m_nextVisitorId, look, this, Layout.StairsInside, Layout.StairsFloor, 0, false, null, Layout.MerchantStall);
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
            Layout.Rebuild(Grid.Cells, PlacedThings);
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
            Spawn(new PlazaVisitor(++m_nextVisitorId, look, this, Layout.StairsInside, Layout.StairsFloor, visits, wantsShop, null, null, NextShop()));
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

        // 설계 53: 문이 늘 열린 곳도 손님을 받는 가게가 된다(낚시터는 좌대를 놓으면 IsOpen). 연 가게를 번갈아 손님을 보낸다
        internal void AddVisited(IShop shop)
        {
            m_shops.Add(shop);
        }

        // 설계 47 · 설계 59: 광장 문이 닫힌 채 시작하는 곳(횟집 · 빵집). 닫힌 동안 문 통로는 막히고 문 앞에 여는 사물(gateId 행 shop_gate)이 선다
        internal void AddGate(IGated shop, string gateId)
        {
            PlazaDoor door = Layout.DoorTo(shop.Id);

            foreach (Interactable thing in Placed)
            {
                if (thing is PassageInteractable passage && passage.To == shop.Id)
                {
                    passage.Gate = () => shop.GateOpen;
                }
            }

            ShopGateInteractable gate = new ShopGateInteractable(Tables.Get<InteractableTable>(gateId), this, door.Floor, shop);
            m_gates.Add(gate);
            Placed.Add(gate);
        }

        // 열린 가게의 문 앞 표식은 뺀다(불러온 저장의 열린 가게도)
        private void SyncGates()
        {
            for (int i = m_gates.Count - 1; i >= 0; i--)
            {
                if (m_gates[i].Shop.GateOpen)
                {
                    Placed.Remove(m_gates[i]);
                    m_gates.RemoveAt(i);
                }
            }
        }

        // 연 가게를 번갈아
        private IShop NextShop()
        {
            for (int tried = 0; tried < m_shops.Count; tried++)
            {
                IShop shop = m_shops[m_nextShop++ % m_shops.Count];

                if (shop.IsOpen)
                {
                    return shop;
                }
            }

            return Bakery;
        }

        // 횟집 · 낚시터에서 나간 손님도 문에서 톡 나와 들를 곳을 들르고 계단으로
        private void Bus_RestaurantVisitorLeft(Events.RestaurantVisitorLeft e)
        {
            ReturnFrom(e.Visitor.Restaurant, e.Visitor.Look, e.Visitor.Angry);
        }

        private void Bus_FishingVisitorLeft(Events.FishingVisitorLeft e)
        {
            ReturnFrom(e.Visitor.Fishing, e.Visitor.Look, e.Visitor.Angry);
        }

        private void ReturnFrom(IShop shop, VisitorTable look, bool angry)
        {
            PlazaDoor door = Layout.DoorTo(shop.Id);
            int visits = angry ? 0 : RandomIndex(m_config.VisitsMax + 1);
            Spawn(new PlazaVisitor(++m_nextVisitorId, look, this, door.Inside, door.Floor, visits, false, null, null, shop));
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
