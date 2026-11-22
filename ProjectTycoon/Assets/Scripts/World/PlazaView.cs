using System;
using System.Collections;
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
        [Tooltip("설계 59 아트방 bakery-closed(사용자 A 판자 덧댐): 닫힌 빵집 문 아치. 없으면 어둡게 칠한다")]
        [SerializeField] private Sprite m_bakeryClosed;
        private Sprite m_bakeryArch;
        [Tooltip("설계 65 냇물: 물결 네 칸 · 물 안쪽 윗면 흙 면(낚시터 것) · 둑 그림(큰 것 → 작은 것, 맞을 때마다 다음 그림)")]
        [SerializeField] private Texture2D[] m_waterTiles;
        [SerializeField] private Texture2D m_streamFace;
        [SerializeField] private Sprite[] m_damSprites;
        [Tooltip("설계 64: 파기 표식(빵집 · 농장과 같은 것) · 새 칸이 밝아지는 초")]
        [SerializeField] private MarkerView m_digTagPrefab;
        [SerializeField] private float m_digSeconds = 0.6f;
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
        // 설계 65 냇물: 물은 굴 그림 바로 위(낚시터와 같은 순서) · 물결 한 칸 초 · 둑이 터진 뒤 물이 한 줄 내려가는 초 · 물보라
        private const int k_WaterOrder = -1985;
        private const float k_WaterFrameSeconds = 0.35f;
        private const float k_FlowSeconds = 0.6f;
        private const int k_SplashCount = 14;
        private const float k_SplashSpread = 1.6f;
        private const float k_SplashLift = 3.5f;
        private const float k_SplashGravity = 12f;
        private const float k_SplashSeconds = 0.5f;
        private const int k_SplashCells = 10;
        private const int k_SplashOrder = 1000;
        private static readonly Color k_Splash = new Color32(184, 214, 220, 255);
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
        // 설계 62: 봉투 떨어지기(초 · 출발 높이 · 꽂히는 높이 · 좌우 흔들림 · 기울기 · 봉투 크기)
        private PoopViews m_poopViews;
        private DigView m_dig;
        private Sprite m_square;
        private float m_glow;
        private SpriteRenderer m_water;
        private SpriteMask m_waterMask;
        private Sprite[] m_waterFrames = new Sprite[0];
        private SpriteRenderer m_dam;
        // 지금 물이 보이는 아랫변(터진 뒤 냇물 끝까지 내려간다)
        private float m_flowBottom;
        private Coroutine m_flow;

        // 설계 64: 굴을 팠다(카메라 경계를 다시 잰다)
        public event Action Expanded;

        public Vector3 Origin => transform.position;
        public Transform Wombat => m_wombat.transform;
        public WombatView WombatView => m_wombat;

        // 굴 그림 둘레 + 흙 한 칸(설계 64: 판 칸이 늘면 넓어진다)
        public Rect Bounds
        {
            get
            {
                NavRect b = m_plaza.Layout.Bounds;
                Vector3 origin = transform.position;
                return new Rect(origin.x + b.XMin - 1f, origin.y + b.YMin - 1f, b.XMax - b.XMin + 2f, b.YMax - b.YMin + 2f);
            }
        }

        public void Bind(PlazaArea plaza, EventBus bus, FrameCache frames, TableSet tables)
        {
            m_plaza = plaza;
            m_frames = frames;
            PlazaLayout layout = plaza.Layout;
            CellMetrics cells = layout.Cells;
            m_dig = new DigView(this, m_digTagPrefab, tables, plaza.Grid, cell => transform.position + new Vector3(cells.CellCenter(cell).X, cells.CellCenter(cell).Y, 0f),
                new Vector2(cells.CellWidth, cells.CellHeight), m_digSeconds, cell => !plaza.InShaft(cell));
            m_square = Fx.NewSquare();
            m_water = NewRenderer("Water", k_WaterOrder);
            m_water.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            m_waterMask = new GameObject("WaterMask").AddComponent<SpriteMask>();
            m_waterMask.transform.SetParent(transform, false);
            m_waterMask.sprite = m_square;
            m_dam = NewRenderer("Dam", 0);
            m_dam.spriteSortPoint = SpriteSortPoint.Pivot;
            m_flowBottom = layout.Stream.YMin;
            Repaint();
            ShowDam();

            float wallBottom = -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit;
            m_stairs.localPosition = new Vector3(layout.StairsFloor.X, wallBottom, 0f);
            m_tables = tables;

            // 설계 64: 문 그림은 그 주머니 뒷벽 띠 밑변에
            foreach (PlazaDoor door in layout.Doors)
            {
                DoorArt art = Art(door.To);
                art.Root.localPosition = new Vector3(door.Floor.X, door.WallBottom, 0f);
                art.Sign.text = tables.Text(k_SignPrefix + door.To);
            }

            ShowDoors();

            SetSign(plaza.Wombat.Worker.Wallet.Stars.Count(BakeryArea.k_Id));
            m_ghost = GhostView.Create(transform);
            m_statue.localPosition = new Vector3(plaza.Statue.Position.X, plaza.Statue.Position.Y, 0f);
            m_statue.gameObject.SetActive(plaza.StatueOpen);
            m_poopViews = new PoopViews(this, plaza, bus, m_poopPrefab, m_poopFrames, m_poopFrameRate, m_popupPrefab, tables, frames, p => transform.position + new Vector3(p.X, p.Y, 0f));
            Build();

            m_wombat.Bind(plaza, transform, frames);
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.BlessingChanged>(Bus_BlessingChanged),
                bus.Subscribe<Events.StarsChanged>(Bus_StarsChanged),
                bus.Subscribe<Events.Dug>(Bus_Dug),
                bus.Subscribe<Events.DamStruck>(Bus_DamStruck),
            };
        }

        private SpriteRenderer NewRenderer(string name, int order)
        {
            SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sortingOrder = order;
            return renderer;
        }

        // 굴 모양은 Core가 계산해 둔 마스크(걷는 땅과 같은 것)를 칠한다
        private void Repaint()
        {
            BurrowShape.Result shape = m_plaza.Layout.Shape;
            Sprite old = m_burrow.sprite;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile, m_wallFace);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / BurrowShape.k_PixelsPerUnit, -shape.OriginY / BurrowShape.k_PixelsPerUnit, 0f);

            if (old != null)
            {
                Destroy(old.texture);
                Destroy(old);
            }

            // 설계 65: 냇물 = 굴 바닥 중 냇물 네모 안(나머지는 땅). 보이는 곳은 가리개(SetFlow)가 정한다
            NavRect s = m_plaza.Layout.Stream;
            const float far = 1000f;
            NavRect[] lands =
            {
                new NavRect(-far, -far, s.XMin, far), new NavRect(s.XMax, -far, far, far),
                new NavRect(-far, s.YMax, far, far), new NavRect(-far, -far, far, s.YMin),
            };

            foreach (Sprite frame in m_waterFrames)
            {
                Destroy(frame.texture);
                Destroy(frame);
            }

            m_waterFrames = Array.ConvertAll(WaterPainter.Pixels(shape, lands, new WaterPainter.Textures(m_waterTiles, m_streamFace)),
                pixels => BurrowPainter.ToSprite(pixels, shape.Width, shape.Height));
            m_water.transform.localPosition = m_burrow.transform.localPosition;
            m_water.sprite = m_waterFrames[0];
            SetFlow(m_flowBottom);
        }

        // 물이 보이는 곳: 샘 위에서 bottom까지(가리개 네모)
        private void SetFlow(float bottom)
        {
            m_flowBottom = bottom;
            NavRect s = m_plaza.Layout.Stream;
            float top = s.YMax + 1f;
            m_waterMask.transform.localPosition = new Vector3(0f, (top + bottom) / 2f, 0f);
            m_waterMask.transform.localScale = new Vector3(s.XMax - s.XMin + 1f, top - bottom, 1f);
        }

        // 둑 그림: 냇물 끝에, 맞은 만큼 작은 그림. 굴길을 다 팠으면 없다
        private void ShowDam()
        {
            DamInteractable dam = m_plaza.Dam;
            m_dam.enabled = dam != null;

            if (dam != null)
            {
                m_dam.sprite = m_damSprites[Mathf.Min(dam.Hits, m_damSprites.Length - 1)];
                m_dam.transform.localPosition = new Vector3(dam.Position.X, dam.Position.Y, 0f);
            }
        }

        // 설계 65: 둑을 쳤다 — 웜뱃이 흙을 던질 때 둑이 한 단계 작아지고 흙덩이가 튄다. 터지는 타는 Bus_Dug가 물을 흘린다
        private void Bus_DamStruck(Events.DamStruck e)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            StartCoroutine(Struck(m_wombat.Dig(), e.Dam.Left <= 0, m_dam.transform.position));
        }

        private IEnumerator Struck(float delay, bool burst, Vector3 at)
        {
            yield return new WaitForSeconds(delay);
            m_dig.Clods(at);

            if (burst)
            {
                StartCoroutine(Fx.Burst(transform, m_square, at, k_SplashCount, k_SplashSpread, k_SplashLift, k_SplashGravity, k_SplashSeconds, k_SplashCells, k_Splash, k_SplashOrder));
                yield break;
            }

            ShowDam();
            StartCoroutine(Fx.Bounce(m_dam.transform));
        }

        // 둑이 터졌다: 옛 둑이 사라지고 물이 새 냇물 끝까지 내려간 뒤 다음 둑이 보인다
        private IEnumerator Flow(float delay)
        {
            yield return new WaitForSeconds(delay);
            m_dam.enabled = false;
            float from = m_flowBottom;
            float to = m_plaza.Layout.Stream.YMin;

            for (float t = 0f; t < k_FlowSeconds; t += Time.deltaTime)
            {
                SetFlow(Mathf.Lerp(from, to, t / k_FlowSeconds));
                yield return null;
            }

            SetFlow(to);
            ShowDam();
            m_flow = null;

            if (m_dam.enabled)
            {
                Vector3 at = m_dam.transform.position;
                StartCoroutine(Fx.Burst(transform, m_square, at, k_SplashCount, k_SplashSpread, k_SplashLift, k_SplashGravity, k_SplashSeconds, k_SplashCells, k_Splash, k_SplashOrder));
                StartCoroutine(Fx.Bounce(m_dam.transform));
            }
        }

        // 설계 64: 굴 파기 연출 — 다시 칠하고 새 칸이 흙빛에서 밝아진다. 석상 주머니면 석상이 보인다
        private void Bus_Dug(Events.Dug e)
        {
            if (e.Grid != m_plaza.Grid)
            {
                return;
            }

            bool flows = m_plaza.Layout.Stream.YMin < m_flowBottom - 0.01f;
            Repaint();
            float delay = flows ? m_wombat.DigThrowSeconds : m_wombat.Dig();
            m_dig.Play(e.Cell, delay);

            if (flows && isActiveAndEnabled)
            {
                if (m_flow != null)
                {
                    StopCoroutine(m_flow);
                }

                m_flow = StartCoroutine(Flow(delay));
            }
            else if (m_flow == null)
            {
                SetFlow(m_plaza.Layout.Stream.YMin);
                ShowDam();
            }

            m_statue.gameObject.SetActive(m_plaza.StatueOpen);
            ShowDoors();
            Expanded?.Invoke();
        }

        // 설계 64: 문 그림은 그 주머니를 판 뒤에만(2026-10-28 플레이: 안 판 주머니 문이 보였다)
        private void ShowDoors()
        {
            foreach (PlazaDoor door in m_plaza.Layout.Doors)
            {
                Art(door.To).Root.gameObject.SetActive(m_plaza.Grid.Contains(door.Cell));
            }
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
            m_dig.Refresh(editing);

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

                    // 빵집 문은 어둡게 칠하는 대신 판자 덧댄 그림으로 막는다
                    if (gate.Shop.Id == BakeryArea.k_Id && m_bakeryClosed != null)
                    {
                        m_bakeryArch = Arch(gate.Shop.Id).sprite;
                        Arch(gate.Shop.Id).sprite = m_bakeryClosed;
                    }
                    else
                    {
                        Tint(Art(gate.Shop.Id).Root, k_Closed);
                    }
                }

                tag.Show(gate.Shop.GateLocked ? m_tables.Format("row_star_needed", gate.Shop.GateStar)
                    : gate.Shop.GateItem != null ? m_tables.Format("tag_open_item", m_tables.Get<ItemTable>(gate.Shop.GateItem).Name, gate.Shop.GateCount)
                    : m_tables.Format("tag_open_shop", gate.Shop.GateCost.ToString("0", System.Globalization.CultureInfo.InvariantCulture)));
                tag.gameObject.SetActive(m_plaza.Grid.Contains(m_plaza.Layout.DoorTo(gate.Shop.Id).Cell));
            }

            foreach (ShopGateInteractable gate in new List<ShopGateInteractable>(m_gateTags.Keys))
            {
                if (!m_plaza.Gates.Contains(gate))
                {
                    Destroy(m_gateTags[gate].gameObject);
                    m_gateTags.Remove(gate);
                    Transform door = Art(gate.Shop.Id).Root;

                    if (gate.Shop.Id == BakeryArea.k_Id && m_bakeryArch != null)
                    {
                        Arch(gate.Shop.Id).sprite = m_bakeryArch;
                    }

                    Tint(door, Color.white);
                    StartCoroutine(Fx.Bounce(door));
                }
            }
        }

        private SpriteRenderer Arch(string to)
        {
            return Art(to).Root.Find("Arch").GetComponent<SpriteRenderer>();
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
                m_dig.Refresh(m_editing);
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

            if (e.Area == m_plaza && m_plaza.Target is DigInteractable dig)
            {
                m_dig.Bounce(dig.Cell);
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

            if (m_waterFrames.Length > 0)
            {
                m_water.sprite = m_waterFrames[(int)(Time.time / k_WaterFrameSeconds) % m_waterFrames.Length];
            }

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
            Vector3 at = SparkPoint(thing);
            StartCoroutine(Fx.Burst(transform, m_square, at, k_SparkCount, k_SparkSpread, k_SparkLift, k_SparkGravity, k_SparkSeconds, k_SparkCells, k_SparkLight, k_SparkOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, k_SparkCount, k_SparkSpread, k_SparkLift, k_SparkGravity, k_SparkSeconds, k_SparkCells, k_SparkGold, k_SparkOrder));
        }
    }
}
