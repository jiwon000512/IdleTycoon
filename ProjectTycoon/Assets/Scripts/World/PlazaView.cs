using TMPro;
using UnityEngine;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 11: 굴 밖 광장. 굴 그림은 빵집과 같은 BurrowPainter로 칠하고, 빵집 문(아치·차양·간판)·지상 계단·장식은 Core PlazaLayout이 정한 자리에 놓기만 한다.
    // 웜뱃이 문 앞에 서면(대상이 문) 문이 한 번 튄다
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
        [SerializeField] private ShopWombat m_wombat;

        private PlazaSim m_plaza;

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

        public void Bind(PlazaSim plaza, FrameCache frames, TableSet tables)
        {
            m_plaza = plaza;
            PlazaLayout layout = plaza.Layout;
            BurrowShape.Result shape = layout.Shape;
            m_burrow.sprite = BurrowPainter.Paint(shape, m_floorTile, m_wallTile, m_wallFace);
            m_burrow.transform.localPosition = new Vector3(shape.OriginX / ShopLayout.k_PixelsPerUnit, -shape.OriginY / ShopLayout.k_PixelsPerUnit, 0f);

            float wallBottom = -BurrowShape.k_EntranceFloorTop / ShopLayout.k_PixelsPerUnit;
            m_door.localPosition = new Vector3(layout.DoorFloor.X, wallBottom, 0f);
            m_stairs.localPosition = new Vector3(layout.StairsFloor.X, wallBottom, 0f);
            m_sign.text = tables.Text(k_SignKey);

            foreach (PlazaDecor decor in layout.Decor)
            {
                GameObject go = new GameObject(decor.Decoration.Id);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(decor.Position.X, decor.Position.Y, 0f);
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = frames.Get(decor.Decoration.Sprite)[0];

                if (decor.Decoration.Frames > 0)
                {
                    Sprite[] loop = new Sprite[decor.Decoration.Frames];

                    for (int i = 0; i < loop.Length; i++)
                    {
                        loop[i] = frames.Get(decor.Decoration.Sprite + "_" + i)[0];
                    }

                    go.AddComponent<SpriteAnimator>().Play(renderer, loop, (float)decor.Decoration.FrameRate);
                }
            }

            m_wombat.Bind(plaza, transform, frames);
            m_plaza.TargetChanged += Plaza_TargetChanged;
        }

        private void OnDestroy()
        {
            if (m_plaza != null)
            {
                m_plaza.TargetChanged -= Plaza_TargetChanged;
            }
        }

        private void Plaza_TargetChanged()
        {
            if (m_plaza.Target.HasValue)
            {
                StartCoroutine(Fx.Bounce(m_door));
            }
        }
    }
}
