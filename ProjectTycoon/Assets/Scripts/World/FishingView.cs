using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using GameKit.Audio;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 44: 낚시터 화면. 굴 그림은 광장처럼 고정 방(BurrowPainter), 구멍 아치(광장으로)는 첫 줄 가운데.
    // 물길은 아트방 「흙 도랑」을 굴 그림처럼 칸마다 칠한다(StreamPainter). 설계 45: 판 데까지만 물이고 끝은 둑으로 막힌다. 파면 다시 칠하고, 막다른 끝 앞 땅에 파기 값.
    // 말뚝은 아트방 「나무 말뚝」(발끝 피벗), 대는 「통통한 대」(피벗 = 꽂는 자리)를 말뚝 윗면 구멍에 꽂는다(말뚝 앞, 밑동 밧줄 깃이 구멍을 덮는다). 대는 돌리지도 뒤집지도 않고, 감는 동안 끄덕인다.
    // 물고기는 아트방 「둥근 귀염」 위에서 본 모습(오른쪽을 봄, 월척은 2배), 낚이면 옆모습(창고 아이콘)으로 튄다. 물고기 · 말뚝은 매 프레임 Core에서 읽어 맞춘다.
    // 단계 팻말과 값 표식(열기 · 파기 · 단계 올리기)은 값 표식 알약(MarkerView, 1배), 붙잡힌 월척 위는 「!」 말풍선. 낚으면 재료 팝업, 쿵 · 털썩 · 파기는 웜뱃 동작.
    // 낚시 폴리싱: 감는 대 끝에서 물고기까지 낚싯줄, 물고기는 물길 방향으로 눕고 살랑인다(90도 단위). 낚으면 대 끝으로 튀어 오르고, 놓치면 막다른 끝에서 사라진다.
    // 설계 46 대 사기: 등급 뽑기 — 말뚝 별이 하나씩 켜진 뒤(k_RevealStep마다) 대가 꽂히며 등급만큼 반짝임(드묾 · 전설 글)
    public sealed class FishingView : MonoBehaviour, IAreaView
    {
        // 설계 46 등급 뽑기: 별 하나가 켜지는 간격(대는 별이 다 켜진 뒤 꽂힌다, 소리도 그때)
        public const float k_RevealStep = 0.15f;
        private const string k_PopupKey = "harvest_popup";
        // 그림 층: 굴(−2000) 위에 물, 그 위에 물고기 · 사거리 원, 팝업 · 알갱이는 맨 위
        private const int k_WaterOrder = -1985;
        private const int k_FishOrder = -1980;
        private const int k_RangeOrder = -1975;
        private const int k_TopOrder = 1000;
        // 「!」 말풍선은 값 알약(k_TopOrder · 글 +1) 위: 같은 순서면 알약과 섞여 그려진다
        private const int k_AlertOrder = k_TopOrder + 2;
        private static readonly Color k_Water = new Color32(0x3E, 0x7F, 0x95, 255);
        private static readonly Color k_WaterLight = new Color32(0xDD, 0xF1, 0xF5, 255);
        private static readonly Color k_Dirt = new Color32(0x8A, 0x5A, 0x36, 255);
        private static readonly Color k_DirtDark = new Color32(0x5C, 0x3A, 0x26, 255);
        private static readonly Color k_Star = new Color32(0xF2, 0xC1, 0x4E, 255);
        private static readonly Color k_Range = new Color32(0xF6, 0xE3, 0xCC, 70);
        private static readonly Color k_BarBack = new Color32(0x2A, 0x1F, 0x17, 200);
        private static readonly Color k_BarFill = new Color32(0xF6, 0xE3, 0xCC, 255);
        // 대 끝(줄이 나오는 점): 꽂는 자리에서 칸(1/40유닛), 등급은 같다(_pull = 당김 판)
        // 아트방 설계 45 A 「말뚝에 맞춘 대」, Source~/make_rods.py가 다시 출력한다
        private static readonly Dictionary<string, Vector2> k_RodTips = new Dictionary<string, Vector2>
        {
            { "rod_bamboo", new Vector2(13f, 38f) },
            { "rod_bamboo_pull", new Vector2(15f, 38f) },
            { "rod_iron", new Vector2(12f, 38f) },
            { "rod_iron_pull", new Vector2(14f, 38f) },
            { "rod_bait", new Vector2(12f, 38f) },
            { "rod_bait_pull", new Vector2(14f, 38f) },
            { "rod_bent", new Vector2(12f, 28f) },
        };
        // 월척 물고기 배율(정수 배라 칸 격자 그대로) · 가로 물길에서 물고기를 흙 면 아래 물 가운데로 내리는 거리
        private const float k_TrophyScale = 2f;
        private const float k_WaterDrop = StreamPainter.k_FaceRows / BurrowShape.k_PixelsPerUnit / 2f;
        // 감는 동안 곧은 판 ↔ 당김 판을 번갈아 쓰는 한 번의 초
        private const float k_PullSeconds = 0.5f;
        // 말뚝 윗면 구멍(대를 꽂는 자리): 발끝에서 위로(17칸, 아트방 make_stakes.py) · 대 높이(38칸)
        private const float k_RodSocket = 0.425f;
        private const float k_RodHeight = 0.95f;
        private const float k_PopupHeight = 1.2f;
        private const float k_TagRise = 1.3f;
        // 쿵: 물결 고리가 쿵 반지름까지 퍼지는 초 · 멈춘 물고기 머리 위 별(높이 · 도는 반지름 · 빠르기)
        private const float k_RippleSeconds = 0.45f;
        private const float k_DizzyLift = 0.25f;
        private const float k_DizzyRadius = 0.15f;
        private const float k_DizzySpeed = 7f;
        // 설계 46 소용돌이: 물 소용돌이가 되돌린 물고기를 따라가는 초 · 한 칸 초(아트방 제안)
        private const float k_WhirlSeconds = 0.6f;
        private const float k_WhirlFrameSeconds = 0.08f;
        // 물결 한 칸 초(아트방 「고인 물」 수정)
        private const float k_WaterFrameSeconds = 0.35f;
        // 등급 별: 발끝 아래 높이 · 별 사이(8칸 = 별 7칸 + 틈 1칸)
        private const float k_StarDrop = 0.15f;
        private const float k_StarGap = 0.2f;
        // 낚싯줄 굵기 · 낚은 물고기가 대 끝까지 튀는 초 · 높이 · 살랑임(칸) · 글 알약이 떠오르는 초
        private const float k_LineWidth = 0.025f;
        private const float k_CatchSeconds = 0.35f;
        private const float k_CatchArc = 0.8f;
        private const float k_WobbleCells = 2f;
        private const float k_RiseSeconds = 0.9f;
        private static readonly Color k_Line = new Color32(0xF6, 0xE3, 0xCC, 200);

        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("바닥·벽·윗벽 띠 재료(빵집과 같은 것)")]
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [SerializeField] private Texture2D m_wallFace;
        [Tooltip("설계 44: 물길 재료(아트방 「흙 도랑」, 굴 재료처럼 픽셀을 읽는다). 물 타일은 물결 네 칸(water_tile_0~3)을 k_WaterFrameSeconds마다 돈다")]
        [SerializeField] private Texture2D[] m_waterTiles;
        [SerializeField] private Texture2D m_streamFace;
        [Tooltip("설계 44: 물속 물고기(아트방 「둥근 귀염」 <재료 id>_swim, 오른쪽을 봄, 가운데 피벗)")]
        [SerializeField] private Sprite[] m_fishSwim;
        [Tooltip("설계 22 이모지 말풍선 시트(BubbleTable alert 칸 = 붙잡힌 월척 위 「!」)")]
        [SerializeField] private Sprite[] m_bubbleFrames;
        [Tooltip("설계 44: 말뚝 · 잠긴 말뚝(아트방 「나무 말뚝」, 발끝 피벗)")]
        [SerializeField] private Sprite m_stake;
        [SerializeField] private Sprite m_stakeLocked;
        [Tooltip("말뚝 밑 등급 별(아트방 7×7칸, 가운데 피벗)")]
        [SerializeField] private Sprite m_stakeStar;
        [Tooltip("설계 46 소용돌이(미끼 노점 업그레이드)가 물고기를 되돌릴 때 그 자리의 물 소용돌이 네 칸(아트방, 가운데 피벗)")]
        [SerializeField] private Sprite[] m_whirl;
        [Tooltip("설계 44: 낚싯대(아트방 「통통한 대」 rod_<계열>_<등급> · 월척을 붙잡은 rod_bent, 피벗 = 꽂는 자리)")]
        [SerializeField] private Sprite[] m_rods;
        [Tooltip("구멍 아치(나가기 대상이면 튄다)")]
        [SerializeField] private Transform m_arch;
        [Tooltip("점원 오두막(아트방 B 「미끼 노점」, 발끝 피벗, 점원 자리 바로 뒤)")]
        [SerializeField] private Transform m_hut;
        [Tooltip("값 표식 · 팻말(빵집 파기 표식과 같은 프리팹)")]
        [SerializeField] private MarkerView m_tagPrefab;
        [Tooltip("낚은 물고기 팝업(코인 팝업 프리팹에 재료 아이콘을 얹는다)")]
        [SerializeField] private CoinPopup m_popupPrefab;
        [Tooltip("설계 37: 웜뱃 똥 그림(빵집과 같은 프리팹 · 프레임)")]
        [SerializeField] private SpriteAnimator m_poopPrefab;
        [SerializeField] private Sprite[] m_poopFrames;
        [SerializeField] private float m_poopFrameRate = 2f;
        [Tooltip("낚시 점원 그림(손님 · 빵집 점원과 같은 프리팹)")]
        [SerializeField] private VisitorView m_clerkPrefab;
        [SerializeField] private WombatView m_wombat;

        private readonly Dictionary<Fish, FishArt> m_fish = new Dictionary<Fish, FishArt>();
        private readonly List<StakeArt> m_stakes = new List<StakeArt>();
        private readonly List<SpriteRenderer> m_ranges = new List<SpriteRenderer>();
        private FishingArea m_fishing;
        private FrameCache m_frames;
        private TableSet m_tables;
        private PoopViews m_poopViews;
        private ClerkViews m_clerkViews;
        private IDisposable[] m_subscriptions;
        private Sprite m_square;
        private Sprite m_halfRing;
        private Sprite m_ring;
        private SpriteRenderer m_stream;
        // ponytail: 물결 칸마다 물길 전체를 한 장씩(568×588 넷 ≈ 5MB). 물길이 더 커지면 물 칸만 따로 그리는 층으로
        private Sprite[] m_streamFrames = new Sprite[0];
        private MarkerView m_waveSign;
        private MarkerView m_priceTag;
        private MarkerView m_digTag;
        private BubbleTable m_alert;
        private readonly Dictionary<string, Sprite> m_rodSprites = new Dictionary<string, Sprite>();
        // 등급 뽑기 중인 말뚝 → 켜진 별 수(그동안 대는 숨긴다)
        private readonly Dictionary<StakeInteractable, int> m_revealing = new Dictionary<StakeInteractable, int>();
        private Rect m_bounds;

        // 물고기 하나: 몸 · 감기 막대(뒤 · 앞)
        private sealed class FishArt
        {
            public SpriteRenderer Body;
            public SpriteRenderer BarBack;
            public SpriteRenderer BarFill;
            public SpriteRenderer Dizzy;
            public float Length;
            public float Phase;
        }

        // 말뚝 하나: 몸(기둥 + 대) · 등급 별 셋 · 「!」 말풍선 · 낚싯줄
        private sealed class StakeArt
        {
            public StakeInteractable Stake;
            public Transform Body;
            public SpriteRenderer Post;
            public SpriteRenderer Rod;
            public SpriteRenderer[] Stars;
            public SpriteRenderer Alert;
            public SpriteRenderer Line;
        }

        public Vector3 Origin => transform.position;
        public Transform Wombat => m_wombat.transform;
        public Rect Bounds => new Rect(transform.position.x + m_bounds.x, transform.position.y + m_bounds.y, m_bounds.width, m_bounds.height);

        public void Bind(FishingArea fishing, EventBus bus, FrameCache frames, TableSet tables)
        {
            m_fishing = fishing;
            m_frames = frames;
            m_tables = tables;
            m_square = Fx.NewSquare();
            m_halfRing = NewHalfRing();
            m_ring = NewRing();
            m_alert = tables.Get<BubbleTable>("alert");
            m_poopViews = new PoopViews(this, fishing, bus, m_poopPrefab, m_poopFrames, m_poopFrameRate, m_popupPrefab, tables, frames, ToWorld);
            m_clerkViews = new ClerkViews(this, fishing, bus, m_clerkPrefab, tables, frames, transform, null);

            BurrowShape.Result shape = fishing.Layout.Shape;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile, m_wallFace);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);
            float width = shape.Width / BurrowShape.k_PixelsPerUnit;
            float height = shape.Height / BurrowShape.k_PixelsPerUnit;
            m_bounds = new Rect(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit - height, width, height);
            m_arch.localPosition = new Vector3(fishing.Layout.HoleFloor.X, -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit, 0f);
            m_hut.localPosition = new Vector3(fishing.Layout.HutSpot.X, fishing.Layout.HutSpot.Y + 0.3f, 0f);

            BuildStream();
            BuildStakes();
            // 단계 팻말: 입구 아치 위쪽 가운데(옆에 두면 좁은 폰에서 알약이 화면 밖으로 잘린다)
            m_waveSign = NewTag(ToWorld(new System.Numerics.Vector2(fishing.Layout.HoleFloor.X, -0.6f)));
            m_priceTag = NewTag(Vector3.zero);
            m_priceTag.gameObject.SetActive(false);
            m_digTag = NewTag(Vector3.zero);
            m_rods.ToList().ForEach(sprite => m_rodSprites[sprite.name] = sprite);
            m_wombat.Bind(fishing, transform, frames);

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.FishCaught>(Bus_FishCaught),
                bus.Subscribe<Events.FishEscaped>(Bus_FishEscaped),
                bus.Subscribe<Events.WaveStarted>(Bus_WaveStarted),
                bus.Subscribe<Events.RodSummoned>(Bus_RodSummoned),
                bus.Subscribe<Events.StageRaised>(Bus_StageRaised),
                bus.Subscribe<Events.FishWhirled>(Bus_FishWhirled),
                bus.Subscribe<Events.Thumped>(Bus_Thumped),
                bus.Subscribe<Events.StreamDug>(Bus_StreamDug),
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

            m_stream.sprite = m_streamFrames[(int)(Time.time / k_WaterFrameSeconds) % m_streamFrames.Length];
            SyncFish();
            SyncStakes();
            SyncRanges();
            SyncPriceTag();
            m_waveSign.Show(m_tables.Format("fishing_wave", m_fishing.Stage));
            // 파기 값은 웜뱃이 막다른 끝 앞 땅을 대상으로 할 때만(빵집 · 농장 파기와 같이, 2026-10-05 사용자 「늘 떠 있는 건 별로」)
            bool digTarget = !m_fishing.StreamFull && m_fishing.Target == m_fishing.StreamEnd;
            m_digTag.gameObject.SetActive(digTarget);

            if (digTarget)
            {
                m_digTag.transform.position = ToWorld(m_fishing.StreamEnd.Position);
                m_digTag.Show(m_tables.Format("tag_dig", BigNumberText(m_fishing.DigPrice)));
            }
        }

        // 「!」 말풍선 지금 칸(BubbleTable alert 칸 · 프레임)
        private Sprite AlertFrame()
        {
            int frame = m_alert.Frame + (m_alert.Frames > 1 ? (int)(Time.time / m_alert.FrameSeconds) % m_alert.Frames : 0);
            return m_bubbleFrames[Mathf.Clamp(frame, 0, m_bubbleFrames.Length - 1)];
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
            m_clerkViews.Dispose();
        }

        // ---------- 물길 · 물고기 ----------

        // 물길(굴 그림과 같은 원점 · 크기). 판 데까지만 칠한다
        private void BuildStream()
        {
            m_stream = NewRenderer(transform, "Stream", null, Color.white, k_WaterOrder);
            m_stream.transform.localPosition = m_burrow.transform.localPosition;
            PaintStream();
        }

        // 물길을 다시 칠한다(앞 그림의 텍스처는 버린다)
        private void PaintStream()
        {
            foreach (Sprite frame in m_streamFrames)
            {
                Destroy(frame.texture);
                Destroy(frame);
            }

            m_streamFrames = m_waterTiles.Select(tile => StreamPainter.Paint(m_fishing.Layout.Shape, m_fishing.Layout, tile, m_streamFace)).ToArray();
            m_stream.sprite = m_streamFrames[0];
        }

        private void SyncFish()
        {
            foreach (Fish fish in m_fishing.Fish)
            {
                if (!m_fish.TryGetValue(fish, out FishArt art))
                {
                    art = NewFish(fish);
                    m_fish[fish] = art;
                }

                // 물길 방향으로 눕고(90도 단위라 칸 격자 그대로) 옆으로 살랑인다. 쿵에 멈추면 깜빡이고, 붙잡힌 월척은 몸부림친다
                System.Numerics.Vector2 dir = m_fishing.Layout.PointAt(fish.S + 0.05f) - m_fishing.Layout.PointAt(fish.S - 0.05f);
                bool vertical = Math.Abs(dir.Y) > Math.Abs(dir.X);
                bool stunned = fish.StunLeft > 0d && fish.HookedBy == null;
                float wobble = fish.HookedBy != null ? Mathf.Sin(Time.time * 30f) : stunned ? 0f : Mathf.Sin(Time.time * 5f + art.Phase);
                Vector3 at = ToWorld(m_fishing.Layout.PointAt(fish.S)) + (vertical ? Vector3.right : Vector3.up) * (Mathf.Round(wobble * k_WobbleCells) * Fx.k_Cell)
                    + (vertical ? Vector3.zero : Vector3.down * k_WaterDrop);
                art.Body.transform.position = at;
                art.Body.transform.localRotation = Quaternion.Euler(0f, 0f, vertical ? (dir.Y < 0f ? -90f : 90f) : 0f);
                art.Body.flipX = !vertical && dir.X < 0f;
                art.Dizzy.enabled = stunned;

                // 쿵에 멈춘 동안 별 하나가 머리 위를 돈다(그림자에 색을 곱하면 거의 안 보였다)
                if (stunned)
                {
                    float angle = Time.time * k_DizzySpeed + art.Phase;
                    art.Dizzy.transform.position = at + new Vector3(Mathf.Cos(angle) * k_DizzyRadius, k_DizzyLift + Mathf.Sin(angle) * k_DizzyRadius * 0.4f, 0f);
                }
                bool reeling = fish.Reeled > 0d && (!fish.Trophy || m_fishing.TrophyReelScale > 0d);
                art.BarBack.gameObject.SetActive(reeling);
                art.BarFill.gameObject.SetActive(reeling);

                if (reeling)
                {
                    float progress = Mathf.Clamp01((float)(fish.Reeled / fish.Weight));
                    Vector3 bar = at + new Vector3(0f, 0.35f + art.Length * 0.2f, 0f);
                    art.BarBack.transform.position = bar;
                    art.BarFill.transform.position = bar + new Vector3(-art.Length * 0.5f * (1f - progress), 0f, 0f);
                    art.BarFill.transform.localScale = new Vector3(art.Length * progress, 0.06f, 1f);
                }
            }

            foreach (Fish gone in m_fish.Keys.Where(f => !m_fishing.Fish.Contains(f)).ToList())
            {
                Destroy(TakeFish(gone).gameObject);
            }
        }

        // 물고기 그림을 목록에서 떼어 몸만 돌려준다(감기 막대는 지운다). 없으면 null
        private Transform TakeFish(Fish fish)
        {
            if (!m_fish.Remove(fish, out FishArt art))
            {
                return null;
            }

            Destroy(art.BarBack.gameObject);
            Destroy(art.BarFill.gameObject);
            Destroy(art.Dizzy.gameObject);
            return art.Body.transform;
        }

        // 물속 모습은 <재료 id>_swim. 월척은 2배
        private FishArt NewFish(Fish fish)
        {
            Sprite sprite = m_fishSwim.FirstOrDefault(s => s.name == fish.Kind.Item + "_swim") ?? m_fishSwim[0];
            SpriteRenderer body = NewRenderer(transform, "Fish", sprite, Color.white, k_FishOrder);
            body.transform.localScale = Vector3.one * (fish.Trophy ? k_TrophyScale : 1f);
            float length = sprite.bounds.size.x * body.transform.localScale.x;
            SpriteRenderer back = NewRenderer(transform, "ReelBack", m_square, k_BarBack, k_TopOrder - 2);
            back.transform.localScale = new Vector3(length, 0.1f, 1f);
            SpriteRenderer fill = NewRenderer(transform, "ReelFill", m_square, k_BarFill, k_TopOrder - 1);
            back.gameObject.SetActive(false);
            fill.gameObject.SetActive(false);
            SpriteRenderer dizzy = NewRenderer(transform, "Dizzy", m_stakeStar, Color.white, k_FishOrder + 1);
            dizzy.enabled = false;
            return new FishArt { Body = body, BarBack = back, BarFill = fill, Dizzy = dizzy, Length = length, Phase = UnityEngine.Random.value * Mathf.PI * 2f };
        }

        // ---------- 말뚝 · 대 ----------

        private void BuildStakes()
        {
            foreach (StakeInteractable stake in m_fishing.Stakes)
            {
                Transform holder = new GameObject("Stake" + stake.Index).transform;
                holder.SetParent(transform, false);
                holder.localPosition = new Vector3(stake.Position.X, stake.Position.Y, 0f);
                // 기둥 + 대는 SortingGroup(발끝)으로 묶어 대를 기둥 앞에: 대 밑동의 밧줄 깃이 윗면 구멍을 덮어 꽂힌 모습이 된다
                Transform body = new GameObject("Body", typeof(SortingGroup)).transform;
                body.SetParent(holder, false);
                SpriteRenderer post = NewRenderer(body, "Post", m_stake, Color.white, 0);
                SpriteRenderer rod = NewRenderer(body, "Rod", null, Color.white, 1);
                SpriteRenderer[] stars = new SpriteRenderer[3];

                for (int i = 0; i < stars.Length; i++)
                {
                    stars[i] = NewRenderer(holder, "Star", m_stakeStar, Color.white, 1);
                }

                SpriteRenderer alert = NewRenderer(holder, "Alert", null, Color.white, k_AlertOrder);
                alert.transform.localPosition = Vector3.up * k_TagRise;
                SpriteRenderer line = NewRenderer(holder, "Line", m_square, k_Line, 1);
                m_stakes.Add(new StakeArt { Stake = stake, Body = body, Post = post, Rod = rod, Stars = stars, Alert = alert, Line = line });
            }
        }

        private void SyncStakes()
        {
            foreach (StakeArt art in m_stakes)
            {
                StakeInteractable stake = art.Stake;
                art.Post.sprite = stake.Open ? m_stake : m_stakeLocked;
                bool revealing = m_revealing.TryGetValue(stake, out int lit);
                art.Rod.gameObject.SetActive(stake.Rod != null && !revealing);

                if (stake.Rod != null)
                {
                    SyncRod(art);
                }

                art.Line.enabled = art.Rod.gameObject.activeSelf && stake.Reeling != null && m_fish.ContainsKey(stake.Reeling) && k_RodTips.ContainsKey(TipKey(art.Rod.sprite));

                if (art.Line.enabled)
                {
                    Vector3 tip = TipOf(art.Rod);
                    Vector3 d = m_fish[stake.Reeling].Body.transform.position - tip;
                    art.Line.transform.position = tip + d / 2f;
                    art.Line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    art.Line.transform.localScale = new Vector3(d.magnitude, k_LineWidth, 1f);
                }

                // 등급 수만큼 말뚝 가운데에 맞춰 늘어놓는다(뽑기 중이면 켜진 별까지만)
                int shown = stake.Rod != null ? Math.Min(stake.Grade, art.Stars.Length) : 0;

                for (int i = 0; i < art.Stars.Length; i++)
                {
                    art.Stars[i].enabled = i < shown && (!revealing || i < lit);
                    art.Stars[i].transform.localPosition = new Vector3((i - (shown - 1) / 2f) * k_StarGap, -k_StarDrop, 0f);
                }

                art.Alert.enabled = stake.Hooked != null;
                art.Alert.sprite = AlertFrame();
            }
        }

        // 대는 그림 그대로(돌리지도 뒤집지도 않는다). 감는 동안 곧은 판 ↔ 당김 판을 번갈아 쓰고, 월척을 붙잡으면 휜 대로 바꿔 말뚝째 한 칸씩 떤다(대 밑동 테가 말뚝 테에서 어긋나지 않게)
        private void SyncRod(StakeArt art)
        {
            bool hooked = art.Stake.Hooked != null;
            bool pull = !hooked && art.Stake.Reeling != null && Mathf.Repeat(Time.time + art.Stake.Index * 0.17f, k_PullSeconds) < k_PullSeconds / 2f;
            art.Rod.sprite = hooked ? m_rodSprites["rod_bent"] : RodSprite(art.Stake.Rod, art.Stake.Grade, pull);
            art.Rod.transform.localPosition = new Vector3(0f, k_RodSocket, 0f);
            art.Body.localPosition = new Vector3(hooked ? Mathf.Round(Mathf.Sin(Time.time * 30f)) * Fx.k_Cell : 0f, 0f, 0f);
        }

        // 대는 rod_<계열>_<등급>(3등급까지, 당김 판은 _pull). 그림이 없는 계열(그물 · 등불, 아직 시트에 안 나옴)은 대나무대
        private Sprite RodSprite(RodTable rod, int grade, bool pull = false)
        {
            int shown = Mathf.Clamp(grade, 1, 3);
            string name = "rod_" + rod.Family + "_" + shown;
            string family = m_rodSprites.ContainsKey(name) ? name : "rod_bamboo_" + shown;
            return pull && m_rodSprites.TryGetValue(family + "_pull", out Sprite pulled) ? pulled : m_rodSprites[family];
        }

        // 등급 숫자를 뺀 이름: rod_bamboo_2 → rod_bamboo, rod_iron_3_pull → rod_iron_pull
        private static string TipKey(Sprite sprite)
        {
            return string.Join("_", sprite.name.Split('_').Where(part => !int.TryParse(part, out _)));
        }

        // 줄이 나오는 점. 대 끝이 없으면 그림 가운데
        private static Vector3 TipOf(SpriteRenderer rod)
        {
            Vector2 cells = k_RodTips.TryGetValue(TipKey(rod.sprite), out Vector2 tip) ? tip : new Vector2(0f, rod.sprite.rect.height / 4f);
            return rod.transform.position + (Vector3)cells * Fx.k_Cell;
        }

        // 대상 위 값: 잠긴 말뚝이면 열기, 미끼 노점이면 다음 단계 값(대 값은 시트 칩에). 열거나 올리면 바로 바뀐다
        private void SyncPriceTag()
        {
            Interactable target = m_fishing.WombatPresent ? m_fishing.Target : null;
            (string price, Vector3 at) = target is StakeInteractable stake && !stake.Open ? (m_tables.Format("tag_open_stake", BigNumberText(stake.Cost)), ToWorld(stake.Position))
                : ((string)null, Vector3.zero);

            if (price == null)
            {
                m_priceTag.gameObject.SetActive(false);
                return;
            }

            m_priceTag.transform.position = at + Vector3.up * k_TagRise;
            m_priceTag.Show(price);
        }

        // 대상 말뚝의 사거리(설계 45: 말뚝 위쪽 반원)
        private void SyncRanges()
        {
            List<(StakeInteractable stake, float range)> shown = new List<(StakeInteractable, float)>();

            if (m_fishing.Target is StakeInteractable target && target.Rod != null)
            {
                shown.Add((target, m_fishing.RangeOf(target)));
            }

            while (m_ranges.Count < shown.Count)
            {
                m_ranges.Add(NewRenderer(transform, "Range", m_halfRing, k_Range, k_RangeOrder));
            }

            for (int i = 0; i < m_ranges.Count; i++)
            {
                m_ranges[i].gameObject.SetActive(i < shown.Count);

                if (i < shown.Count)
                {
                    m_ranges[i].transform.position = ToWorld(shown[i].stake.Position);
                    m_ranges[i].transform.localScale = Vector3.one * shown[i].range * 2f;
                }
            }
        }

        // ---------- 사건 ----------

        // 대상이 된 말뚝 · 미끼 노점 · 아치가 튄다(값 표식은 SyncPriceTag)
        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area != m_fishing)
            {
                return;
            }

            if (m_fishing.Target is StakeInteractable stake)
            {
                StartCoroutine(Fx.Bounce(m_stakes[stake.Index].Post.transform.parent));
            }
            else if (m_fishing.Target is FishingHutInteractable)
            {
                StartCoroutine(Fx.Bounce(m_hut));
            }
            else if (m_fishing.Target is StreamEndInteractable)
            {
                m_digTag.Bounce();
            }
            else if (m_fishing.Target is PassageInteractable)
            {
                StartCoroutine(Fx.Bounce(m_arch));
            }
        }

        // 낚았다: 물보라 알갱이, 물고기가 옆모습(창고 아이콘)으로 바뀌어 대 끝으로 튀어 오르고 닿으면 재료 팝업(감은 말뚝 위).
        // 웜뱃이 털썩한 월척이면 웜뱃이 끌어올리는 동작
        private void Bus_FishCaught(Events.FishCaught e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            Transform body = TakeFish(e.Fish);
            Vector3 at = body != null ? body.position : ToWorld(e.Stake.Position);
            Splash(at, 1);
            string amount = m_tables.Format(k_PopupKey, e.Count);
            Sprite icon = m_frames.Get(m_tables.Get<ItemTable>(e.Fish.Kind.Item).Icon)[0];
            Vector3 popupAt = ToWorld(e.Stake.Position) + Vector3.up * k_PopupHeight;

            if (body != null)
            {
                SpriteRenderer side = body.GetComponent<SpriteRenderer>();
                side.sprite = icon;
                side.color = Color.white;
                body.localRotation = Quaternion.identity;
            }

            if (body == null)
            {
                ShowPopup(popupAt, amount, icon);
            }
            else
            {
                StartCoroutine(Fly(body, TipOf(m_stakes[e.Stake.Index].Rod), k_CatchArc, 1f, () => ShowPopup(popupAt, amount, icon)));
            }

            if (e.Fish.Trophy && m_fishing.WombatPresent && m_fishing.Wombat.Busy)
            {
                m_wombat.Haul((float)m_fishing.Config.HaulSeconds);
            }
        }

        // 놓쳤다: 물고기가 막다른 끝 둑 틈으로 작아지며 빠져나간다(작은 물보라 · 소리). 놓친 수는 띄우지 않는다(2026-10-05 사용자)
        private void Bus_FishEscaped(Events.FishEscaped e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            Transform body = TakeFish(e.Fish);

            if (body != null)
            {
                StartCoroutine(Fly(body, body.position, 0f, 0f, null));
                StartCoroutine(Fx.Burst(transform, m_square, body.position, 4, 0.6f, 1.6f, 9f, 0.35f, 3, k_WaterLight, k_TopOrder));
            }

        }

        // 설계 45: 물길을 팠다. 웜뱃이 퍼 던지는 때에 새 물을 칠하고 흙덩이가 튄다
        private void Bus_StreamDug(Events.StreamDug e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            Vector3 at = ToWorld(m_fishing.Layout.PointAt((e.From + e.To) / 2f));
            StartCoroutine(AfterDigThrow(m_fishing.WombatPresent ? m_wombat.Dig() : 0f, at));
        }

        private IEnumerator AfterDigThrow(float seconds, Vector3 at)
        {
            yield return new WaitForSeconds(seconds);
            PaintStream();
            StartCoroutine(Fx.Burst(transform, m_square, at, 10, 1.4f, 2.6f, 9f, 0.5f, 4, k_Dirt, k_TopOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, 8, 1.4f, 2.6f, 9f, 0.5f, 4, k_DirtDark, k_TopOrder));
            Splash(at, 1);
        }

        private void Bus_WaveStarted(Events.WaveStarted e)
        {
            if (e.Fishing == m_fishing)
            {
                m_waveSign.Bounce();
            }
        }

        // 설계 46 소용돌이: 되돌린 물고기 자리에 물 소용돌이가 돌고(k_WhirlFrameSeconds마다 한 칸) 물보라가 튄다
        private void Bus_FishWhirled(Events.FishWhirled e)
        {
            if (e.Fishing == m_fishing && m_whirl.Length > 0)
            {
                StartCoroutine(Whirl(ToWorld(m_fishing.Layout.PointAt(e.From)), ToWorld(m_fishing.Layout.PointAt(e.Fish.S))));
            }
        }

        private IEnumerator Whirl(Vector3 from, Vector3 to)
        {
            SpriteRenderer swirl = NewRenderer(transform, "Whirl", m_whirl[0], Color.white, k_FishOrder + 1);
            Splash(from, 1);

            for (float t = 0f; t < k_WhirlSeconds; t += Time.deltaTime)
            {
                swirl.sprite = m_whirl[(int)(t / k_WhirlFrameSeconds) % m_whirl.Length];
                swirl.transform.position = Vector3.Lerp(from, to, t / k_WhirlSeconds) + Vector3.down * k_WaterDrop;
                yield return null;
            }

            Destroy(swirl.gameObject);
        }

        private void Bus_StageRaised(Events.StageRaised e)
        {
            if (e.Fishing == m_fishing)
            {
                m_waveSign.Bounce();
                StartCoroutine(Fx.Bounce(m_hut));
            }
        }

        // 등급 뽑기가 끝나기까지 초
        public static float RevealSeconds(StakeInteractable stake)
        {
            return stake.Rod == null ? 0f : k_RevealStep * stake.Grade;
        }

        // 설계 46 대 사기: 옛 대는 사라지고, 말뚝 별이 하나씩 켜지며 튄 뒤(등급 뽑기) 대가 꽂히고 등급만큼 반짝임. 드묾 · 전설이면 그 글이 떠오른다
        private void Bus_RodSummoned(Events.RodSummoned e)
        {
            if (e.Stake.Fishing == m_fishing)
            {
                StartCoroutine(Reveal(m_stakes[e.Stake.Index]));
            }
        }

        private IEnumerator Reveal(StakeArt art)
        {
            StakeInteractable stake = art.Stake;

            for (int i = 0; i < stake.Grade; i++)
            {
                m_revealing[stake] = i + 1;
                StartCoroutine(Fx.Bounce(art.Stars[i].transform));
                SoundManager.Instance.Play(SoundTable.k_RodStar);
                yield return new WaitForSeconds(k_RevealStep);
            }

            m_revealing.Remove(stake);
            int grade = stake.Grade;
            Vector3 top = ToWorld(stake.Position) + Vector3.up * (k_RodSocket + k_RodHeight / 2f);
            StartCoroutine(Fx.Bounce(art.Post.transform.parent));
            StartCoroutine(Fx.Burst(transform, m_square, top, 4 + 6 * grade, 0.6f * grade, 2f, 9f, 0.3f + 0.15f * grade, 3, grade > 1 ? k_Star : k_WaterLight, k_TopOrder));

            if (grade > 1)
            {
                StartCoroutine(Rise(m_tables.Text("rod_grade_" + Math.Min(grade, 3)), ToWorld(stake.Position) + Vector3.up * k_TagRise));
            }
        }

        private void Bus_Thumped(Events.Thumped e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            float impact = m_wombat.Thump((float)m_fishing.Config.ThumpSeconds);
            StartCoroutine(Ripple(impact, ToWorld(e.At), (float)m_fishing.Config.ThumpRadius, ToWorld(m_fishing.Layout.NearestOnStream(e.At))));
        }

        // target이 to까지 k_CatchSeconds 동안 포물선(arc 높이)으로 날며 배율이 endScale배가 된 뒤 사라진다
        private IEnumerator Fly(Transform target, Vector3 to, float arc, float endScale, Action landed)
        {
            Vector3 from = target.position;
            Vector3 scale = target.localScale;

            if (target.TryGetComponent(out SpriteRenderer body))
            {
                body.sortingOrder = k_TopOrder - 3;
            }

            for (float t = 0f; t < k_CatchSeconds; t += Time.deltaTime)
            {
                float k = t / k_CatchSeconds;
                target.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * arc);
                target.localScale = scale * Mathf.Lerp(1f, endScale, k);
                yield return null;
            }

            Destroy(target.gameObject);
            landed?.Invoke();
        }

        // 잠깐 떠올라 사라지는 글 알약(드묾 · 전설)
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

        // 쿵: 쿵 칸(impact초 뒤)에 물보라와 함께 웜뱃 자리에서 고리가 쿵 반지름까지 퍼지며 사라진다(멈추는 범위를 보인다)
        private IEnumerator Ripple(float impact, Vector3 at, float radius, Vector3 splashAt)
        {
            yield return new WaitForSeconds(impact);
            Splash(splashAt, 2);
            SpriteRenderer ring = NewRenderer(transform, "Ripple", m_ring, k_WaterLight, k_RangeOrder);
            ring.transform.position = at;

            for (float t = 0f; t < k_RippleSeconds; t += Time.deltaTime)
            {
                float k = t / k_RippleSeconds;
                ring.transform.localScale = Vector3.one * radius * 2f * (1f - (1f - k) * (1f - k));
                ring.color = new Color(k_WaterLight.r, k_WaterLight.g, k_WaterLight.b, 1f - k * k);
                yield return null;
            }

            Destroy(ring.gameObject);
        }

        private void Splash(Vector3 at, int strength)
        {
            StartCoroutine(Fx.Burst(transform, m_square, at, 6 * strength, 1.2f * strength, 2.4f, 9f, 0.45f, 4, k_WaterLight, k_TopOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, 6 * strength, 1.2f * strength, 2.4f, 9f, 0.45f, 4, k_Water, k_TopOrder));
        }

        // ---------- 조각 ----------

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

        private static string BigNumberText(double value)
        {
            return value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }

        // 설계 45: 사거리 반원 고리(한 변 1유닛, 가운데 피벗, 위쪽만). 대는 말뚝 위쪽만 본다
        // 쿵 물결 고리: 테두리만(가운데는 비움)
        private static Sprite NewRing()
        {
            const int k_Size = 128;
            const float k_Inner = 0.9f;
            Texture2D texture = new Texture2D(k_Size, k_Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Color32[] pixels = new Color32[k_Size * k_Size];

            for (int y = 0; y < k_Size; y++)
            {
                for (int x = 0; x < k_Size; x++)
                {
                    float dx = (x + 0.5f) / k_Size * 2f - 1f;
                    float dy = (y + 0.5f) / k_Size * 2f - 1f;
                    float d = dx * dx + dy * dy;
                    pixels[y * k_Size + x] = d <= 1f && d >= k_Inner * k_Inner ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, k_Size, k_Size), new Vector2(0.5f, 0.5f), k_Size);
        }

        private static Sprite NewHalfRing()
        {
            const int k_Size = 128;
            const float k_Inner = 0.94f;
            Texture2D texture = new Texture2D(k_Size, k_Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Color32[] pixels = new Color32[k_Size * k_Size];

            for (int y = 0; y < k_Size; y++)
            {
                for (int x = 0; x < k_Size; x++)
                {
                    float dx = (x + 0.5f) / k_Size * 2f - 1f;
                    float dy = (y + 0.5f) / k_Size * 2f - 1f;
                    float d = dx * dx + dy * dy;
                    bool edge = d >= k_Inner * k_Inner || y == k_Size / 2;
                    pixels[y * k_Size + x] = d <= 1f && dy >= 0f ? new Color32(255, 255, 255, (byte)(edge ? 255 : 90)) : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, k_Size, k_Size), new Vector2(0.5f, 0.5f), k_Size);
        }
    }
}
