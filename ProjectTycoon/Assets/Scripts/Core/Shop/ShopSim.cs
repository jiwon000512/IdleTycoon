using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08 v0.5: 빵집 규칙의 단일 소유자. 손님 도착 → 진열대 → 줄 → 계산, 오븐 굽기, 업그레이드·빵 해금.
    // 굴 격자 설계 v0.5: 진열대·오븐은 굴 칸의 자리에 놓인다.
    // 손님 동선 설계 v0.2: 매 프레임 돈다. 손님(행동 트리, ShopSim.Customers.cs)의 위치·걷는 시간(A* 길 ÷ walkSpeed)도 여기서 정하고 화면은 읽기만 한다.
    // 설계 09: 웜뱃은 플레이어가 조이스틱으로 움직이고, 가까운 사물(대상)을 버튼으로 다룬다.
    // v0.4: 사물마다 거리·행동은 interactables.json, 행동마다 수동/자동은 actions.json. 행동이 하는 일은 여기(k_Action*)
    public sealed partial class ShopSim
    {
        public const string k_OvenCount = "oven_count";
        public const string k_OvenSpeed = "oven_speed";
        public const string k_ShelfCapacity = "shelf_capacity";
        public const string k_CheckoutSpeed = "checkout_speed";
        public const string k_ActionTakeOut = "take_out";
        public const string k_ActionFill = "fill";
        public const string k_ActionServe = "serve";
        public const string k_ActionOpen = "open";
        public const string k_ActionDig = "dig";

        private readonly ZooState m_state;
        private readonly GameTables m_tables;
        private readonly GameConfig.ShopConfig m_config;
        private readonly IRandom m_random;
        private readonly BurrowGrid m_grid;
        private readonly ShopLayout m_layout;
        private readonly List<BreadRecord> m_unlocked = new List<BreadRecord>();
        private readonly Dictionary<Cell, BreadRecord> m_shelves = new Dictionary<Cell, BreadRecord>();
        private readonly Dictionary<string, int> m_stock = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_levels = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Oven> m_ovens = new List<Oven>();
        // 웜뱃이 끼인 자리(배치가 바뀜)에서 걷는 땅을 찾는 거리(맨해튼)
        private const float k_UnstuckDistance = 3f;

        private readonly Mover m_wombat;
        private Vector2 m_wombatInput;
        private bool m_wombatMoving;
        private readonly InteractableRecord[] m_elements;
        private readonly Dictionary<string, ActionRecord> m_actions = new Dictionary<string, ActionRecord>(StringComparer.Ordinal);
        private readonly List<Interactable> m_inRange = new List<Interactable>();
        private Interactable? m_target;
        private ActionRecord m_targetAction;
        private Interactable? m_lastTarget;
        private BreadRecord m_carried;
        private int m_carriedCount;

        public BurrowGrid Grid => m_grid;
        public ShopLayout Layout => m_layout;
        public IReadOnlyList<BreadRecord> UnlockedBreads => m_unlocked;
        public IReadOnlyDictionary<Cell, BreadRecord> Shelves => m_shelves;
        public IReadOnlyList<Oven> Ovens => m_ovens;
        public int ShelfCapacity => m_config.ShelfCapacity + (int)Effect(k_ShelfCapacity);
        public BreadRecord NextBread => m_unlocked.Count < m_tables.Breads.Count ? m_tables.Breads[m_unlocked.Count] : null;
        // 설계 09 v0.4: 웜뱃이 계산대 range 안에 있나(serve가 auto면 있는 동안만 계산이 흐른다)
        public bool WombatAtCounter => Vector2.Distance(m_wombat.Position, m_layout.WombatHome) <= Range(InteractKind.Counter);
        public Vector2 WombatPosition => m_wombat.Position;
        public Facing WombatFacing => m_wombat.Facing;
        public bool WombatMoving => m_wombatMoving;
        // 자기 range 안에 든 사물 중 가장 가까운 것. 없으면 null
        public Interactable? Target => m_target;
        public BreadRecord Carried => m_carried;
        public int CarriedCount => m_carriedCount;

        // 11장: 대상의 actions를 순서대로 보고 지금 할 수 있는 첫 manual 행동(버튼). 없으면 null
        public ActionRecord TargetAction
        {
            get
            {
                if (!m_target.HasValue)
                {
                    return null;
                }

                foreach (string id in Element(m_target.Value.Kind).Actions)
                {
                    ActionRecord action = m_actions[id];

                    if (!action.IsAuto && CanDo(id, m_target.Value))
                    {
                        return action;
                    }
                }

                return null;
            }
        }

        public event Action<string> StockChanged;
        public event Action<int> OvenChanged;
        // 굴을 팠거나 자리에 진열대·오븐이 놓였다
        public event Action LayoutChanged;
        public event Action UpgradesChanged;
        // 대상이 바뀌었거나 대상의 버튼 행동이 바뀌었다(다 구웠다·빵을 들었다 등)
        public event Action TargetChanged;
        public event Action CarryChanged;

        // 첫 손님은 첫 틱에 온다
        public ShopSim(ZooState state, GameTables tables, IRandom random)
        {
            m_state = state;
            m_tables = tables;
            m_config = tables.Config.Shop;
            m_random = random;
            m_grid = new BurrowGrid(state, m_config);
            m_grid.Dug += Grid_Dug;
            m_layout = new ShopLayout(m_config);
            m_elements = new InteractableRecord[Interactable.k_KindIds.Length];

            foreach (InteractableRecord element in tables.Interactables)
            {
                m_elements[Array.IndexOf(Interactable.k_KindIds, element.Id)] = element;
            }

            foreach (ActionRecord action in tables.Actions)
            {
                m_actions[action.Id] = action;
            }
            m_arrivalElapsed = m_config.ArrivalSeconds;

            // 시작 배치: 왼쪽 열(−1)의 자리 줄 두 곳에 첫 빵과 오븐. 오른쪽 열(0)은 빈 자리
            Unlock(tables.Breads[0], new Cell(-1, 1));
            m_ovens.Add(new Oven { Cell = new Cell(-1, 3) });
            RebuildLayout();
            m_wombat = new Mover(m_layout.WombatHome, Facing.Down);
            RefreshTarget();
        }

        // 자리는 있는데 진열대도 오븐도 없는 칸
        public IEnumerable<Cell> EmptySlots
        {
            get
            {
                foreach (Cell cell in m_grid.Cells)
                {
                    if (IsEmptySlot(cell))
                    {
                        yield return cell;
                    }
                }
            }
        }

        public bool IsEmptySlot(Cell cell)
        {
            return m_grid.HasSlot(cell) && !m_shelves.ContainsKey(cell) && OvenAt(cell) < 0;
        }

        // 그 칸의 오븐 번호. 없으면 −1
        public int OvenAt(Cell cell)
        {
            for (int i = 0; i < m_ovens.Count; i++)
            {
                if (m_ovens[i].Cell.Equals(cell))
                {
                    return i;
                }
            }

            return -1;
        }

        public Cell ShelfCell(string breadId)
        {
            foreach (KeyValuePair<Cell, BreadRecord> pair in m_shelves)
            {
                if (pair.Value.Id == breadId)
                {
                    return pair.Key;
                }
            }

            throw new KeyNotFoundException($"빵 '{breadId}'의 진열대가 없다.");
        }

        public int Stock(string breadId)
        {
            return m_stock[breadId];
        }

        // 오븐 외형 2단계(shop_upgrades.oven_speed.lookLevel 이상)
        public bool OvenLookUpgraded
        {
            get
            {
                ShopUpgradeRecord record = GetUpgrade(k_OvenSpeed);
                return record.LookLevel > 0 && UpgradeLevel(k_OvenSpeed) >= record.LookLevel;
            }
        }

        // 사물 시트 효과 전후 표시용: 그 단계일 때의 게임 값(진열대 용량·오븐 수·속도 배수)
        public double UpgradeValue(string upgradeId, int level)
        {
            double effect = GetUpgrade(upgradeId).EffectPerLevel * level;

            switch (upgradeId)
            {
                case k_ShelfCapacity: return m_config.ShelfCapacity + effect;
                case k_OvenCount: return m_config.OvenCount + effect;
                default: return 1d + effect;
            }
        }

        public int UpgradeLevel(string upgradeId)
        {
            m_levels.TryGetValue(upgradeId, out int level);
            return level;
        }

        public double UpgradeCost(string upgradeId)
        {
            ShopUpgradeRecord record = GetUpgrade(upgradeId);
            return record.BaseCost * Math.Pow(record.CostGrowth, UpgradeLevel(upgradeId));
        }

        public bool IsMaxed(string upgradeId)
        {
            return UpgradeLevel(upgradeId) >= GetUpgrade(upgradeId).MaxLevel;
        }

        // 매 프레임. 순서: 웜뱃(이동 → 대상) → 오븐 → 손님 행동 트리 → 계산 → 도착
        public void Tick(double dt)
        {
            TickWombat(dt);
            TickOvens(dt);
            TickCustomers(dt);
            TickCheckout(dt);
            TickArrival(dt);
        }

        // 조이스틱 방향(길이 1까지). 다음 Tick부터 이 방향으로 걷는다
        public void SetWombatInput(Vector2 input)
        {
            float length = input.Length();
            m_wombatInput = length > 1f ? input / length : input;
        }

        // 설계 09: 시트에서 빵을 고르면 바로 굽는다(오븐 시트는 웜뱃이 오븐 앞에 있을 때만 열린다)
        public bool TryBake(int ovenIndex, string breadId)
        {
            Oven oven = m_ovens[ovenIndex];
            BreadRecord bread = m_unlocked.Find(b => b.Id == breadId);

            if (!oven.IsEmpty || bread == null)
            {
                return false;
            }

            oven.Bread = bread;
            oven.Remaining = bread.BakeSeconds;
            OnOvenChanged(ovenIndex);
            return true;
        }

        // 버튼: 대상의 manual 행동을 한다. 시트 열기(open·dig)는 화면 몫이라 false
        public bool TryInteract()
        {
            ActionRecord action = TargetAction;

            if (action == null || action.Id == k_ActionOpen || action.Id == k_ActionDig)
            {
                return false;
            }

            Do(action.Id, m_target.Value);
            return true;
        }

        public bool TryUpgrade(string upgradeId)
        {
            if (IsMaxed(upgradeId) || !m_state.TrySpendCoins(UpgradeCost(upgradeId)))
            {
                return false;
            }

            m_levels[upgradeId] = UpgradeLevel(upgradeId) + 1;
            OnUpgradesChanged();
            return true;
        }

        // 오븐 추가(oven_count)는 빈 자리를 골라 산다
        public bool TryAddOven(Cell cell)
        {
            if (!IsEmptySlot(cell) || !TryUpgrade(k_OvenCount))
            {
                return false;
            }

            m_ovens.Add(new Oven { Cell = cell });
            RebuildLayout();
            OnLayoutChanged();
            return true;
        }

        // 빵은 breads.json 행 순서대로만 해금한다(설계 08 결정 1). 진열대는 고른 빈 자리에
        public bool TryUnlockNextBread(Cell cell)
        {
            BreadRecord next = NextBread;

            if (next == null || !IsEmptySlot(cell) || !m_state.TrySpendCoins(next.UnlockCost))
            {
                return false;
            }

            Unlock(next, cell);
            RebuildLayout();
            OnLayoutChanged();
            OnUpgradesChanged();
            return true;
        }

        private void Unlock(BreadRecord bread, Cell cell)
        {
            m_unlocked.Add(bread);
            m_shelves[cell] = bread;
            m_stock[bread.Id] = 0;
        }

        private void Grid_Dug(Cell cell)
        {
            RebuildLayout();
            OnLayoutChanged();
        }

        // 배치가 바뀌면 걷는 땅·줄 자리·서는 자리를 다시 만들고, 걷는 중인 손님·웜뱃은 새 땅에서 길을 다시 찾는다
        private void RebuildLayout()
        {
            List<Cell> ovenCells = new List<Cell>();

            foreach (Oven oven in m_ovens)
            {
                ovenCells.Add(oven.Cell);
            }

            m_layout.Rebuild(m_grid.Cells, m_shelves.Keys, ovenCells, m_config.MaxCustomers);
            RepathCustomers();

            if (m_wombat == null)
            {
                return;
            }

            // 새 사물이 웜뱃 발밑에 놓이면 가까운 걷는 땅으로 비켜 선다
            BurrowNav nav = m_layout.WombatNav;

            if (!nav.IsWalkable(m_wombat.Position) && nav.TryNearestFree(m_wombat.Position, _ => false, k_UnstuckDistance, out Vector2 free))
            {
                m_wombat.Place(free);
            }

            // 대상은 다음 Tick에 고른다. 여기서 고르면 LayoutChanged보다 TargetChanged가 먼저 나가 화면에 없는 사물(새 오븐·진열대)을 가리킨다
        }

        // 설계 09 3장: 조이스틱 방향으로 걷고, 막히면 X만·Y만 시도해 벽을 따라 미끄러진다. 걸은 뒤 대상을 다시 고른다
        private void TickWombat(double dt)
        {
            Vector2 from = m_wombat.Position;
            Vector2 step = m_wombatInput * (float)(m_config.WombatSpeed * dt);
            BurrowNav nav = m_layout.WombatNav;
            Vector2 to = from + step;

            if (!nav.IsWalkable(to))
            {
                to = from + new Vector2(step.X, 0f);
            }

            if (!nav.IsWalkable(to))
            {
                to = from + new Vector2(0f, step.Y);
            }

            m_wombatMoving = to != from && nav.IsWalkable(to);

            if (m_wombatMoving)
            {
                m_wombat.Place(to);
                m_wombat.Facing = Mover.FacingOf(to - from);
            }

            RefreshTarget();
        }

        // 11장: range 안 사물을 모으고, 그 사물들의 auto 행동을 할 수 있으면 하고, 가장 가까운 것을 버튼 대상으로
        private void RefreshTarget()
        {
            Vector2 p = m_wombat.Position;
            float best = float.MaxValue;
            Interactable? found = null;
            m_inRange.Clear();

            foreach (Cell cell in m_shelves.Keys)
            {
                Consider(new Interactable(InteractKind.Shelf, cell), Vector2.Distance(p, m_layout.ShelfBase(cell)), ref found, ref best);
            }

            for (int i = 0; i < m_ovens.Count; i++)
            {
                Consider(new Interactable(InteractKind.Oven, m_ovens[i].Cell, i), Vector2.Distance(p, m_layout.OvenBase(m_ovens[i].Cell)), ref found, ref best);
            }

            Consider(new Interactable(InteractKind.Counter, m_grid.Counter), Vector2.Distance(p, m_layout.WombatHome), ref found, ref best);

            foreach (Cell cell in EmptySlots)
            {
                Consider(new Interactable(InteractKind.EmptySlot, cell), Vector2.Distance(p, m_layout.SlotBase(cell)), ref found, ref best);
            }

            foreach (Cell cell in m_grid.Frontier())
            {
                Consider(new Interactable(InteractKind.Dig, cell), m_layout.DistanceToCell(cell, p), ref found, ref best);
            }

            foreach (Interactable element in m_inRange)
            {
                foreach (string id in Element(element.Kind).Actions)
                {
                    // serve auto는 할 일이 아니라 TickCheckout의 타이머 조건이다
                    if (m_actions[id].IsAuto && id != k_ActionServe && CanDo(id, element))
                    {
                        Do(id, element);
                    }
                }
            }

            m_target = found;
            ActionRecord action = TargetAction;

            if (!Nullable.Equals(found, m_lastTarget) || action != m_targetAction)
            {
                m_lastTarget = found;
                m_targetAction = action;
                OnTargetChanged();
            }
        }

        private void Consider(Interactable element, float distance, ref Interactable? found, ref float best)
        {
            if (distance > Range(element.Kind))
            {
                return;
            }

            m_inRange.Add(element);

            if (distance < best)
            {
                best = distance;
                found = element;
            }
        }

        private InteractableRecord Element(InteractKind kind)
        {
            return m_elements[(int)kind];
        }

        private float Range(InteractKind kind)
        {
            return (float)Element(kind).Range;
        }

        // 행동을 지금 할 수 있나. 시트 열기는 언제나
        private bool CanDo(string actionId, Interactable element)
        {
            switch (actionId)
            {
                case k_ActionTakeOut:
                    return CanTakeOut(m_ovens[element.Index]);
                case k_ActionFill:
                    return CanFill(element.Cell);
                case k_ActionServe:
                    return HeadWaiting;
                default:
                    return true;
            }
        }

        private void Do(string actionId, Interactable element)
        {
            switch (actionId)
            {
                case k_ActionTakeOut:
                    TakeOut(element.Index);
                    break;
                case k_ActionFill:
                    Fill(element.Cell);
                    break;
                case k_ActionServe:
                    PayHead();
                    break;
            }
        }

        private bool CanTakeOut(Oven oven)
        {
            return oven.Ready > 0 && m_carriedCount < m_config.CarryCapacity && (m_carried == null || m_carried == oven.Bread);
        }

        private bool CanFill(Cell shelf)
        {
            return m_carried != null && m_shelves[shelf] == m_carried && m_stock[m_carried.Id] < ShelfCapacity;
        }

        // 다 구운 빵을 들 수 있는 만큼 머리에 인다. 오븐이 비면 빈 오븐이 된다
        private void TakeOut(int ovenIndex)
        {
            Oven oven = m_ovens[ovenIndex];
            int count = Math.Min(oven.Ready, m_config.CarryCapacity - m_carriedCount);
            m_carried = oven.Bread;
            m_carriedCount += count;
            oven.Ready -= count;

            if (oven.Ready == 0)
            {
                oven.Bread = null;
            }

            OnOvenChanged(ovenIndex);
            OnCarryChanged();
        }

        // 진열대 자리만큼 내려놓고 남은 건 계속 든다
        private void Fill(Cell shelf)
        {
            string breadId = m_carried.Id;
            int count = Math.Min(m_carriedCount, ShelfCapacity - m_stock[breadId]);
            m_stock[breadId] += count;
            m_carriedCount -= count;

            if (m_carriedCount == 0)
            {
                m_carried = null;
            }

            OnStockChanged(breadId);
            OnCarryChanged();
        }

        // 걷는 중이면 지금 선분을 마저 걷고 그 끝점에서 새 길을 찾는다. 닿을 수 없으면 곧장(ponytail: 막힌 배치에서만 생김)
        private void Walk(Mover mover, Vector2 target, Facing arrive)
        {
            Vector2 from = mover.NextNode;
            List<Vector2> path = m_layout.Nav.FindPath(from, target);

            if (path.Count == 0 && Vector2.DistanceSquared(from, target) > 1e-6f)
            {
                path.Add(target);
            }

            if (mover.Moving)
            {
                path.Insert(0, from);
            }

            mover.Follow(path, arrive);
        }

        // 설계 09: 다 구운 빵은 웜뱃이 꺼낼 때까지 오븐에서 기다린다(자동 진열 없음).
        // 오븐 사건은 남은 초(올림)가 바뀔 때만 낸다. 진행 막대는 화면이 매 프레임 읽는다
        private void TickOvens(double dt)
        {
            for (int i = 0; i < m_ovens.Count; i++)
            {
                Oven oven = m_ovens[i];

                if (oven.IsEmpty || oven.Remaining <= 0d)
                {
                    continue;
                }

                double before = Math.Ceiling(oven.Remaining);
                oven.Remaining -= dt * (1d + Effect(k_OvenSpeed));

                if (oven.Remaining <= 0d)
                {
                    oven.Remaining = 0d;
                    oven.Ready = oven.Bread.BatchSize;
                }

                if (Math.Ceiling(oven.Remaining) != before)
                {
                    OnOvenChanged(i);
                }
            }
        }

        private double Effect(string upgradeId)
        {
            return GetUpgrade(upgradeId).EffectPerLevel * UpgradeLevel(upgradeId);
        }

        private ShopUpgradeRecord GetUpgrade(string upgradeId)
        {
            for (int i = 0; i < m_tables.ShopUpgrades.Count; i++)
            {
                if (m_tables.ShopUpgrades[i].Id == upgradeId)
                {
                    return m_tables.ShopUpgrades[i];
                }
            }

            throw new KeyNotFoundException($"업그레이드 ID '{upgradeId}'가 shop_upgrades.json에 없다.");
        }

        private void OnStockChanged(string breadId)
        {
            StockChanged?.Invoke(breadId);
        }

        private void OnOvenChanged(int ovenIndex)
        {
            OvenChanged?.Invoke(ovenIndex);
        }

        private void OnLayoutChanged()
        {
            LayoutChanged?.Invoke();
        }

        private void OnUpgradesChanged()
        {
            UpgradesChanged?.Invoke();
        }

        private void OnTargetChanged()
        {
            TargetChanged?.Invoke();
        }

        private void OnCarryChanged()
        {
            CarryChanged?.Invoke();
        }
    }
}
