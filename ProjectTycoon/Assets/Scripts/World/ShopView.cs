using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 굴 격자 설계 v0.5: 가게 = 파낸 칸(BurrowGrid)을 한 덩어리로 그린 굴 그림 + 칸 위의 진열대·오븐·계산대·빈 자리·파기 태그.
    // 손님 동선 설계 v0.2: 사물 자리·굴 모양은 Core BakeryLayout이 정하고 여기서는 그 자리에 놓고 그리기만 한다. 손님·웜뱃도 Core 위치를 읽는다.
    // 설계 09: 웜뱃의 상호작용 대상이 바뀌면 그 사물이 한 번 튀고, 대상이 팔 수 있는 칸이면 그 칸에만 파기 태그를 보인다
    // 설계 10: 업그레이드를 사면 그 대상(shop_upgrades.target)이, 새로 놓인 진열대·오븐이 한 번 튄다
    // 설계 13: 진열대·오븐 그림은 자기 사물의 Changed만 듣는다
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private ShelfView m_shelfPrefab;
        [Tooltip("진열대 재고 표지판(칠판 입간판). 진열대 벽 쪽 앞 모서리에 따로 선다")]
        [SerializeField] private ShelfSignView m_shelfSignPrefab;
        [SerializeField] private OvenView m_ovenPrefab;
        [SerializeField] private CounterView m_counterPrefab;
        [SerializeField] private MarkerView m_digTagPrefab;
        [Tooltip("빈 자리 점선 칸(2026-09-23부터 글자 태그 없음)")]
        [SerializeField] private SpriteRenderer m_slotMarkerPrefab;
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

        private const int k_DirtOrder = -1990;
        private static readonly Color k_Dirt = new Color(0.45f, 0.3f, 0.18f);

        private readonly Dictionary<ShelfInteractable, ShelfView> m_shelves = new Dictionary<ShelfInteractable, ShelfView>();
        private readonly Dictionary<ShelfInteractable, ShelfSignView> m_signs = new Dictionary<ShelfInteractable, ShelfSignView>();
        private readonly Dictionary<OvenInteractable, OvenView> m_ovens = new Dictionary<OvenInteractable, OvenView>();
        private readonly Dictionary<Cell, SpriteRenderer> m_slotMarkers = new Dictionary<Cell, SpriteRenderer>();
        private readonly Dictionary<Cell, MarkerView> m_digTags = new Dictionary<Cell, MarkerView>();
        private CounterView m_counter;
        private BakeryArea m_shop;
        private FrameCache m_frames;
        private TableSet m_tables;
        private Sprite m_white;
        private Interactable m_shownTarget;
        private bool m_built;

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

                float cellWidth = (float)m_tables.Get<ConfigTable>(ConfigTable.k_CellWidth).Value;
                float xMin = transform.position.x + (minCol - 1) * cellWidth;
                float xMax = transform.position.x + (maxCol + 2) * cellWidth;
                float yMin = transform.position.y - Layout.RowTop(maxRow + 2);
                return new Rect(xMin, yMin, xMax - xMin, transform.position.y - yMin);
            }
        }

        public Transform Wombat => m_counter.Wombat.transform;

        public Vector3 OvenBreadPosition(OvenInteractable oven)
        {
            return m_ovens[oven].BreadPosition;
        }

        public Vector3 ShelfIconPosition(string breadId)
        {
            return m_shelves[m_shop.ShelfOf(breadId)].IconPosition;
        }

        public void Bind(BakeryArea shop, FrameCache frames, TableSet tables)
        {
            m_shop = shop;
            m_frames = frames;
            m_tables = tables;
            m_counter = Instantiate(m_counterPrefab, transform);
            m_counter.transform.localPosition = new Vector3(0f, -Layout.RowTop(BurrowGrid.k_CounterRow), 0f);
            m_counter.Wombat.Bind(shop, this, frames);
            Repaint();
            Build();
            m_built = true;

            m_shop.LayoutChanged += Shop_LayoutChanged;
            m_shop.Upgraded += Shop_Upgraded;
            m_shop.Grid.Dug += Grid_Dug;
            m_shop.TargetChanged += Shop_TargetChanged;
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
                case SlotInteractable slot:
                    StartCoroutine(Fx.Bounce(m_slotMarkers[slot.Cell].transform));
                    break;
                case DigInteractable dig:
                    m_digTags[dig.Cell].Bounce();
                    break;
                case ExitInteractable _:
                    StartCoroutine(Fx.Bounce(m_arch));
                    break;
                default:
                    m_counter.Bounce();
                    break;
            }
        }

        // 굴 격자 D4 → 설계 09: 대상이 된 팔 수 있는 칸에 비용 태그
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

        // 오븐 진행 막대는 매 프레임 읽는다(오븐 사건은 초가 바뀔 때만 온다). 설계 10: 대상인 오븐은 빈 오븐 화살표를 끈다
        private void Update()
        {
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
                foreach (ShelfInteractable shelf in m_shelves.Keys)
                {
                    shelf.Changed -= Shelf_Changed;
                }

                foreach (OvenInteractable oven in m_ovens.Keys)
                {
                    oven.Changed -= Oven_Changed;
                }

                m_shop.LayoutChanged -= Shop_LayoutChanged;
                m_shop.Upgraded -= Shop_Upgraded;
                m_shop.Grid.Dug -= Grid_Dug;
                m_shop.TargetChanged -= Shop_TargetChanged;
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

        // 업그레이드를 산 사물 종류(진열대 전부·오븐 전부·계산대)가 한 번 튀고, 값(오븐 외형·진열대)을 다시 그린다
        private void Shop_Upgraded(string interactableId)
        {
            RefreshShelves();

            foreach (OvenInteractable oven in m_ovens.Keys)
            {
                RefreshOven(oven);
            }

            switch (interactableId)
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
                    m_counter.Bounce();
                    break;
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
            StartCoroutine(DigRoutine(cell));
            OnExpanded();
        }

        // 버튼 행동만 바뀐 사건(다 구웠다 등)에서는 튀지 않는다
        private void Shop_TargetChanged()
        {
            if (m_shop.Target == m_shownTarget)
            {
                return;
            }

            m_shownTarget = m_shop.Target;
            HideDigTags();

            if (m_shownTarget == null)
            {
                return;
            }

            if (m_shownTarget is DigInteractable dig)
            {
                ShowDigTag(dig.Cell);
            }

            Bounce(m_shownTarget);
        }

        private void OnExpanded()
        {
            Expanded?.Invoke();
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
            cellWidth = (float)m_tables.Get<ConfigTable>(ConfigTable.k_CellWidth).Value;
            cellHeight = (float)m_tables.Get<ConfigTable>(ConfigTable.k_CellHeight).Value;
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
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BakeryLayout.k_PixelsPerUnit, -shape.OriginY / BakeryLayout.k_PixelsPerUnit, 0f);

            if (old != null)
            {
                Destroy(old.texture);
                Destroy(old);
            }
        }

        // 자리마다 진열대·오븐·빈 자리 표시를 맞춘다
        private void Build()
        {
            foreach (ShelfInteractable shelf in m_shop.Shelves.Values)
            {
                if (!m_shelves.ContainsKey(shelf))
                {
                    ShelfView view = Instantiate(m_shelfPrefab, transform);
                    view.transform.position = ToWorld(Layout.ShelfBase(shelf.Cell));
                    m_shelves[shelf] = view;
                    ShelfSignView sign = Instantiate(m_shelfSignPrefab, transform);
                    sign.transform.position = ToWorld(Layout.ShelfSignBase(shelf.Cell));
                    m_signs[shelf] = sign;
                    shelf.Changed += Shelf_Changed;

                    if (m_built)
                    {
                        view.Bounce();
                    }
                }
            }

            foreach (OvenInteractable oven in m_shop.Ovens)
            {
                if (!m_ovens.ContainsKey(oven))
                {
                    OvenView view = Instantiate(m_ovenPrefab, transform);
                    view.transform.position = ToWorld(Layout.OvenBase(oven.Cell));
                    m_ovens[oven] = view;
                    oven.Changed += Oven_Changed;

                    if (m_built)
                    {
                        view.Bounce();
                    }
                }
            }

            foreach (SpriteRenderer marker in m_slotMarkers.Values)
            {
                marker.gameObject.SetActive(false);
            }

            foreach (Cell cell in m_shop.Slots.Keys)
            {
                if (!m_slotMarkers.TryGetValue(cell, out SpriteRenderer marker))
                {
                    marker = Instantiate(m_slotMarkerPrefab, transform);
                    marker.transform.position = CellCenter(cell) + Vector2.down * 0.3f;
                    m_slotMarkers[cell] = marker;
                }

                marker.gameObject.SetActive(true);
            }

            RefreshShelves();

            foreach (OvenInteractable oven in m_ovens.Keys)
            {
                RefreshOven(oven);
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
            m_shelves[shelf].Show(Icon(shelf.Bread));
            m_signs[shelf].Show(shelf.Stock);
        }

        private void RefreshOven(OvenInteractable oven)
        {
            OvenView view = m_ovens[oven];
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

        private Sprite Icon(BreadTable bread)
        {
            return m_frames.Get(bread.Sprite)[0];
        }
    }
}
