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
    // 설계 11 · 설계 18: 굴 밖 광장. 굴 그림은 빵집과 같은 BurrowPainter로 칠하고, 빵집 문(아치·차양·간판)·농장 문(아치·간판, 설계 25)·지상 계단은 Core PlazaLayout이 정한 자리에 놓는다.
    // 장식은 Core PlazaArea.Decor를 객체 키로 맞춘다(놓이면 만들고 옮기면 옮기고 치우면 지운다). 웜뱃이 문 앞에 서면(대상이 문) 그 문이 한 번 튄다
    public sealed class PlazaView : MonoBehaviour, IAreaView
    {
        private const string k_SignKey = "sign_bakery";
        private const string k_FarmSignKey = "sign_farm";
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
        // 설계 33: 행상 발 ↔ 접힌 수레 피벗 거리는 방향에 따라 이 타원 위. 옆은 손잡이 혹(피벗에서 1.4유닛)이 앞발에 닿게(좌판 worker 자리 dx와 같아
        // 오른쪽으로 끌고 들어와 손잡이 자리에 서면 수레가 좌판 자리에 멈춘다), 위아래는 끝면 손잡이가 앞발 높이에 오게
        private const float k_CartReachSide = 1.4f;
        private const float k_CartReachEnd = 0.5f;
        // 발자취를 남기는 최소 걸음 · 길을 훑는 걸음(유닛)
        private const float k_TrailStep = 0.02f;
        private const float k_LineStep = 0.05f;
        // 옆 ↔ 끝면 그림은 한쪽 성분이 다른 쪽의 이 배를 넘을 때만 바꾼다(모퉁이에서 떨리지 않게)
        private const float k_CartTurnRatio = 1.3f;
        // 그림 방향은 수레가 실제로 굴러간 방향 — 이만큼(유닛) 움직일 때마다 새로 잰다(짧은 꺾임에 흔들리지 않게)
        private const float k_CartHeadingStep = 0.12f;
        // 펼칠 때 세로로 늘었다 돌아오는 정도
        private const float k_CartStretch = 0.15f;

        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("바닥·벽·윗벽 띠 재료(빵집과 같은 것)")]
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [SerializeField] private Texture2D m_wallFace;
        [Tooltip("빵집 문(아치 + 차양 + 간판). 원점 = 구멍 밑변 가운데")]
        [SerializeField] private Transform m_door;
        [SerializeField] private TextMeshPro m_sign;
        [Tooltip("농장 문(아치 + 간판). 원점 = 구멍 밑변 가운데")]
        [SerializeField] private Transform m_farmDoor;
        [SerializeField] private TextMeshPro m_farmSign;
        [Tooltip("지상 계단. 원점 = 띠 밑변 가운데")]
        [SerializeField] private Transform m_stairs;
        [SerializeField] private WombatView m_wombat;
        [Tooltip("설계 29: 웜뱃 석상(자리는 Core 석상 기준점)")]
        [SerializeField] private Transform m_statue;
        [Tooltip("설계 31 · 32: 유물 수레 = 행상의 좌판. 행상이 밀고 다니다 Core 수레 기준점에 세우고 펼친다. 행상이 없으면 숨긴다")]
        [SerializeField] private SpriteRenderer m_relicCart;
        [Tooltip("접힘 옆(손잡이 오른쪽, 수레가 행상 오른쪽이면 뒤집는다) · 접힘 끝면(손잡이 아래 = 행상 쪽) · 펼침(좌판)")]
        [SerializeField] private Sprite m_cartFoldedSide;
        [SerializeField] private Sprite m_cartFoldedEnd;
        [SerializeField] private Sprite m_cartOpen;

        private readonly Dictionary<DecorationData, GameObject> m_decor = new Dictionary<DecorationData, GameObject>();
        private PlazaArea m_plaza;
        private FrameCache m_frames;
        private GhostView m_ghost;
        private bool m_editing;
        private IPlaced m_held;
        private IDisposable[] m_subscriptions;
        private Sprite m_square;
        private float m_glow;
        // 설계 33: 행상이 지나온 길(올 때 끌려오는 수레가 따라간다) · 지금 길 위 거리 · 마지막으로 길 위에 둔 자리(펼칠 때 여기서 좌판 자리로)
        // 행상이 지나온 길(올 때) · 수레가 훑는 길(행상 발에서 멀어지는 쪽으로)
        private readonly List<Vector2> m_trail = new List<Vector2>();
        private readonly List<Vector2> m_line = new List<Vector2>();
        private Vector2 m_cartPos;
        // 행상 → 수레 방향 · 지금 옆 그림인가
        private Vector2 m_cartDir = Vector2.up;
        private bool m_cartSide;
        // 수레가 굴러가는 방향 · 그 방향을 잰 마지막 자리
        private Vector2 m_cartHeading = Vector2.down;
        private Vector2 m_cartTrack;
        private bool m_cartTracking;
        // 설계 31: 계단에 걸리는 유물(풍경)
        private RelicProps m_relicProps;

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
            m_door.localPosition = new Vector3(layout.DoorFloor.X, wallBottom, 0f);
            m_stairs.localPosition = new Vector3(layout.StairsFloor.X, wallBottom, 0f);
            m_sign.text = tables.Text(k_SignKey);
            m_farmDoor.localPosition = new Vector3(layout.FarmDoorFloor.X, wallBottom, 0f);
            m_farmSign.text = tables.Text(k_FarmSignKey);
            m_ghost = GhostView.Create(transform);
            m_statue.localPosition = new Vector3(plaza.Statue.Position.X, plaza.Statue.Position.Y, 0f);
            DrawCart();
            m_relicProps = new RelicProps(this, plaza.Wombat.Worker.Wallet.Relics,
                anchor => anchor == RelicTable.k_AnchorPlazaStairs ? new[] { layout.StairsFloor } : new System.Numerics.Vector2[0],
                p => transform.position + new Vector3(p.X, p.Y, 0f));
            m_relicProps.Rebuild();
            Build();

            m_wombat.Bind(plaza, transform, frames);
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.BlessingChanged>(Bus_BlessingChanged),
                bus.Subscribe<Events.RelicsChanged>(Bus_RelicsChanged),
                bus.Subscribe<Events.MerchantChanged>(Bus_MerchantChanged),
                bus.Subscribe<Events.PlazaVisitorArrived>(_ => m_relicProps.Pop(BlessingTable.k_Visitors)),
            };
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
                StartCoroutine(Fx.Bounce(passage.To == FarmArea.k_Id ? m_farmDoor : m_door));
            }

            if (e.Area == m_plaza && m_plaza.Target is StatueInteractable)
            {
                StartCoroutine(Fx.Bounce(m_statue));
            }

            if (e.Area == m_plaza && m_plaza.Target is RelicCartInteractable cart && cart.IsOpen)
            {
                StartCoroutine(Fx.Bounce(m_relicCart.transform));
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

            DrawCart();

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

        // 설계 31: 유물을 뽑으면 수레가 톡 튀고 반짝 알갱이가 튄다. 칸이 바뀌면 계단의 유물을 다시 건다
        private void Bus_RelicsChanged(Events.RelicsChanged e)
        {
            m_relicProps.Rebuild();

            if (e.Change == RelicChange.Drawn && isActiveAndEnabled)
            {
                Sparkle(m_relicCart.transform);
            }
        }

        // 설계 31: 행상이 좌판을 다 펴면 수레가 튀며 반짝
        private void Bus_MerchantChanged(Events.MerchantChanged e)
        {
            if (e.Merchant.IsOpen && isActiveAndEnabled)
            {
                Sparkle(m_relicCart.transform);
            }
        }

        // 설계 32 · 33: 수레는 행상 단계를 매 프레임 그대로 그리고, 움직일 땐 늘 행상이 걷는 길 위에 있다 — 행상 발에서 그 방향의 타원 거리만큼
        // 떨어진 길 위 첫 점(올 때는 지나온 길, 갈 때는 남은 길). 거리가 방향에 따라 이어서 바뀌어 모퉁이에서도 떨지 않고, 곧은 길에서는 정확히 그 거리.
        // 처음엔 계단 안쪽 길을 이어 붙여 계단에서 굴러 나오고, 갈 때는 길 끝 너머 계단 안으로 들어간다. 세운 동안은 좌판 자리.
        // 그림은 방향에 따라 옆(손잡이를 행상 쪽으로 뒤집기) · 끝면(경계에서 떨지 않게 넘어설 때만 바꿈), 계단 바닥선 위(계단 안)에서는 흐려진다.
        // 펼침 앞 반은 마지막 자리에서 좌판 자리로 마저 가고 뒤 반은 펼친 그림으로 바꿔 세로로 늘었다 돌아온다. 접음은 그 반대. 엶 동안은 크기를 건드리지 않는다(튀기 연출)
        private void DrawCart()
        {
            RelicMerchant merchant = m_plaza.Merchant;
            bool shown = merchant.Phase != MerchantPhase.Away;

            if (m_relicCart.gameObject.activeSelf != shown)
            {
                m_relicCart.gameObject.SetActive(shown);
            }

            if (!shown)
            {
                m_trail.Clear();
                m_cartDir = Vector2.up;
                m_cartSide = false;
                m_cartHeading = Vector2.down;
                m_cartTracking = false;
                return;
            }

            PlazaVisitor figure = merchant.Figure;
            Vector2 feet = V(figure.Position);
            Vector2 stall = V(m_plaza.RelicCart.Position);
            bool moving = merchant.Phase == MerchantPhase.Coming || merchant.Phase == MerchantPhase.Leaving;

            if (moving)
            {
                m_line.Clear();
                m_line.Add(feet);

                if (merchant.Phase == MerchantPhase.Coming)
                {
                    Remember(feet);

                    for (int i = m_trail.Count - 1; i >= 0; i--)
                    {
                        m_line.Add(m_trail[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < figure.PathLeft; i++)
                    {
                        m_line.Add(V(figure.PathPoint(i)));
                    }

                    m_line.Add(m_line[m_line.Count - 1] + Vector2.up * (k_CartReachSide + k_CartReachEnd));
                }

                m_cartPos = Tether(feet);
                Vector2 away = m_cartPos - feet;

                if (away.sqrMagnitude > 1e-6f)
                {
                    m_cartDir = away.normalized;
                }

                if (!m_cartTracking)
                {
                    m_cartTrack = m_cartPos;
                    m_cartTracking = true;
                }

                Vector2 rolled = m_cartPos - m_cartTrack;

                if (rolled.magnitude >= k_CartHeadingStep)
                {
                    m_cartHeading = rolled.normalized;
                    m_cartTrack = m_cartPos;
                }
            }
            else
            {
                // 세워 둔 동안은 옆으로 놓여 있고, 밀고 나갈 때는 이 자리에서 다시 잰다
                m_cartHeading = Vector2.left;
                m_cartTracking = false;
            }

            Vector2 look = moving ? m_cartHeading : Vector2.left;

            if (m_cartSide ? Mathf.Abs(look.y) > Mathf.Abs(look.x) * k_CartTurnRatio : Mathf.Abs(look.x) > Mathf.Abs(look.y) * k_CartTurnRatio)
            {
                m_cartSide = !m_cartSide;
            }

            float t = merchant.Phase == MerchantPhase.Unpacking ? 1f - (float)(merchant.SetupLeft / merchant.SetupSeconds)
                : merchant.Phase == MerchantPhase.Packing ? (float)(merchant.SetupLeft / merchant.SetupSeconds)
                : merchant.IsOpen ? 1f : 0f;
            float unfold = Mathf.Clamp01(t * 2f - 1f);
            Vector2 at = moving ? m_cartPos
                : merchant.Phase == MerchantPhase.Unpacking ? Vector2.Lerp(m_cartPos, stall, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2f)))
                : stall;
            m_relicCart.transform.localPosition = new Vector3(at.x, at.y, 0f);
            m_relicCart.sprite = unfold > 0f ? m_cartOpen : m_cartSide ? m_cartFoldedSide : m_cartFoldedEnd;
            // 옆 그림은 손잡이가 오른쪽(펼친 좌판과 같다) → 끌거나 밀 때 수레가 행상 오른쪽에 있으면 뒤집는다. 세워 둔 동안은 그대로
            m_relicCart.flipX = moving && m_cartSide && m_cartDir.x > 0f;
            Vector2 floor = V(m_plaza.Layout.StairsFloor);
            Vector2 inside = V(m_plaza.Layout.StairsInside);
            m_relicCart.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((at.y - floor.y) / (inside.y - floor.y)));

            if (!merchant.IsOpen)
            {
                float stretch = Mathf.Sin(unfold * Mathf.PI) * k_CartStretch;
                m_relicCart.transform.localScale = new Vector3(1f - stretch * 0.5f, 1f + stretch, 1f);
            }
        }

        // 그 방향의 행상 발 ↔ 수레 거리(타원)
        private static float Reach(Vector2 direction)
        {
            float x = direction.x / k_CartReachSide;
            float y = direction.y / k_CartReachEnd;
            return 1f / Mathf.Sqrt(x * x + y * y);
        }

        // 발자취: 처음에는 계단 안쪽에서 바닥으로 내려오는 길을 깔아 수레가 계단 속에서 굴러 나오게
        private void Remember(Vector2 feet)
        {
            if (m_trail.Count == 0)
            {
                Vector2 inside = V(m_plaza.Layout.StairsInside);
                m_trail.Add(inside + Vector2.up * (k_CartReachSide + k_CartReachEnd));
                m_trail.Add(inside);
            }

            if (Vector2.Distance(m_trail[m_trail.Count - 1], feet) >= k_TrailStep)
            {
                m_trail.Add(feet);
            }
        }

        // m_line(행상 발에서 멀어지는 길)을 훑어 발과의 거리가 그 방향의 타원 거리에 처음 닿는 점. 길이 모자라면 끝점
        private Vector2 Tether(Vector2 feet)
        {
            Vector2 last = feet;
            float lastGap = -Reach(m_cartDir);

            for (int i = 1; i < m_line.Count; i++)
            {
                Vector2 from = m_line[i - 1];
                Vector2 to = m_line[i];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / k_LineStep));

                for (int k = 1; k <= steps; k++)
                {
                    Vector2 p = Vector2.Lerp(from, to, (float)k / steps);
                    Vector2 away = p - feet;
                    float gap = away.magnitude - (away.sqrMagnitude > 1e-6f ? Reach(away.normalized) : Reach(m_cartDir));

                    if (gap >= 0f)
                    {
                        return Vector2.Lerp(last, p, lastGap / (lastGap - gap));
                    }

                    last = p;
                    lastGap = gap;
                }
            }

            return last;
        }

        private static Vector2 V(System.Numerics.Vector2 p)
        {
            return new Vector2(p.X, p.Y);
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
