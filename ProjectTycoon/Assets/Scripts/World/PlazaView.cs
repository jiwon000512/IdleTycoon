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

        private readonly Dictionary<DecorationData, GameObject> m_decor = new Dictionary<DecorationData, GameObject>();
        private PlazaArea m_plaza;
        private FrameCache m_frames;
        private GhostView m_ghost;
        private bool m_editing;
        private IPlaced m_held;
        private IDisposable[] m_subscriptions;
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
            m_door.localPosition = new Vector3(layout.DoorFloor.X, wallBottom, 0f);
            m_stairs.localPosition = new Vector3(layout.StairsFloor.X, wallBottom, 0f);
            m_sign.text = tables.Text(k_SignKey);
            m_farmDoor.localPosition = new Vector3(layout.FarmDoorFloor.X, wallBottom, 0f);
            m_farmSign.text = tables.Text(k_FarmSignKey);
            m_ghost = GhostView.Create(transform);
            m_statue.localPosition = new Vector3(plaza.Statue.Position.X, plaza.Statue.Position.Y, 0f);
            Build();

            m_wombat.Bind(plaza, transform, frames);
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.BlessingChanged>(Bus_BlessingChanged),
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
        }

        private Vector3 StatueSparkPoint
        {
            get
            {
                Bounds body = m_statue.GetComponentInChildren<SpriteRenderer>().bounds;
                return new Vector3(m_statue.position.x, body.min.y + body.size.y * k_StatueSparkHeight, m_statue.position.z);
            }
        }

        private void Update()
        {
            if (m_plaza == null || m_plaza.Statue.Blessing.Active == null)
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

            StartCoroutine(Fx.Bounce(m_statue));
            m_square = m_square != null ? m_square : Fx.NewSquare();
            Vector3 at = StatueSparkPoint;
            StartCoroutine(Fx.Burst(transform, m_square, at, k_SparkCount, k_SparkSpread, k_SparkLift, k_SparkGravity, k_SparkSeconds, k_SparkCells, k_SparkLight, k_SparkOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, k_SparkCount, k_SparkSpread, k_SparkLift, k_SparkGravity, k_SparkSeconds, k_SparkCells, k_SparkGold, k_SparkOrder));
        }
    }
}
