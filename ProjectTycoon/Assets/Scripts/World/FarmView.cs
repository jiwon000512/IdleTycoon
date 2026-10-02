using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 25 → 설계 27: 농장 굴 화면. 굴 그림은 빵집·광장과 같은 BurrowPainter(판 칸이 늘면 다시 칠한다), 구멍 아치(광장으로)는 첫 줄 가운데, 파기 표식·연출은 공용 DigView.
    // 밭은 Core FarmArea.Plots(판 칸마다 하나, 사라지지 않는다)를 객체 키로 맞추고, 대상인 흙 칸 위에 갈기 값 표식, 거두면 밭 위로 재료 아이콘과 「+3」이 떠오른다.
    // 놓는 사물이 없어 편집 그림자·외곽선은 없다(편집 모드 = 파기 표식 전부).
    // 설계 39: 층마다 화면 하나. 굴 재료(바닥 · 벽 · 윗벽 띠 · 둘레 흙)는 FarmConfigTable earthEvery층마다 다음 묶음(1~5층 빵집과 같은 흙, 6층~ 붉은 흙),
    // 2층부터 위 구멍은 올라가는 계단. 다 판 층은 맨 아래 가운데에
    // 계단 값 표식, 아래층이 열리면 그 자리에 계단 굴이 파이고(흙덩이) 내려가는 계단이 선다(층별 굴 그림은 아트방 · 지금은 색조)
    public sealed class FarmView : MonoBehaviour, IAreaView
    {
        private const string k_PopupKey = "harvest_popup";
        // 작물 그림 반쪽 수(make_farm_art.py가 한 줄을 둘로 나눈다)
        private const int k_CropHalves = 2;
        // 거두기 팝업이 뜨는 높이(거둔 웜뱃 발끝에서 머리 위)
        private const float k_PopupHeight = 1.4f;
        // 갈기 값 표식(흙 칸마다 늘 보인다 — QA B): 웜뱃이 그 칸에 서기도 하므로 칸 윗변 바로 아래에, 모든 그림 위에
        private const float k_TillTagDrop = 0.3f;
        private const int k_TillTagOrder = 1000;
        // 거두는 순간 밭 가운데에서 낟알이 금빛 두 톤으로 튄다(2026-09-30 안 A: 밟고 거두기가 보이게)
        private static readonly Color k_Grain = new Color32(0xE4, 0xA8, 0x3C, 255);
        private static readonly Color k_GrainLight = new Color32(0xF0, 0xD8, 0x90, 255);
        private const int k_GrainCount = 8;
        private const float k_GrainSpread = 1.6f;
        private const float k_GrainLift = 3.2f;
        private const float k_GrainGravity = 10f;
        private const float k_GrainSeconds = 0.5f;
        private const int k_GrainCells = 5;
        private const int k_GrainOrder = 1000;
        // 설계 28: 거름을 섞어 심는 순간 거름 알갱이가 튀고, 덤은 한 박자 늦게 떠서 거두기 팝업 위에 쌓이며(CoinPopup) 반짝 알갱이가 튄다
        private static readonly Color k_Manure = new Color32(0x4A, 0x30, 0x22, 255);
        private static readonly Color k_ManureLight = new Color32(0x6E, 0x4C, 0x30, 255);
        private static readonly Color k_Spark = new Color32(0xFB, 0xF4, 0xE6, 255);
        internal const float k_BonusDelay = 0.25f;
        // 설계 39: 계단 값 표식은 계단 자리(가운데 통로 끝)에서 이만큼 위
        private const float k_StairTagRise = 0.35f;

        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("설계 39: 층 재료 묶음. [0] = 빵집과 같은 흙, [1] = 붉은 흙 …. 묶음이 모자라면 마지막 것")]
        [SerializeField] private Earth[] m_earths;
        [Tooltip("구멍 아치(나가기 대상이면 튄다)")]
        [SerializeField] private Transform m_arch;
        [Tooltip("설계 39: 둘레 흙(층 재료의 벽 타일을 깐다)")]
        [SerializeField] private SpriteRenderer m_backdrop;
        [Tooltip("설계 39: 2층부터 위 구멍 그림(올라가는 계단)")]
        [SerializeField] private Sprite m_stairsUp;
        [Tooltip("설계 39: 아래층으로 내려가는 계단(계단 굴에 선다, 아래층이 열린 층만)")]
        [SerializeField] private SpriteRenderer m_stairsDown;
        [Tooltip("입구 양옆 고정 장식(씨앗 자루 · 허수아비 · 작업대 · 도구). 아치 자리를 따라간다")]
        [SerializeField] private Transform m_entranceProps;
        [SerializeField] private PlotView m_plotPrefab;
        [Tooltip("파기·갈기 값 표식(빵집과 같은 프리팹)")]
        [SerializeField] private MarkerView m_digTagPrefab;
        [Tooltip("새로 판 칸이 흙빛에서 밝아지는 시간(초)")]
        [SerializeField] private float m_digSeconds = 0.6f;
        [Tooltip("거두기 팝업(코인 팝업 프리팹에 재료 아이콘을 얹는다)")]
        [SerializeField] private CoinPopup m_popupPrefab;
        [Tooltip("설계 37: 웜뱃 똥 그림(빵집과 같은 프리팹 · 프레임)")]
        [SerializeField] private SpriteAnimator m_poopPrefab;
        [SerializeField] private Sprite[] m_poopFrames;
        [SerializeField] private float m_poopFrameRate = 2f;
        [Tooltip("설계 38: 농장 점원 그림(손님 · 빵집 점원과 같은 프리팹)")]
        [SerializeField] private VisitorView m_clerkPrefab;
        [SerializeField] private WombatView m_wombat;

        private readonly Dictionary<PlotInteractable, PlotView> m_plots = new Dictionary<PlotInteractable, PlotView>();
        private readonly Dictionary<PlotInteractable, MarkerView> m_tillTags = new Dictionary<PlotInteractable, MarkerView>();
        // 밭마다 마지막으로 보인 단계(빈 밭 −1). 오르면 늘어남 연출
        private readonly Dictionary<PlotInteractable, int> m_stages = new Dictionary<PlotInteractable, int>();
        private FarmArea m_farm;
        private FrameCache m_frames;
        private TableSet m_tables;
        private DigView m_dig;
        private PoopViews m_poopViews;
        private ClerkViews m_clerkViews;
        private Interactable m_shownTarget;
        private MarkerView m_stairTag;
        private Earth m_earth;
        private bool m_editing;
        private IDisposable[] m_subscriptions;
        private Sprite m_square;

        // 설계 39: 굴 재료 한 묶음(바닥 · 벽 · 윗벽 띠는 굴 그림이 픽셀을 읽고, 둘레 흙은 벽 타일 그림)
        [Serializable]
        private struct Earth
        {
            public Texture2D Floor;
            public Texture2D Wall;
            public Texture2D Face;
            public Sprite Backdrop;
        }

        // 굴을 팠다(카메라 경계가 넓어진다)
        public event Action Expanded;

        public Vector3 Origin => transform.position;
        public Transform Wombat => m_wombat.transform;

        // 빵집과 같은 규칙(QA D): 판 칸의 경계 + 좌·우·아래로 한 칸(팔 수 있는 흙이 보이게). 위는 입구 윗변
        public Rect Bounds
        {
            get
            {
                CellMetrics cells = m_farm.Layout.Cells;
                int minCol = int.MaxValue, maxCol = int.MinValue, maxRow = 0;

                foreach (Cell cell in m_farm.Grid.Cells)
                {
                    minCol = Mathf.Min(minCol, cell.Col);
                    maxCol = Mathf.Max(maxCol, cell.Col);
                    maxRow = Mathf.Max(maxRow, cell.Row);
                }

                float xMin = transform.position.x + (minCol - 1) * cells.CellWidth;
                float xMax = transform.position.x + (maxCol + 2) * cells.CellWidth;
                float yMin = transform.position.y - cells.RowTop(maxRow + 2);
                return new Rect(xMin, yMin, xMax - xMin, transform.position.y - yMin);
            }
        }

        public void Bind(FarmArea farm, EventBus bus, FrameCache frames, TableSet tables)
        {
            m_farm = farm;
            m_frames = frames;
            m_tables = tables;
            CellMetrics cells = farm.Layout.Cells;
            m_dig = new DigView(this, m_digTagPrefab, tables, farm.Grid, cell => ToWorld(cells.CellCenter(cell)), new Vector2(cells.CellWidth, cells.CellHeight), m_digSeconds);
            m_poopViews = new PoopViews(this, farm, bus, m_poopPrefab, m_poopFrames, m_poopFrameRate, m_popupPrefab, tables, frames, ToWorld);
            m_clerkViews = new ClerkViews(this, farm, bus, m_clerkPrefab, tables, frames, transform, null);
            m_earth = m_earths[Math.Min((farm.Number - 1) / farm.Config.EarthEvery, m_earths.Length - 1)];
            m_backdrop.sprite = m_earth.Backdrop;
            Repaint();
            m_arch.localPosition = new Vector3(farm.Layout.HoleFloor.X, -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit, 0f);
            m_entranceProps.localPosition = m_arch.localPosition;

            if (farm.Upper != null)
            {
                m_arch.GetComponent<SpriteRenderer>().sprite = m_stairsUp;
            }

            m_stairsDown.transform.localPosition = new Vector3(farm.Layout.StairSpot.X, farm.Layout.StairSpot.Y, 0f);
            Build();
            m_wombat.Bind(farm, transform, frames);

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.Dug>(Bus_Dug),
                bus.Subscribe<Events.Tilled>(Bus_Tilled),
                bus.Subscribe<Events.Harvested>(Bus_Harvested),
                bus.Subscribe<Events.BonusFound>(Bus_BonusFound),
                bus.Subscribe<Events.FloorOpened>(Bus_FloorOpened),
            };
        }

        // 편집 모드: 파기 표식 전부 + 밭이 한 번 톡 튄다
        public void SetEditing(bool editing)
        {
            m_editing = editing;
            m_dig.Refresh(editing, m_shownTarget);

            if (!editing)
            {
                return;
            }

            foreach (PlotView plot in m_plots.Values)
            {
                plot.Bounce();
            }
        }

        public void SetHeld(IPlaced held)
        {
        }

        public void ShowGhost(IPlacedKind kind, System.Numerics.Vector2 at, bool ok)
        {
        }

        public void HideGhost()
        {
        }

        // 자라는 타이머는 매 프레임 읽는다(밭 사건은 그림 단계가 바뀔 때만 온다). 대상인 빈 밭은 화살표를 끈다
        private void Update()
        {
            foreach (KeyValuePair<PlotInteractable, PlotView> pair in m_plots)
            {
                pair.Value.SetMarkHidden(m_farm.Target == pair.Key);

                if (!pair.Key.IsEmpty && !pair.Key.IsRipe)
                {
                    pair.Value.SetProgress((float)pair.Key.Progress);
                }
            }
        }

        private void OnDestroy()
        {
            if (m_subscriptions == null)
            {
                return;
            }

            foreach (PlotInteractable plot in m_plots.Keys)
            {
                plot.Changed -= Plot_Changed;
            }

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }

            m_poopViews.Dispose();
            m_clerkViews.Dispose();
        }

        private Vector3 ToWorld(System.Numerics.Vector2 p)
        {
            return transform.position + new Vector3(p.X, p.Y, 0f);
        }

        // 굴 모양은 Core가 계산해 둔 마스크(걷는 땅과 같은 것)를 칠한다
        private void Repaint()
        {
            BurrowShape.Result shape = m_farm.Layout.Shape;
            Sprite old = m_burrow.sprite;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_earth.Floor, m_earth.Wall, m_earth.Face);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);

            if (old != null)
            {
                Destroy(old.texture);
                Destroy(old);
            }
        }

        // 밭 뷰를 Core 목록에 맞춘다: 새 칸(판 흙 칸)은 만들고, 밭은 사라지지 않는다
        private void Build()
        {
            foreach (PlotInteractable plot in m_farm.Plots)
            {
                if (m_plots.ContainsKey(plot))
                {
                    continue;
                }

                PlotView view = Instantiate(m_plotPrefab, transform);
                view.transform.position = ToWorld(plot.Position);
                m_plots[plot] = view;
                plot.Changed += Plot_Changed;
                Refresh(plot);
            }

            RefreshTillTags();
            RefreshStair();
        }

        // 값 표식(파기 표식 프리팹)은 모든 그림 위에
        private MarkerView NewTag(Vector3 at)
        {
            MarkerView tag = Instantiate(m_digTagPrefab, at, Quaternion.identity, transform);

            foreach (SpriteRenderer renderer in tag.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sortingOrder = k_TillTagOrder;
            }

            foreach (TMPro.TextMeshPro label in tag.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            {
                label.sortingOrder = k_TillTagOrder + 1;
            }

            return tag;
        }

        // 설계 39: 다 판 층(아래층이 닫힘)은 계단 자리에 값 표식을 늘 보인다. 아래층이 열렸으면 내려가는 계단
        private void RefreshStair()
        {
            FarmArea lower = m_farm.Lower;
            m_stairsDown.gameObject.SetActive(lower != null && lower.IsOpen);

            if (lower == null || lower.IsOpen || !m_farm.IsFull)
            {
                if (m_stairTag != null)
                {
                    m_stairTag.gameObject.SetActive(false);
                }

                return;
            }

            if (m_stairTag == null)
            {
                m_stairTag = NewTag(ToWorld(m_farm.Layout.StairSpot + new System.Numerics.Vector2(0f, k_StairTagRise)));
            }

            m_stairTag.gameObject.SetActive(true);
            m_stairTag.Show(m_tables.Format("tag_stair", lower.Floor.OpenCost.ToString("0", System.Globalization.CultureInfo.InvariantCulture)));
        }

        // 작물 그림은 CropTable sprite + _단계_반쪽(0·1). 줄마다 반쪽을 번갈아 얹어 같은 그림이 반복되지 않는다.
        // 무럭무럭: 단계 1부터는 바람에 기운 두 장(_l · _r)도 준다. 단계가 오르면(심기 포함) 밭 뷰가 늘어나며 바꾼다. 다 익음 표시는 작물마다(CropTable readyMark)
        private void Refresh(PlotInteractable plot)
        {
            PlotView view = m_plots[plot];
            Sprite[][] crops = null;
            int stage = plot.Stage;

            if (!plot.IsEmpty)
            {
                crops = new Sprite[view.Rows][];

                for (int i = 0; i < crops.Length; i++)
                {
                    string path = plot.Crop.Sprite + "_" + stage + "_" + i % k_CropHalves;
                    crops[i] = stage == 0
                        ? new[] { m_frames.Get(path)[0] }
                        : new[] { m_frames.Get(path)[0], m_frames.Get(path + "_l")[0], m_frames.Get(path + "_r")[0] };
                }
            }

            bool grew = m_stages.TryGetValue(plot, out int before) && stage > before;
            m_stages[plot] = stage;
            Sprite readyMark = plot.IsEmpty ? null : m_frames.Get(plot.Crop.ReadyMark)[0];
            view.Show(plot.IsTilled, crops, readyMark, !plot.IsEmpty && !plot.IsRipe, plot.IsRipe, plot.IsFertilized, grew);
        }

        // 흙 칸(갈기 전)마다 「갈기 값」 표식을 늘 보인다. 갈면 사라진다
        private void RefreshTillTags()
        {
            CellMetrics cells = m_farm.Layout.Cells;
            string text = m_tables.Format("tag_till", m_farm.Floor.TillCost.ToString("0", System.Globalization.CultureInfo.InvariantCulture));

            foreach (PlotInteractable plot in m_farm.Plots)
            {
                m_tillTags.TryGetValue(plot, out MarkerView tag);

                if (plot.IsTilled)
                {
                    if (tag != null)
                    {
                        tag.gameObject.SetActive(false);
                    }

                    continue;
                }

                if (tag == null)
                {
                    tag = NewTag(ToWorld(cells.CellCenter(plot.Cell) + new System.Numerics.Vector2(0f, cells.CellHeight * 0.5f - k_TillTagDrop)));
                    m_tillTags[plot] = tag;
                }

                tag.Show(text);
            }
        }

        // 심기 · 자람 · 익음은 밭 뷰가 늘어나며 보인다(Refresh). 거름을 섞어 심은 순간은 거름 알갱이도 튄다. 거두기 · 갈기는 각자 연출
        private void Plot_Changed(Interactable thing)
        {
            PlotInteractable plot = (PlotInteractable)thing;
            Refresh(plot);

            if (!plot.IsEmpty && plot.Stage == 0 && plot.IsFertilized)
            {
                Burst(ToWorld(m_farm.Layout.Cells.CellCenter(plot.Cell)), k_Manure, k_ManureLight);
            }
        }

        private void Bus_LayoutChanged(Events.LayoutChanged e)
        {
            if (e.Area == m_farm)
            {
                Build();
                m_dig.Refresh(m_editing, m_shownTarget);
            }
        }

        // 굴 파기 연출: 다시 칠하고 새 칸이 흙빛에서 밝아진다
        private void Bus_Dug(Events.Dug e)
        {
            if (e.Grid != m_farm.Grid)
            {
                return;
            }

            Repaint();
            m_dig.Play(e.Cell);
            Expanded?.Invoke();
        }

        // 갈았다: 흙덩이가 튀고(파기와 같은 연출) 밭이 튄다. 갈기 표식은 사라진다
        private void Bus_Tilled(Events.Tilled e)
        {
            if (e.Plot.Farm != m_farm)
            {
                return;
            }

            m_dig.Clods(ToWorld(m_farm.Layout.Cells.CellCenter(e.Plot.Cell)));

            if (m_plots.TryGetValue(e.Plot, out PlotView view))
            {
                view.Bounce();
            }

            RefreshTillTags();
        }

        // 설계 39: 이 층의 계단으로 아래층이 열렸다. 계단 굴을 칠하고 흙덩이가 튄 뒤 내려가는 계단이 톡
        private void Bus_FloorOpened(Events.FloorOpened e)
        {
            if (e.Floor.Upper != m_farm)
            {
                return;
            }

            Repaint();
            RefreshStair();
            NavRect stair = m_farm.Layout.StairRect;
            m_dig.Clods(ToWorld(new System.Numerics.Vector2((stair.XMin + stair.XMax) * 0.5f, (stair.YMin + stair.YMax) * 0.5f)));
            StartCoroutine(Fx.Bounce(m_stairsDown.transform));
        }

        // 대상이 바뀌면 튄다. 버튼 행동만 바뀐 사건(익었다 등)에서는 튀지 않는다
        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area != m_farm || m_farm.Target == m_shownTarget)
            {
                return;
            }

            m_shownTarget = m_farm.Target;
            m_dig.Refresh(m_editing, m_shownTarget);

            switch (m_shownTarget)
            {
                case PlotInteractable plot when m_plots.ContainsKey(plot):
                    m_plots[plot].Bounce();
                    break;
                case DigInteractable dig:
                    m_dig.Bounce(dig.Cell);
                    break;
                case PassageInteractable passage:
                    StartCoroutine(Fx.Bounce(m_farm.Lower != null && passage.To == m_farm.Lower.Id ? m_stairsDown.transform : m_arch));
                    break;
                case StairInteractable _ when m_stairTag != null:
                    m_stairTag.Bounce();
                    break;
                case PoopInteractable poop:
                    m_poopViews.Bounce(poop);
                    break;
            }
        }

        private void Bus_Harvested(Events.Harvested e)
        {
            if (e.Plot.Farm != m_farm)
            {
                return;
            }

            // 거두는 것은 밟은 웜뱃이므로 팝업은 웜뱃 머리 위에서 떠오른다(그림이 아니라 sim 자리: 그림은 한 프레임 늦다). 밭에서는 낟알이 튄다
            Popup(ToWorld(m_farm.Wombat.Mover.Position) + Vector3.up * k_PopupHeight, e.Crop.Item, e.Count);
            m_plots[e.Plot].Bounce();
            Burst(ToWorld(m_farm.Layout.Cells.CellCenter(e.Plot.Cell)), k_Grain, k_GrainLight);
        }

        // 설계 28: 덤이 나왔다. 거두기 팝업이 뜬 뒤 같은 자리에 하나 더 띄우면 그 위에 쌓이고, 쌓인 자리에서 반짝 알갱이가 튄다
        private void Bus_BonusFound(Events.BonusFound e)
        {
            if (e.Plot.Farm == m_farm)
            {
                StartCoroutine(BonusRoutine(e));
            }
        }

        private IEnumerator BonusRoutine(Events.BonusFound e)
        {
            yield return new WaitForSeconds(k_BonusDelay);
            CoinPopup popup = Popup(ToWorld(m_farm.Wombat.Mover.Position) + Vector3.up * k_PopupHeight, e.Item, e.Count);
            Burst(popup.transform.position, k_Spark, k_GrainLight);
        }

        private CoinPopup Popup(Vector3 at, string item, int count)
        {
            CoinPopup popup = Instantiate(m_popupPrefab, at, Quaternion.identity, transform);
            popup.Show(m_tables.Format(k_PopupKey, count), m_frames.Get(m_tables.Get<ItemTable>(item).Icon)[0]);
            return popup;
        }

        // 네모 알갱이 두 빛깔이 튄다(낟알 · 거름 · 반짝)
        private void Burst(Vector3 at, Color first, Color second)
        {
            m_square = m_square != null ? m_square : Fx.NewSquare();
            StartCoroutine(Fx.Burst(transform, m_square, at, k_GrainCount, k_GrainSpread, k_GrainLift, k_GrainGravity, k_GrainSeconds, k_GrainCells, first, k_GrainOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, k_GrainCount, k_GrainSpread, k_GrainLift, k_GrainGravity, k_GrainSeconds, k_GrainCells, second, k_GrainOrder));
        }
    }
}
