using UnityEngine;
using UnityEditor;
using ZooTycoon.World;

namespace ZooTycoon.Editor
{
    // Zone.prefab의 자식을 통째로 다시 굽는다: 굴 입구 1장 + 흙↔길 경계선 28조각. 존 6×8, 로컬 논리 (-3,-4)~(3,4)
    // 2026-09-18: 울타리 → 굴(사업가 웜뱃). 굴 그림은 단계별 3장(burrow_1~3), 단계 기준은 미정이라 1단계를 굽는다
    public static class ZoneBaker
    {
        const int k_EdgeOrder = -950;
        const string k_BurrowPath = "Assets/Sprites/World/Burrow/burrow_1.png";

        [MenuItem("ZooTycoon/Bake/Zone")]
        public static void Bake()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            Sprite burrow = AssetDatabase.LoadAssetAtPath<Sprite>(k_BurrowPath);
            Sprite edge = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/edge.png");
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Zone.prefab");
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            Rect g = new Rect(-3f, -4f, 6f, 8f);

            // 굴 밑면은 2:1 마름모라 피벗(하단 중앙 = 밑면 앞 모서리)을 존 중심에서 폭의 1/4만큼 아래에 두면
            // 밑면 중심이 존 중심과 겹친다. 굴이 존보다 작아 둘레에 흙 바닥이 드러난다(굴 사이 간격)
            Add(root.transform, "Burrow", Vector2.zero, burrow, false, 0);
            root.transform.Find("Burrow").localPosition = new Vector3(0f, -burrow.bounds.size.x * 0.25f, 0f);

            int n = 0;
            for (float x = g.xMin; x < g.xMax; x += 1f)
            {
                Add(root.transform, "EdgeFront" + n, new Vector2(x, g.yMin), edge, false, k_EdgeOrder);
                Add(root.transform, "EdgeBack" + n, new Vector2(x, g.yMax), edge, false, k_EdgeOrder);
                n++;
            }
            for (float z = g.yMin; z < g.yMax; z += 1f)
            {
                Add(root.transform, "EdgeLeft" + n, new Vector2(g.xMin, z), edge, true, k_EdgeOrder);
                Add(root.transform, "EdgeRight" + n, new Vector2(g.xMax, z), edge, true, k_EdgeOrder);
                n++;
            }

            int count = root.transform.childCount;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Zone.prefab");
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.ImportAsset("Assets/Prefabs/Branch01.prefab", ImportAssetOptions.ForceUpdate);
            return "Zone children " + count;
        }

        static void Add(Transform parent, string name, Vector2 logical, Sprite sprite, bool flip, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Iso.ToScreen(logical);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.flipX = flip;
            r.sortingOrder = order;
            r.spriteSortPoint = SpriteSortPoint.Pivot;
        }
    }
}
