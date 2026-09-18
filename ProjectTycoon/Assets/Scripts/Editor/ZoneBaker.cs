using UnityEngine;
using UnityEditor;
using ZooTycoon.World;

namespace ZooTycoon.Editor
{
    // Zone.prefab의 자식을 통째로 다시 굽는다: 울타리 29칸(앞·뒤 6, 왼·오 8, 뒤 모서리 기둥) + 흙↔길 경계선 28조각. 존 6×8, 로컬 논리 (-3,-4)~(3,4)
    public static class ZoneBaker
    {
        const int k_EdgeOrder = -950;

        [MenuItem("ZooTycoon/Bake/Zone")]
        public static void Bake()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            Sprite fence = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/fence.png");
            Sprite post = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/fence_post.png");
            Sprite edge = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/edge.png");
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Zone.prefab");
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            Rect g = new Rect(-3f, -4f, 6f, 8f);
            int n = 0;
            for (float x = g.xMin; x < g.xMax; x += 1f)
            {
                Add(root.transform, "FenceFront" + n, new Vector2(x, g.yMin), fence, false, 0);
                Add(root.transform, "FenceBack" + n, new Vector2(x, g.yMax), fence, false, 0);
                n++;
            }
            n = 0;
            for (float z = g.yMin; z < g.yMax; z += 1f)
            {
                Add(root.transform, "FenceLeft" + n, new Vector2(g.xMin, z), fence, true, 0);
                Add(root.transform, "FenceRight" + n, new Vector2(g.xMax, z), fence, true, 0);
                n++;
            }
            Add(root.transform, "FenceCornerPost", new Vector2(g.xMax, g.yMax), post, false, 0);

            n = 0;
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
