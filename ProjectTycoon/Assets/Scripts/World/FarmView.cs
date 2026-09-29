using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 25: 농장 굴 화면. 굴 그림은 빵집·광장과 같은 BurrowPainter, 구멍 아치(광장으로)는 첫 줄 가운데.
    // 밭은 Core FarmArea.Plots를 객체 키로 맞추고(놓이면 만들고 옮기면 옮기고 치우면 지운다), 거두면 밭 위로 재료 아이콘과 「+3」이 떠오른다
    public sealed class FarmView : MonoBehaviour, IAreaView
    {
        private const string k_PopupKey = "harvest_popup";
        // 거두기 팝업이 뜨는 높이(밭 밑변에서)
        private const float k_PopupHeight = 0.9f;

        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("바닥·벽·윗벽 띠 재료(빵집과 같은 것)")]
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [SerializeField] private Texture2D m_wallFace;
        [Tooltip("구멍 아치(나가기 대상이면 튄다)")]
        [SerializeField] private Transform m_arch;
        [SerializeField] private PlotView m_plotPrefab;
        [Tooltip("편집 그림자용 밭 그림")]
        [SerializeField] private Sprite m_ghostPlot;
        [Tooltip("거두기 팝업(코인 팝업 프리팹에 재료 아이콘을 얹는다)")]
        [SerializeField] private CoinPopup m_popupPrefab;
        [SerializeField] private WombatView m_wombat;

        private readonly Dictionary<PlotInteractable, PlotView> m_plots = new Dictionary<PlotInteractable, PlotView>();
        private FarmArea m_farm;
        private FrameCache m_frames;
        private TableSet m_tables;
        private GhostView m_ghost;
        private Interactable m_shownTarget;
        private bool m_built;
        private bool m_editing;
        private IPlaced m_held;
        private IDisposable[] m_subscriptions;

        public Vector3 Origin => transform.position;
        public Transform Wombat => m_wombat.transform;

        // 농장 둘레 + 좌·우·아래 흙 한 칸. 위는 첫 줄 윗변
        public Rect Bounds
        {
            get
            {
                FarmLayout layout = m_farm.Layout;
                Vector3 origin = transform.position;
                return new Rect(origin.x - layout.Width * 0.5f - 1f, origin.y - layout.Height - 1f, layout.Width + 2f, layout.Height + 1f);
            }
        }

        public void Bind(FarmArea farm, EventBus bus, FrameCache frames, TableSet tables)
        {
            m_farm = farm;
            m_frames = frames;
            m_tables = tables;
            BurrowShape.Result shape = farm.Layout.Shape;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile, m_wallFace);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);
            m_arch.localPosition = new Vector3(farm.Layout.HoleFloor.X, -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit, 0f);
            m_ghost = GhostView.Create(transform);
            Build();
            m_wombat.Bind(farm, transform, frames);
            m_built = true;

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.Harvested>(Bus_Harvested),
            };
        }

        public void ShowGhost(IPlacedKind kind, System.Numerics.Vector2 at, bool ok)
        {
            m_ghost.Show(m_ghostPlot, ToWorld(at), ok);
        }

        public void HideGhost()
        {
            m_ghost.Hide();
        }

        // 편집 모드: 밭마다 도는 점선 외곽선 + 들어갈 때 한 번 톡 튄다
        public void SetEditing(bool editing)
        {
            m_editing = editing;
            RefreshOutlines();

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
            m_held = held;
            RefreshOutlines();
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
        }

        private Vector3 ToWorld(System.Numerics.Vector2 p)
        {
            return transform.position + new Vector3(p.X, p.Y, 0f);
        }

        private void RefreshOutlines()
        {
            foreach (KeyValuePair<PlotInteractable, PlotView> pair in m_plots)
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

        // 밭 뷰를 Core 목록에 맞춘다: 없는 것은 만들고(놓인 것은 튐), 사라진 것은 지우고, 자리는 늘 다시 놓는다
        private void Build()
        {
            foreach (PlotInteractable plot in new List<PlotInteractable>(m_plots.Keys))
            {
                if (!m_farm.Plots.Contains(plot))
                {
                    plot.Changed -= Plot_Changed;
                    Destroy(m_plots[plot].gameObject);
                    m_plots.Remove(plot);
                }
            }

            foreach (PlotInteractable plot in m_farm.Plots)
            {
                if (!m_plots.TryGetValue(plot, out PlotView view))
                {
                    view = Instantiate(m_plotPrefab, transform);
                    m_plots[plot] = view;
                    plot.Changed += Plot_Changed;
                    Refresh(plot);

                    if (m_built)
                    {
                        view.Bounce();
                    }
                }

                view.transform.position = ToWorld(plot.Position);
            }
        }

        private void Refresh(PlotInteractable plot)
        {
            Sprite crop = plot.IsEmpty ? null : m_frames.Get(plot.Crop.Sprite + "_" + plot.Stage)[0];
            m_plots[plot].Show(crop, !plot.IsEmpty && !plot.IsRipe);
        }

        private void Plot_Changed(Interactable plot)
        {
            Refresh((PlotInteractable)plot);
        }

        private void Bus_LayoutChanged(Events.LayoutChanged e)
        {
            if (e.Area == m_farm)
            {
                Build();
                RefreshOutlines();
            }
        }

        // 버튼 행동만 바뀐 사건(익었다 등)에서는 튀지 않는다
        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area != m_farm || m_farm.Target == m_shownTarget)
            {
                return;
            }

            m_shownTarget = m_farm.Target;

            switch (m_shownTarget)
            {
                case PlotInteractable plot:
                    m_plots[plot].Bounce();
                    break;
                case PassageInteractable _:
                    StartCoroutine(Fx.Bounce(m_arch));
                    break;
            }
        }

        private void Bus_Harvested(Events.Harvested e)
        {
            if (e.Plot.Farm != m_farm)
            {
                return;
            }

            CoinPopup popup = Instantiate(m_popupPrefab, ToWorld(e.Plot.Position) + Vector3.up * k_PopupHeight, Quaternion.identity, transform);
            popup.Show(m_tables.Format(k_PopupKey, e.Count), m_frames.Get(m_tables.Get<ItemTable>(e.Item).Icon)[0]);
            m_plots[e.Plot].Bounce();
        }
    }
}
