using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 18 편집 모드 하이라이트(C-2, 2026-09-26 사용자 선택): 몸체 스프라이트 사본 4장을 상하좌우 한 칸(2px = 0.025유닛) 밀어 몸체 뒤에 그리면
    // 바깥으로 삐져나온 테두리만 보인다. 사본은 EditOutline 셰이더로 실루엣을 따라 도는 점선이 된다. 크림 = 옮길 수 있음, 노랑 = 잡음
    public sealed class EditOutlineView : MonoBehaviour
    {
        private const float k_Step = 0.025f;
        private static readonly Color k_Free = new Color32(0xF4, 0xEE, 0xDC, 0xFF);
        private static readonly Color k_Held = new Color32(0xF2, 0xC9, 0x4C, 0xFF);
        private static Material s_material;

        private SpriteRenderer m_body;
        private SpriteRenderer[] m_copies;

        // 몸체 렌더러에 붙인다(이미 있으면 그것)
        public static EditOutlineView Attach(SpriteRenderer body)
        {
            if (!body.TryGetComponent(out EditOutlineView outline))
            {
                outline = body.gameObject.AddComponent<EditOutlineView>();
                outline.Build(body);
            }

            return outline;
        }

        public void Show(bool held)
        {
            Color color = held ? k_Held : k_Free;

            foreach (SpriteRenderer copy in m_copies)
            {
                copy.sprite = m_body.sprite;
                copy.color = color;
                copy.enabled = true;
            }
        }

        public void Hide()
        {
            foreach (SpriteRenderer copy in m_copies)
            {
                copy.enabled = false;
            }
        }

        private void Build(SpriteRenderer body)
        {
            m_body = body;
            Vector3[] offsets = { new Vector3(k_Step, 0f, 0f), new Vector3(-k_Step, 0f, 0f), new Vector3(0f, k_Step, 0f), new Vector3(0f, -k_Step, 0f) };
            m_copies = new SpriteRenderer[offsets.Length];

            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject go = new GameObject("Outline");
                go.transform.SetParent(body.transform, false);
                go.transform.localPosition = offsets[i];
                SpriteRenderer copy = go.AddComponent<SpriteRenderer>();
                copy.sharedMaterial = Material;
                copy.sortingLayerID = body.sortingLayerID;
                copy.sortingOrder = body.sortingOrder - 1;
                copy.spriteSortPoint = SpriteSortPoint.Pivot;
                copy.enabled = false;
                m_copies[i] = copy;
            }
        }

        private static Material Material
        {
            get
            {
                if (s_material == null)
                {
                    s_material = new Material(Shader.Find("ZooTycoon/EditOutline"));
                }

                return s_material;
            }
        }
    }
}
