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
    // 설계 11 · 설계 18: 굴 밖 광장. 굴 그림은 빵집과 같은 BurrowPainter로 칠하고, 빵집 문(아치·차양·간판)·지상 계단은 Core PlazaLayout이 정한 자리에 놓는다.
    // 장식은 Core PlazaArea.Decor를 객체 키로 맞춘다(놓이면 만들고 옮기면 옮기고 치우면 지운다). 웜뱃이 문 앞에 서면(대상이 문) 문이 한 번 튄다
    public sealed class PlazaView : MonoBehaviour
    {
        private const string k_SignKey = "sign_bakery";

        [Tooltip("굴 그림(실행 중 생성)")]
        [SerializeField] private SpriteRenderer m_burrow;
        [Tooltip("바닥·벽·윗벽 띠 재료(빵집과 같은 것)")]
        [SerializeField] private Texture2D m_floorTile;
        [SerializeField] private Texture2D m_wallTile;
        [SerializeField] private Texture2D m_wallFace;
        [Tooltip("빵집 문(아치 + 차양 + 간판). 원점 = 구멍 밑변 가운데")]
        [SerializeField] private Transform m_door;
        [SerializeField] private TextMeshPro m_sign;
        [Tooltip("지상 계단. 원점 = 띠 밑변 가운데")]
        [SerializeField] private Transform m_stairs;
        [SerializeField] private WombatView m_wombat;

        private readonly Dictionary<DecorationData, GameObject> m_decor = new Dictionary<DecorationData, GameObject>();
        private PlazaArea m_plaza;
        private FrameCache m_frames;
        private GhostView m_ghost;
        private IDisposable[] m_subscriptions;

        public Transform Wombat => m_wombat.transform;

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
            m_ghost = GhostView.Create(transform);
            Build();

            m_wombat.Bind(plaza, transform, frames);
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
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
            }
        }

        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area == m_plaza && m_plaza.Target is PassageInteractable)
            {
                StartCoroutine(Fx.Bounce(m_door));
            }
        }
    }
}
