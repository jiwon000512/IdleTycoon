using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 44: 낚시터 화면. 굴 그림은 광장처럼 고정 방(BurrowPainter), 구멍 아치(광장으로)는 첫 줄 가운데.
    // 물길은 아트방 「흙 도랑」을 굴 그림처럼 칸마다 칠하고(StreamPainter) 끝에 바닥 구멍. 물고기 · 말뚝 · 대 · 사거리 원은 그림이 올 때까지 코드로 그린다(네모 · 타원 · 고리).
    // 말뚝은 아트방 「나무 말뚝」(발끝 피벗, 대는 윗면 구멍에 꽂는다). 물고기 · 말뚝은 매 프레임 Core에서 읽어 맞춘다.
    // 팻말(물때 · 놓침)과 값 표식(소환 · 열기) · 「!!」은 값 표식 알약(MarkerView). 낚으면 재료 팝업, 쿵 · 털썩은 웜뱃 짧은 동작
    public sealed class FishingView : MonoBehaviour, IAreaView
    {
        private const string k_PopupKey = "harvest_popup";
        // 그림 층: 굴(−2000) 위에 물, 그 위에 물고기 · 사거리 원, 팝업 · 알갱이는 맨 위
        private const int k_WaterOrder = -1985;
        private const int k_FishOrder = -1980;
        private const int k_RangeOrder = -1975;
        private const int k_TopOrder = 1000;
        private static readonly Color k_Water = new Color32(0x3E, 0x7F, 0x95, 255);
        private static readonly Color k_WaterLight = new Color32(0xDD, 0xF1, 0xF5, 255);
        private static readonly Color k_Shadow = new Color32(0x24, 0x50, 0x5E, 220);
        private static readonly Color k_Trophy = new Color32(0xC9, 0xA2, 0x3A, 230);
        private static readonly Color k_Star = new Color32(0xF2, 0xC1, 0x4E, 255);
        private static readonly Color k_Range = new Color32(0xF6, 0xE3, 0xCC, 70);
        private static readonly Color k_BarBack = new Color32(0x2A, 0x1F, 0x17, 200);
        private static readonly Color k_BarFill = new Color32(0xF6, 0xE3, 0xCC, 255);
        // 계열 색(그림이 올 때까지 막대 색)
        private static readonly Dictionary<string, Color> k_FamilyColors = new Dictionary<string, Color>
        {
            { RodTable.k_Bamboo, new Color32(0x7F, 0xB3, 0x6A, 255) },
            { RodTable.k_Iron, new Color32(0x9A, 0xA6, 0xB2, 255) },
            { RodTable.k_Bait, new Color32(0xE2, 0x57, 0x4C, 255) },
        };
        private static readonly Color k_SpecialColor = new Color32(0xF2, 0xC1, 0x4E, 255);
        // 크기[작은 · 보통 · 큰] · 대물 물고기 길이(유닛)
        private static readonly float[] k_FishLengths = { 0.45f, 0.6f, 0.8f };
        private const float k_BossLength = 1.8f;
        private const float k_RodLength = 0.9f;
        private const float k_RodWidth = 0.06f;
        private const float k_RodLean = 20f;
        private const float k_RodBent = 55f;
        // 말뚝 윗면 구멍(대를 꽂는 자리): 발끝에서 위로(17칸, 아트방 make_stakes.py)
        private const float k_RodSocket = 0.425f;
        private const float k_PopupHeight = 1.2f;
        private const float k_TagRise = 1.3f;
        // 값 표식 알약(굴 파기 표식 크기)을 줄여 쓰는 배율: 「!!」 · 팻말 · 값
        private const float k_AlertScale = 0.45f;
        private const float k_SignScale = 0.7f;

        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("바닥·벽·윗벽 띠 재료(빵집과 같은 것)")]
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [SerializeField] private Texture2D m_wallFace;
        [Tooltip("설계 44: 물길 재료(아트방 「흙 도랑」, 굴 재료처럼 픽셀을 읽는다) · 바닥 구멍 「흙 구멍」")]
        [SerializeField] private Texture2D m_waterTile;
        [SerializeField] private Texture2D m_streamFace;
        [SerializeField] private Sprite m_drainHole;
        [Tooltip("설계 44: 말뚝 · 잠긴 말뚝(아트방 「나무 말뚝」, 발끝 피벗)")]
        [SerializeField] private Sprite m_stake;
        [SerializeField] private Sprite m_stakeLocked;
        [Tooltip("구멍 아치(나가기 대상이면 튄다)")]
        [SerializeField] private Transform m_arch;
        [Tooltip("점원 오두막(그림이 올 때까지 농장 작업대)")]
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
        private Sprite m_ellipse;
        private Sprite m_ring;
        private MarkerView m_waveSign;
        private MarkerView m_missedSign;
        private MarkerView m_priceTag;
        private Rod m_carried;
        private SpriteRenderer m_catch;
        private Rect m_bounds;

        // 물고기 하나: 그림자 · 감기 막대(뒤 · 앞)
        private sealed class FishArt
        {
            public SpriteRenderer Body;
            public SpriteRenderer BarBack;
            public SpriteRenderer BarFill;
            public float Length;
        }

        // 말뚝 하나: 기둥 · 대 · 등급 별 셋 · 「!!」
        private sealed class StakeArt
        {
            public StakeInteractable Stake;
            public SpriteRenderer Post;
            public Rod Rod;
            public SpriteRenderer[] Stars;
            public MarkerView Alert;
        }

        // 막대 하나(기둥 위에서 기울어진 대)
        private sealed class Rod
        {
            public Transform Pivot;
            public SpriteRenderer Stick;
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
            m_ellipse = NewEllipse();
            m_ring = NewRing();
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
            m_waveSign = NewTag(ToWorld(new System.Numerics.Vector2(-3.6f, -1.9f)), k_SignScale);
            System.Numerics.Vector2 drain = fishing.Layout.Stream[fishing.Layout.Stream.Count - 1];
            m_missedSign = NewTag(ToWorld(drain + new System.Numerics.Vector2(0f, -1.0f)), k_SignScale);
            m_priceTag = NewTag(Vector3.zero, k_SignScale);
            m_priceTag.gameObject.SetActive(false);
            m_carried = NewRod(transform, "Carried");
            // 튀기(Fx.Bounce)는 배율을 1로 되돌리므로 늘인 그림은 한 겹 안에 둔다
            Transform catchHolder = new GameObject("BossCatch").transform;
            catchHolder.SetParent(transform, false);
            catchHolder.localPosition = new Vector3(fishing.Catch.Position.X, fishing.Catch.Position.Y, 0f);
            m_catch = NewRenderer(catchHolder, "Body", m_ellipse, k_Trophy, 0);
            m_catch.transform.localScale = new Vector3(k_BossLength, k_BossLength * 0.45f, 1f);
            m_wombat.Bind(fishing, transform, frames);

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.FishCaught>(Bus_FishCaught),
                bus.Subscribe<Events.Thumped>(Bus_Thumped),
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

            SyncFish();
            SyncStakes();
            SyncRanges();
            m_waveSign.Show(m_fishing.BossOut ? m_tables.Text("fishing_boss") : m_tables.Format("fishing_wave", Math.Max(1, m_fishing.Wave), m_fishing.Config.BossEvery));
            m_missedSign.Show(m_tables.Format("fishing_missed", m_fishing.Missed));

            RodTable carried = m_fishing.Carried;
            m_carried.Pivot.gameObject.SetActive(carried != null);

            if (carried != null)
            {
                m_carried.Pivot.position = m_wombat.transform.position + new Vector3(0f, 1.15f, 0f);
                m_carried.Pivot.localRotation = Quaternion.Euler(0f, 0f, 90f);
                m_carried.Stick.color = ColorOf(carried);
            }

            m_catch.transform.parent.gameObject.SetActive(m_fishing.Choice != null);
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

        // 물길(굴 그림과 같은 원점 · 크기) 위에 바닥 구멍
        private void BuildStream()
        {
            BurrowShape.Result shape = m_fishing.Layout.Shape;
            SpriteRenderer stream = NewRenderer(transform, "Stream", StreamPainter.Paint(shape, m_fishing.Layout, m_waterTile, m_streamFace), Color.white, k_WaterOrder);
            stream.transform.localPosition = m_burrow.transform.localPosition;
            SpriteRenderer drain = NewRenderer(transform, "Drain", m_drainHole, Color.white, k_WaterOrder + 1);
            drain.transform.localPosition = StreamPainter.HoleCenter(m_fishing.Layout, m_drainHole);
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

                Vector3 at = ToWorld(m_fishing.Layout.PointAt(fish.S));
                art.Body.transform.position = at;
                bool reeling = fish.Reeled > 0d && !fish.Trophy;
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
                Destroy(m_fish[gone].Body.gameObject);
                Destroy(m_fish[gone].BarBack.gameObject);
                Destroy(m_fish[gone].BarFill.gameObject);
                m_fish.Remove(gone);
            }
        }

        private FishArt NewFish(Fish fish)
        {
            float length = fish.Boss ? k_BossLength : k_FishLengths[Mathf.Clamp(fish.Size, 0, k_FishLengths.Length - 1)];
            SpriteRenderer body = NewRenderer(transform, "Fish", m_ellipse, fish.Trophy ? k_Trophy : k_Shadow, k_FishOrder);
            body.transform.localScale = new Vector3(length, length * 0.4f, 1f);
            SpriteRenderer back = NewRenderer(transform, "ReelBack", m_square, k_BarBack, k_TopOrder - 2);
            back.transform.localScale = new Vector3(length, 0.1f, 1f);
            SpriteRenderer fill = NewRenderer(transform, "ReelFill", m_square, k_BarFill, k_TopOrder - 1);
            back.gameObject.SetActive(false);
            fill.gameObject.SetActive(false);
            return new FishArt { Body = body, BarBack = back, BarFill = fill, Length = length };
        }

        // ---------- 말뚝 · 대 ----------

        private void BuildStakes()
        {
            foreach (StakeInteractable stake in m_fishing.Stakes)
            {
                Transform holder = new GameObject("Stake" + stake.Index).transform;
                holder.SetParent(transform, false);
                holder.localPosition = new Vector3(stake.Position.X, stake.Position.Y, 0f);
                SpriteRenderer post = NewRenderer(holder, "Post", m_stake, Color.white, 0);
                post.spriteSortPoint = SpriteSortPoint.Pivot;
                Rod rod = NewRod(holder, "Rod");
                rod.Pivot.localPosition = new Vector3(0f, k_RodSocket, 0f);
                SpriteRenderer[] stars = new SpriteRenderer[3];

                for (int i = 0; i < stars.Length; i++)
                {
                    stars[i] = NewRenderer(holder, "Star", m_square, k_Star, 1);
                    stars[i].transform.localPosition = new Vector3((i - 1) * 0.12f, -0.15f, 0f);
                    stars[i].transform.localScale = new Vector3(0.08f, 0.08f, 1f);
                }

                MarkerView alert = NewTag(ToWorld(stake.Position) + Vector3.up * k_TagRise, k_AlertScale);
                alert.Show("!!");
                alert.gameObject.SetActive(false);
                m_stakes.Add(new StakeArt { Stake = stake, Post = post, Rod = rod, Stars = stars, Alert = alert });
            }
        }

        private void SyncStakes()
        {
            foreach (StakeArt art in m_stakes)
            {
                StakeInteractable stake = art.Stake;
                art.Post.sprite = stake.Open ? m_stake : m_stakeLocked;
                art.Rod.Pivot.gameObject.SetActive(stake.Rod != null);

                if (stake.Rod != null)
                {
                    art.Rod.Stick.color = ColorOf(stake.Rod);
                    art.Rod.Pivot.localRotation = Quaternion.Euler(0f, 0f, stake.Hooked != null ? -k_RodBent : -k_RodLean);
                }

                for (int i = 0; i < art.Stars.Length; i++)
                {
                    art.Stars[i].enabled = stake.Rod != null && !stake.Rod.IsSpecial && i < stake.Grade;
                }

                if (art.Alert.gameObject.activeSelf != (stake.Hooked != null))
                {
                    art.Alert.gameObject.SetActive(stake.Hooked != null);

                    if (stake.Hooked != null)
                    {
                        art.Alert.Bounce();
                    }
                }
            }
        }

        // 들고 있으면 열린 말뚝마다 그 대의 사거리, 아니면 대상 말뚝의 사거리
        private void SyncRanges()
        {
            List<(StakeInteractable stake, float range)> shown = new List<(StakeInteractable, float)>();

            if (m_fishing.WombatPresent && m_fishing.Carried != null)
            {
                foreach (StakeInteractable stake in m_fishing.Stakes.Where(s => s.Open && s.Rod == null))
                {
                    shown.Add((stake, (float)m_fishing.Carried.Range));
                }
            }
            else if (m_fishing.Target is StakeInteractable target && target.Rod != null)
            {
                shown.Add((target, m_fishing.RangeOf(target)));
            }

            while (m_ranges.Count < shown.Count)
            {
                m_ranges.Add(NewRenderer(transform, "Range", m_ring, k_Range, k_RangeOrder));
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

        private Rod NewRod(Transform parent, string name)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            SpriteRenderer stick = NewRenderer(pivot, "Stick", m_square, Color.white, 1);
            stick.transform.localPosition = new Vector3(0f, k_RodLength / 2f, 0f);
            stick.transform.localScale = new Vector3(k_RodWidth, k_RodLength, 1f);
            return new Rod { Pivot = pivot, Stick = stick };
        }

        private static Color ColorOf(RodTable rod)
        {
            return rod.IsSpecial ? k_SpecialColor : k_FamilyColors.TryGetValue(rod.Family, out Color color) ? color : Color.white;
        }

        // ---------- 사건 ----------

        // 대상 말뚝이 튀고, 비었으면 소환 값 · 잠겼으면 열기 값을 그 위에
        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area != m_fishing)
            {
                return;
            }

            m_priceTag.gameObject.SetActive(false);

            if (m_fishing.Target is StakeInteractable stake)
            {
                StartCoroutine(Fx.Bounce(m_stakes[stake.Index].Post.transform.parent));
                string price = !stake.Open ? m_tables.Format("tag_open_stake", BigNumberText(stake.Cost))
                    : stake.Rod == null && m_fishing.Carried == null ? m_tables.Format("tag_summon", BigNumberText(m_fishing.SummonPrice)) : null;

                if (price != null)
                {
                    m_priceTag.transform.position = ToWorld(stake.Position) + Vector3.up * k_TagRise;
                    m_priceTag.Show(price);
                }
            }
            else if (m_fishing.Target is BossCatchInteractable)
            {
                StartCoroutine(Fx.Bounce(m_catch.transform.parent));
            }
            else if (m_fishing.Target is PassageInteractable)
            {
                StartCoroutine(Fx.Bounce(m_arch));
            }
        }

        // 낚았다: 물보라 알갱이, 재료 팝업(감은 말뚝 위). 웜뱃이 털썩한 월척이면 웜뱃이 끌어올리는 동작
        private void Bus_FishCaught(Events.FishCaught e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            Vector3 at = m_fish.TryGetValue(e.Fish, out FishArt art) ? art.Body.transform.position : ToWorld(e.Stake.Position);
            Splash(at, e.Fish.Boss ? 3 : 1);
            CoinPopup popup = Instantiate(m_popupPrefab, ToWorld(e.Stake.Position) + Vector3.up * k_PopupHeight, Quaternion.identity, transform);
            popup.Show(m_tables.Format(k_PopupKey, e.Count), m_frames.Get(m_tables.Get<ItemTable>(e.Fish.Kind.Item).Icon)[0]);

            if (e.Fish.Trophy && m_fishing.WombatPresent && m_fishing.Wombat.Busy)
            {
                m_wombat.Act(0, 8, (float)m_fishing.Config.HaulSeconds);
            }
        }

        private void Bus_Thumped(Events.Thumped e)
        {
            if (e.Fishing != m_fishing)
            {
                return;
            }

            m_wombat.Act(8, 4, (float)m_fishing.Config.ThumpSeconds);
            Splash(ToWorld(m_fishing.Layout.NearestOnStream(e.At)), 2);
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

        private MarkerView NewTag(Vector3 at, float scale)
        {
            MarkerView tag = Instantiate(m_tagPrefab, at, Quaternion.identity, transform);
            tag.transform.localScale = Vector3.one * scale;

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

        // 한 변 1유닛 흰 타원 · 고리(그림이 올 때까지)
        private static Sprite NewEllipse()
        {
            const int k_Size = 32;
            Texture2D texture = new Texture2D(k_Size, k_Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Color32[] pixels = new Color32[k_Size * k_Size];

            for (int y = 0; y < k_Size; y++)
            {
                for (int x = 0; x < k_Size; x++)
                {
                    float dx = (x + 0.5f) / k_Size * 2f - 1f;
                    float dy = (y + 0.5f) / k_Size * 2f - 1f;
                    pixels[y * k_Size + x] = dx * dx + dy * dy <= 1f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, k_Size, k_Size), new Vector2(0.5f, 0.5f), k_Size);
        }

        private static Sprite NewRing()
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
                    pixels[y * k_Size + x] = d <= 1f ? new Color32(255, 255, 255, (byte)(d >= k_Inner * k_Inner ? 255 : 90)) : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, k_Size, k_Size), new Vector2(0.5f, 0.5f), k_Size);
        }
    }
}
