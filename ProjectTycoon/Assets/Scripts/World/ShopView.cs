using System;
using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 굴 격자 설계 v0.5: 가게 = 파낸 칸(BurrowGrid)을 한 덩어리로 그린 굴 그림 + 칸 위의 진열대·오븐·계산대·빈 자리·파기 태그.
    // 칸은 화면에 안 보인다. 굴 그림은 팔 때마다 BurrowPainter로 다시 만든다. 재고·오븐 표시도 여기서 ShopSim 이벤트로 갱신한다
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private ShelfView m_shelfPrefab;
        [SerializeField] private OvenView m_ovenPrefab;
        [SerializeField] private CounterView m_counterPrefab;
        [SerializeField] private MarkerView m_digTagPrefab;
        [SerializeField] private MarkerView m_slotMarkerPrefab;
        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("바닥·벽 타일 한 주기(한 칸 = 1px, 읽기 가능)")]
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [Tooltip("굴 모서리 반지름(칸)")]
        [SerializeField] private int m_roundRadius = 12;
        [Tooltip("줄 칸 간격(유닛). 계산대에서 위로")]
        [SerializeField] private float m_queueSpacing = 0.8f;
        [Tooltip("입구에서 손님이 나타나는 깊이(유닛, 입구 윗변 기준)")]
        [SerializeField] private float m_doorDepth = 1.5f;
        [Tooltip("계산을 마친 손님이 올라가는 통로 x(유닛)")]
        [SerializeField] private float m_exitLaneX = 0.7f;
        [Tooltip("웜뱃이 굽기 심부름 때 서는 자리: 오븐 위로 이만큼(유닛)")]
        [SerializeField] private float m_ovenStandOffset = 1.35f;
        [Tooltip("새로 판 칸이 흙빛에서 밝아지는 시간(초)")]
        [SerializeField] private float m_digSeconds = 0.6f;
        [Tooltip("드래그 뒤 파기 태그가 보이는 시간(초)")]
        [SerializeField] private float m_digTagSeconds = 2f;
        [Tooltip("진열대 밑변: 칸 가운데에서 아래로(유닛)")]
        [SerializeField] private float m_shelfDrop = 0.65f;
        [Tooltip("오븐 밑변: 칸 가운데에서 아래로(유닛)")]
        [SerializeField] private float m_ovenDrop = 0.85f;

        private const int k_DirtOrder = -1990;
        private static readonly Color k_Dirt = new Color(0.45f, 0.3f, 0.18f);

        private readonly Dictionary<Cell, ShelfView> m_shelves = new Dictionary<Cell, ShelfView>();
        private readonly List<OvenView> m_ovens = new List<OvenView>();
        private readonly Dictionary<Cell, MarkerView> m_slotMarkers = new Dictionary<Cell, MarkerView>();
        private readonly Dictionary<Cell, MarkerView> m_digTags = new Dictionary<Cell, MarkerView>();
        private CounterView m_counter;
        private ShopSim m_shop;
        private FrameCache m_frames;
        private GameTables m_tables;
        private GameConfig.ShopConfig m_config;
        private float m_digTagTimer;
        private Sprite m_white;

        // 새로 판 칸의 가운데(카메라가 보여 줄 곳)
        public event Action<Vector2> Expanded;

        private float CellWidth => (float)m_config.CellWidth;
        private float CellHeight => (float)m_config.CellHeight;
        private float EntranceHeight => (float)m_config.EntranceHeight;

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

                float xMin = transform.position.x + (minCol - 1) * CellWidth;
                float xMax = transform.position.x + (maxCol + 2) * CellWidth;
                float yMin = transform.position.y - RowTop(maxRow + 2);
                return new Rect(xMin, yMin, xMax - xMin, transform.position.y - yMin);
            }
        }

        public Vector2 Top => transform.position;
        public Vector2 Door => (Vector2)transform.position + new Vector2(0f, -m_doorDepth);
        public float ExitLaneX => transform.position.x + m_exitLaneX;

        public void Bind(ShopSim shop, FrameCache frames, GameTables tables)
        {
            m_shop = shop;
            m_frames = frames;
            m_tables = tables;
            m_config = tables.Config.Shop;
            m_counter = Instantiate(m_counterPrefab, transform);
            m_counter.transform.localPosition = new Vector3(0f, -RowTop(BurrowGrid.k_CounterRow), 0f);
            Repaint();
            Build();

            m_shop.StockChanged += Shop_StockChanged;
            m_shop.OvenChanged += Shop_OvenChanged;
            m_shop.LayoutChanged += Shop_LayoutChanged;
            m_shop.UpgradesChanged += Shop_UpgradesChanged;
            m_shop.QueueChanged += Shop_QueueChanged;
            m_shop.WombatLeft += Shop_WombatLeft;
            m_shop.WombatAtOven += Shop_WombatAtOven;
            m_shop.Grid.Dug += Grid_Dug;
        }

        public Vector2 CellCenter(Cell cell)
        {
            float top = RowTop(cell.Row);
            float height = cell.Row == 0 ? EntranceHeight : CellHeight;
            return (Vector2)transform.position + new Vector2((cell.Col + 0.5f) * CellWidth, -(top + height * 0.5f));
        }

        public Vector2 ShelfSpot(Cell cell)
        {
            return m_shelves[cell].StandPoint;
        }

        public Vector2 QueueSpot(int index)
        {
            return m_counter.QueueHead + Vector2.up * (m_queueSpacing * index);
        }

        // from → to 경로의 중간 칸 가운데들 + 끝점. 첫 칸(지금 자리)과 마지막 칸(end가 대신한다)은 뺀다
        public List<Vector2> Route(Cell from, Cell to, Vector2 end)
        {
            IReadOnlyList<Cell> path = m_shop.Grid.Path(from, to);
            List<Vector2> points = new List<Vector2>();

            for (int i = 1; i < path.Count - 1; i++)
            {
                points.Add(CellCenter(path[i]));
            }

            points.Add(end);
            return points;
        }

        // 그 칸에서 가운데(x = 0)까지 같은 줄로 곧장 걸을 수 있나(사이 칸이 전부 파여 있나)
        public bool StraightToCenter(Cell cell)
        {
            int inner = cell.Col < 0 ? -1 : 0;
            int step = cell.Col < inner ? 1 : -1;

            for (int col = cell.Col; col != inner; col += step)
            {
                if (!m_shop.Grid.Contains(new Cell(col, cell.Row)))
                {
                    return false;
                }
            }

            return true;
        }

        // 사물 터치 기획: 탭 위치의 사물. 파기 태그는 보이는 동안만
        public ShopTarget? HitTest(Vector3 world)
        {
            foreach (KeyValuePair<Cell, ShelfView> pair in m_shelves)
            {
                if (pair.Value.Contains(world))
                {
                    return new ShopTarget(ShopTargetKind.Shelf, pair.Key);
                }
            }

            for (int i = 0; i < m_ovens.Count; i++)
            {
                if (m_ovens[i].Contains(world))
                {
                    return new ShopTarget(ShopTargetKind.Oven, m_shop.Ovens[i].Cell, i);
                }
            }

            if (m_counter.Contains(world))
            {
                return new ShopTarget(ShopTargetKind.Counter, m_shop.Grid.Counter);
            }

            foreach (KeyValuePair<Cell, MarkerView> pair in m_slotMarkers)
            {
                if (pair.Value.gameObject.activeSelf && pair.Value.Contains(world))
                {
                    return new ShopTarget(ShopTargetKind.EmptySlot, pair.Key);
                }
            }

            foreach (KeyValuePair<Cell, MarkerView> pair in m_digTags)
            {
                if (pair.Value.gameObject.activeSelf && pair.Value.Contains(world))
                {
                    return new ShopTarget(ShopTargetKind.Dig, pair.Key);
                }
            }

            return null;
        }

        public void Bounce(ShopTarget target)
        {
            switch (target.Kind)
            {
                case ShopTargetKind.Shelf:
                    m_shelves[target.Cell].Bounce();
                    break;
                case ShopTargetKind.Oven:
                    m_ovens[target.Index].Bounce();
                    break;
                case ShopTargetKind.EmptySlot:
                    m_slotMarkers[target.Cell].Bounce();
                    break;
                case ShopTargetKind.Dig:
                    m_digTags[target.Cell].Bounce();
                    break;
                default:
                    m_counter.Bounce();
                    break;
            }
        }

        // D4: 끈 방향(dCol, dRow)의 팔 수 있는 칸에 비용 태그를 잠깐 띄운다
        public void ShowDigTags(int dCol, int dRow)
        {
            HideDigTags();
            string text = m_tables.Strings.Format("tag_dig", Cost(m_shop.Grid.DigCost));

            foreach (Cell cell in m_shop.Grid.Frontier(dCol, dRow))
            {
                if (!m_digTags.TryGetValue(cell, out MarkerView tag))
                {
                    tag = Instantiate(m_digTagPrefab, transform);
                    tag.transform.position = CellCenter(cell);
                    m_digTags[cell] = tag;
                }

                tag.Show(text);
            }

            m_digTagTimer = m_digTagSeconds;
        }

        private void Update()
        {
            if (m_digTagTimer <= 0f)
            {
                return;
            }

            m_digTagTimer -= Time.deltaTime;

            if (m_digTagTimer <= 0f)
            {
                HideDigTags();
            }
        }

        private void HideDigTags()
        {
            foreach (MarkerView tag in m_digTags.Values)
            {
                tag.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_shop.StockChanged -= Shop_StockChanged;
                m_shop.OvenChanged -= Shop_OvenChanged;
                m_shop.LayoutChanged -= Shop_LayoutChanged;
                m_shop.UpgradesChanged -= Shop_UpgradesChanged;
                m_shop.QueueChanged -= Shop_QueueChanged;
                m_shop.WombatLeft -= Shop_WombatLeft;
                m_shop.WombatAtOven -= Shop_WombatAtOven;
                m_shop.Grid.Dug -= Grid_Dug;
            }
        }

        private void Shop_StockChanged(string breadId)
        {
            RefreshShelves();
        }

        private void Shop_OvenChanged(int index)
        {
            RefreshOven(index);
        }

        private void Shop_UpgradesChanged()
        {
            RefreshShelves();

            for (int i = 0; i < m_ovens.Count; i++)
            {
                RefreshOven(i);
            }
        }

        private void Shop_QueueChanged()
        {
            m_counter.SetServing(m_shop.Queue.Count > 0);
        }

        // v0.6: 오븐까지 칸 경로로 걸어가고, 도착하면(굽기 시작) 같은 시간 걸려 계산대로 돌아온다
        private void Shop_WombatLeft(int ovenIndex, double seconds)
        {
            Cell cell = m_shop.Ovens[ovenIndex].Cell;
            Vector2 stand = (Vector2)m_ovens[ovenIndex].transform.position + Vector2.up * m_ovenStandOffset;
            m_counter.Wombat.WalkPath(Route(m_shop.Grid.CounterNear(cell), cell, stand), (float)seconds);
        }

        private void Shop_WombatAtOven(int ovenIndex, double seconds)
        {
            Cell cell = m_shop.Ovens[ovenIndex].Cell;
            List<Vector2> home = Route(cell, m_shop.Grid.CounterNear(cell), m_counter.Wombat.Home);
            m_counter.Wombat.WalkHome(home, (float)seconds);
        }

        private void Shop_LayoutChanged()
        {
            Build();
        }

        // 굴 파기 연출: 다시 그리고, 새 칸 자리에 흙빛 사각형을 얹어 밝아지게 한다
        private void Grid_Dug(Cell cell)
        {
            Repaint();
            HideDigTags();
            StartCoroutine(DigRoutine(cell));
            Expanded?.Invoke(CellCenter(cell));
        }

        private System.Collections.IEnumerator DigRoutine(Cell cell)
        {
            GameObject go = new GameObject("Dirt");
            go.transform.SetParent(transform, false);
            go.transform.position = CellCenter(cell);
            go.transform.localScale = new Vector3(CellWidth, CellHeight, 1f);
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

        private void Repaint()
        {
            int unit = (int)BurrowPainter.k_PixelsPerUnit;
            BurrowShape.Result shape = BurrowShape.Build(m_shop.Grid.Cells, Mathf.RoundToInt(CellWidth * unit), Mathf.RoundToInt(CellHeight * unit),
                Mathf.RoundToInt(EntranceHeight * unit), m_roundRadius);
            Sprite old = m_burrow.sprite;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / (float)unit, -shape.OriginY / (float)unit, 0f);

            if (old != null)
            {
                Destroy(old.texture);
                Destroy(old);
            }
        }

        // 자리마다 진열대·오븐·빈 자리 표시를 맞춘다
        private void Build()
        {
            foreach (KeyValuePair<Cell, BreadRecord> pair in m_shop.Shelves)
            {
                if (!m_shelves.ContainsKey(pair.Key))
                {
                    ShelfView shelf = Instantiate(m_shelfPrefab, transform);
                    shelf.transform.position = CellCenter(pair.Key) + Vector2.down * m_shelfDrop;
                    m_shelves[pair.Key] = shelf;
                }
            }

            while (m_ovens.Count < m_shop.Ovens.Count)
            {
                OvenView oven = Instantiate(m_ovenPrefab, transform);
                oven.transform.position = CellCenter(m_shop.Ovens[m_ovens.Count].Cell) + Vector2.down * m_ovenDrop;
                m_ovens.Add(oven);
            }

            foreach (MarkerView marker in m_slotMarkers.Values)
            {
                marker.gameObject.SetActive(false);
            }

            foreach (Cell cell in m_shop.EmptySlots)
            {
                if (!m_slotMarkers.TryGetValue(cell, out MarkerView marker))
                {
                    marker = Instantiate(m_slotMarkerPrefab, transform);
                    marker.transform.position = CellCenter(cell) + Vector2.down * 0.3f;
                    m_slotMarkers[cell] = marker;
                }

                marker.Show(m_tables.Strings.Get("tag_slot"));
            }

            m_counter.SetServing(m_shop.Queue.Count > 0);
            RefreshShelves();

            for (int i = 0; i < m_ovens.Count; i++)
            {
                RefreshOven(i);
            }
        }

        private float RowTop(int row)
        {
            return row <= 0 ? 0f : EntranceHeight + (row - 1) * CellHeight;
        }

        private void RefreshShelves()
        {
            foreach (KeyValuePair<Cell, ShelfView> pair in m_shelves)
            {
                BreadRecord bread = m_shop.Shelves[pair.Key];
                pair.Value.Show(Icon(bread), m_shop.Stock(bread.Id), m_shop.ShelfCapacity);
            }
        }

        private void RefreshOven(int index)
        {
            OvenView view = m_ovens[index];
            view.SetLook(m_shop.OvenLookUpgraded);
            Oven oven = m_shop.Ovens[index];

            if (oven.IsEmpty)
            {
                view.ShowEmpty();
                return;
            }

            float progress = 1f - (float)(oven.Remaining / oven.Bread.BakeSeconds);
            view.ShowBaking(Icon(oven.Bread), progress, oven.Ready);
        }

        private static string Cost(double cost)
        {
            return cost.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private Sprite Icon(BreadRecord bread)
        {
            return m_frames.Get(bread.Sprite)[0];
        }
    }
}
