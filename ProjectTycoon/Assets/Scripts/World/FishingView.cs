using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 52 강가 낚시 화면: 굴 그림(고정 방) · 강(물결 타일을 사각형으로 코드 그림, 둑 띠는 벽 안쪽 바닥 폭만큼 Tiled) · 손에 든 대 · 낚싯줄 · 찌 · 물속 그림자 · 긴장 게이지를
    // 매 프레임 Core(FishingArea)대로 그린다. 사건: 던짐(대를 휘두르고 찌가 대 끝에서 포물선으로 난다) · 가짜 입질(찌 까딱 · 고리) · 물었다 · 걸렸다 · 달리기(대 떨림 · 거품) · 낚음(물고기가 돌며 손으로 튀고 웜뱃이 만세, 이름 · 무게 알약) · 끊김(줄이 튀고 대가 튕긴다)
    // 생동감(2026-10-07 사용자 「밋밋하다」): 새 그림 없이 네모 알갱이(물결 고리 · 거품 · 반짝) · 대 회전 · 줄 색 · 머리 위 말풍선(Core)으로.
    // 설계 53 넓히기: 구간이 열리거나 물이 차면 굴 · 물 · 둑을 다시 칠하고(Expanded로 카메라 경계도), 끝 바위(값 표식) · 물속 댐(남은 조각만큼) · 좌대 · 좌대 손님(대 · 줄 · 찌)을 매 프레임 Core대로 맞춘다
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
        // 찌: 기다릴 때는 물가에서 가운데 쪽으로 반지름의 이만큼 · 줄다리기는 남은 거리 비율로 먼 쪽(k_TugFar)에서 물가(k_TugNear)로 · 달릴 때 옆으로 흔들리는 폭(유닛) · 빠르기 · 가짜 입질 까딱 초
        private const float k_BobberIn = 0.45f;
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
        // 떠 있는 찌는 살랑인다(유닛 · 빠르기) · 물결 고리 간격(초) · 고리 반지름(기다림 · 입질 · 물었다 · 떨어짐 · 달림) · 고리 색
        private const float k_BobAmount = 0.02f;
        private const float k_BobSpeed = 2.5f;
        private const float k_RippleEvery = 1.6f;
        private const float k_RippleIdle = 0.3f;
        private const float k_RippleNibble = 0.45f;
        private const float k_RippleBite = 0.8f;
        private const float k_RippleLand = 0.6f;
        private const float k_RippleRun = 0.35f;
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
        // 설계 53: 바위 값 표식 높이 · 좌대 값 표식 높이 · 댐 그림 층(물 위, 줄 아래) · 잔해에 건 찌가 댐 발끝에서 뜬 높이 · 물이 차오르는 초
        private const float k_RockTagRise = 2.0f;
        private const float k_SeatTagRise = 0.9f;
        private const int k_DamOrder = k_FishOrder + 3;
        private const float k_DamHookRise = 0.7f;
        private const float k_FloodSeconds = 0.9f;
        // 그림이 오기 전 빌려 쓰는 둑 소품과 그 배율 · 놓지 않은 좌대 흐림 · 손님 찌가 좌대 앞 물에 뜬 깊이 · 손님 손 자리
        private const float k_RockFallbackScale = 1.6f;
        private const float k_DamFallbackScale = 1.4f;
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
        [Tooltip("둑 띠(아트방 bank, 피벗 아래 가운데, 둑 선을 따라 벽 안쪽 바닥 폭만큼 Tiled). 없으면 코드가 둑을 그린다")]
        [SerializeField] private Sprite m_bank;
        [Tooltip("둑 · 물 소품(아트방 bank_<이름> · water_<이름>, 발끝 피벗). 자리는 FishingConfigTable decor")]
        [SerializeField] private Sprite[] m_props;
        [Tooltip("설계 53: 마른 강바닥 타일(아트방 dry_bed, 물 타일처럼 픽셀을 읽어 반복). 없으면 흙 면을 어둡게")]
        [SerializeField] private Texture2D m_dryBed;
        [Tooltip("설계 53: 끝 바위벽(rock_wall, 발끝 피벗, 오른쪽 경계 기준 · 왼쪽은 뒤집기) · 댐 단계(dam_1~3, [0] = 조각 하나) · 좌대(seat) · 끌려 나오는 잔해(debris_*). 없으면 둑 소품을 빌린다")]
        [SerializeField] private Sprite m_rockWall;
        [SerializeField] private Sprite[] m_dams;
        [SerializeField] private Sprite m_seat;
        [SerializeField] private Sprite[] m_debris;
        [Tooltip("물속 물고기(<재료 id>_swim, 오른쪽을 봄, 가운데 피벗)")]
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
        // 설계 53: 다시 칠하는 둑 띠 · 끝 바위(그림 + 값 표식) · 댐 · 좌대 · 좌대 손님 그림
        private SpriteRenderer m_bankArt;
        private readonly Dictionary<RockInteractable, (SpriteRenderer art, MarkerView tag)> m_rocks = new Dictionary<RockInteractable, (SpriteRenderer, MarkerView)>();
        private readonly Dictionary<DamInteractable, SpriteRenderer> m_damArts = new Dictionary<DamInteractable, SpriteRenderer>();
        private readonly Dictionary<SeatInteractable, SpriteRenderer> m_seatArts = new Dictionary<SeatInteractable, SpriteRenderer>();
        private readonly Dictionary<FishingVisitor, CustomerArt> m_customers = new Dictionary<FishingVisitor, CustomerArt>();
        private MarkerView m_seatTag;

        // 좌대 손님 하나의 그림: 손님(빵집 손님과 같은 VisitorView) · 손에 든 대 · 줄 · 찌
        private sealed class CustomerArt
        {
            public VisitorView Unit;
            public SpriteRenderer Rod;
            public SpriteRenderer Line;
            public SpriteRenderer Bobber;
        }

        // 설계 53: 구간이 열리거나 물이 차 굴이 넓어졌다(카메라 경계를 다시 잰다)
        public event Action Expanded;

        public Vector3 Origin => transform.position;
        public Transform Wombat => m_wombat.transform;
        // 던진 동안 카메라가 비추는 점: 웜뱃과 찌의 가운데(깊은 물의 찌 · 물고기가 화면 아래 조작 밑에 숨지 않게, 사용자 2026-10-07)
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

            if (m_bank != null)
            {
                m_bankArt = NewRenderer(transform, "Bank", m_bank, Color.white, k_WaterOrder + 1);
                m_bankArt.drawMode = SpriteDrawMode.Tiled;
            }

            Repaint();

            foreach (FishingDecorData decor in fishing.Config.Decor)
            {
                Sprite prop = m_props.FirstOrDefault(sprite => sprite.name == decor.Sprite);

                if (prop != null)
                {
                    SpriteRenderer art = NewRenderer(transform, decor.Sprite, prop, Color.white, 0);
                    art.spriteSortPoint = SpriteSortPoint.Pivot;
                    art.transform.localPosition = new Vector3((float)decor.X, (float)decor.Y, 0f);
                }
            }

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
                bus.Subscribe<Events.StretchOpened>(Bus_StretchOpened),
                bus.Subscribe<Events.DebrisPulled>(Bus_DebrisPulled),
                bus.Subscribe<Events.DamBroken>(Bus_DamBroken),
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
            m_water.sprite = m_waterFrames[(int)(Time.time / k_WaterFrameSeconds) % m_waterFrames.Length];
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
            // 대는 물가(설계 53 댐 포함)가 대상일 때부터 손에 보이고, 낚아 만세하는 동안만 내려놓는다(놓침 · 끊김의 잠깐 서기에는 들고 튕긴다)
            m_cheerLeft = Mathf.Max(0f, m_cheerLeft - dt);
            SyncRod(fishing || ((m_fishing.Target == m_fishing.Water || m_fishing.Target is DamInteractable) && m_cheerLeft <= 0f), angle);
            SyncWorld();
            m_line.enabled = fishing;
            m_bobberArt.enabled = fishing;
            m_shadow.enabled = tug && m_fishing.Debris == null;
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

        // 던지기 대 각: 뒤로 당겼다가(0~0.3) 앞으로 휘둘러(0.3~0.6) 제자리로(0.6~1)
        private static float Swing(float k)
        {
            return k < 0.3f ? Mathf.Lerp(0f, k_SwingBack, k / 0.3f)
                : k < 0.6f ? Mathf.Lerp(k_SwingBack, k_SwingForward, (k - 0.3f) / 0.3f)
                : Mathf.Lerp(k_SwingForward, 0f, (k - 0.6f) / 0.4f);
        }

        // 둑 줄(물가 바로 위 줄)에서 걷는 바닥의 왼쪽 · 오른쪽 끝(유닛, 낚시터 원점 기준). 둑 띠를 벽 안쪽에만 깐다(사용자 2026-10-07 「벽 쪽 경계가 깨진다」)
        private static (float left, float right) FloorSpan(BurrowShape.Result shape, float y)
        {
            int row = (int)Math.Round(-y * BurrowShape.k_PixelsPerUnit) - shape.OriginY - 1;
            int min = shape.Width;
            int max = -1;

            for (int x = 0; x < shape.Width; x++)
            {
                if (shape.IsFloor(x, row))
                {
                    min = Math.Min(min, x);
                    max = Math.Max(max, x);
                }
            }

            return ((shape.OriginX + min) / BurrowShape.k_PixelsPerUnit, (shape.OriginX + max + 1) / BurrowShape.k_PixelsPerUnit);
        }

        // 찌 자리: 웜뱃 쪽 물가에서 물 안쪽으로. 기다림 · 입질은 깊이의 k_BobberIn, 줄다리기는 남은 거리 비율만큼 먼 쪽(달릴 때는 옆으로 흔들린다).
        // 설계 53: 잔해를 끄는 중이면 댐 위(걸릴 때 흔들린다)
        private Vector3 BobberAt(FishingPhase phase)
        {
            if (m_fishing.Debris != null)
            {
                float shake = m_fishing.Running ? Mathf.Sin(Time.time * k_RunWobbleSpeed) * k_RunWobble : 0f;
                return ToWorld(m_fishing.Debris.Position) + new Vector3(shake, k_DamHookRise, 0f);
            }

            System.Numerics.Vector2 wombat = m_fishing.Wombat.Mover.Position;
            float k = k_BobberIn;
            float wobble = 0f;

            if (phase == FishingPhase.Tug)
            {
                float ratio = m_fishing.DistanceStart > 0d ? (float)(m_fishing.Distance / m_fishing.DistanceStart) : 0f;
                k = Mathf.Lerp(k_TugNear, k_TugFar, ratio);
                wobble = m_fishing.Running ? Mathf.Sin(Time.time * k_RunWobbleSpeed) * k_RunWobble : 0f;
            }

            System.Numerics.Vector2 inward = m_fishing.Layout.Inward(m_fishing.Layout.Shore(wombat));
            System.Numerics.Vector2 side = new System.Numerics.Vector2(-inward.Y, inward.X) * wobble;
            return ToWorld(m_fishing.Layout.Deep(wombat, k) + side);
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
            // 잔해(Fish null)는 던지는 순간 걸려 찌가 아직 날지 않았다(물보라는 떨어질 때)
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

        // ---------- 설계 53 넓히기 ----------

        // 굴 · 물 · 마른 강바닥 · 둑 띠를 지금 배치대로 다시 칠한다(옛 그림은 버린다)
        private void Repaint()
        {
            BurrowShape.Result shape = m_fishing.Layout.Shape;
            Sprite oldBurrow = m_burrow.sprite;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile, m_wallFace);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);
            float width = shape.Width / BurrowShape.k_PixelsPerUnit;
            float height = shape.Height / BurrowShape.k_PixelsPerUnit;
            m_bounds = new Rect(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit - height, width, height);
            DestroySprite(oldBurrow);

            // ponytail: 물결 칸마다 굴 전체를 한 장씩. 구간이 더 많아지면 물 칸만 따로 그리는 층으로
            foreach (Sprite frame in m_waterFrames)
            {
                DestroySprite(frame);
            }

            m_water.transform.localPosition = m_burrow.transform.localPosition;
            m_waterFrames = m_waterTiles.Select(tile => WaterPainter.Paint(shape, m_fishing.Layout, tile, m_streamFace, m_dryBed, m_bank == null)).ToArray();
            m_water.sprite = m_waterFrames[0];

            if (m_bankArt != null)
            {
                float bank = m_fishing.Layout.BankY;
                (float left, float right) = FloorSpan(shape, bank);
                m_bankArt.size = new Vector2(right - left, m_bank.bounds.size.y);
                m_bankArt.transform.localPosition = new Vector3((left + right) / 2f, bank, 0f);
            }
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite != null)
            {
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }

        // 끝 바위 · 댐 · 좌대 그림을 Core 사물에 맞춘다(새로 생긴 것은 만들고 사라진 것은 지운다). 좌대 손님은 사건으로 만들고 여기서 대 · 줄 · 찌를 그린다
        private void SyncWorld()
        {
            Interactable target = m_fishing.Target;

            foreach (RockInteractable rock in m_fishing.Things.OfType<RockInteractable>())
            {
                if (!m_rocks.ContainsKey(rock))
                {
                    SpriteRenderer art = NewRenderer(transform, "Rock", m_rockWall != null ? m_rockWall : Prop("bank_rock"), Color.white, 0);
                    art.spriteSortPoint = SpriteSortPoint.Pivot;
                    art.flipX = rock.Side < 0;
                    art.transform.localPosition = new Vector3(rock.Position.X, rock.Position.Y, 0f);
                    art.transform.localScale = Vector3.one * (m_rockWall != null ? 1f : k_RockFallbackScale);
                    MarkerView tag = NewTag(ToWorld(rock.Position) + Vector3.up * k_RockTagRise);
                    tag.Show(m_tables.Format("tag_rock", rock.Stretch.RockCost.ToString("0", CultureInfo.InvariantCulture)));
                    m_rocks[rock] = (art, tag);
                }
            }

            foreach (RockInteractable gone in m_rocks.Keys.Where(rock => !m_fishing.Things.Contains(rock)).ToList())
            {
                Destroy(m_rocks[gone].art.gameObject);
                Destroy(m_rocks[gone].tag.gameObject);
                m_rocks.Remove(gone);
            }

            foreach (DamInteractable dam in m_fishing.Things.OfType<DamInteractable>())
            {
                if (!m_damArts.TryGetValue(dam, out SpriteRenderer art))
                {
                    art = NewRenderer(transform, "Dam", null, Color.white, k_DamOrder);
                    art.transform.localPosition = new Vector3(dam.Position.X, dam.Position.Y, 0f);
                    m_damArts[dam] = art;
                }

                bool drawn = m_dams != null && m_dams.Length > 0;
                art.sprite = drawn ? m_dams[Mathf.Clamp(dam.PiecesLeft, 1, m_dams.Length) - 1] : Prop("bank_driftwood");
                art.transform.localScale = Vector3.one * (drawn ? 1f : k_DamFallbackScale);
            }

            foreach (DamInteractable gone in m_damArts.Keys.Where(dam => !m_fishing.Things.Contains(dam)).ToList())
            {
                Destroy(m_damArts[gone].gameObject);
                m_damArts.Remove(gone);
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
            }

            // 놓지 않은 좌대가 대상이면 그 자리에 값 표식
            if (target is SeatInteractable aimed && !aimed.Placed)
            {
                m_seatTag = m_seatTag != null ? m_seatTag : NewTag(Vector3.zero);
                m_seatTag.gameObject.SetActive(true);
                m_seatTag.transform.position = ToWorld(aimed.Position) + Vector3.up * k_SeatTagRise;
                m_seatTag.Show(m_tables.Format("tag_seat", aimed.Row.Cost.ToString("0", CultureInfo.InvariantCulture)));
            }
            else if (m_seatTag != null)
            {
                m_seatTag.gameObject.SetActive(false);
            }

            foreach (KeyValuePair<FishingVisitor, CustomerArt> pair in m_customers)
            {
                DrawCustomer(pair.Key, pair.Value);
            }
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
            Vector3 bobber = ToWorld(new System.Numerics.Vector2(visitor.Seat.Position.X + 0.4f, m_fishing.Layout.BankY - k_CustomerBobberDrop))
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

        // 바위를 뚫었다: 웜뱃은 퍼 던지기, 흙덩이가 튀고 굴 · 물을 다시 칠한다
        private void Bus_StretchOpened(Events.StretchOpened e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            float edge = m_fishing.Layout.InnerEdge(e.Stretch);
            Vector3 at = ToWorld(new System.Numerics.Vector2(edge, m_fishing.Layout.BankY + 1f));
            StartCoroutine(ClodsLater(m_wombat.Dig(), at));
            Repaint();
            Expanded?.Invoke();
        }

        private IEnumerator ClodsLater(float seconds, Vector3 at)
        {
            yield return new WaitForSeconds(seconds);
            StartCoroutine(Fx.Burst(transform, m_square, at, 12, 2.4f, 4f, 12f, 0.6f, 12, k_Dirt, k_TopOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, 12, 2.4f, 4f, 12f, 0.6f, 12, k_DirtDark, k_TopOrder));
        }

        // 잔해 한 조각: 물보라 · 잔해 그림이 웜뱃 옆 둑으로 튀어 잠깐 놓였다 사라지고 「+20」 · 남은 조각 알약
        private void Bus_DebrisPulled(Events.DebrisPulled e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            Vector3 from = m_bobberAt;
            Splash(from, 2);
            Sprite piece = m_debris != null && m_debris.Length > 0 ? m_debris[UnityEngine.Random.Range(0, m_debris.Length)] : Prop("bank_driftwood");
            SpriteRenderer body = NewRenderer(transform, "Debris", piece, Color.white, k_TopOrder - 3);
            body.transform.position = from;
            Vector3 wombat = m_wombat.transform.position;
            Vector3 to = wombat + new Vector3(e.Dam.Position.X > m_wombat.transform.localPosition.x ? 0.6f : -0.6f, 0.1f, 0f);
            int left = e.Dam.PiecesLeft;
            StartCoroutine(Fly(body.transform, to, k_CatchArc, () =>
            {
                Destroy(body.gameObject, 0.8f);
                ShowPopup(wombat + Vector3.up * k_PopupHeight, m_tables.Format("coin_popup", e.Coins.ToString("0", CultureInfo.InvariantCulture)), null);

                if (left > 0)
                {
                    StartCoroutine(Rise(m_tables.Format("fishing_debris_left", left), wombat + Vector3.up * (k_PopupHeight + 0.5f)));
                }
            }));
        }

        // 댐이 무너졌다: 굴 · 물을 다시 칠하고, 경계에서 먼 쪽으로 물보라가 줄지어 튄다(물이 쏟아져 차오름). 새 물고기 이름 알약
        private void Bus_DamBroken(Events.DamBroken e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            Repaint();
            Expanded?.Invoke();
            StartCoroutine(Flood(e.Stretch));
            FishingStretchTable stretch = m_tables.GetAll<FishingStretchTable>().First(row => row.Index == e.Stretch);
            string names = string.Join(" · ", stretch.Fish.Select(fish => m_tables.Get<ItemTable>(m_tables.Get<FishTable>(fish.Id).Item).Name));
            Vector3 center = ToWorld(new System.Numerics.Vector2(m_fishing.Layout.CenterOf(e.Stretch), m_fishing.Layout.BankY + 1.5f));
            StartCoroutine(Rise(m_tables.Format("fishing_flooded", names), center));
        }

        private IEnumerator Flood(int stretch)
        {
            FishingLayout layout = m_fishing.Layout;
            float from = layout.InnerEdge(stretch);
            float to = stretch > 0 ? layout.RightOf(stretch) : layout.LeftOf(stretch);
            float y = layout.BankY - 1.2f;

            for (float t = 0f; t < k_FloodSeconds; t += 0.1f)
            {
                float x = Mathf.Lerp(from, to, t / k_FloodSeconds);
                Splash(ToWorld(new System.Numerics.Vector2(x, y)), 1);
                Ripple(ToWorld(new System.Numerics.Vector2(x, y - 1.5f)), k_RippleBite);
                yield return new WaitForSeconds(0.1f);
            }
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
