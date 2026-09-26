using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 18: 곳에 사물을 사서 놓기·옮기기·보관하기. 규칙은 하나(Check): 바닥 안 · 겹침 없음 · 입구를 막지 않음 · 작업 자리가 걷는 땅이고 입구에서 닿음.
    // 곳은 무엇을 파는지(ShopKinds)·놓인 것(PlacedThings)·만들고 없애는 법만 다르다. 보관함은 종류별 개수(상태는 버린다)
    public abstract partial class WombatArea
    {
        private const float k_SpotSearch = 3f;
        private readonly Dictionary<string, int> m_stored = new Dictionary<string, int>(StringComparer.Ordinal);

        // 배치 격자 한 변(ConfigTable placeCell). 사물 밑변 가운데를 여기에 맞춘다
        public float PlaceCell { get; private set; }
        public IReadOnlyDictionary<string, int> Stored => m_stored;
        // 상점 카드에 나올 종류
        public abstract IReadOnlyList<IPlacedKind> ShopKinds { get; }

        protected abstract BurrowShape.Result Shape { get; }
        protected abstract IEnumerable<IPlaced> PlacedThings { get; }
        protected abstract IPlacedKind KindOf(string kindId);
        protected abstract IPlaced Create(IPlacedKind kind, Vector2 at);
        protected abstract void Destroy(IPlaced thing);
        // 놓인 것이 바뀌었다: 걷는 땅·자리를 다시 만들고 LayoutChanged
        protected abstract void OnPlacementChanged();

        protected virtual bool CanRemove(IPlaced thing)
        {
            return true;
        }

        public Vector2 Snap(Vector2 p)
        {
            return new Vector2((float)Math.Round(p.X / PlaceCell) * PlaceCell, (float)Math.Round(p.Y / PlaceCell) * PlaceCell);
        }

        // 설계 20: near에서 가장 가까운 놓을 수 있는 자리(배치 격자 고리를 넓혀 가며, 반지름 k_SpotSearch 안). 상점 카드를 탭하면 화면 가운데 근처에 생긴다
        // ponytail: 고리마다 CanPlace가 걷는 땅을 새로 만든다(최대 31×31회). 탭 한 번이라 두고, 느려지면 후보 사각형만 먼저 거른다
        public bool TryFindSpot(string kindId, Vector2 near, out Vector2 spot)
        {
            // 격자 번호로 계산해야 Snap 결과와 비트까지 같다(더한 값은 1ulp 어긋나 모서리 맞닿음 판정이 뒤집힌다)
            int cx = (int)Math.Round(near.X / PlaceCell);
            int cy = (int)Math.Round(near.Y / PlaceCell);
            int rings = (int)Math.Round(k_SpotSearch / PlaceCell);

            for (int r = 0; r <= rings; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                        {
                            continue;
                        }

                        Vector2 p = new Vector2((cx + dx) * PlaceCell, (cy + dy) * PlaceCell);

                        if (CanPlace(kindId, p) == PlacementCheck.Ok)
                        {
                            spot = p;
                            return true;
                        }
                    }
                }
            }

            spot = Snap(near);
            return false;
        }

        // 놓인 것 + 보관함
        public int CountOf(string kindId)
        {
            int count = StoredCount(kindId);

            foreach (IPlaced thing in PlacedThings)
            {
                if (thing.Kind.Id == kindId)
                {
                    count++;
                }
            }

            return count;
        }

        public int StoredCount(string kindId)
        {
            m_stored.TryGetValue(kindId, out int count);
            return count;
        }

        public double PriceOf(string kindId)
        {
            return KindOf(kindId).Price.CostAt(CountOf(kindId));
        }

        public bool IsMaxed(string kindId)
        {
            return CountOf(kindId) >= KindOf(kindId).Price.Max;
        }

        // 편집에서 집기: 그 점 위에 그려진 사물(뒤에 있는 것 = 위쪽부터 그려지므로 마지막 것)
        public IPlaced ThingAt(Vector2 p)
        {
            IPlaced found = null;

            foreach (IPlaced thing in PlacedThings)
            {
                if (Placement.ContainsPick(thing, p) && (found == null || thing.Position.Y <= found.Position.Y))
                {
                    found = thing;
                }
            }

            return found;
        }

        public PlacementCheck CanPlace(string kindId, Vector2 at)
        {
            return Check(KindOf(kindId), at, null);
        }

        public PlacementCheck CanMove(IPlaced thing, Vector2 at)
        {
            return Check(thing.Kind, at, thing);
        }

        public bool TryBuy(string kindId, Vector2 at)
        {
            IPlacedKind kind = KindOf(kindId);

            if (IsMaxed(kindId) || Check(kind, at, null) != PlacementCheck.Ok || !Wombat.Worker.Wallet.TrySpendCoins(PriceOf(kindId)))
            {
                return false;
            }

            Create(kind, at);
            OnPlacementChanged();
            return true;
        }

        public bool TryPlaceStored(string kindId, Vector2 at)
        {
            IPlacedKind kind = KindOf(kindId);

            if (StoredCount(kindId) == 0 || Check(kind, at, null) != PlacementCheck.Ok)
            {
                return false;
            }

            m_stored[kindId]--;
            Create(kind, at);
            OnPlacementChanged();
            return true;
        }

        // 상태(재고·굽는 중)는 그대로 자리만 옮긴다
        public bool TryMove(IPlaced thing, Vector2 at)
        {
            if (Check(thing.Kind, at, thing) != PlacementCheck.Ok)
            {
                return false;
            }

            thing.MoveTo(at);
            OnPlacementChanged();
            return true;
        }

        // 보관함으로(환불 없음, 다시 놓기 무료). 상태는 버린다
        public bool TryStore(IPlaced thing)
        {
            if (!CanRemove(thing))
            {
                return false;
            }

            Destroy(thing);
            m_stored[thing.Kind.Id] = StoredCount(thing.Kind.Id) + 1;
            OnPlacementChanged();
            return true;
        }

        private PlacementCheck Check(IPlacedKind kind, Vector2 at, IPlaced ignore)
        {
            NavRect rect = Placement.Rect(kind, at);

            if (!Placement.OnFloor(Shape, rect))
            {
                return PlacementCheck.OutsideFloor;
            }

            if (rect.Contains(Entrance, BurrowNav.k_Clearance))
            {
                return PlacementCheck.Overlaps;
            }

            List<NavRect> blocked = new List<NavRect>();

            foreach (IPlaced other in PlacedThings)
            {
                if (other == ignore)
                {
                    continue;
                }

                NavRect otherRect = Placement.Rect(other);

                if (otherRect.Intersects(rect))
                {
                    return PlacementCheck.Overlaps;
                }

                blocked.Add(otherRect);
            }

            // 웜뱃 자리는 그 사물 자체는 안 보고(계산대 뒤에 바짝 붙는다) 다른 것만 본다
            BurrowNav navForWorker = new BurrowNav(Shape, blocked);
            blocked.Add(rect);
            BurrowNav nav = new BurrowNav(Shape, blocked);
            bool needsCustomer = false;
            bool customerWalkable = false;
            bool customerReachable = false;

            foreach (SpotOffset offset in kind.Spots)
            {
                // 손님 자리는 BakeryLayout처럼(y 내림), 줄 머리·웜뱃 자리는 격자 반올림
                Vector2 raw = Placement.SpotAt(at, offset);
                BurrowNav check = offset.Role == SpotRole.Worker ? navForWorker : nav;
                Vector2 spot = offset.Role == SpotRole.Customer ? Placement.SnapSpot(nav, raw) : check.Snap(raw);
                bool walkable = check.IsWalkable(spot);
                bool reachable = walkable && check.FindPath(Entrance, spot).Count > 0;

                if (offset.Role == SpotRole.Customer)
                {
                    needsCustomer = true;
                    customerWalkable |= walkable;
                    customerReachable |= reachable;
                    continue;
                }

                if (!walkable)
                {
                    return PlacementCheck.NoWorkSpot;
                }

                if (!reachable)
                {
                    return PlacementCheck.Unreachable;
                }
            }

            if (needsCustomer && !customerWalkable)
            {
                return PlacementCheck.NoWorkSpot;
            }

            if (needsCustomer && !customerReachable)
            {
                return PlacementCheck.Unreachable;
            }

            return PlacementCheck.Ok;
        }
    }
}
