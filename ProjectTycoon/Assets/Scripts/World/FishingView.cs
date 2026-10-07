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
    // 매 프레임 Core(FishingArea)대로 그린다. 사건: 던짐(찌가 대 끝에서 포물선으로 난다) · 가짜 입질(찌 까딱) · 물었다 · 걸렸다 · 달리기(물보라) · 낚음(물고기가 손으로 튀고 웜뱃이 만세, 이름 · 무게 알약) · 끊김(줄이 튄다)
    public sealed class FishingView : MonoBehaviour, IAreaView
    {
        private const string k_PopupKey = "harvest_popup";
        // 그림 층: 굴(−2000) 위에 물 · 둑, 그 위에 물속 그림자, 팝업 · 알갱이 · 게이지는 맨 위
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
        // 던지기: 찌가 대 끝에서 물 자리까지 포물선으로 나는 초 · 높이(유닛)
        private const float k_CastSeconds = 0.35f;
        private const float k_CastArc = 0.6f;
        // 물속 그림자는 찌 아래로 · 긴장 게이지(릴 16칸)는 웜뱃 오른쪽 어깨 옆(대 끝 · 줄과 안 겹치게)
        private const float k_ShadowDrop = 0.12f;
        private static readonly Vector3 k_GaugeAt = new Vector3(0.95f, 1.1f, 0f);
        // 낚은 물고기가 손으로 튀는 초 · 높이 · 만세한 두 앞발 사이 높이(유닛, 아트방 make_cheer.py) · 이름 알약이 떠오르는 초 · 팝업 높이(든 물고기 위)
        private const float k_CatchSeconds = 0.35f;
        private const float k_CatchArc = 0.8f;
        private const float k_CheerHold = 1.08f;
        private const float k_RiseSeconds = 0.9f;
        private const float k_PopupHeight = 1.7f;

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
        private float m_handScale = 1f;
        private Vector3 m_bobberAt;

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

            BurrowShape.Result shape = fishing.Layout.Shape;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile, m_wallFace);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);
            float width = shape.Width / BurrowShape.k_PixelsPerUnit;
            float height = shape.Height / BurrowShape.k_PixelsPerUnit;
            m_bounds = new Rect(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit - height, width, height);
            m_arch.localPosition = new Vector3(fishing.Layout.HoleFloor.X, -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit, 0f);

            // ponytail: 물결 칸마다 방 전체를 한 장씩. 방이 더 커지면 물 칸만 따로 그리는 층으로
            m_water = NewRenderer(transform, "Water", null, Color.white, k_WaterOrder);
            m_water.transform.localPosition = m_burrow.transform.localPosition;
            m_waterFrames = m_waterTiles.Select(tile => WaterPainter.Paint(shape, fishing.Layout, tile, m_streamFace, m_bank == null)).ToArray();
            m_water.sprite = m_waterFrames[0];

            if (m_bank != null)
            {
                NavRect water = fishing.Layout.Water;
                (float left, float right) = FloorSpan(shape, water.YMax);
                SpriteRenderer bank = NewRenderer(transform, "Bank", m_bank, Color.white, k_WaterOrder + 1);
                bank.drawMode = SpriteDrawMode.Tiled;
                bank.size = new Vector2(right - left, m_bank.bounds.size.y);
                bank.transform.localPosition = new Vector3((left + right) / 2f, water.YMax, 0f);
            }

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

            m_shadow = NewRenderer(transform, "Shadow", SwimSprite(fishing.Config.Fish), Color.white, k_FishOrder);
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

            m_water.sprite = m_waterFrames[(int)(Time.time / k_WaterFrameSeconds) % m_waterFrames.Length];
            m_nibbleLeft = Mathf.Max(0f, m_nibbleLeft - Time.deltaTime);
            FishingPhase phase = m_fishing.Phase;
            bool fishing = phase != FishingPhase.Idle;
            // 대는 물가가 대상일 때부터 손에 보이고, 낚아 만세하는 동안(Busy)은 내려놓는다
            SyncRod(fishing || (m_fishing.Target == m_fishing.Water && !m_fishing.Wombat.Busy));
            m_line.enabled = fishing;
            m_bobberArt.enabled = fishing;
            m_shadow.enabled = phase == FishingPhase.Tug;
            m_gauge.enabled = phase == FishingPhase.Tug;

            if (!fishing)
            {
                m_castLeft = 0f;
                return;
            }

            Vector3 tip = TipOf();
            Vector3 spot = BobberAt(phase);
            float castLeft = Mathf.Max(0f, m_castLeft - Time.deltaTime);

            // 던진 직후에는 찌가 대 끝에서 물 자리까지 포물선으로 날아가 떨어진다
            if (m_castLeft > 0f)
            {
                float k = 1f - castLeft / k_CastSeconds;
                m_bobberAt = Vector3.Lerp(tip, spot, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * k_CastArc);

                if (castLeft <= 0f)
                {
                    Splash(spot, 1);
                }
            }
            else
            {
                m_bobberAt = spot;
            }

            m_castLeft = castLeft;
            m_bobberArt.transform.position = m_bobberAt;
            m_bobberArt.sprite = m_bobber[phase != FishingPhase.Waiting || m_nibbleLeft > 0f ? 1 : 0];
            Vector3 d = m_bobberAt - tip;
            m_line.transform.position = tip + d / 2f;
            m_line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            m_line.transform.localScale = new Vector3(d.magnitude, k_LineWidth, 1f);

            if (phase == FishingPhase.Tug)
            {
                m_shadow.transform.position = m_bobberAt + Vector3.down * k_ShadowDrop;
                m_shadow.flipX = Mathf.Sin(Time.time * k_RunWobbleSpeed) < 0f;
                int frame = Mathf.Clamp(Mathf.RoundToInt((float)(m_fishing.Tension / m_fishing.Config.Rod.Line) * (m_reelGauge.Length - 1)), 0, m_reelGauge.Length - 1);
                m_gauge.sprite = m_reelGauge[frame];
                m_gauge.transform.position = m_wombat.transform.position + k_GaugeAt;
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

        // 찌 자리: 웜뱃 쪽 물가에서 물 안쪽으로. 기다림 · 입질은 깊이의 k_BobberIn, 줄다리기는 남은 거리 비율만큼 먼 쪽(달릴 때는 옆으로 흔들린다)
        private Vector3 BobberAt(FishingPhase phase)
        {
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

        // 손에 든 대(자리는 WombatView.Hand가 맞춘다): 달릴 때 휘고(rod_bent), 감을 때 당김 판. 왼쪽을 보면 뒤집고 뒷모습이면 몸 뒤(든 빵과 같은 규칙)
        private void SyncRod(bool shown)
        {
            m_rod.enabled = shown;

            if (!shown)
            {
                return;
            }

            Facing facing = m_fishing.Wombat.Mover.Facing;
            bool tug = m_fishing.Phase == FishingPhase.Tug;
            string name = tug && m_fishing.Running ? "rod_bent" : tug && m_fishing.Wombat.Holding ? "rod_bamboo_1_pull" : "rod_bamboo_1";
            m_rod.sprite = m_rodSprites[name];
            m_rod.flipX = facing == Facing.Left;
            m_rod.transform.localPosition = new Vector3(facing == Facing.Left || facing == Facing.Right ? 0f : k_RodGrip.x, k_RodGrip.y, 0f) / m_handScale;
            m_rod.sortingOrder = Fx.CarryOrder(facing, k_RodFrontOrder, k_RodBackOrder);
        }

        // 줄이 나오는 대 끝(왼쪽을 보면 좌우가 뒤집힌다)
        private Vector3 TipOf()
        {
            Vector2 cells = k_RodTips.TryGetValue(m_rod.sprite.name, out Vector2 tip) ? tip : new Vector2(0f, m_rod.sprite.rect.height / 4f);
            return m_rod.transform.position + new Vector3((m_rod.flipX ? -cells.x : cells.x) * Fx.k_Cell, cells.y * Fx.k_Cell, 0f);
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
            }
        }

        private void Bus_Bitten(Events.Bitten e)
        {
            if (e.Fishing == m_fishing)
            {
                Splash(m_bobberAt, 1);
            }
        }

        private void Bus_Hooked(Events.Hooked e)
        {
            if (e.Fishing == m_fishing)
            {
                Splash(m_bobberAt, 2);
            }
        }

        private void Bus_RunChanged(Events.RunChanged e)
        {
            if (e.Fishing == m_fishing && e.Running)
            {
                Splash(m_bobberAt, 1);
            }
        }

        // 낚았다: 큰 물보라, 웜뱃은 대를 내려놓고 만세(Core가 landSeconds 동안 세워 둔다), 물고기 그림이 든 두 앞발 사이로 튀어 올라 그동안 머문다. 「+1」 팝업과 「붕어 1.2kg」 알약
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
            float seconds = (float)m_fishing.Config.LandSeconds;
            string tag = m_tables.Format("fishing_catch_tag", item.Name, e.Fish.Weight.ToString("0.#", CultureInfo.InvariantCulture));
            m_wombat.Cheer(seconds);
            StartCoroutine(Fly(body.transform, wombat + Vector3.up * k_CheerHold, k_CatchArc, () =>
            {
                Destroy(body.gameObject, seconds - k_CatchSeconds);
                ShowPopup(wombat + Vector3.up * k_PopupHeight, m_tables.Format(k_PopupKey, 1), icon);
                StartCoroutine(Rise(tag, wombat + Vector3.up * (k_PopupHeight + 0.5f)));
            }));
        }

        // 끊기면 대 끝에서 줄 조각이 튄다
        private void Bus_FishingEnded(Events.FishingEnded e)
        {
            if (e.Fishing == m_fishing && e.Reason == FishingEnd.Snapped)
            {
                StartCoroutine(Fx.Burst(transform, m_square, TipOf(), 8, 1.0f, 2f, 9f, 0.4f, 3, k_Line, k_TopOrder));
                Splash(m_bobberAt, 1);
            }
        }

        // ---------- 연출 조각 ----------

        private IEnumerator Fly(Transform target, Vector3 to, float arc, Action landed)
        {
            Vector3 from = target.position;

            for (float t = 0f; t < k_CatchSeconds; t += Time.deltaTime)
            {
                float k = t / k_CatchSeconds;
                target.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * arc);
                yield return null;
            }

            target.position = to;
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
