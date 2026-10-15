using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GameKit.Events;
using GameKit.Tables;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 52 · 61 낚시터 그림: 굴 그림(BurrowPainter) 위에 호수(WaterPainter, 섬 뭍 밖 전부) · 섬마다 둑 띠 · 놓인 다리 · 다리 자리(흐림 + 값 표식) · 안 닿은 섬 위 어두운 덮개 ·
    // 손에 든 대 · 줄 · 찌 · 물속 그림자 · 긴장 게이지 · 명물 그림자 · 떠내려온 상자 · 좌대 · 좌대 손님. 상태는 매 프레임 FishingArea에서 읽고 사건은 버스에서 받는다
    public sealed class FishingView : MonoBehaviour, IAreaView
    {
        private const string k_PopupKey = "harvest_popup";
        // 그림 층: 굴(−2000) 위에 물 · 둑, 그 위에 물속 그림자 · 물결 고리, 팝업 · 알갱이 · 게이지는 맨 위
        private const int k_WaterOrder = -1985;
        private const int k_FishOrder = -1980;
        private const int k_TopOrder = 1000;
        // 손에 든 대: 웜뱃 앞발 자리(WombatView.Hand = 든 빵 가운데 0.35)에서 앞발 높이(0.21)로 내린 자리. 앞 · 뒷모습은 오른쪽 앞발(3칸)에 쥐어 대가 얼굴을 가로지르지 않게. 몸 앞 · 뒤 순서는 든 빵과 같은 규칙
        private static readonly Vector3 k_RodGrip = new Vector3(0.075f, -0.14f, 0f);
        private const int k_RodFrontOrder = 52;
        private const int k_RodBackOrder = -11;
        // 줄 · 찌는 몸과 대 위에(사용자 2026-10-07 「줄이 몸에 가려짐」)
        private const int k_LineOrder = k_RodFrontOrder + 1;
        // 대 끝(줄이 나오는 점): 피벗(손이 쥐는 점)에서 칸(1/40유닛), 아트방 make_rods.py
        private static readonly Dictionary<string, Vector2> k_RodTips = new Dictionary<string, Vector2>
        {
            { "rod_bamboo_1", new Vector2(15f, 36f) },
            { "rod_bamboo_1_pull", new Vector2(17f, 35f) },
            { "rod_bent", new Vector2(28f, 21f) },
        };
        private const float k_LineWidth = 0.025f;
        private static readonly Color k_Line = new Color32(0xF6, 0xE3, 0xCC, 200);
        private static readonly Color k_Water = new Color32(0x3E, 0x7F, 0x95, 255);
        private static readonly Color k_WaterLight = new Color32(0xDD, 0xF1, 0xF5, 255);
        private const float k_WaterFrameSeconds = 0.35f;
        // 찌: 줄다리기는 남은 거리 비율로 먼 쪽(k_TugFar)에서 물가(k_TugNear)로 · 달릴 때 옆으로 흔들리는 폭(유닛) · 빠르기 · 가짜 입질 까딱 초. 기다릴 때 자리는 Core(BobberPoint)
        private const float k_TugNear = 0.12f;
        private const float k_TugFar = 0.6f;
        private const float k_RunWobble = 0.15f;
        private const float k_RunWobbleSpeed = 12f;
        private const float k_NibbleSeconds = 0.18f;
        // 던지기: 찌가 대 끝에서 물 자리까지 포물선으로 나는 초 · 높이(유닛) · 대를 뒤로 당겼다(도) 앞으로 휘두르는(도) 각
        private const float k_CastSeconds = 0.35f;
        private const float k_CastArc = 0.6f;
        private const float k_SwingBack = -35f;
        private const float k_SwingForward = 18f;
        // 달릴 때 대 떨림(도 · 초당) · 끊길 때 대가 뒤로 튕겼다 돌아오는 각(도) · 초
        private const float k_ShakeDegrees = 4f;
        private const float k_ShakeHz = 14f;
        private const float k_RecoilDegrees = -25f;
        private const float k_RecoilSeconds = 0.3f;
        // 떠 있는 찌는 살랑인다(유닛 · 빠르기) · 물결 고리 간격(초) · 고리 반지름(기다림 · 입질 · 물었다 · 떨어짐 · 달림 · 그림자) · 고리 색
        private const float k_BobAmount = 0.02f;
        private const float k_BobSpeed = 2.5f;
        private const float k_RippleEvery = 1.6f;
        private const float k_RippleIdle = 0.3f;
        private const float k_RippleNibble = 0.45f;
        private const float k_RippleBite = 0.8f;
        private const float k_RippleLand = 0.6f;
        private const float k_RippleRun = 0.35f;
        private const float k_RippleShadow = 0.5f;
        private const float k_RippleSeconds = 0.6f;
        private static readonly Color k_Ripple = new Color32(0xDD, 0xF1, 0xF5, 210);
        // 줄다리기: 줄 색은 긴장만큼 붉어진다 · 게이지는 이 비율부터 두근거린다 · 달리는 물고기 뒤 거품 간격(초)
        private static readonly Color k_LineTense = new Color32(0xE0, 0x5A, 0x4A, 230);
        private const float k_GaugeAlarm = 0.7f;
        private const float k_FoamEvery = 0.12f;
        // 물속 그림자는 찌 아래로 · 긴장 게이지(릴 16칸)는 웜뱃 오른쪽 어깨 옆(대 끝 · 줄과 안 겹치게)
        private const float k_ShadowDrop = 0.12f;
        private static readonly Vector3 k_GaugeAt = new Vector3(0.95f, 1.1f, 0f);
        // 낚은 물고기가 손으로 튀는 초 · 높이 · 나는 동안 도는 각(도) · 만세한 두 앞발 사이 높이(유닛, 아트방 make_cheer.py) · 앞발에 닿을 때 금빛 알갱이 · 이름 알약이 떠오르는 초 · 팝업 높이(든 물고기 위)
        private const float k_CatchSeconds = 0.35f;
        private const float k_CatchArc = 0.8f;
        private const float k_CatchSpin = 540f;
        private const float k_CheerHold = 1.08f;
        private static readonly Color k_Sparkle = new Color32(0xF2, 0xC1, 0x4E, 255);
        private const float k_RiseSeconds = 0.9f;
        private const float k_PopupHeight = 1.7f;
        // 설계 61: 다리 값 표식 높이 · 좌대 값 표식 높이 · 상자 · 명물 그림자 층(물 위, 줄 아래) · 상자에 건 찌가 상자 발끝에서 뜬 높이 · 안 닿은 섬 덮개 · 명물 그림자 물결 간격
        private const float k_BridgeTagRise = 1.0f;
        private const float k_SeatTagRise = 0.9f;
        private const int k_CrateOrder = k_FishOrder + 3;
        private const float k_CrateHookRise = 0.7f;
        private static readonly Color k_Fog = new Color(0.05f, 0.08f, 0.12f, 0.5f);
        private const float k_ShadowRippleEvery = 0.8f;
        // 그림이 오기 전 빌려 쓰는 둑 소품과 그 배율 · 놓지 않은 좌대 · 다리 자리 흐림 · 손님 찌가 좌대 앞 물에 뜬 깊이 · 손님 손 자리
        private const float k_CrateFallbackScale = 1.2f;
        private const float k_GhostAlpha = 0.35f;
        private const float k_CustomerBobberDrop = 1.4f;
        private static readonly Vector3 k_CustomerHand = new Vector3(0.075f, 0.21f, 0f);
        private static readonly Color k_Dirt = new Color(0.45f, 0.3f, 0.18f);
        private static readonly Color k_DirtDark = new Color32(52, 32, 32, 255);

        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("바닥·벽·윗벽 띠 재료(빵집과 같은 것)")]
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [SerializeField] private Texture2D m_wallFace;
        [Tooltip("물결 네 칸(water_tile_0~3, k_WaterFrameSeconds마다 돈다) · 물 안쪽 윗면 흙 면(stream_face)")]
        [SerializeField] private Texture2D[] m_waterTiles;
        [SerializeField] private Texture2D m_streamFace;
        [Tooltip("둑 띠(아트방 bank, 피벗 아래 가운데, 섬 뭍 아랫변을 따라 뭍 폭만큼 Tiled). 사방 둑 고리는 WaterPainter가 늘 그린다 · 섬마다 다른 띠(bankstrip_<이름>, FishingIslandTable bank가 고른다 — 2026-10-14 사용자 보류)")]
        [SerializeField] private Sprite m_bank;
        [SerializeField] private Sprite[] m_bankStrips;
        [Tooltip("둑 · 물 소품(아트방 bank_<이름> · water_<이름>, 발끝 피벗). 자리는 FishingIslandTable decor")]
        [SerializeField] private Sprite[] m_props;
        [Tooltip("설계 61: 다리 널판(아트방 bridge, 가운데 피벗, 다리 네모 크기로 Tiled). 없으면 둑 띠를 깐다")]
        [SerializeField] private Sprite m_bridge;
        [Tooltip("설계 61: 가로 다리 전용 그림(아트방 bridge_side). 없으면 세로 그림을 90도 돌린다")]
        [SerializeField] private Sprite m_bridgeSide;
        [Tooltip("설계 53 · 61: 좌대(seat) · 떠내려온 상자(아트방 crate, 발끝 피벗). 상자 그림이 없으면 잔해(debris_*)의 [0], 그것도 없으면 둑 소품을 빌린다")]
        [SerializeField] private Sprite m_seat;
        [SerializeField] private Sprite m_crate;
        [SerializeField] private Sprite[] m_debris;
        [Tooltip("물속 물고기(<재료 id>_swim, 오른쪽을 봄, 가운데 피벗). 명물 그림자도 여기서(fish_king_catfish_swim)")]
        [SerializeField] private Sprite[] m_fishSwim;
        [Tooltip("긴장 게이지(아트방 B 「릴」 16칸, [0] = 0% · [15] = 100%, 피벗 = 릴 가운데)")]
        [SerializeField] private Sprite[] m_reelGauge;
        [Tooltip("찌(아트방 A 「동글 찌」 [0] 떠 있음 · [1] 까딱 잠김, 피벗 = 맨 위 가운데 = 줄 끝)")]
        [SerializeField] private Sprite[] m_bobber;
        [Tooltip("낚싯대(rod_bamboo_1 · _pull · rod_bent, 피벗 = 손이 쥐는 점)")]
        [SerializeField] private Sprite[] m_rods;
        [Tooltip("구멍 아치")]
        [SerializeField] private Transform m_arch;
        [Tooltip("이름 · 무게 알약(빵집 파기 표식과 같은 프리팹)")]
        [SerializeField] private MarkerView m_tagPrefab;
        [Tooltip("낚은 물고기 팝업(코인 팝업 프리팹에 재료 아이콘을 얹는다)")]
        [SerializeField] private CoinPopup m_popupPrefab;
        [Tooltip("설계 37: 웜뱃 똥 그림(빵집과 같은 프리팹 · 프레임)")]
        [SerializeField] private SpriteAnimator m_poopPrefab;
        [SerializeField] private Sprite[] m_poopFrames;
        [SerializeField] private float m_poopFrameRate = 2f;
        [Tooltip("점원 그림(2회차 알바, 지금은 자리가 없다)")]
        [SerializeField] private VisitorView m_clerkPrefab;
        [SerializeField] private WombatView m_wombat;

        private readonly Dictionary<string, Sprite> m_rodSprites = new Dictionary<string, Sprite>();
        private FishingArea m_fishing;
        private FrameCache m_frames;
        private TableSet m_tables;
        private PoopViews m_poopViews;
        private ClerkViews m_clerkViews;
        private IDisposable[] m_subscriptions;
        private Sprite m_square;
        private SpriteRenderer m_water;
        private Sprite[] m_waterFrames = new Sprite[0];
        private SpriteRenderer m_rod;
        private SpriteRenderer m_line;
        private SpriteRenderer m_bobberArt;
        private SpriteRenderer m_shadow;
        private SpriteRenderer m_gauge;
        private Rect m_bounds;
        private float m_nibbleLeft;
        private float m_castLeft;
        private float m_recoilLeft;
        private float m_cheerLeft;
        private float m_rippleIn;
        private float m_foamIn;
        private float m_handScale = 1f;
        private Vector3 m_bobberAt;
        // 설계 61: 다시 칠하는 둑 띠(섬마다) · 놓인 다리 · 안 닿은 섬 덮개 · 그린 소품의 섬 · 다리 자리(흐린 다리) · 상자 · 명물 그림자 · 좌대 · 좌대 손님 그림
        private readonly List<SpriteRenderer> m_bridgeArts = new List<SpriteRenderer>();
        private readonly Dictionary<int, SpriteRenderer> m_fogs = new Dictionary<int, SpriteRenderer>();
        private readonly HashSet<int> m_decorated = new HashSet<int>();
        private readonly Dictionary<BridgeInteractable, SpriteRenderer> m_bridgeSpots = new Dictionary<BridgeInteractable, SpriteRenderer>();
        private readonly Dictionary<SeatInteractable, SpriteRenderer> m_seatArts = new Dictionary<SeatInteractable, SpriteRenderer>();
        private readonly Dictionary<FishingVisitor, CustomerArt> m_customers = new Dictionary<FishingVisitor, CustomerArt>();
        private SpriteRenderer m_crateArt;
        private CrateInteractable m_crateShown;
        private SpriteRenderer m_shadowFish;
        // 다시 그리기 차례(늦게 끝난 옛 계산은 버린다)
        private int m_repaintVersion;
        private float m_shadowRippleIn;
        private float m_shadowLastX;
        private MarkerView m_tag;

        // 좌대 손님 하나의 그림: 손님(빵집 손님과 같은 VisitorView) · 손에 든 대 · 줄 · 찌
        private sealed class CustomerArt
        {
            public VisitorView Unit;
            public SpriteRenderer Rod;
            public SpriteRenderer Line;
            public SpriteRenderer Bobber;
        }

        // 굴이 넓어졌다(카메라 경계를 다시 읽는다)
        public event Action Expanded;

        public Vector3 Origin => transform.position;
        public Transform Wombat => m_wombat.transform;
        // 던진 동안 카메라가 비추는 점(웜뱃과 찌의 가운데)
        public Vector3 Focus => (m_wombat.transform.position + m_bobberAt) / 2f;
        public Rect Bounds => new Rect(transform.position.x + m_bounds.x, transform.position.y + m_bounds.y, m_bounds.width, m_bounds.height);

        public void Bind(FishingArea fishing, EventBus bus, FrameCache frames, TableSet tables)
        {
            m_fishing = fishing;
            m_frames = frames;
            m_tables = tables;
            m_square = Fx.NewSquare();
            m_poopViews = new PoopViews(this, fishing, bus, m_poopPrefab, m_poopFrames, m_poopFrameRate, m_popupPrefab, tables, frames, ToWorld);
            m_clerkViews = new ClerkViews(this, fishing, bus, m_clerkPrefab, tables, frames, transform, null);

            m_arch.localPosition = new Vector3(fishing.Layout.HoleFloor.X, -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit, 0f);
            m_water = NewRenderer(transform, "Water", null, Color.white, k_WaterOrder);
            m_shadowFish = NewRenderer(transform, "ShadowFish", m_fishSwim[m_fishSwim.Length - 1], Color.white, k_FishOrder);
            m_shadowFish.enabled = false;
            Repaint();

            m_shadow = NewRenderer(transform, "Shadow", m_fishSwim[0], Color.white, k_FishOrder);
            m_line = NewRenderer(transform, "Line", m_square, k_Line, k_LineOrder);
            m_bobberArt = NewRenderer(transform, "Bobber", m_bobber[0], Color.white, k_LineOrder + 1);
            m_gauge = NewRenderer(transform, "Gauge", m_reelGauge[0], Color.white, k_TopOrder);
            m_rods.ToList().ForEach(sprite => m_rodSprites[sprite.name] = sprite);
            m_wombat.Bind(fishing, transform, frames);
            m_rod = NewRenderer(m_wombat.Hand, "Rod", m_rodSprites["rod_bamboo_1"], Color.white, k_RodFrontOrder);
            // 든 빵 부모는 빵 크기(BakeryBaker k_CarryScale 0.6)라 대는 1배로 되돌린다(자리 · 대 끝 칸도 그 배율로 나눈다)
            m_handScale = m_wombat.Hand.localScale.x;
            m_rod.transform.localScale = Vector3.one / m_handScale;

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.CastThrown>(Bus_CastThrown),
                bus.Subscribe<Events.Nibbled>(Bus_Nibbled),
                bus.Subscribe<Events.Bitten>(Bus_Bitten),
                bus.Subscribe<Events.Hooked>(Bus_Hooked),
                bus.Subscribe<Events.RunChanged>(Bus_RunChanged),
                bus.Subscribe<Events.FishLanded>(Bus_FishLanded),
                bus.Subscribe<Events.FishingEnded>(Bus_FishingEnded),
                bus.Subscribe<Events.IslandReached>(Bus_IslandReached),
                bus.Subscribe<Events.ShadowShown>(Bus_ShadowShown),
                bus.Subscribe<Events.CrateDrifted>(Bus_CrateDrifted),
                bus.Subscribe<Events.CratePulled>(Bus_CratePulled),
                bus.Subscribe<Events.SeatPlaced>(Bus_SeatPlaced),
                bus.Subscribe<Events.FishingVisitorArrived>(Bus_VisitorArrived),
                bus.Subscribe<Events.FishingVisitorCaught>(Bus_VisitorCaught),
                bus.Subscribe<Events.FishingVisitorLeft>(Bus_VisitorLeft),
            };
        }

        public void SetEditing(bool editing)
        {
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

        private void Update()
        {
            if (m_fishing == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (m_waterFrames.Length > 0)
            {
                m_water.sprite = m_waterFrames[(int)(Time.time / k_WaterFrameSeconds) % m_waterFrames.Length];
            }
            m_nibbleLeft = Mathf.Max(0f, m_nibbleLeft - dt);
            m_recoilLeft = Mathf.Max(0f, m_recoilLeft - dt);
            FishingPhase phase = m_fishing.Phase;
            bool fishing = phase != FishingPhase.Idle;
            bool tug = phase == FishingPhase.Tug;
            // 던진 직후 찌가 나는 동안(flying): 대는 뒤로 당겼다 앞으로 휘두른다. 달릴 때 떨리고, 끊기면 뒤로 튕겼다 돌아온다
            bool flying = fishing && m_castLeft > 0f;
            m_castLeft = flying ? Mathf.Max(0f, m_castLeft - dt) : 0f;
            float k = flying ? 1f - m_castLeft / k_CastSeconds : 1f;
            float angle = flying ? Swing(k) : tug && m_fishing.Running ? Mathf.Sin(Time.time * k_ShakeHz * Mathf.PI * 2f) * k_ShakeDegrees : 0f;
            angle += k_RecoilDegrees * (m_recoilLeft / k_RecoilSeconds);
            // 대는 물가(설계 61 상자 포함)가 대상일 때부터 손에 보이고, 낚아 만세하는 동안만 내려놓는다(놓침 · 끊김의 잠깐 서기에는 들고 튕긴다)
            m_cheerLeft = Mathf.Max(0f, m_cheerLeft - dt);
            SyncRod(fishing || ((m_fishing.Target == m_fishing.Water || m_fishing.Target is CrateInteractable) && m_cheerLeft <= 0f), angle);
            SyncWorld();
            SyncShadowFish(dt);
            m_line.enabled = fishing;
            m_bobberArt.enabled = fishing;
            m_shadow.enabled = tug && m_fishing.Pulling == null;
            m_gauge.enabled = tug;

            if (!fishing)
            {
                return;
            }

            Vector3 tip = TipOf();
            Vector3 spot = BobberAt(phase);

            if (flying)
            {
                m_bobberAt = Vector3.Lerp(tip, spot, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * k_CastArc);

                if (m_castLeft <= 0f)
                {
                    Splash(spot, 1);
                    Ripple(spot, k_RippleLand);
                    m_rippleIn = k_RippleEvery;
                }
            }
            else
            {
                m_bobberAt = spot;
            }

            // 떠 있는 찌는 살랑이고 가끔 작은 물결 고리를 낸다
            Vector3 shown = m_bobberAt;

            if (!flying && !tug)
            {
                shown += Vector3.up * (Mathf.Sin(Time.time * k_BobSpeed) * k_BobAmount);
                m_rippleIn -= dt;

                if (m_rippleIn <= 0f)
                {
                    m_rippleIn = k_RippleEvery;
                    Ripple(m_bobberAt, k_RippleIdle);
                }
            }

            m_bobberArt.transform.position = shown;
            m_bobberArt.sprite = m_bobber[phase != FishingPhase.Waiting || m_nibbleLeft > 0f ? 1 : 0];
            Vector3 d = shown - tip;
            m_line.transform.position = tip + d / 2f;
            m_line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            m_line.transform.localScale = new Vector3(d.magnitude, k_LineWidth, 1f);
            float tension = tug ? (float)(m_fishing.Tension / m_fishing.Config.Rod.Line) : 0f;
            m_line.color = Color.Lerp(k_Line, k_LineTense, tension);

            if (tug)
            {
                m_shadow.transform.position = m_bobberAt + Vector3.down * k_ShadowDrop;
                m_shadow.flipX = Mathf.Sin(Time.time * k_RunWobbleSpeed) < 0f;
                int frame = Mathf.Clamp(Mathf.RoundToInt(tension * (m_reelGauge.Length - 1)), 0, m_reelGauge.Length - 1);
                m_gauge.sprite = m_reelGauge[frame];
                m_gauge.transform.position = m_wombat.transform.position + k_GaugeAt;
                // 긴장이 높으면 게이지가 두근거린다
                m_gauge.transform.localScale = Vector3.one * (tension >= k_GaugeAlarm ? 1f + 0.15f * Mathf.Abs(Mathf.Sin(Time.time * 20f)) : 1f);

                // 달리는 물고기 뒤로 거품
                if (m_fishing.Running)
                {
                    m_foamIn -= dt;

                    if (m_foamIn <= 0f)
                    {
                        m_foamIn = k_FoamEvery;
                        StartCoroutine(Fx.Burst(transform, m_square, m_bobberAt, 2, 0.5f, 0.6f, 3f, 0.35f, 2, k_WaterLight, k_FishOrder + 1));
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (m_subscriptions != null)
            {
                foreach (IDisposable subscription in m_subscriptions)
                {
                    subscription.Dispose();
                }
            }

            m_poopViews?.Dispose();
            m_clerkViews?.Dispose();
        }

        // 던지는 동안 대 각(뒤로 → 앞으로 → 제자리)
        private static float Swing(float k)
        {
            return k < 0.3f ? Mathf.Lerp(0f, k_SwingBack, k / 0.3f)
                : k < 0.6f ? Mathf.Lerp(k_SwingBack, k_SwingForward, (k - 0.3f) / 0.3f)
                : Mathf.Lerp(k_SwingForward, 0f, (k - 0.6f) / 0.4f);
        }

        // 찌 자리: 기다림 · 입질은 Core의 찌 자리, 줄다리기는 남은 거리 비율만큼 물가에서 먼 쪽(달릴 때는 옆으로 흔들린다).
        // 설계 61: 상자를 끄는 중이면 상자 위(걸릴 때 흔들린다)
        private Vector3 BobberAt(FishingPhase phase)
        {
            if (m_fishing.Pulling != null)
            {
                float shake = m_fishing.Running ? Mathf.Sin(Time.time * k_RunWobbleSpeed) * k_RunWobble : 0f;
                return ToWorld(m_fishing.Pulling.Position) + new Vector3(shake, k_CrateHookRise, 0f);
            }

            if (phase != FishingPhase.Tug)
            {
                return ToWorld(m_fishing.BobberPoint());
            }

            System.Numerics.Vector2 wombat = m_fishing.Wombat.Mover.Position;
            float ratio = m_fishing.DistanceStart > 0d ? (float)(m_fishing.Distance / m_fishing.DistanceStart) : 0f;
            float k = Mathf.Lerp(k_TugNear, k_TugFar, ratio);
            float wobble = m_fishing.Running ? Mathf.Sin(Time.time * k_RunWobbleSpeed) * k_RunWobble : 0f;
            System.Numerics.Vector2 deep = m_fishing.Layout.Deep(wombat, k, out System.Numerics.Vector2 inward);
            System.Numerics.Vector2 side = new System.Numerics.Vector2(-inward.Y, inward.X) * wobble;
            return ToWorld(deep + side);
        }

        // 손에 든 대(자리는 WombatView.Hand가 맞춘다): 달릴 때 휘고(rod_bent), 감을 때 당김 판. 왼쪽을 보면 뒤집고(각도도) 뒷모습이면 몸 뒤(든 빵과 같은 규칙)
        private void SyncRod(bool shown, float angle)
        {
            m_rod.enabled = shown;

            if (!shown)
            {
                return;
            }

            Facing facing = m_fishing.Wombat.Mover.Facing;
            bool tug = m_fishing.Phase == FishingPhase.Tug;
            bool flip = facing == Facing.Left;
            string name = tug && m_fishing.Running ? "rod_bent" : tug && m_fishing.Wombat.Holding ? "rod_bamboo_1_pull" : "rod_bamboo_1";
            m_rod.sprite = m_rodSprites[name];
            m_rod.flipX = flip;
            m_rod.transform.localPosition = new Vector3(flip || facing == Facing.Right ? 0f : k_RodGrip.x, k_RodGrip.y, 0f) / m_handScale;
            m_rod.transform.localRotation = Quaternion.Euler(0f, 0f, flip ? -angle : angle);
            m_rod.sortingOrder = Fx.CarryOrder(facing, k_RodFrontOrder, k_RodBackOrder);
        }

        // 줄이 나오는 대 끝(왼쪽을 보면 좌우가 뒤집히고, 대가 돌면 같이 돈다. 대의 월드 배율은 1)
        private Vector3 TipOf()
        {
            Vector2 cells = k_RodTips.TryGetValue(m_rod.sprite.name, out Vector2 tip) ? tip : new Vector2(0f, m_rod.sprite.rect.height / 4f);
            return m_rod.transform.TransformPoint(new Vector3((m_rod.flipX ? -cells.x : cells.x) * Fx.k_Cell, cells.y * Fx.k_Cell, 0f));
        }

        private Sprite SwimSprite(string fishId)
        {
            string name = m_tables.Get<FishTable>(fishId).Item + "_swim";
            return m_fishSwim.FirstOrDefault(sprite => sprite.name == name) ?? m_fishSwim[0];
        }

        // 설계 61 명물 그림자: Core 자리대로 물속을 오가고(가는 쪽을 본다) 가끔 물결
        private void SyncShadowFish(float dt)
        {
            bool shown = m_fishing.ShadowVisible;
            m_shadowFish.enabled = shown;

            if (!shown)
            {
                return;
            }

            Vector3 at = ToWorld(m_fishing.ShadowPosition);
            m_shadowFish.flipX = at.x < m_shadowLastX;
            m_shadowLastX = at.x;
            m_shadowFish.transform.position = at;
            m_shadowRippleIn -= dt;

            if (m_shadowRippleIn <= 0f)
            {
                m_shadowRippleIn = k_ShadowRippleEvery;
                Ripple(at, k_RippleShadow);
            }
        }

        // ---------- 사건 ----------

        private void Bus_CastThrown(Events.CastThrown e)
        {
            if (e.Fishing == m_fishing)
            {
                m_castLeft = k_CastSeconds;
            }
        }

        private void Bus_Nibbled(Events.Nibbled e)
        {
            if (e.Fishing == m_fishing)
            {
                m_nibbleLeft = k_NibbleSeconds;
                Ripple(m_bobberAt, k_RippleNibble);
            }
        }

        private void Bus_Bitten(Events.Bitten e)
        {
            if (e.Fishing == m_fishing)
            {
                Splash(m_bobberAt, 1);
                Ripple(m_bobberAt, k_RippleBite);
            }
        }

        private void Bus_Hooked(Events.Hooked e)
        {
            // 상자(Fish null)는 던지는 순간 걸려 찌가 아직 날지 않았다(물보라는 떨어질 때)
            if (e.Fishing == m_fishing && e.Fish != null)
            {
                Splash(m_bobberAt, 2);
                Ripple(m_bobberAt, k_RippleBite);
                m_shadow.sprite = SwimSprite(e.Fish.Kind.Id);
            }
        }

        private void Bus_RunChanged(Events.RunChanged e)
        {
            if (e.Fishing == m_fishing && e.Running)
            {
                Splash(m_bobberAt, 1);
                Ripple(m_bobberAt, k_RippleRun);
            }
        }

        // 낚았다: 큰 물보라, 웜뱃은 대를 내려놓고 만세(Core가 landSeconds 동안 세워 둔다), 물고기 그림이 돌며 든 두 앞발 사이로 튀어 올라 금빛 알갱이와 함께 그동안 머문다. 「+1」 팝업과 「붕어 1.2kg」 알약
        private void Bus_FishLanded(Events.FishLanded e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            Vector3 from = m_bobberAt;
            Splash(from, 3);
            ItemTable item = m_tables.Get<ItemTable>(e.Fish.Kind.Item);
            Sprite icon = m_frames.Get(item.Icon)[0];
            SpriteRenderer body = NewRenderer(transform, "Caught", icon, Color.white, k_TopOrder - 3);
            body.transform.position = from;
            Vector3 wombat = m_wombat.transform.position;
            Vector3 hold = wombat + Vector3.up * k_CheerHold;
            float seconds = (float)m_fishing.Config.LandSeconds;
            string tag = m_tables.Format("fishing_catch_tag", item.Name, e.Fish.Weight.ToString("0.#", CultureInfo.InvariantCulture));
            m_cheerLeft = seconds;
            m_wombat.Cheer(seconds);
            StartCoroutine(Fly(body.transform, hold, k_CatchArc, () =>
            {
                Destroy(body.gameObject, seconds - k_CatchSeconds);
                StartCoroutine(Fx.Burst(transform, m_square, hold, 8, 0.8f, 1.6f, 4f, 0.45f, 3, k_Sparkle, k_TopOrder));
                ShowPopup(wombat + Vector3.up * k_PopupHeight, m_tables.Format(k_PopupKey, 1), icon);
                StartCoroutine(Rise(tag, wombat + Vector3.up * (k_PopupHeight + 0.5f)));
            }));
        }

        // 끊기면 대 끝에서 줄 조각이 튀고 대가 뒤로 튕긴다. 끊김 · 놓침 글은 공용 곳 이름 띠가 아니라 웜뱃 머리 위 낚시 알약(사용자 2026-10-07), 거둔 것은 조용히
        private void Bus_FishingEnded(Events.FishingEnded e)
        {
            if (e.Fishing != m_fishing || e.Reason == FishingEnd.Retrieved)
            {
                return;
            }

            if (e.Reason == FishingEnd.Snapped)
            {
                StartCoroutine(Fx.Burst(transform, m_square, TipOf(), 8, 1.0f, 2f, 9f, 0.4f, 3, k_Line, k_TopOrder));
                Splash(m_bobberAt, 1);
                m_recoilLeft = k_RecoilSeconds;
            }

            string text = m_tables.Text(e.Reason == FishingEnd.Snapped ? "fishing_snapped" : "fishing_missed");
            StartCoroutine(Rise(text, m_wombat.transform.position + Vector3.up * (k_PopupHeight + 0.5f)));
        }

        // ---------- 설계 61 섬들 ----------

        // 굴 · 호수 · 놓인 다리 · 안 닿은 섬 덮개 · 소품을 지금 배치대로 다시 칠한다(옛 그림은 버린다).
        // 굴 · 물 픽셀(1,100 × 600칸, 230ms)은 다른 스레드에서 계산하고 텍스처만 끝난 프레임에 바꾼다(사용자 2026-10-14 「섬 열 때 멈칫」). 그동안은 옛 그림이 남는다
        private void Repaint()
        {
            FishingLayout layout = m_fishing.Layout;
            BurrowShape.Result shape = layout.Shape;
            float width = shape.Width / BurrowShape.k_PixelsPerUnit;
            float height = shape.Height / BurrowShape.k_PixelsPerUnit;
            m_bounds = new Rect(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit - height, width, height);
            BurrowPainter.Textures burrowTextures = new BurrowPainter.Textures(m_floorTile, m_wallTile, m_wallFace);
            WaterPainter.Textures waterTextures = new WaterPainter.Textures(m_waterTiles, m_streamFace);
            NavRect[] lands = layout.Lands.ToArray();
            int version = ++m_repaintVersion;
            StartCoroutine(PaintLater(version, shape, Task.Run(() => (BurrowPainter.Pixels(shape, burrowTextures), WaterPainter.Pixels(shape, lands, waterTextures)))));

            // 다리는 네모 크기로 깐다(그림이 오기 전엔 둑 띠). 섬 아랫변의 자갈 둑 띠는 쓰지 않는다(둥근 모서리 + 둑 고리, 사용자 2026-10-14)
            foreach (SpriteRenderer art in m_bridgeArts)
            {
                Destroy(art.gameObject);
            }

            m_bridgeArts.Clear();

            foreach (NavRect bridge in layout.Bridges)
            {
                m_bridgeArts.Add(NewBridgeArt(bridge, 1f));
            }

            // 안 닿은 섬은 어둡게 덮고, 보이는 섬의 소품은 한 번 그린다
            foreach (FishingIslandTable island in layout.Islands.Where(island => m_fishing.Visible.Contains(island.Index)))
            {
                bool reached = m_fishing.Reached.Contains(island.Index);

                if (!reached && !m_fogs.ContainsKey(island.Index))
                {
                    NavRect land = island.Land.ToRect();
                    SpriteRenderer fog = NewRenderer(transform, "Fog", m_square, k_Fog, k_FishOrder + 4);
                    fog.transform.localPosition = new Vector3((land.XMin + land.XMax) / 2f, (land.YMin + Mathf.Min(land.YMax, -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit)) / 2f, 0f);
                    fog.transform.localScale = new Vector3(land.XMax - land.XMin, Mathf.Min(land.YMax, -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit) - land.YMin, 1f);
                    m_fogs[island.Index] = fog;
                }
                else if (reached && m_fogs.TryGetValue(island.Index, out SpriteRenderer fog))
                {
                    Destroy(fog.gameObject);
                    m_fogs.Remove(island.Index);
                }

                if (m_decorated.Add(island.Index))
                {
                    foreach (FishingDecorData decor in island.Decor)
                    {
                        Sprite prop = m_props.FirstOrDefault(sprite => sprite.name == decor.Sprite);

                        if (prop != null)
                        {
                            SpriteRenderer art = NewRenderer(transform, decor.Sprite, prop, Color.white, 0);
                            art.spriteSortPoint = SpriteSortPoint.Pivot;
                            art.transform.localPosition = new Vector3((float)decor.X, (float)decor.Y, 0f);
                        }
                    }
                }
            }
        }

        // 픽셀 계산이 끝나면 굴 · 물 텍스처를 만들어 바꾼다. 그 사이 다시 그리기가 또 시작됐으면 이 결과는 버린다
        private IEnumerator PaintLater(int version, BurrowShape.Result shape, Task<(Color32[] burrow, Color32[][] water)> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception;
            }

            if (version != m_repaintVersion || this == null)
            {
                yield break;
            }

            Sprite oldBurrow = m_burrow.sprite;
            m_burrow.sprite = BurrowPainter.ToSprite(task.Result.burrow, shape.Width, shape.Height);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);
            DestroySprite(oldBurrow);

            // ponytail: 물결 칸마다 굴 전체를 한 장씩. 섬이 더 많아지면 물 칸만 따로 그리는 층으로
            foreach (Sprite frame in m_waterFrames)
            {
                DestroySprite(frame);
            }

            m_water.transform.localPosition = m_burrow.transform.localPosition;
            m_waterFrames = System.Array.ConvertAll(task.Result.water, pixels => BurrowPainter.ToSprite(pixels, shape.Width, shape.Height));
            m_water.sprite = m_waterFrames[0];
        }

        private SpriteRenderer NewBridgeArt(NavRect rect, float alpha)
        {
            // 가로 다리는 전용 그림(bridge_side)을 그대로, 없으면 세로 그림을 90도 돌려 깐다(사용자 2026-10-14 「가로 다리를 세로 다리와 맞추자」)
            bool sideways = rect.XMax - rect.XMin > rect.YMax - rect.YMin;
            bool rotate = sideways && m_bridgeSide == null;
            Sprite sprite = sideways && m_bridgeSide != null ? m_bridgeSide : m_bridge != null ? m_bridge : m_bank;
            SpriteRenderer art = NewRenderer(transform, "Bridge", sprite, new Color(1f, 1f, 1f, alpha), k_WaterOrder + 2);

            if (sprite != null)
            {
                art.drawMode = SpriteDrawMode.Tiled;
                art.size = rotate ? new Vector2(rect.YMax - rect.YMin, rect.XMax - rect.XMin) : new Vector2(rect.XMax - rect.XMin, rect.YMax - rect.YMin);
                art.transform.localRotation = Quaternion.Euler(0f, 0f, rotate ? 90f : 0f);
            }
            else
            {
                art.sprite = m_square;
                art.color = new Color(k_Dirt.r, k_Dirt.g, k_Dirt.b, alpha);
                art.transform.localScale = new Vector3(rect.XMax - rect.XMin, rect.YMax - rect.YMin, 1f);
            }

            // 둑 띠 피벗은 아래 가운데라 네모 밑변에, 다리 그림은 가운데 피벗
            float y = sprite == m_bank && sprite != null ? rect.YMin : (rect.YMin + rect.YMax) / 2f;
            art.transform.localPosition = new Vector3((rect.XMin + rect.XMax) / 2f, y, 0f);
            return art;
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite != null)
            {
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }

        // 다리 자리 · 상자 · 좌대 그림을 Core 사물에 맞춘다(새로 생긴 것은 만들고 사라진 것은 지운다). 좌대 손님은 사건으로 만들고 여기서 대 · 줄 · 찌를 그린다
        private void SyncWorld()
        {
            Interactable target = m_fishing.Target;

            foreach (BridgeInteractable bridge in m_fishing.Things.OfType<BridgeInteractable>())
            {
                if (!m_bridgeSpots.ContainsKey(bridge))
                {
                    m_bridgeSpots[bridge] = NewBridgeArt(bridge.Rect, k_GhostAlpha);
                }
            }

            foreach (BridgeInteractable gone in m_bridgeSpots.Keys.Where(bridge => !m_fishing.Things.Contains(bridge)).ToList())
            {
                Destroy(m_bridgeSpots[gone].gameObject);
                m_bridgeSpots.Remove(gone);
            }

            CrateInteractable crate = m_fishing.Crate;

            if (crate != m_crateShown)
            {
                if (m_crateArt != null)
                {
                    Destroy(m_crateArt.gameObject);
                    m_crateArt = null;
                }

                if (crate != null)
                {
                    Sprite sprite = CrateSprite();
                    m_crateArt = NewRenderer(transform, "Crate", sprite, Color.white, k_CrateOrder);
                    m_crateArt.spriteSortPoint = SpriteSortPoint.Pivot;
                    m_crateArt.transform.localScale = Vector3.one * (sprite == m_crate || m_debris != null && m_debris.Length > 0 ? 1f : k_CrateFallbackScale);
                }

                m_crateShown = crate;
            }

            if (m_crateArt != null)
            {
                m_crateArt.transform.localPosition = new Vector3(crate.Position.X, crate.Position.Y + Mathf.Sin(Time.time * k_BobSpeed) * k_BobAmount, 0f);
            }

            foreach (SeatInteractable seat in m_fishing.Seats)
            {
                if (!m_seatArts.TryGetValue(seat, out SpriteRenderer art))
                {
                    art = NewRenderer(transform, "Seat", m_seat != null ? m_seat : Prop("bank_bucket"), Color.white, 0);
                    art.spriteSortPoint = SpriteSortPoint.Pivot;
                    art.transform.localPosition = new Vector3(seat.Position.X, seat.Position.Y, 0f);
                    m_seatArts[seat] = art;
                }

                art.color = new Color(1f, 1f, 1f, seat.Placed ? 1f : k_GhostAlpha);
                // 손님이 올라앉은 좌대는 손님 뒤에(둘 다 발끝 정렬이라 같은 자리면 좌대가 손님을 덮는다, 사용자 2026-10-07)
                art.sortingOrder = m_fishing.Visitors.Any(visitor => visitor.Seat == seat && visitor.OnSeat) ? -1 : 0;
            }

            // 놓지 않은 좌대 · 다리 자리가 대상이면 그 자리에 값 표식
            if (target is SeatInteractable aimedSeat && !aimedSeat.Placed)
            {
                ShowTag(ToWorld(aimedSeat.Position) + Vector3.up * k_SeatTagRise, m_tables.Format("tag_seat", aimedSeat.Row.Cost.ToString("0", CultureInfo.InvariantCulture)));
            }
            else if (target is BridgeInteractable aimedBridge)
            {
                ShowTag(ToWorld(aimedBridge.Position) + Vector3.up * k_BridgeTagRise, m_tables.Format("tag_bridge", aimedBridge.Island.Bridge.Cost.ToString("0", CultureInfo.InvariantCulture)));
            }
            else if (m_tag != null)
            {
                m_tag.gameObject.SetActive(false);
            }

            foreach (KeyValuePair<FishingVisitor, CustomerArt> pair in m_customers)
            {
                DrawCustomer(pair.Key, pair.Value);
            }
        }

        private void ShowTag(Vector3 at, string text)
        {
            m_tag = m_tag != null ? m_tag : NewTag(Vector3.zero);
            m_tag.gameObject.SetActive(true);
            m_tag.transform.position = at;
            m_tag.Show(text);
        }

        // 앉아 낚는 손님만 대 · 줄 · 찌(찌는 좌대 앞 물에서 살랑)
        private void DrawCustomer(FishingVisitor visitor, CustomerArt art)
        {
            bool seated = visitor.Seated && visitor.Seat != null;
            art.Rod.enabled = seated;
            art.Line.enabled = seated;
            art.Bobber.enabled = seated;

            if (!seated)
            {
                return;
            }

            Vector2 cells = k_RodTips["rod_bamboo_1"];
            Vector3 tip = art.Rod.transform.TransformPoint(new Vector3(cells.x * Fx.k_Cell, cells.y * Fx.k_Cell, 0f));
            Vector3 bobber = ToWorld(new System.Numerics.Vector2(visitor.Seat.Position.X + 0.4f, visitor.Seat.BankY - k_CustomerBobberDrop))
                + Vector3.up * (Mathf.Sin(Time.time * k_BobSpeed + visitor.Id) * k_BobAmount);
            art.Bobber.transform.position = bobber;
            Vector3 d = bobber - tip;
            art.Line.transform.position = tip + d / 2f;
            art.Line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            art.Line.transform.localScale = new Vector3(d.magnitude, k_LineWidth, 1f);
        }

        private Sprite Prop(string name)
        {
            return m_props.FirstOrDefault(sprite => sprite.name == name) ?? m_props[0];
        }

        // 상자 그림: 아트방 crate → 잔해 [0] → 둑 소품 순으로 빌린다
        private Sprite CrateSprite()
        {
            return m_crate != null ? m_crate : m_debris != null && m_debris.Length > 0 ? m_debris[0] : Prop("bank_driftwood");
        }

        // 다리를 놓아 섬에 닿았다: 웜뱃은 두드리기(퍼 던지기), 다리 자리에 흙덩이가 튀고 굴 · 물 · 다리 · 덮개를 다시 칠한다. 섬 이름 알약
        private void Bus_IslandReached(Events.IslandReached e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            NavRect bridge = m_fishing.Layout.BridgeOf(e.Island);
            Vector3 at = ToWorld(new System.Numerics.Vector2((bridge.XMin + bridge.XMax) / 2f, (bridge.YMin + bridge.YMax) / 2f));
            StartCoroutine(ClodsLater(m_wombat.Dig(), at));
            Repaint();
            Expanded?.Invoke();
            FishingIslandTable island = m_fishing.Layout.Island(e.Island);
            NavRect land = island.Land.ToRect();
            Vector3 center = ToWorld(new System.Numerics.Vector2((land.XMin + land.XMax) / 2f, land.YMax + 0.5f));
            StartCoroutine(Rise(m_tables.Format("fishing_reached", m_tables.Text("island_" + island.Id)), center));
        }

        private IEnumerator ClodsLater(float seconds, Vector3 at)
        {
            yield return new WaitForSeconds(seconds);
            StartCoroutine(Fx.Burst(transform, m_square, at, 12, 2.4f, 4f, 12f, 0.6f, 12, k_Dirt, k_TopOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, 12, 2.4f, 4f, 12f, 0.6f, 12, k_DirtDark, k_TopOrder));
        }

        // 명물 그림자가 나왔다: 큰 물결 고리
        private void Bus_ShadowShown(Events.ShadowShown e)
        {
            if (e.Fishing == m_fishing)
            {
                m_shadowFish.sprite = SwimSprite(e.Island.Boss);
                m_shadowLastX = ToWorld(m_fishing.ShadowPosition).x;
                Ripple(ToWorld(m_fishing.ShadowPosition), k_RippleBite);
            }
        }

        // 상자가 떠내려왔다: 작은 물보라
        private void Bus_CrateDrifted(Events.CrateDrifted e)
        {
            if (e.Fishing == m_fishing)
            {
                Splash(ToWorld(e.Crate.Position), 1);
            }
        }

        // 상자를 끌어냈다: 물보라 · 상자 그림이 웜뱃 옆 둑으로 튀어 잠깐 놓였다 사라지고 코인 팝업
        private void Bus_CratePulled(Events.CratePulled e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            Vector3 from = m_bobberAt;
            Splash(from, 2);
            SpriteRenderer body = NewRenderer(transform, "Crate", CrateSprite(), Color.white, k_TopOrder - 3);
            body.transform.position = from;
            Vector3 wombat = m_wombat.transform.position;
            Vector3 to = wombat + new Vector3(e.Crate.Position.X > m_wombat.transform.localPosition.x ? 0.6f : -0.6f, 0.1f, 0f);
            StartCoroutine(Fly(body.transform, to, k_CatchArc, () =>
            {
                Destroy(body.gameObject, 0.8f);
                ShowPopup(wombat + Vector3.up * k_PopupHeight, m_tables.Format("coin_popup", e.Coins.ToString("0", CultureInfo.InvariantCulture)), null);
            }));
        }

        private void Bus_SeatPlaced(Events.SeatPlaced e)
        {
            if (e.Fishing == m_fishing && m_seatArts.TryGetValue(e.Seat, out SpriteRenderer art))
            {
                art.color = Color.white;
                StartCoroutine(Fx.Bounce(art.transform));
            }
        }

        // 좌대 손님: 외형은 광장에서 정해져 온다. 손에 대(웜뱃 대와 같은 그림), 앉으면 줄 · 찌
        private void Bus_VisitorArrived(Events.FishingVisitorArrived e)
        {
            if (e.Visitor.Fishing != m_fishing)
            {
                return;
            }

            VisitorView unit = Instantiate(m_clerkPrefab, transform);
            unit.Initialize(e.Visitor, m_frames, transform);
            CustomerArt art = new CustomerArt
            {
                Unit = unit,
                Rod = NewRenderer(unit.transform, "Rod", m_rodSprites["rod_bamboo_1"], Color.white, k_RodFrontOrder),
                Line = NewRenderer(transform, "CustomerLine", m_square, k_Line, k_LineOrder),
                Bobber = NewRenderer(transform, "CustomerBobber", m_bobber[0], Color.white, k_LineOrder + 1),
            };
            art.Rod.transform.localPosition = k_CustomerHand;
            m_customers[e.Visitor] = art;
            DrawCustomer(e.Visitor, art);
        }

        // 손님이 한 마리: 찌에서 작은 물보라 · 물고기가 손님 머리 위로 튀고 코인
        private void Bus_VisitorCaught(Events.FishingVisitorCaught e)
        {
            if (!m_customers.TryGetValue(e.Visitor, out CustomerArt art))
            {
                return;
            }

            Vector3 from = art.Bobber.transform.position;
            Splash(from, 1);
            Sprite icon = m_frames.Get(m_tables.Get<ItemTable>(e.Kind.Item).Icon)[0];
            SpriteRenderer body = NewRenderer(transform, "CustomerFish", icon, Color.white, k_TopOrder - 3);
            body.transform.position = from;
            Vector3 head = art.Unit.transform.position + Vector3.up * 1.0f;
            StartCoroutine(Fly(body.transform, head, k_CatchArc, () =>
            {
                Destroy(body.gameObject, 0.4f);
                art.Unit.Pay(m_tables.Format("coin_popup", e.Coins.ToString("0", CultureInfo.InvariantCulture)));
            }));
        }

        private void Bus_VisitorLeft(Events.FishingVisitorLeft e)
        {
            if (m_customers.TryGetValue(e.Visitor, out CustomerArt art))
            {
                Destroy(art.Unit.gameObject);
                Destroy(art.Line.gameObject);
                Destroy(art.Bobber.gameObject);
                m_customers.Remove(e.Visitor);
            }
        }

        // ---------- 연출 조각 ----------

        // 포물선으로 날며 한 바퀴 반 돈다
        private IEnumerator Fly(Transform target, Vector3 to, float arc, Action landed)
        {
            Vector3 from = target.position;

            for (float t = 0f; t < k_CatchSeconds; t += Time.deltaTime)
            {
                float k = t / k_CatchSeconds;
                target.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * arc);
                target.rotation = Quaternion.Euler(0f, 0f, k_CatchSpin * k);
                yield return null;
            }

            target.position = to;
            target.rotation = Quaternion.identity;
            landed();
        }

        // 잠깐 떠올라 사라지는 글 알약
        private IEnumerator Rise(string text, Vector3 at)
        {
            MarkerView tag = NewTag(at);
            tag.Show(text);
            tag.Bounce();

            for (float t = 0f; t < k_RiseSeconds; t += Time.deltaTime)
            {
                tag.transform.position = at + Vector3.up * (t / k_RiseSeconds * 0.5f);
                yield return null;
            }

            Destroy(tag.gameObject);
        }

        private void ShowPopup(Vector3 at, string amount, Sprite icon)
        {
            Instantiate(m_popupPrefab, at, Quaternion.identity, transform).Show(amount, icon);
        }

        private void Splash(Vector3 at, int strength)
        {
            StartCoroutine(Fx.Burst(transform, m_square, at, 6 * strength, 1.2f * strength, 2.4f, 9f, 0.45f, 4, k_WaterLight, k_TopOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, 6 * strength, 1.2f * strength, 2.4f, 9f, 0.45f, 4, k_Water, k_TopOrder));
        }

        private void Ripple(Vector3 at, float radius)
        {
            StartCoroutine(Fx.Ripple(transform, m_square, at, radius, k_RippleSeconds, 10, k_Ripple, k_FishOrder + 1));
        }

        private Vector3 ToWorld(System.Numerics.Vector2 p)
        {
            return transform.position + new Vector3(p.X, p.Y, 0f);
        }

        // 값 표식 알약(빵집 파기 표식과 같은 1배, UI 규칙 8 정수 배)
        private MarkerView NewTag(Vector3 at)
        {
            MarkerView tag = Instantiate(m_tagPrefab, at, Quaternion.identity, transform);

            foreach (SpriteRenderer renderer in tag.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sortingOrder = k_TopOrder;
            }

            foreach (TMPro.TextMeshPro label in tag.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            {
                label.sortingOrder = k_TopOrder + 1;
            }

            return tag;
        }

        private static SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
