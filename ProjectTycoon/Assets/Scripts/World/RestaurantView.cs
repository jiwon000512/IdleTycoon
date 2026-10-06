using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GameKit.Audio;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 47: 횟집 굴 화면. 굴 그림은 Core 마스크를 칠하고, 사물(수조 · 도마 · 탁자)은 Core 목록을 객체 키로 맞춘다(LayoutChanged 한 번에 만들고 · 옮기고 · 지운다).
    // 수조 물 창에 담긴 물고기 그림자가 두 줄로 헤엄치고, 도마 위에는 뜨는 동안 게이지, 탁자에는 먹는 손님 몫 접시. 손님 그림은 빵집과 같은 VisitorView(고른 물고기 말풍선)
    public sealed class RestaurantView : MonoBehaviour, IAreaView
    {
        // 수조 물 창 안에 보이는 그림자 수 · 헤엄 속도(라디안/초)
        private const int k_FishShown = 4;
        private const float k_FishSpeed = 0.6f;
        // 도마 게이지는 도마 왼쪽 옆(뒤에 선 웜뱃을 가리지 않게) · 탁자 위 접시 높이(사물 밑변에서, 유닛). 게이지는 말풍선 아래 층
        private static readonly Vector3 k_TimerOffset = new Vector3(-1.0f, 0.55f, 0f);
        private const int k_TimerOrder = 45;
        // 탁자 윗면(아트방 table.png 가운데가 피벗 위 0.66)에 손님 쪽으로
        private const float k_PlateHeight = 0.5f;
        private const float k_PlateSide = 0.25f;
        private const float k_Pixel = 1f / BurrowShape.k_PixelsPerUnit;
        private const string k_CoinKey = "coin_popup";
        // 도마 위 물고기(아트방 뜨기 단계 그림 <접시>_cut_0~2, 윗면 가운데) · 뜨는 동안 칼질 간격(초)과 튀는 조각
        private const float k_BoardFishHeight = 0.62f;
        private const float k_ChopSeconds = 0.3f;
        private static readonly Color k_Chip = new Color(0.98f, 0.86f, 0.82f, 1f);

        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("입구 아치(나가기 대상이면 튄다)")]
        [SerializeField] private Transform m_arch;
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [SerializeField] private Texture2D m_wallFace;
        [SerializeField] private WombatView m_wombat;
        [SerializeField] private VisitorView m_visitorPrefab;
        [SerializeField] private MarkerView m_digTagPrefab;
        [SerializeField] private SpriteAnimator m_poopPrefab;
        [SerializeField] private Sprite[] m_poopFrames;
        [SerializeField] private float m_poopFrameRate = 2f;
        [SerializeField] private CoinPopup m_popupPrefab;
        [Tooltip("사물 그림(아트방 그림이 올 때까지 빵집 그림을 빌린다)")]
        [SerializeField] private Sprite m_tankSprite;
        [SerializeField] private Sprite m_boardSprite;
        [SerializeField] private Sprite m_tableSprite;
        [Tooltip("도마 뜨기 게이지(계산대 출력기 프레임을 빌린다)")]
        [SerializeField] private Sprite[] m_timerFrames;
        [Tooltip("물속 물고기 그림자(이름 = 물고기 id + _swim)")]
        [SerializeField] private Sprite[] m_fishSwim;
        [Tooltip("수조 물 창(밑변 가운데 기준 유닛, 아트방 tank.png 13~161 · 33~83px)")]
        [SerializeField] private Rect m_water = new Rect(-0.925f, 0.738f, 1.85f, 0.625f);
        [SerializeField] private float m_digSeconds = 0.6f;

        private sealed class ThingArt
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer[] Extras;
        }

        private readonly Dictionary<Interactable, ThingArt> m_things = new Dictionary<Interactable, ThingArt>();
        private readonly Dictionary<RestaurantVisitor, VisitorView> m_units = new Dictionary<RestaurantVisitor, VisitorView>();
        // 도마마다 지난 프레임의 뜨기 진행 · 다음 칼질 시각
        private readonly Dictionary<CuttingBoardInteractable, float> m_chops = new Dictionary<CuttingBoardInteractable, float>();
        private readonly Dictionary<CuttingBoardInteractable, float> m_nextChop = new Dictionary<CuttingBoardInteractable, float>();
        private Sprite m_square;
        private RestaurantArea m_shop;
        private FrameCache m_frames;
        private TableSet m_tables;
        private DigView m_dig;
        private PoopViews m_poopViews;
        private GhostView m_ghost;
        private Interactable m_shownTarget;
        private bool m_built;
        private bool m_editing;
        private IPlaced m_held;
        private IDisposable[] m_subscriptions;

        // 굴을 팠다(카메라 경계가 넓어진다)
        public event Action Expanded;

        public Vector3 Origin => transform.position;
        public Transform Wombat => m_wombat.transform;

        // 파낸 칸의 경계 + 좌·우·아래로 한 칸. 위는 입구 윗변(빵집과 같다)
        public Rect Bounds
        {
            get
            {
                int minCol = int.MaxValue, maxCol = int.MinValue, maxRow = 0;

                foreach (Cell cell in m_shop.Grid.Cells)
                {
                    minCol = Mathf.Min(minCol, cell.Col);
                    maxCol = Mathf.Max(maxCol, cell.Col);
                    maxRow = Mathf.Max(maxRow, cell.Row);
                }

                CellMetrics cells = m_shop.Layout.Cells;
                float xMin = transform.position.x + (minCol - 1) * cells.CellWidth;
                float xMax = transform.position.x + (maxCol + 2) * cells.CellWidth;
                float yMin = transform.position.y - cells.RowTop(maxRow + 2);
                return new Rect(xMin, yMin, xMax - xMin, transform.position.y - yMin);
            }
        }

        public void Bind(RestaurantArea shop, EventBus bus, FrameCache frames, TableSet tables)
        {
            m_shop = shop;
            m_frames = frames;
            m_tables = tables;
            m_ghost = GhostView.Create(transform);
            CellMetrics cells = shop.Layout.Cells;
            m_dig = new DigView(this, m_digTagPrefab, tables, shop.Grid, cell => ToWorld(cells.CellCenter(cell)), new Vector2(cells.CellWidth, cells.CellHeight), m_digSeconds);
            m_poopViews = new PoopViews(this, shop, bus, m_poopPrefab, m_poopFrames, m_poopFrameRate, m_popupPrefab, tables, frames, ToWorld);
            m_arch.localPosition = new Vector3(0f, -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit, 0f);
            Repaint();
            Build();
            m_wombat.BindHands(shop, transform, frames);
            m_built = true;

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.Upgraded>(Bus_Upgraded),
                bus.Subscribe<Events.Dug>(Bus_Dug),
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.RestaurantVisitorArrived>(Bus_VisitorArrived),
                bus.Subscribe<Events.RestaurantVisitorPaid>(Bus_VisitorPaid),
                bus.Subscribe<Events.RestaurantVisitorLeft>(Bus_VisitorLeft),
                bus.Subscribe<Events.DishServed>(Bus_DishServed),
                bus.Subscribe<Events.TankFilled>(Bus_TankFilled),
                bus.Subscribe<Events.FishPlaced>(Bus_FishPlaced),
            };
        }

        public Vector3 ToWorld(System.Numerics.Vector2 p)
        {
            return transform.position + new Vector3(p.X, p.Y, 0f);
        }

        // 편집 모드: 팔 수 있는 흙 칸의 태그를 늘 보이고, 옮길 수 있는 사물은 점선 외곽선 + 한 번 톡
        public void SetEditing(bool editing)
        {
            m_editing = editing;
            m_dig.Refresh(editing, m_shownTarget);
            RefreshOutlines();

            if (!editing)
            {
                return;
            }

            foreach (ThingArt art in m_things.Values)
            {
                StartCoroutine(Fx.Bounce(art.Body.transform));
            }
        }

        public void SetHeld(IPlaced held)
        {
            m_held = held;
            RefreshOutlines();
        }

        public void ShowGhost(IPlacedKind kind, System.Numerics.Vector2 at, bool ok)
        {
            m_ghost.Show(SpriteOf(kind.Id), ToWorld(at), ok);
        }

        public void HideGhost()
        {
            m_ghost.Hide();
        }

        // 수조 물고기 · 도마 게이지 · 탁자 접시는 매 프레임 Core를 읽는다
        private void Update()
        {
            if (m_shop == null)
            {
                return;
            }

            foreach (KeyValuePair<Interactable, ThingArt> pair in m_things)
            {
                switch (pair.Key)
                {
                    case TankInteractable tank:
                        ShowFish(tank, pair.Value);
                        break;
                    case CuttingBoardInteractable board:
                        ShowTimer(board, pair.Value);
                        break;
                    case DiningTableInteractable table:
                        ShowPlates(table, pair.Value);
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            if (m_subscriptions == null)
            {
                return;
            }

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }

            m_poopViews.Dispose();
        }

        // ---------- 사물 ----------

        private Sprite SpriteOf(string kindId)
        {
            switch (kindId)
            {
                case TankInteractable.k_Id: return m_tankSprite;
                case CuttingBoardInteractable.k_Id: return m_boardSprite;
                default: return m_tableSprite;
            }
        }

        // 사물 뷰를 Core 목록에 맞춘다: 없는 것은 만들고(놓인 것은 튐), 사라진 것은 지우고, 자리는 늘 다시 놓는다
        private void Build()
        {
            List<Interactable> things = new List<Interactable>();
            things.AddRange(m_shop.Tanks);
            things.AddRange(m_shop.Boards);
            things.AddRange(m_shop.DiningTables);

            foreach (Interactable old in new List<Interactable>(m_things.Keys))
            {
                if (!things.Contains(old))
                {
                    Destroy(m_things[old].Root.gameObject);
                    m_things.Remove(old);
                }
            }

            foreach (Interactable thing in things)
            {
                if (!m_things.TryGetValue(thing, out ThingArt art))
                {
                    art = NewArt(thing);
                    m_things[thing] = art;

                    if (m_built)
                    {
                        StartCoroutine(Fx.Bounce(art.Body.transform));
                    }
                }

                art.Root.position = ToWorld(((IPlaced)thing).Position);
            }
        }

        // 사물 하나 = 발끝 SortingGroup(몸 0 · 안쪽 그림 1). 도마 게이지는 말풍선 아래 층
        private ThingArt NewArt(Interactable thing)
        {
            Transform root = new GameObject(thing.Table.Id, typeof(SortingGroup)).transform;
            root.SetParent(transform, false);
            ThingArt art = new ThingArt { Root = root, Body = NewRenderer(root, "Body", SpriteOf(thing.Table.Id), 0) };

            switch (thing)
            {
                case TankInteractable _:
                    art.Extras = new SpriteRenderer[k_FishShown];

                    for (int i = 0; i < k_FishShown; i++)
                    {
                        art.Extras[i] = NewRenderer(root, "Fish", null, 1);
                    }

                    break;
                case CuttingBoardInteractable _:
                    SpriteRenderer timer = NewRenderer(root, "Timer", m_timerFrames[0], k_TimerOrder);
                    timer.transform.localPosition = k_TimerOffset;
                    SpriteRenderer fish = NewRenderer(root, "Fish", null, 1);
                    fish.transform.localPosition = Vector3.up * k_BoardFishHeight;
                    art.Extras = new[] { timer, fish };
                    break;
                case DiningTableInteractable table:
                    art.Extras = new SpriteRenderer[table.Kind.Spots.Count];

                    for (int i = 0; i < art.Extras.Length; i++)
                    {
                        float side = Mathf.Sign((float)table.Kind.Spots[i].Dx);
                        art.Extras[i] = NewRenderer(root, "Plate", null, 1);
                        art.Extras[i].transform.localPosition = new Vector3(side * k_PlateSide, k_PlateHeight, 0f);
                    }

                    break;
            }

            return art;
        }

        // 담긴 물고기를 회 순서대로 넷까지, 위아래 두 줄로 물 창 안을 좌우로 헤엄친다(1px 격자)
        private void ShowFish(TankInteractable tank, ThingArt art)
        {
            int shown = 0;

            foreach (DishTable dish in m_shop.Dishes)
            {
                for (int n = tank.CountOf(dish); n > 0 && shown < art.Extras.Length; n--, shown++)
                {
                    SpriteRenderer fish = art.Extras[shown];
                    fish.sprite = Swim(dish.Fish);
                    float half = m_water.width * 0.5f - fish.sprite.bounds.extents.x;
                    float phase = Time.time * k_FishSpeed * (1f + 0.15f * shown) + shown * 1.7f;
                    float x = m_water.center.x + Mathf.Sin(phase) * half;
                    float y = m_water.yMin + m_water.height * (shown % 2 == 0 ? 0.7f : 0.3f);
                    fish.transform.localPosition = new Vector3(Mathf.Round(x / k_Pixel) * k_Pixel, Mathf.Round(y / k_Pixel) * k_Pixel, 0f);
                    fish.flipX = Mathf.Cos(phase) < 0f;
                    fish.enabled = true;
                }
            }

            for (int i = shown; i < art.Extras.Length; i++)
            {
                art.Extras[i].enabled = false;
            }
        }

        private Sprite Swim(string fishItem)
        {
            foreach (Sprite sprite in m_fishSwim)
            {
                if (sprite.name == fishItem + "_swim")
                {
                    return sprite;
                }
            }

            return m_fishSwim[0];
        }

        // 게이지는 주문이 있는 동안, 놓인 물고기는 뜨기 진행으로 통째 → 반쯤 → 조각(1/3씩, 칼질마다 톡 + 조각)
        private void ShowTimer(CuttingBoardInteractable board, ThingArt art)
        {
            SpriteRenderer timer = art.Extras[0];
            timer.enabled = board.Orders.Count > 0;
            int frame = board.Cutting ? 1 + Mathf.RoundToInt(Mathf.Clamp01((float)board.Progress) * (m_timerFrames.Length - 2)) : 0;
            timer.sprite = m_timerFrames[frame];

            SpriteRenderer fish = art.Extras[1];
            RestaurantVisitor order = board.OnBoard;
            fish.enabled = order != null;

            if (order == null)
            {
                m_chops.Remove(board);
                return;
            }

            float progress = (float)board.Progress;
            fish.sprite = m_frames.Get(order.Dish.Sprite + "_cut_" + Mathf.Min(2, (int)(progress * 3f)))[0];

            if (m_chops.TryGetValue(board, out float last) && progress > last && Time.time >= NextChop(board))
            {
                m_nextChop[board] = Time.time + k_ChopSeconds;
                StartCoroutine(Fx.Bounce(fish.transform));

                if (m_shop.WombatPresent)
                {
                    SoundManager.Instance.Play(SoundTable.k_Cut);
                }

                m_square = m_square != null ? m_square : Fx.NewSquare();
                StartCoroutine(Fx.Burst(transform, m_square, fish.transform.position, 3, 0.2f, 0.6f, 6f, 0.25f, 2, k_Chip, k_TimerOrder));
            }

            m_chops[board] = progress;
        }

        private float NextChop(CuttingBoardInteractable board)
        {
            return m_nextChop.TryGetValue(board, out float next) ? next : 0f;
        }

        // 먹는 손님 몫 접시(그 손님 자리 쪽)
        private void ShowPlates(DiningTableInteractable table, ThingArt art)
        {
            for (int i = 0; i < art.Extras.Length; i++)
            {
                art.Extras[i].enabled = false;
            }

            foreach (RestaurantVisitor visitor in m_shop.Visitors)
            {
                if (visitor.Table == table && visitor.Eating && visitor.Seat >= 0 && visitor.Seat < art.Extras.Length)
                {
                    art.Extras[visitor.Seat].sprite = m_frames.Get(visitor.Dish.Sprite)[0];
                    art.Extras[visitor.Seat].enabled = true;
                }
            }
        }

        private void RefreshOutlines()
        {
            foreach (KeyValuePair<Interactable, ThingArt> pair in m_things)
            {
                EditOutlineView outline = EditOutlineView.Attach(pair.Value.Body);

                if (m_editing)
                {
                    outline.Show(pair.Key == m_held);
                }
                else
                {
                    outline.Hide();
                }
            }
        }

        private void Bounce(Interactable target)
        {
            switch (target)
            {
                case DigInteractable dig:
                    m_dig.Bounce(dig.Cell);
                    break;
                case PassageInteractable _:
                    StartCoroutine(Fx.Bounce(m_arch));
                    break;
                case PoopInteractable poop:
                    m_poopViews.Bounce(poop);
                    break;
                default:
                    if (target != null && m_things.TryGetValue(target, out ThingArt art))
                    {
                        StartCoroutine(Fx.Bounce(art.Body.transform));
                    }

                    break;
            }
        }

        // ---------- 사건 ----------

        private void Bus_LayoutChanged(Events.LayoutChanged e)
        {
            if (e.Area == m_shop)
            {
                Build();
                m_dig.Refresh(m_editing, m_shownTarget);
                RefreshOutlines();
            }
        }

        private void Bus_Upgraded(Events.Upgraded e)
        {
            if (e.Area != m_shop)
            {
                return;
            }

            foreach (KeyValuePair<Interactable, ThingArt> pair in m_things)
            {
                if (pair.Key.Table.Id == e.InteractableId)
                {
                    StartCoroutine(Fx.Bounce(pair.Value.Body.transform));
                }
            }
        }

        private void Bus_Dug(Events.Dug e)
        {
            if (e.Grid != m_shop.Grid)
            {
                return;
            }

            Repaint();
            m_dig.Play(e.Cell, m_wombat.Dig());
            Expanded?.Invoke();
        }

        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area != m_shop || m_shop.Target == m_shownTarget)
            {
                return;
            }

            m_shownTarget = m_shop.Target;
            m_dig.Refresh(m_editing, m_shownTarget);
            Bounce(m_shownTarget);
        }

        private void Bus_TankFilled(Events.TankFilled e)
        {
            if (m_things.TryGetValue(e.Tank, out ThingArt art))
            {
                StartCoroutine(Fx.Bounce(art.Body.transform));
            }
        }

        private void Bus_FishPlaced(Events.FishPlaced e)
        {
            if (m_things.TryGetValue(e.Board, out ThingArt art))
            {
                StartCoroutine(Fx.Bounce(art.Body.transform));
            }
        }

        private void Bus_DishServed(Events.DishServed e)
        {
            if (m_things.TryGetValue(e.Table, out ThingArt art))
            {
                StartCoroutine(Fx.Bounce(art.Body.transform));
            }
        }

        // 손님: 외형은 광장에서 정해져 온다. 고른 뒤 접시가 올 때까지 머리 위에 그 물고기
        private void Bus_VisitorArrived(Events.RestaurantVisitorArrived e)
        {
            if (e.Visitor.Restaurant != m_shop)
            {
                return;
            }

            RestaurantVisitor visitor = e.Visitor;
            VisitorView unit = Instantiate(m_visitorPrefab, transform);
            unit.Initialize(visitor, m_frames, transform);
            unit.IconWhile(() => visitor.Dish != null && !visitor.Served && !visitor.Angry ? m_frames.Get(m_tables.Get<ItemTable>(visitor.Dish.Fish).Icon)[0] : null);
            m_units[visitor] = unit;
        }

        private void Bus_VisitorPaid(Events.RestaurantVisitorPaid e)
        {
            if (m_units.TryGetValue(e.Visitor, out VisitorView unit))
            {
                unit.Pay(m_tables.Format(k_CoinKey, e.Coins.ToString("0", System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        private void Bus_VisitorLeft(Events.RestaurantVisitorLeft e)
        {
            if (m_units.TryGetValue(e.Visitor, out VisitorView unit))
            {
                Destroy(unit.gameObject);
                m_units.Remove(e.Visitor);
            }
        }

        // 굴 모양은 Core가 계산해 둔 마스크(걷는 땅과 같은 것)를 칠한다
        private void Repaint()
        {
            BurrowShape.Result shape = m_shop.Layout.Shape;
            Sprite old = m_burrow.sprite;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile, m_wallFace);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);

            if (old != null)
            {
                Destroy(old.texture);
                Destroy(old);
            }
        }

        private static SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite, int order)
        {
            SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
