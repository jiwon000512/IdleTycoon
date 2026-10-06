using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 11 · 설계 18: 굴 밖 광장. 굴 그림은 빵집과 같은 BurrowPainter로 칠하고, 곳으로 가는 문(아치·간판, 빵집은 차양도, 설계 25 · 44)·지상 계단은 Core PlazaLayout이 정한 자리에 놓는다.
    // 장식은 Core PlazaArea.Decor를 객체 키로 맞춘다(놓이면 만들고 옮기면 옮기고 치우면 지운다). 웜뱃이 문 앞에 서면(대상이 문) 그 문이 한 번 튄다
    public sealed class PlazaView : MonoBehaviour, IAreaView
    {
        // 문 간판 글 = 이것 + 갈 곳 id(빵집은 별 수와 함께)
        private const string k_SignPrefix = "sign_";
        // 설계 29 · 30: 석상에 빌 때 반짝 알갱이(거두기 덤과 같은 두 빛깔). 축복이 걸린 동안은 k_GlowSeconds마다 금빛 알갱이 몇 개
        private static readonly Color k_SparkLight = new Color32(0xFB, 0xF4, 0xE6, 255);
        private static readonly Color k_SparkGold = new Color32(0xF0, 0xD8, 0x90, 255);
        // 알갱이가 튀는 높이: 석상 그림 높이의 이 비율(머리 위쪽)
        private const float k_StatueSparkHeight = 0.88f;
        private const int k_SparkCount = 8;
        private const float k_SparkSpread = 1.6f;
        private const float k_SparkLift = 3.2f;
        private const float k_SparkGravity = 10f;
        private const float k_SparkSeconds = 0.5f;
        private const int k_SparkCells = 5;
        private const int k_SparkOrder = 1000;
        private const float k_GlowSeconds = 1.4f;
        private const int k_GlowCount = 3;
        private const float k_GlowSpread = 1.0f;
        private const float k_GlowLift = 1.8f;

        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("바닥·벽·윗벽 띠 재료(빵집과 같은 것)")]
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [SerializeField] private Texture2D m_wallFace;
        [Tooltip("설계 44: 곳으로 가는 문 그림(갈 곳 id마다 하나, PlazaConfigTable doors). 원점 = 구멍 밑변 가운데")]
        [SerializeField] private DoorArt[] m_doors;
        [Tooltip("지상 계단. 원점 = 띠 밑변 가운데")]
        [SerializeField] private Transform m_stairs;
        [SerializeField] private WombatView m_wombat;
        [Tooltip("설계 29: 웜뱃 석상(자리는 Core 석상 기준점)")]
        [SerializeField] private Transform m_statue;
        [Tooltip("설계 37: 거름 국자 코인 · 거름 「+N」 팝업(빵집과 같은 프리팹)")]
        [SerializeField] private CoinPopup m_popupPrefab;
        [Tooltip("설계 37: 웜뱃 똥 그림(빵집과 같은 프리팹 · 프레임)")]
        [SerializeField] private SpriteAnimator m_poopPrefab;
        [SerializeField] private Sprite[] m_poopFrames;
        [SerializeField] private float m_poopFrameRate = 2f;
        [Tooltip("설계 47: 닫힌 가게 문 앞 값 표식(파기 표식과 같은 조각)")]
        [SerializeField] private MarkerView m_tagPrefab;

        [Serializable]
        private struct DoorArt
        {
            public string To;
            public Transform Root;
            public TextMeshPro Sign;
        }

        // 설계 47: 닫힌 문 표식은 문 아래 바닥에서 이만큼 위(아치 앞), 닫힌 동안 아치 · 장식은 어둡게
        private const float k_GateTagRise = 0.9f;
        private static readonly Color k_Closed = new Color(0.45f, 0.45f, 0.45f, 1f);

        private readonly Dictionary<DecorationData, GameObject> m_decor = new Dictionary<DecorationData, GameObject>();
        private readonly Dictionary<ShopGateInteractable, MarkerView> m_gateTags = new Dictionary<ShopGateInteractable, MarkerView>();
        private PlazaArea m_plaza;
        private TableSet m_tables;
        private FrameCache m_frames;
        private GhostView m_ghost;
        private bool m_editing;
        private IPlaced m_held;
        private IDisposable[] m_subscriptions;
        private PoopViews m_poopViews;
        private Sprite m_square;
        private float m_glow;

        public Vector3 Origin => transform.position;
        public Transform Wombat => m_wombat.transform;
        public WombatView WombatView => m_wombat;

        // 광장 둘레 + 좌·우·아래 흙 한 칸. 위는 첫 줄 윗변
        public Rect Bounds
        {
            get
            {
                PlazaLayout layout = m_plaza.Layout;
                Vector3 origin = transform.position;
                return new Rect(origin.x - layout.Width * 0.5f - 1f, origin.y - layout.Height - 1f, layout.Width + 2f, layout.Height + 1f);
            }
        }

        public void Bind(PlazaArea plaza, EventBus bus, FrameCache frames, TableSet tables)
        {
            m_plaza = plaza;
            m_frames = frames;
            PlazaLayout layout = plaza.Layout;
            BurrowShape.Result shape = layout.Shape;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile, m_wallFace);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);

            float wallBottom = -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit;
            m_stairs.localPosition = new Vector3(layout.StairsFloor.X, wallBottom, 0f);
            m_tables = tables;

            foreach (PlazaDoor door in layout.Doors)
            {
                DoorArt art = Art(door.To);
                art.Root.localPosition = new Vector3(door.Floor.X, wallBottom, 0f);
                art.Sign.text = tables.Text(k_SignPrefix + door.To);
            }

            SetSign(plaza.Wombat.Worker.Wallet.Stars.Count(BakeryArea.k_Id));
            m_ghost = GhostView.Create(transform);
            m_statue.localPosition = new Vector3(plaza.Statue.Position.X, plaza.Statue.Position.Y, 0f);
            m_poopViews = new PoopViews(this, plaza, bus, m_poopPrefab, m_poopFrames, m_poopFrameRate, m_popupPrefab, tables, frames, p => transform.position + new Vector3(p.X, p.Y, 0f));
            Build();

            m_wombat.Bind(plaza, transform, frames);
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.BlessingChanged>(Bus_BlessingChanged),
                bus.Subscribe<Events.StarsChanged>(Bus_StarsChanged),
            };
        }

        // 설계 40: 빵집 간판에 별 수(「빵집 ★7」, 0이면 이름만). 별 색 단계 그림은 아트방
        private DoorArt Art(string to)
        {
            return m_doors.First(door => door.To == to);
        }

        private void SetSign(int stars)
        {
            string name = m_tables.Text(k_SignPrefix + BakeryArea.k_Id);
            Art(BakeryArea.k_Id).Sign.text = stars > 0 ? m_tables.Format("sign_stars", name, stars) : name;
        }

        private void Bus_StarsChanged(Events.StarsChanged e)
        {
            if (e.Shop == BakeryArea.k_Id)
            {
                SetSign(e.Count);
                StartCoroutine(Fx.Bounce(Art(BakeryArea.k_Id).Sign.transform));
            }
        }

        public void ShowGhost(IPlacedKind kind, System.Numerics.Vector2 at, bool ok)
        {
            m_ghost.Show(m_frames.Get(kind.Icon)[0], transform.position + new Vector3(at.X, at.Y, 0f), ok);
        }

        public void HideGhost()
        {
            m_ghost.Hide();
        }

        // 편집 모드: 장식마다 도는 점선 외곽선 + 들어갈 때 한 번 톡 튄다
        public void SetEditing(bool editing)
        {
            m_editing = editing;
            RefreshOutlines();

            if (!editing)
            {
                return;
            }

            foreach (GameObject go in m_decor.Values)
            {
                StartCoroutine(Fx.Bounce(go.transform));
            }
        }

        public void SetHeld(IPlaced held)
        {
            m_held = held;
            RefreshOutlines();
        }

        private void RefreshOutlines()
        {
            foreach (KeyValuePair<DecorationData, GameObject> pair in m_decor)
            {
                EditOutlineView outline = EditOutlineView.Attach(pair.Value.GetComponent<SpriteRenderer>());

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

        // 설계 47: 닫힌 가게 문 앞 표식(빵집 별이 모자라면 「★n 필요」, 닿았으면 「열기 값」). 열리면 지우고 아치를 밝힌다
        private void SyncGateTags()
        {
            foreach (ShopGateInteractable gate in m_plaza.Gates)
            {
                if (!m_gateTags.TryGetValue(gate, out MarkerView tag))
                {
                    tag = Instantiate(m_tagPrefab, transform);
                    tag.transform.localPosition = new Vector3(gate.Floor.X, gate.Floor.Y + k_GateTagRise, 0f);
                    m_gateTags[gate] = tag;
                    Tint(Art(gate.Shop.Id).Root, k_Closed);
                }

                RestaurantConfigTable config = gate.Shop.Config;
                tag.Show(gate.Shop.StarLocked ? m_tables.Format("row_star_needed", config.OpenStar)
                    : m_tables.Format("tag_open_shop", config.OpenCost.ToString("0", System.Globalization.CultureInfo.InvariantCulture)));
            }

            foreach (ShopGateInteractable gate in new List<ShopGateInteractable>(m_gateTags.Keys))
            {
                if (!m_plaza.Gates.Contains(gate))
                {
                    Destroy(m_gateTags[gate].gameObject);
                    m_gateTags.Remove(gate);
                    Transform door = Art(gate.Shop.Id).Root;
                    Tint(door, Color.white);
                    StartCoroutine(Fx.Bounce(door));
                }
            }
        }

        private static void Tint(Transform root, Color color)
        {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>())
            {
                renderer.color = color;
            }
        }

        // 장식을 Core 목록에 맞춘다: 없는 것은 만들고, 사라진 것은 지우고, 자리는 늘 다시 놓는다
        private void Build()
        {
            foreach (DecorationData decor in new List<DecorationData>(m_decor.Keys))
            {
                if (!m_plaza.Decor.Contains(decor))
                {
                    Destroy(m_decor[decor]);
                    m_decor.Remove(decor);
                }
            }

            foreach (DecorationData decor in m_plaza.Decor)
            {
                if (!m_decor.TryGetValue(decor, out GameObject go))
                {
                    go = CreateDecor(decor);
                    m_decor[decor] = go;
                }

                go.transform.localPosition = new Vector3(decor.Position.X, decor.Position.Y, 0f);
            }
        }

        private GameObject CreateDecor(DecorationData decor)
        {
            GameObject go = new GameObject(decor.Table.Id);
            go.transform.SetParent(transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = m_frames.Get(decor.Table.Sprite)[0];
            // 월드 스프라이트는 밑변(피벗)으로 정렬(BakeryBaker.Renderer와 같은 규칙)
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            if (decor.Table.Frames > 0)
            {
                Sprite[] loop = new Sprite[decor.Table.Frames];

                for (int i = 0; i < loop.Length; i++)
                {
                    loop[i] = m_frames.Get(decor.Table.Sprite + "_" + i)[0];
                }

                go.AddComponent<SpriteAnimator>().Play(renderer, loop, (float)decor.Table.FrameRate);
            }

            return go;
        }

        private void Bus_LayoutChanged(Events.LayoutChanged e)
        {
            if (e.Area == m_plaza)
            {
                Build();
                RefreshOutlines();
            }
        }

        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area == m_plaza && m_plaza.Target is PassageInteractable passage)
            {
                StartCoroutine(Fx.Bounce(Art(passage.To).Root));
            }

            if (e.Area == m_plaza && m_plaza.Target is ShopGateInteractable gate && m_gateTags.TryGetValue(gate, out MarkerView tag))
            {
                tag.Bounce();
            }

            if (e.Area == m_plaza && m_plaza.Target is StatueInteractable)
            {
                StartCoroutine(Fx.Bounce(m_statue));
            }

            if (e.Area == m_plaza && m_plaza.Target is PoopInteractable poop)
            {
                m_poopViews.Bounce(poop);
            }
        }

        private Vector3 StatueSparkPoint => SparkPoint(m_statue);

        // 그림 높이의 k_StatueSparkHeight 비율 자리(머리 위쪽)
        private static Vector3 SparkPoint(Transform thing)
        {
            Bounds body = thing.GetComponentInChildren<SpriteRenderer>().bounds;
            return new Vector3(thing.position.x, body.min.y + body.size.y * k_StatueSparkHeight, thing.position.z);
        }

        private void Update()
        {
            if (m_plaza == null)
            {
                return;
            }

            SyncGateTags();

            if (m_plaza.Statue.Blessing.Active == null)
            {
                return;
            }

            m_glow -= Time.deltaTime;

            if (m_glow > 0f)
            {
                return;
            }

            m_glow = k_GlowSeconds;
            m_square = m_square != null ? m_square : Fx.NewSquare();
            Vector3 at = StatueSparkPoint;
            StartCoroutine(Fx.Burst(transform, m_square, at, k_GlowCount, k_GlowSpread, k_GlowLift, k_SparkGravity, k_SparkSeconds, k_SparkCells, k_SparkGold, k_SparkOrder));
        }

        // 설계 30: 빌면 석상이 톡 튀고 머리 위에서 반짝 알갱이가 튄다
        private void Bus_BlessingChanged(Events.BlessingChanged e)
        {
            if (!e.Prayed || !isActiveAndEnabled)
            {
                return;
            }

            Sparkle(m_statue);
        }

        private void Sparkle(Transform thing)
        {
            StartCoroutine(Fx.Bounce(thing));
            m_square = m_square != null ? m_square : Fx.NewSquare();
            Vector3 at = SparkPoint(thing);
            StartCoroutine(Fx.Burst(transform, m_square, at, k_SparkCount, k_SparkSpread, k_SparkLift, k_SparkGravity, k_SparkSeconds, k_SparkCells, k_SparkLight, k_SparkOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, k_SparkCount, k_SparkSpread, k_SparkLift, k_SparkGravity, k_SparkSeconds, k_SparkCells, k_SparkGold, k_SparkOrder));
        }
    }
}
