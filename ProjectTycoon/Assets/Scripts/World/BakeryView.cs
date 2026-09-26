using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.5 · 굴 격자 설계 v0.5 · 설계 13 · 설계 18: 빵집 굴 화면. 굴 그림은 Core 마스크를 칠하고, 사물(진열대·오븐·계산대)은 Core 목록을 객체 키로 맞춘다
    // (놓이면 만들고, 옮기면 옮기고, 치우면 지운다 — 전부 LayoutChanged 한 번에). 웜뱃 그림은 첫 계산대 프리팹의 것 하나만 쓴다(웜뱃은 하나).
    // 편집 모드(설계 18): 끌고 있는 사물 그림자, 팔 수 있는 흙의 파기 태그 상시
    public sealed class BakeryView : MonoBehaviour
    {
        [SerializeField] private ShelfView m_shelfPrefab;
        [Tooltip("진열대 재고 표지판(칠판 입간판). 진열대 왼쪽 앞 모서리에 따로 선다")]
        [SerializeField] private ShelfSignView m_shelfSignPrefab;
        [SerializeField] private OvenView m_ovenPrefab;
        [SerializeField] private CounterView m_counterPrefab;
        [SerializeField] private MarkerView m_digTagPrefab;
        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("입구 아치(설계 11: 나가기 대상이면 튄다)")]
        [SerializeField] private Transform m_arch;
        [Tooltip("바닥·벽 타일 한 주기(한 칸 = 1px, 읽기 가능)")]
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [SerializeField] private Texture2D m_wallFace;
        [Tooltip("새로 판 칸이 흙빛에서 밝아지는 시간(초)")]
        [SerializeField] private float m_digSeconds = 0.6f;
        [Tooltip("편집 그림자용 사물 그림(진열대·오븐·계산대)")]
        [SerializeField] private Sprite m_ghostShelf;
        [SerializeField] private Sprite m_ghostOven;
        [SerializeField] private Sprite m_ghostCounter;

        private const int k_DirtOrder = -1990;
        private static readonly Color k_Dirt = new Color(0.45f, 0.3f, 0.18f);

        private readonly Dictionary<ShelfInteractable, ShelfView> m_shelves = new Dictionary<ShelfInteractable, ShelfView>();
        private readonly Dictionary<ShelfInteractable, ShelfSignView> m_signs = new Dictionary<ShelfInteractable, ShelfSignView>();
        private readonly Dictionary<OvenInteractable, OvenView> m_ovens = new Dictionary<OvenInteractable, OvenView>();
        private readonly Dictionary<CounterInteractable, CounterView> m_counters = new Dictionary<CounterInteractable, CounterView>();
        private readonly Dictionary<Cell, MarkerView> m_digTags = new Dictionary<Cell, MarkerView>();
        private CounterView m_wombatCounter;
        private BakeryArea m_shop;
        private FrameCache m_frames;
        private TableSet m_tables;
        private Sprite m_white;
        private GhostView m_ghost;
        private Interactable m_shownTarget;
        private bool m_built;
        private bool m_editing;
        private IDisposable[] m_subscriptions;

        // 굴을 팠다(카메라 경계가 넓어진다)
        public event Action Expanded;

        private BakeryLayout Layout => m_shop.Layout;

        // 파낸 칸의 경계 + 좌·우·아래로 한 칸(팔 수 있는 흙이 보이게). 위는 입구 윗변
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

                float xMin = transform.position.x + (minCol - 1) * Layout.CellWidth;
                float xMax = transform.position.x + (maxCol + 2) * Layout.CellWidth;
                float yMin = transform.position.y - Layout.RowTop(maxRow + 2);
                return new Rect(xMin, yMin, xMax - xMin, transform.position.y - yMin);
            }
        }

        public Transform Wombat => m_wombatCounter.Wombat.transform;

        public Vector3 OvenBreadPosition(OvenInteractable oven)
        {
            return m_ovens[oven].BreadPosition;
        }

        public Vector3 ShelfIconPosition(ShelfInteractable shelf)
        {
            return m_shelves[shelf].IconPosition;
        }

        public void Bind(BakeryArea shop, EventBus bus, FrameCache frames, TableSet tables)
        {
            m_shop = shop;
            m_frames = frames;
            m_tables = tables;
            m_ghost = GhostView.Create(transform);
            Repaint();
            Build();
            m_wombatCounter.Wombat.Bind(shop, this, frames);
            m_built = true;

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.Upgraded>(Bus_Upgraded),
                bus.Subscribe<Events.Dug>(Bus_Dug),
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
            };
        }

        // 가게 좌표(유닛) → 월드
        public Vector3 ToWorld(System.Numerics.Vector2 shop)
        {
            return transform.position + new Vector3(shop.X, shop.Y, 0f);
        }

        public Vector2 CellCenter(Cell cell)
        {
            return ToWorld(Layout.CellCenter(cell));
        }

        // 편집 모드: 팔 수 있는 흙 칸의 태그를 늘 보인다
        public void SetEditing(bool editing)
        {
            m_editing = editing;
            RefreshDigTags();
        }

        public void ShowGhost(IPlacedKind kind, System.Numerics.Vector2 at, bool ok)
        {
            m_ghost.Show(GhostSprite(kind.Id), ToWorld(at), ok);
        }

        public void HideGhost()
        {
            m_ghost.Hide();
        }

        private Sprite GhostSprite(string kindId)
        {
            switch (kindId)
            {
                case ShelfInteractable.k_Id: return m_ghostShelf;
                case OvenInteractable.k_Id: return m_ghostOven;
                default: return m_ghostCounter;
            }
        }

        private void Bounce(Interactable target)
        {
            switch (target)
            {
                case ShelfInteractable shelf:
                    m_shelves[shelf].Bounce();
                    break;
                case OvenInteractable oven:
                    m_ovens[oven].Bounce();
                    break;
                case CounterInteractable counter:
                    m_counters[counter].Bounce();
                    break;
                case DigInteractable dig:
                    m_digTags[dig.Cell].Bounce();
                    break;
                case PassageInteractable _:
                    StartCoroutine(Fx.Bounce(m_arch));
                    break;
            }
        }

        private void ShowDigTag(Cell cell)
        {
            if (!m_digTags.TryGetValue(cell, out MarkerView tag))
            {
                tag = Instantiate(m_digTagPrefab, transform);
                tag.transform.position = CellCenter(cell);
                m_digTags[cell] = tag;
            }

            tag.Show(m_tables.Format("tag_dig", Cost(m_shop.Grid.DigCost)));
        }

        // 편집 중이면 팔 수 있는 칸 전부, 아니면 대상인 칸만
        private void RefreshDigTags()
        {
            foreach (MarkerView tag in m_digTags.Values)
            {
                tag.gameObject.SetActive(false);
            }

            if (m_editing)
            {
                foreach (Cell cell in m_shop.Grid.Frontier())
                {
                    ShowDigTag(cell);
                }
            }
            else if (m_shownTarget is DigInteractable dig)
            {
                ShowDigTag(dig.Cell);
            }
        }

        // 오븐 진행 막대는 매 프레임 읽는다(오븐 사건은 초가 바뀔 때만 온다). 설계 10: 대상인 오븐은 빈 오븐 화살표를 끈다
        private void Update()
        {
            foreach (KeyValuePair<CounterInteractable, CounterView> pair in m_counters)
            {
                pair.Value.SetProgress((float)pair.Key.Progress, pair.Key.Serving);
            }

            foreach (KeyValuePair<OvenInteractable, OvenView> pair in m_ovens)
            {
                OvenInteractable oven = pair.Key;
                pair.Value.SetMarkHidden(m_shop.Target == oven);

                if (!oven.IsEmpty && oven.Ready == 0)
                {
                    pair.Value.SetProgress((float)oven.Progress);
                }
            }
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                foreach (ShelfInteractable shelf in m_shelves.Keys)
                {
                    shelf.Changed -= Shelf_Changed;
                }

                foreach (OvenInteractable oven in m_ovens.Keys)
                {
                    oven.Changed -= Oven_Changed;
                }

                foreach (IDisposable subscription in m_subscriptions)
                {
                    subscription.Dispose();
                }
            }
        }

        private void Shelf_Changed(Interactable shelf)
        {
            RefreshShelf((ShelfInteractable)shelf);
        }

        private void Oven_Changed(Interactable oven)
        {
            RefreshOven((OvenInteractable)oven);
        }

        // 업그레이드를 산 사물 종류(진열대 전부·오븐 전부·계산대 전부)가 한 번 튀고, 값(오븐 외형·진열대)을 다시 그린다
        private void Bus_Upgraded(Events.Upgraded e)
        {
            if (e.Area != m_shop)
            {
                return;
            }

            RefreshShelves();

            foreach (OvenInteractable oven in m_ovens.Keys)
            {
                RefreshOven(oven);
            }

            switch (e.InteractableId)
            {
                case ShelfInteractable.k_Id:
                    foreach (ShelfView shelf in m_shelves.Values)
                    {
                        shelf.Bounce();
                    }

                    break;
                case OvenInteractable.k_Id:
                    foreach (OvenView oven in m_ovens.Values)
                    {
                        oven.Bounce();
                    }

                    break;
                case CounterInteractable.k_Id:
                    foreach (CounterView counter in m_counters.Values)
                    {
                        counter.Bounce();
                    }

                    break;
            }
        }

        private void Bus_LayoutChanged(Events.LayoutChanged e)
        {
            if (e.Area == m_shop)
            {
                Build();
                RefreshDigTags();
            }
        }

        // 굴 파기 연출: 다시 그리고, 새 칸 자리에 흙빛 사각형을 얹어 밝아지게 한다
        private void Bus_Dug(Events.Dug e)
        {
            if (e.Grid != m_shop.Grid)
            {
                return;
            }

            Repaint();
            StartCoroutine(DigRoutine(e.Cell));
            OnExpanded();
        }

        // 버튼 행동만 바뀐 사건(다 구웠다 등)에서는 튀지 않는다
        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area != m_shop || m_shop.Target == m_shownTarget)
            {
                return;
            }

            m_shownTarget = m_shop.Target;
            RefreshDigTags();

            if (m_shownTarget != null)
            {
                Bounce(m_shownTarget);
            }
        }

        private void OnExpanded()
        {
            Expanded?.Invoke();
        }

        private System.Collections.IEnumerator DigRoutine(Cell cell)
        {
            GameObject go = new GameObject("Dirt");
            go.transform.SetParent(transform, false);
            go.transform.position = CellCenter(cell);
            go.transform.localScale = new Vector3(Layout.CellWidth, Layout.CellHeight, 1f);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = White;
            r.sortingOrder = k_DirtOrder;

            for (float t = 0f; t < m_digSeconds; t += Time.deltaTime)
            {
                r.color = new Color(k_Dirt.r, k_Dirt.g, k_Dirt.b, 1f - t / m_digSeconds);
                yield return null;
            }

            Destroy(go);
        }

        private Sprite White
        {
            get
            {
                if (m_white == null)
                {
                    m_white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                }

                return m_white;
            }
        }

        // 굴 모양은 Core가 이미 계산해 둔 마스크(걷는 땅과 같은 것)를 칠한다
        private void Repaint()
        {
            BurrowShape.Result shape = Layout.Shape;
            Sprite old = m_burrow.sprite;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile, m_wallFace);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);

            if (old != null)
            {
                Destroy(old.texture);
                Destroy(old);
            }
        }

        // 사물 뷰를 Core 목록에 맞춘다: 없는 것은 만들고(놓인 것은 튐), 사라진 것은 지우고, 자리는 늘 다시 놓는다
        private void Build()
        {
            Sync(m_shelves, m_shop.Shelves, m_shelfPrefab, shelf => shelf.Changed += Shelf_Changed, shelf => shelf.Changed -= Shelf_Changed);
            Sync(m_ovens, m_shop.Ovens, m_ovenPrefab, oven => oven.Changed += Oven_Changed, oven => oven.Changed -= Oven_Changed);
            Sync(m_counters, m_shop.Counters, m_counterPrefab, null, null);

            foreach (ShelfInteractable shelf in new List<ShelfInteractable>(m_signs.Keys))
            {
                if (!m_shelves.ContainsKey(shelf))
                {
                    Destroy(m_signs[shelf].gameObject);
                    m_signs.Remove(shelf);
                }
            }

            foreach (ShelfInteractable shelf in m_shop.Shelves)
            {
                if (!m_signs.TryGetValue(shelf, out ShelfSignView sign))
                {
                    sign = Instantiate(m_shelfSignPrefab, transform);
                    m_signs[shelf] = sign;
                }

                sign.transform.position = ToWorld(Layout.ShelfSignBase(shelf));
            }

            // 웜뱃 그림은 첫 계산대의 것 하나. 나머지 계산대의 웜뱃은 끈다
            m_wombatCounter = m_counters[m_shop.Counter];

            foreach (KeyValuePair<CounterInteractable, CounterView> pair in m_counters)
            {
                pair.Value.Wombat.gameObject.SetActive(pair.Value == m_wombatCounter);
            }

            RefreshShelves();

            foreach (OvenInteractable oven in m_ovens.Keys)
            {
                RefreshOven(oven);
            }
        }

        private void Sync<TThing, TView>(Dictionary<TThing, TView> views, IReadOnlyList<TThing> things, TView prefab,
            Action<TThing> subscribe, Action<TThing> unsubscribe) where TThing : Interactable, IPlaced where TView : MonoBehaviour
        {
            foreach (TThing thing in new List<TThing>(views.Keys))
            {
                if (!things.Contains(thing))
                {
                    unsubscribe?.Invoke(thing);
                    Destroy(views[thing].gameObject);
                    views.Remove(thing);
                }
            }

            foreach (TThing thing in things)
            {
                if (!views.TryGetValue(thing, out TView view))
                {
                    view = Instantiate(prefab, transform);
                    views[thing] = view;
                    subscribe?.Invoke(thing);

                    if (m_built)
                    {
                        StartCoroutine(Fx.Bounce(view.transform));
                    }
                }

                view.transform.position = ToWorld(thing.Position);
            }
        }

        private void RefreshShelves()
        {
            foreach (ShelfInteractable shelf in m_shelves.Keys)
            {
                RefreshShelf(shelf);
            }
        }

        private void RefreshShelf(ShelfInteractable shelf)
        {
            if (!m_shelves.ContainsKey(shelf))
            {
                return;
            }

            m_shelves[shelf].Show(Icon(shelf.Bread), shelf.Stock);
            m_signs[shelf].Show(shelf.Stock);
        }

        private void RefreshOven(OvenInteractable oven)
        {
            if (!m_ovens.TryGetValue(oven, out OvenView view))
            {
                return;
            }

            view.SetLook(oven.LookUpgraded);

            if (oven.IsEmpty)
            {
                view.ShowEmpty();
                return;
            }

            view.ShowBaking((float)oven.Progress, oven.Ready);
        }

        private static string Cost(double cost)
        {
            return cost.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }

        // 빈 진열대(설계 17)는 null
        private Sprite Icon(BreadTable bread)
        {
            return bread == null ? null : m_frames.Get(bread.Sprite)[0];
        }
    }
}
