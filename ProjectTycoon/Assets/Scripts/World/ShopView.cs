using System;
using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 굴 격자 설계 v0.5: 가게 = 파낸 칸(BurrowGrid)을 한 덩어리로 그린 굴 그림 + 칸 위의 진열대·오븐·계산대·빈 자리·파기 태그.
    // 손님 동선 설계 v0.2: 사물 자리·굴 모양은 Core ShopLayout이 정하고 여기서는 그 자리에 놓고 그리기만 한다. 손님·웜뱃도 Core 위치를 읽는다
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
        [Tooltip("새로 판 칸이 흙빛에서 밝아지는 시간(초)")]
        [SerializeField] private float m_digSeconds = 0.6f;
        [Tooltip("드래그 뒤 파기 태그가 보이는 시간(초)")]
        [SerializeField] private float m_digTagSeconds = 2f;

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
        private float m_digTagTimer;
        private Sprite m_white;

        // 새로 판 칸의 가운데(카메라가 보여 줄 곳)
        public event Action<Vector2> Expanded;

        private ShopLayout Layout => m_shop.Layout;

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

                float cellWidth = (float)m_tables.Config.Shop.CellWidth;
                float xMin = transform.position.x + (minCol - 1) * cellWidth;
                float xMax = transform.position.x + (maxCol + 2) * cellWidth;
                float yMin = transform.position.y - Layout.RowTop(maxRow + 2);
                return new Rect(xMin, yMin, xMax - xMin, transform.position.y - yMin);
            }
        }

        public Vector2 Top => transform.position;

        public void Bind(ShopSim shop, FrameCache frames, GameTables tables)
        {
            m_shop = shop;
            m_frames = frames;
            m_tables = tables;
            m_counter = Instantiate(m_counterPrefab, transform);
            m_counter.transform.localPosition = new Vector3(0f, -Layout.RowTop(BurrowGrid.k_CounterRow), 0f);
            m_counter.Wombat.Bind(shop, this);
            Repaint();
            Build();

            m_shop.StockChanged += Shop_StockChanged;
            m_shop.OvenChanged += Shop_OvenChanged;
            m_shop.LayoutChanged += Shop_LayoutChanged;
            m_shop.UpgradesChanged += Shop_UpgradesChanged;
            m_shop.Grid.Dug += Grid_Dug;
        }

        // Core 가게 좌표(원점 기준 유닛, y 위) → 월드 위치
        public Vector3 ToWorld(System.Numerics.Vector2 shop)
        {
            return transform.position + new Vector3(shop.X, shop.Y, 0f);
        }

        public Vector2 CellCenter(Cell cell)
        {
            return ToWorld(Layout.CellCenter(cell));
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

        // 굴 격자 D4: 끈 방향(dCol, dRow)의 팔 수 있는 칸에 비용 태그를 잠깐 띄운다
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

        // 오븐 진행 막대는 매 프레임 읽는다(오븐 사건은 초가 바뀔 때만 온다)
        private void Update()
        {
            for (int i = 0; i < m_ovens.Count; i++)
            {
                Oven oven = m_shop.Ovens[i];

                if (!oven.IsEmpty && oven.Ready == 0)
                {
                    m_ovens[i].SetProgress(1f - (float)(oven.Remaining / oven.Bread.BakeSeconds));
                }
            }

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
            ShopConfigSize(out float cellWidth, out float cellHeight);
            GameObject go = new GameObject("Dirt");
            go.transform.SetParent(transform, false);
            go.transform.position = CellCenter(cell);
            go.transform.localScale = new Vector3(cellWidth, cellHeight, 1f);
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

        private void ShopConfigSize(out float cellWidth, out float cellHeight)
        {
            cellWidth = (float)m_tables.Config.Shop.CellWidth;
            cellHeight = (float)m_tables.Config.Shop.CellHeight;
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
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / ShopLayout.k_PixelsPerUnit, -shape.OriginY / ShopLayout.k_PixelsPerUnit, 0f);

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
                    shelf.transform.position = ToWorld(Layout.ShelfBase(pair.Key));
                    m_shelves[pair.Key] = shelf;
                }
            }

            while (m_ovens.Count < m_shop.Ovens.Count)
            {
                OvenView oven = Instantiate(m_ovenPrefab, transform);
                oven.transform.position = ToWorld(Layout.OvenBase(m_shop.Ovens[m_ovens.Count].Cell));
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

            RefreshShelves();

            for (int i = 0; i < m_ovens.Count; i++)
            {
                RefreshOven(i);
            }
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
