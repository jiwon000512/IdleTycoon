using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using ZooTycoon.World;

namespace ZooTycoon.Editor
{
    // 설계 07-5: Branch01.prefab의 내용을 통째로 교체한다(GUID 유지). 존 X 3열 × Z 3줄, 맵 26×32
    // 층: Backdrop(무한 잔디) / Grid(Ground·Floor·Path, 변형 타일 색인) / MapEdge(길↔잔디 경계선) / z01~z09 / Border(맵 밖 0.5~12.5칸 숲)
    public static class BranchBaker
    {
        const int k_Width = 26;
        const int k_Height = 32;
        const int k_Band = 12;
        const int k_Seed = 20260916;

        [MenuItem("ZooTycoon/Bake/Branch01")]
        public static void Bake()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            Tile[] grass = LoadTiles("Grass");
            Tile[] yard = LoadTiles("Yard");
            Tile[] path = LoadTiles("Path");
            Sprite edge = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/edge.png");
            Sprite period = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/Tiles/grass_period.png");
            Sprite[] kinds =
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/Border/tree_big.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/Border/tree_small.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/World/Border/bush.png"),
            };
            GameObject zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Zone.prefab");

            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Branch01.prefab");
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            Rect cameraArea = new Rect(-k_Band, -k_Band, k_Width + 2 * k_Band, k_Height + 2 * k_Band);

            // 무한 잔디: 화면 주기 3×1.5유닛 격자에 정렬한 Tiled 스프라이트
            Rect screen = Iso.ToScreenBounds(cameraArea);
            float x0 = 3f * Mathf.Floor((screen.xMin - 12f) / 3f);
            float y0 = -1.25f + 1.5f * Mathf.Floor((screen.yMin - 12f + 1.25f) / 1.5f);
            float x1 = 3f * Mathf.Ceil((screen.xMax + 12f) / 3f);
            float y1 = -1.25f + 1.5f * Mathf.Ceil((screen.yMax + 12f + 1.25f) / 1.5f);
            GameObject back = new GameObject("Backdrop");
            back.transform.SetParent(root.transform, false);
            back.transform.localPosition = new Vector3(x0, y0, 0f);
            SpriteRenderer br = back.AddComponent<SpriteRenderer>();
            br.sprite = period;
            br.drawMode = SpriteDrawMode.Tiled;
            br.tileMode = SpriteTileMode.Continuous;
            br.size = new Vector2(x1 - x0, y1 - y0);
            br.sortingOrder = -2100;

            GameObject gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform, false);
            Grid grid = gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            Tilemap ground = Layer(gridGo.transform, "Ground", -2000);
            Tilemap floor = Layer(gridGo.transform, "Floor", -1000);
            Tilemap pathMap = Layer(gridGo.transform, "Path", -1500);
            Fill(ground, grass, -k_Band - 2, -k_Band - 2, k_Width + 2 * k_Band + 4, k_Height + 2 * k_Band + 4);
            Fill(pathMap, path, 0, 0, k_Width, k_Height);

            GameObject mapEdge = new GameObject("MapEdge");
            mapEdge.transform.SetParent(root.transform, false);
            for (int x = 0; x < k_Width; x++)
            {
                AddSprite(mapEdge.transform, "EdgeFront" + x, new Vector2(x, 0), edge, false, -950);
                AddSprite(mapEdge.transform, "EdgeBack" + x, new Vector2(x, k_Height), edge, false, -950);
            }
            for (int z = 0; z < k_Height; z++)
            {
                AddSprite(mapEdge.transform, "EdgeLeft" + z, new Vector2(0, z), edge, true, -950);
                AddSprite(mapEdge.transform, "EdgeRight" + z, new Vector2(k_Width, z), edge, true, -950);
            }

            ZoneView[] zones = new ZoneView[9];
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int n = r * 3 + c;
                    int zx = 2 + c * 8;
                    int zz = 2 + r * 10;
                    Fill(floor, yard, zx, zz, 6, 8);
                    GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(zonePrefab, root.transform);
                    inst.name = "z" + (n + 1).ToString("00");
                    ZoneView z = inst.GetComponent<ZoneView>();
                    SerializedObject so = new SerializedObject(z);
                    so.FindProperty("m_center").vector2Value = new Vector2(zx + 3f, zz + 4f);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    zones[n] = z;
                }
            }

            // 숲: 1칸 격자 + ±0.35 흔들림. 맵에서 6칸 안은 85%, 그 밖은 55%. 입구 틈은 앞 가운데 x 11.5~14.5
            GameObject border = new GameObject("Border");
            border.transform.SetParent(root.transform, false);
            System.Random rng = new System.Random(k_Seed);
            int count = 0;
            for (int gx = -k_Band - 1; gx < k_Width + k_Band + 1; gx++)
            {
                for (int gz = -k_Band - 1; gz < k_Height + k_Band + 1; gz++)
                {
                    if (gx >= 0 && gx < k_Width && gz >= 0 && gz < k_Height)
                    {
                        continue;
                    }
                    float x = gx + 0.5f + (float)(rng.NextDouble() * 0.7 - 0.35);
                    float z = gz + 0.5f + (float)(rng.NextDouble() * 0.7 - 0.35);
                    double roll = rng.NextDouble();
                    bool inBand = x >= -k_Band - 0.5f && x <= k_Width + k_Band + 0.5f && z >= -k_Band - 0.5f && z <= k_Height + k_Band + 0.5f
                        && !(x > -0.5f && x < k_Width + 0.5f && z > -0.5f && z < k_Height + 0.5f);
                    float depth = Mathf.Max(Mathf.Max(-x, x - k_Width), Mathf.Max(-z, z - k_Height));
                    if (!inBand || roll < (depth <= 6f ? 0.15 : 0.45))
                    {
                        continue;
                    }
                    if (z < 0f && x > 11.5f && x < 14.5f)
                    {
                        continue;
                    }
                    double t = rng.NextDouble();
                    Sprite sprite = t < 0.5 ? kinds[0] : t < 0.8 ? kinds[1] : kinds[2];
                    AddSprite(border.transform, sprite.name + "_" + count, new Vector2(x, z), sprite, rng.NextDouble() < 0.5, 0);
                    count++;
                }
            }

            SerializedObject bso = new SerializedObject(root.GetComponent<BranchView>());
            bso.FindProperty("m_mapArea").rectValue = cameraArea;
            SerializedProperty arr = bso.FindProperty("m_zones");
            arr.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            }
            bso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Branch01.prefab");
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            return "Branch01 zones 9 trees " + count;
        }

        // 마름모 시트 18장 중 (x−z, x+z)로 고르는 변형 타일 색인. make_tiles.py의 배치와 같다
        static int Index(int x, int z)
        {
            int d = ((x - z) % 6 + 6) % 6;
            int s = ((x + z) % 6 + 6) % 6;
            return d * 3 + (s - d % 2) / 2;
        }

        static Tile[] LoadTiles(string prefix)
        {
            Tile[] tiles = new Tile[18];
            for (int i = 0; i < 18; i++)
            {
                tiles[i] = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/" + prefix + "_" + i.ToString("00") + ".asset");
            }
            return tiles;
        }

        static Tilemap Layer(Transform grid, string name, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(grid, false);
            Tilemap map = go.AddComponent<Tilemap>();
            map.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            TilemapRenderer r = go.AddComponent<TilemapRenderer>();
            r.sortingOrder = order;
            return map;
        }

        static void Fill(Tilemap map, Tile[] tiles, int x0, int z0, int w, int h)
        {
            for (int x = x0; x < x0 + w; x++)
            {
                for (int z = z0; z < z0 + h; z++)
                {
                    map.SetTile(new Vector3Int(x, z, 0), tiles[Index(x, z)]);
                }
            }
        }

        static void AddSprite(Transform parent, string name, Vector2 logical, Sprite sprite, bool flip, int order)
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
