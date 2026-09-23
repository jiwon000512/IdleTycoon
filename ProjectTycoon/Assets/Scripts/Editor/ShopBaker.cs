using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using TMPro;
using ZooTycoon.World;

namespace ZooTycoon.Editor
{
    // 설계 08 v0.5: 빵집 프리팹을 통째로 다시 만든다.
    // 구역 4종(입구·진열 층·계산대·오븐 줄) + Shop(ShopView·손님 스포너) + ShopCustomer + HUD·굽기·업그레이드 팝업.
    // 좌표는 구역 윗변 가운데 기준(유닛). 스프라이트는 한 칸 2px·PPU 80(Sprites/World/Shop, Resources/Sprites/Shop/Breads, 원본 Shop/Source~)
    public static class ShopBaker
    {
        const string k_SpriteDir = "Assets/Sprites/World/Shop/";
        const string k_BreadDir = "Assets/Resources/Sprites/Shop/Breads/";
        const string k_PrefabDir = "Assets/Prefabs/Shop/";
        const string k_ShadowPath = "Assets/Sprites/World/shadow.png";
        const string k_CoinPrefabPath = "Assets/Prefabs/CoinPopup.prefab";
        const string k_FontPath = "Assets/Fonts/Galmuri/Galmuri9.asset";
        const float k_TagPpu = 40f;
        const float k_Ppu = 80f;
        // 가게 유닛(웜뱃·손님) 기본 크기: 한 칸 2px, PPU 80 = 42칸 캐릭터 약 1.05유닛. 스케일은 1로 두고 크기는 PPU로 정한다
        const float k_UnitPpu = 80f;
        const int k_BackgroundOrder = -2000;

        [MenuItem("ZooTycoon/Bake/Shop")]
        public static void Bake()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            ImportSprites();

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Shop"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Shop");
            }

            ShopSection entrance = BakeEntrance();
            ShelfRowView shelfRow = BakeShelfRow();
            CounterView counter = BakeCounter();
            OvenRowView ovenRow = BakeOvenRow();
            BakeCoinPopup();
            ShopCustomer customer = BakeCustomer();
            BakeShop(entrance, shelfRow, counter, ovenRow, customer);
            AssetDatabase.DeleteAsset("Assets/Prefabs/Bakery.prefab");
            AssetDatabase.SaveAssets();
            return "Shop 구역 4 + Shop + ShopCustomer (UI 프리팹은 ZooTycoon/Bake/UI)";
        }

        static void ImportSprites()
        {
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 bottom = new Vector2(0.5f, 0f);
            Vector2 center = new Vector2(0.5f, 0.5f);

            foreach (string name in new[] { "entrance", "shelf_row", "counter_row", "oven_row" })
            {
                Import(k_SpriteDir + name + ".png", top);
            }

            foreach (string name in new[] { "shelf", "oven", "oven_2", "counter", "bubble", "angry" })
            {
                Import(k_SpriteDir + name + ".png", bottom);
            }

            foreach (string side in new[] { "front", "back" })
            {
                foreach (string suffix in new[] { "", "_1", "_2", "_3", "_walk_0", "_walk_1", "_walk_2", "_walk_3" })
                {
                    Import(k_SpriteDir + "wombat_" + side + suffix + ".png", bottom, k_UnitPpu);
                }
            }

            Import(k_SpriteDir + "bar_bg.png", new Vector2(0f, 0.5f));
            Import(k_SpriteDir + "bar_fill.png", new Vector2(0f, 0.5f));

            foreach (string name in new[] { "b01", "b02", "b03" })
            {
                Import(k_BreadDir + name + ".png", center);
            }
        }

        static void Import(string path, Vector2 pivot, float ppu = k_Ppu)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            TextureImporterSettings settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(k_SpriteDir + name + ".png");
        }

        // ---------- 구역 ----------

        static ShopSection BakeEntrance()
        {
            GameObject root = Section("ShopEntrance", "entrance");
            ShopSection section = root.AddComponent<ShopSection>();
            SetFloat(section, "m_height", 2f);
            return Save(root, section, "ShopEntrance");
        }

        static ShelfRowView BakeShelfRow()
        {
            GameObject root = Section("ShopShelfRow", "shelf_row");
            ShelfRowView row = root.AddComponent<ShelfRowView>();
            SetFloat(row, "m_height", 2.4f);
            Set(row, "m_left", Shelf(root.transform, "Left", -1));
            Set(row, "m_right", Shelf(root.transform, "Right", 1));
            return Save(root, row, "ShopShelfRow");
        }

        // side −1 왼쪽, +1 오른쪽. 손님은 가운데 쪽에 선다
        static ShelfView Shelf(Transform parent, string name, int side)
        {
            GameObject go = Child(parent, name, new Vector3(2.25f * side, -1.85f, 0f));
            go.AddComponent<SortingGroup>();
            SpriteRenderer body = Renderer(go.transform, "Body", Load("shelf"), Vector3.zero, 0);
            SpriteRenderer icon = Renderer(go.transform, "Icon", null, new Vector3(0f, 0.62f, 0f), 1);
            TextMeshPro text = WorldText(go.transform, "Stock", new Vector3(0f, 1.4f, 0f), 2);
            Transform stand = Child(go.transform, "StandPoint", new Vector3(-1.05f * side, -0.05f, 0f)).transform;
            (GameObject tag, TextMeshPro tagText) = Tag(go.transform, 1.35f);

            ShelfView view = go.AddComponent<ShelfView>();
            Set(view, "m_tag", tag);
            Set(view, "m_tagText", tagText);
            Set(view, "m_body", body);
            Set(view, "m_icon", icon);
            Set(view, "m_stockText", text);
            Set(view, "m_standPoint", stand);
            return view;
        }

        static CounterView BakeCounter()
        {
            GameObject root = Section("ShopCounter", "counter_row");
            CounterView counter = root.AddComponent<CounterView>();
            SetFloat(counter, "m_height", 2.6f);
            Set(counter, "m_queueHead", Child(root.transform, "QueueHead", new Vector3(0f, -0.55f, 0f)).transform);
            SpriteRenderer counterBody = Renderer(root.transform, "Counter", Load("counter"), new Vector3(0f, -1.55f, 0f), 0);
            Set(counter, "m_body", counterBody);
            // 웜뱃 정면·뒷모습: 가게 유닛 기본 크기(k_UnitPpu), 스케일 1. 숨쉬기 0 → 1 → 2 → 3(Source~/make_breath_frames.py)
            SpriteRenderer wombat = Renderer(root.transform, "Wombat", Load("wombat_front"), new Vector3(0f, -2.45f, 0f), 0);
            SpriteAnimator animator = wombat.gameObject.AddComponent<SpriteAnimator>();
            Set(animator, "m_renderer", wombat);
            // 걷기(v0.6): 딛기 → 왼발 → 딛기 → 오른발(Source~/make_walk_frames.py)
            ShopWombat mover = wombat.gameObject.AddComponent<ShopWombat>();
            Set(mover, "m_animator", animator);
            SetSprites(mover, "m_frontIdle", Frames("wombat_front", "", "_1", "_2", "_3"));
            SetSprites(mover, "m_backIdle", Frames("wombat_back", "", "_1", "_2", "_3"));
            SetSprites(mover, "m_frontWalk", Frames("wombat_front", "_walk_0", "_walk_1", "_walk_2", "_walk_3"));
            SetSprites(mover, "m_backWalk", Frames("wombat_back", "_walk_0", "_walk_1", "_walk_2", "_walk_3"));
            Set(counter, "m_wombat", mover);
            return Save(root, counter, "ShopCounter");
        }

        static OvenRowView BakeOvenRow()
        {
            GameObject root = Section("ShopOvenRow", "oven_row");
            OvenRowView row = root.AddComponent<OvenRowView>();
            SetFloat(row, "m_height", 2f);
            Set(row, "m_left", Oven(root.transform, "Left", -1));
            Set(row, "m_right", Oven(root.transform, "Right", 1));
            return Save(root, row, "ShopOvenRow");
        }

        static OvenView Oven(Transform parent, string name, int side)
        {
            GameObject go = Child(parent, name, new Vector3(1.55f * side, -1.75f, 0f));
            go.AddComponent<SortingGroup>();
            SpriteRenderer body = Renderer(go.transform, "Body", Load("oven"), Vector3.zero, 0);
            // 아이콘은 오븐 아궁이 안
            SpriteRenderer icon = Renderer(go.transform, "Icon", null, new Vector3(0f, 0.35f, 0f), 1);
            icon.transform.localScale = Vector3.one * 0.8f;
            GameObject bar = Child(go.transform, "Bar", new Vector3(-0.5f, 1.45f, 0f));
            Renderer(bar.transform, "Back", Load("bar_bg"), Vector3.zero, 2);
            SpriteRenderer fill = Renderer(bar.transform, "Fill", Load("bar_fill"), Vector3.zero, 3);
            fill.color = new Color(0.95f, 0.76f, 0.3f);
            TextMeshPro ready = WorldText(go.transform, "Ready", new Vector3(0f, 1.45f, 0f), 4);
            (GameObject tag, TextMeshPro tagText) = Tag(go.transform, 1.7f);

            OvenView view = go.AddComponent<OvenView>();
            Set(view, "m_body", body);
            Set(view, "m_icon", icon);
            Set(view, "m_bar", bar);
            Set(view, "m_barFill", fill.transform);
            Set(view, "m_readyText", ready);
            Set(view, "m_tag", tag);
            Set(view, "m_tagText", tagText);
            Set(view, "m_baseBody", Load("oven"));
            Set(view, "m_upgradedBody", Load("oven_2"));
            return view;
        }

        // ---------- 손님 · 가게 ----------

        // 연출 1차: 기존 코인 팝업 프리팹에 금액 글자를 붙인다(스프라이트·수치는 그대로)
        static void BakeCoinPopup()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_CoinPrefabPath);
            Transform old = root.transform.Find("Amount");

            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            TextMeshPro amount = WorldText(root.transform, "Amount", new Vector3(0f, 0.3f, 0f), 60);
            amount.fontSize = 3.5f;
            amount.color = new Color(1f, 0.93f, 0.55f);
            Set(root.GetComponent<CoinPopup>(), "m_amountText", amount);
            PrefabUtility.SaveAsPrefabAsset(root, k_CoinPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 손님 몸체: 루트(SpriteAnimator) → ModelRoot(크기) → Sprite + Shadow(납작한 타원). 외형은 visitors.json 행이 실행 중에 채운다
        static ShopCustomer BakeCustomer()
        {
            GameObject root = new GameObject("ShopCustomer");
            GameObject model = Child(root.transform, "ModelRoot", Vector3.zero);
            SpriteRenderer sprite = Renderer(model.transform, "Sprite", null, Vector3.zero, 0);
            SpriteRenderer shadow = Renderer(model.transform, "Shadow", AssetDatabase.LoadAssetAtPath<Sprite>(k_ShadowPath), Vector3.zero, -900);
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            SpriteAnimator animator = root.AddComponent<SpriteAnimator>();
            Set(animator, "m_renderer", sprite);

            // 말풍선 32×36칸(꼬리 4칸), 아이콘은 몸통 가운데(꼬리 0.1 + 몸통 반 0.4)
            SpriteRenderer bubble = Renderer(root.transform, "Bubble", Load("bubble"), new Vector3(0f, 0.8f, 0f), 50);
            SpriteRenderer icon = Renderer(bubble.transform, "Icon", null, new Vector3(0f, 0.5f, 0f), 51);
            icon.transform.localScale = Vector3.one * 0.7f;

            ShopCustomer customer = root.AddComponent<ShopCustomer>();
            Set(customer, "m_modelRoot", model.transform);
            Set(customer, "m_spriteRenderer", sprite);
            Set(customer, "m_shadowRenderer", shadow);
            Set(customer, "m_animator", animator);
            Set(customer, "m_coinPrefab", AssetDatabase.LoadAssetAtPath<CoinPopup>(k_CoinPrefabPath));
            Set(customer, "m_bubble", bubble);
            Set(customer, "m_bubbleIcon", icon);
            Set(customer, "m_angrySprite", Load("angry"));
            return Save(root, customer, "ShopCustomer");
        }

        static void BakeShop(ShopSection entrance, ShelfRowView shelfRow, CounterView counter, OvenRowView ovenRow, ShopCustomer customer)
        {
            GameObject root = new GameObject("Shop");
            ShopView view = root.AddComponent<ShopView>();
            Set(view, "m_entrancePrefab", entrance);
            Set(view, "m_shelfRowPrefab", shelfRow);
            Set(view, "m_counterPrefab", counter);
            Set(view, "m_ovenRowPrefab", ovenRow);
            // 연출 1차: 줄 간격은 뒤 손님 얼굴이 보이게 0.8
            SetFloat(view, "m_queueSpacing", 0.8f);
            ShopCustomerSpawner spawner = root.AddComponent<ShopCustomerSpawner>();
            Set(spawner, "m_prefab", customer);
            Save(root, view, "Shop");
        }

        // ---------- UI ----------

        static GameObject Section(string name, string background)
        {
            GameObject root = new GameObject(name);
            Renderer(root.transform, "Background", Load(background), Vector3.zero, k_BackgroundOrder);
            return root;
        }

        static GameObject Child(Transform parent, string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go;
        }

        static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, Vector3 position, int order)
        {
            SpriteRenderer r = Child(parent, name, position).AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        // 잠긴 사물 위 비용 태그(UI 규칙 4장: 진갈색 바탕·크림 글자 9px, 월드 스프라이트 PPU 40 = 1px가 한 칸). 처음엔 꺼 둔다
        static (GameObject, TextMeshPro) Tag(Transform parent, float y)
        {
            GameObject tag = Child(parent, "Tag", new Vector3(0f, y, 0f));
            SpriteRenderer bg = Renderer(tag.transform, "Bg", AssetDatabase.LoadAssetAtPath<Sprite>(k_SpriteDir + "tag_cost.png"), Vector3.zero, 3);
            bg.drawMode = SpriteDrawMode.Sliced;
            bg.size = new Vector2(2.1f, 0.35f);
            TextMeshPro text = WorldText(tag.transform, "Text", Vector3.zero, 4);
            text.rectTransform.sizeDelta = new Vector2(2.1f, 0.35f);
            text.fontSize = 2.25f;
            text.color = new Color32(0xF4, 0xDF, 0xBF, 255);
            tag.SetActive(false);
            return (tag, text);
        }

        static TextMeshPro WorldText(Transform parent, string name, Vector3 position, int order)
        {
            GameObject go = Child(parent, name, position);
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontPath);
            text.fontSize = 2.25f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = new Color(0.23f, 0.14f, 0.09f);
            text.sortingOrder = order;
            text.text = "-";
            return text;
        }

        static T Save<T>(GameObject root, T component, string name) where T : Component
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, k_PrefabDir + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<T>();
        }

        static void Set(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Sprite[] Frames(string name, params string[] suffixes)
        {
            return System.Array.ConvertAll(suffixes, suffix => Load(name + suffix));
        }

        static void SetSprites(Object target, string field, Sprite[] sprites)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = sprites.Length;

            for (int i = 0; i < sprites.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
