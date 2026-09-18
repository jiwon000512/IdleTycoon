using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using ZooTycoon.UI;
using ZooTycoon.World;

namespace ZooTycoon.Editor
{
    // 설계 08 v0.5: 빵집 프리팹을 통째로 다시 만든다.
    // 구역 4종(입구·진열 층·계산대·오븐 줄) + Shop(ShopView·손님 스포너) + ShopCustomer + HUD·굽기·업그레이드 팝업.
    // 좌표는 구역 윗변 가운데 기준(유닛). 스프라이트는 PPU 64 더미(Sprites/World/Shop, Resources/Sprites/Shop/Breads)
    public static class ShopBaker
    {
        const string k_SpriteDir = "Assets/Sprites/World/Shop/";
        const string k_BreadDir = "Assets/Resources/Sprites/Shop/Breads/";
        const string k_PrefabDir = "Assets/Prefabs/Shop/";
        const string k_VisitorPrefabPath = "Assets/Prefabs/Visitor.prefab";
        const string k_HudPath = "Assets/Resources/UI/ShopHudView.prefab";
        const string k_BakePopupPath = "Assets/Resources/UI/BakePopupView.prefab";
        const string k_UpgradePopupPath = "Assets/Resources/UI/UpgradePopupView.prefab";
        const float k_Ppu = 64f;
        const float k_TopBarHeight = 160f;
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
            ShopCustomer customer = BakeCustomer();
            BakeShop(entrance, shelfRow, counter, ovenRow, customer);
            BakeHud();
            BakePopup<BakePopupView>(k_BakePopupPath);
            BakePopup<UpgradePopupView>(k_UpgradePopupPath);
            AssetDatabase.DeleteAsset("Assets/Prefabs/Bakery.prefab");
            AssetDatabase.SaveAssets();
            return "Shop 구역 4 + Shop + ShopCustomer + ShopHudView + BakePopupView + UpgradePopupView";
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

            foreach (string name in new[] { "shelf", "oven", "counter", "bubble", "angry", "wombat_front", "wombat_back" })
            {
                Import(k_SpriteDir + name + ".png", bottom);
            }

            Import(k_SpriteDir + "bar_bg.png", new Vector2(0f, 0.5f));
            Import(k_SpriteDir + "bar_fill.png", new Vector2(0f, 0.5f));

            foreach (string name in new[] { "b01", "b02", "b03" })
            {
                Import(k_BreadDir + name + ".png", center);
            }
        }

        static void Import(string path, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = k_Ppu;
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
            icon.transform.localScale = Vector3.one * 1.3f;
            TextMeshPro text = WorldText(go.transform, "Stock", new Vector3(0f, 1.4f, 0f), 2);
            Transform stand = Child(go.transform, "StandPoint", new Vector3(-1.05f * side, -0.05f, 0f)).transform;

            ShelfView view = go.AddComponent<ShelfView>();
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
            Renderer(root.transform, "Counter", Load("counter"), new Vector3(0f, -1.55f, 0f), 0);
            // 웜뱃 정면·뒷모습: 한 칸 2px(PPU 64), 42칸
            SpriteRenderer wombat = Renderer(root.transform, "Wombat", Load("wombat_front"), new Vector3(0f, -2.45f, 0f), 0);
            Set(counter, "m_wombat", wombat);
            Set(counter, "m_wombatFront", Load("wombat_front"));
            Set(counter, "m_wombatBack", Load("wombat_back"));
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
            SpriteRenderer icon = Renderer(go.transform, "Icon", null, new Vector3(0f, 0.62f, 0f), 1);
            icon.transform.localScale = Vector3.one * 1.2f;
            GameObject bar = Child(go.transform, "Bar", new Vector3(-0.5f, 1.45f, 0f));
            Renderer(bar.transform, "Back", Load("bar_bg"), Vector3.zero, 2);
            SpriteRenderer fill = Renderer(bar.transform, "Fill", Load("bar_fill"), Vector3.zero, 3);
            fill.color = new Color(0.95f, 0.76f, 0.3f);
            TextMeshPro ready = WorldText(go.transform, "Ready", new Vector3(0f, 1.45f, 0f), 4);

            OvenView view = go.AddComponent<OvenView>();
            Set(view, "m_body", body);
            Set(view, "m_icon", icon);
            Set(view, "m_bar", bar);
            Set(view, "m_barFill", fill.transform);
            Set(view, "m_readyText", ready);
            return view;
        }

        // ---------- 손님 · 가게 ----------

        // 관광객 프리팹의 몸체(ModelRoot·Sprite·Shadow·Animator)를 그대로 쓰고 말풍선을 붙인다
        static ShopCustomer BakeCustomer()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(k_VisitorPrefabPath);
            GameObject root = Object.Instantiate(source);
            root.name = "ShopCustomer";
            SerializedObject visitor = new SerializedObject(root.GetComponent<Visitor>());
            Object modelRoot = visitor.FindProperty("m_modelRoot").objectReferenceValue;
            Object sprite = visitor.FindProperty("m_spriteRenderer").objectReferenceValue;
            Object shadow = visitor.FindProperty("m_shadowRenderer").objectReferenceValue;
            Object animator = visitor.FindProperty("m_animator").objectReferenceValue;
            Object coin = visitor.FindProperty("m_coinPrefab").objectReferenceValue;
            Object.DestroyImmediate(root.GetComponent<Visitor>());

            SpriteRenderer bubble = Renderer(root.transform, "Bubble", Load("bubble"), new Vector3(0f, 0.8f, 0f), 50);
            bubble.transform.localScale = Vector3.one * 0.8f;
            SpriteRenderer icon = Renderer(bubble.transform, "Icon", null, new Vector3(0f, 0.38f, 0f), 51);
            icon.transform.localScale = Vector3.one * 0.9f;

            ShopCustomer customer = root.AddComponent<ShopCustomer>();
            Set(customer, "m_modelRoot", modelRoot);
            Set(customer, "m_spriteRenderer", sprite);
            Set(customer, "m_shadowRenderer", shadow);
            Set(customer, "m_animator", animator);
            Set(customer, "m_coinPrefab", coin);
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
            ShopCustomerSpawner spawner = root.AddComponent<ShopCustomerSpawner>();
            Set(spawner, "m_prefab", customer);
            Save(root, view, "Shop");
        }

        // ---------- UI ----------

        // 전체 화면 루트: 위 한 줄(뒤로·가게 이름), 아래 업그레이드 버튼
        static void BakeHud()
        {
            GameObject root = new GameObject("ShopHudView", typeof(RectTransform), typeof(ShopHudView));
            Stretch(root.GetComponent<RectTransform>());

            GameObject top = new GameObject("Top", typeof(RectTransform));
            top.transform.SetParent(root.transform, false);
            RectTransform topRect = top.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 120f);
            topRect.anchoredPosition = new Vector2(0f, -k_TopBarHeight - 20f);

            TextMeshProUGUI title = MakeText(top.transform, "TitleText", 56f);
            Anchor(title.rectTransform, new Vector2(0.3f, 0f), new Vector2(0.7f, 1f));
            title.color = new Color(1f, 0.95f, 0.84f);

            Button back = MakeButton(top.transform, "BackButton", 44f, out TextMeshProUGUI backText);
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.sizeDelta = new Vector2(260f, 0f);
            backRect.anchoredPosition = new Vector2(24f, 0f);

            Button upgrade = MakeButton(root.transform, "UpgradeButton", 52f, out TextMeshProUGUI upgradeText);
            upgrade.GetComponent<Image>().color = new Color(0.77f, 0.46f, 0.17f, 0.95f);
            RectTransform upgradeRect = upgrade.GetComponent<RectTransform>();
            upgradeRect.anchorMin = new Vector2(0.5f, 0f);
            upgradeRect.anchorMax = new Vector2(0.5f, 0f);
            upgradeRect.pivot = new Vector2(0.5f, 0f);
            upgradeRect.sizeDelta = new Vector2(600f, 130f);
            upgradeRect.anchoredPosition = new Vector2(0f, 40f);

            ShopHudView view = root.GetComponent<ShopHudView>();
            Set(view, "m_titleText", title);
            Set(view, "m_backButton", back);
            Set(view, "m_backText", backText);
            Set(view, "m_upgradeButton", upgrade);
            Set(view, "m_upgradeText", upgradeText);
            PrefabUtility.SaveAsPrefabAsset(root, k_HudPath);
            Object.DestroyImmediate(root);
        }

        // 어두운 전체 화면(뒤 월드 탭을 막는다) + 가운데 패널: 제목 · 줄 목록 · 닫기
        static void BakePopup<T>(string path) where T : ListPopupView
        {
            GameObject root = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(Image), typeof(T));
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(root.transform, false);
            panel.GetComponent<Image>().color = new Color(0.97f, 0.93f, 0.86f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(920f, 0f);
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = MakeText(panel.transform, "TitleText", 60f);
            title.color = new Color(0.23f, 0.14f, 0.09f);
            Height(title.gameObject, 100f);

            Button row = MakeButton(panel.transform, "RowTemplate", 44f, out _);
            row.GetComponent<Image>().color = new Color(0.66f, 0.48f, 0.27f);
            Height(row.gameObject, 130f);
            row.gameObject.SetActive(false);

            Button close = MakeButton(panel.transform, "CloseButton", 44f, out TextMeshProUGUI closeText);
            Height(close.gameObject, 110f);

            T view = root.GetComponent<T>();
            Set(view, "m_titleText", title);
            Set(view, "m_closeButton", close);
            Set(view, "m_closeText", closeText);
            Set(view, "m_rowTemplate", row);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        // ---------- 도우미 ----------

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

        static TextMeshPro WorldText(Transform parent, string name, Vector3 position, int order)
        {
            GameObject go = Child(parent, name, position);
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
            text.fontSize = 3f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
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

        static void SetFloat(Object target, string field, float value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Height(GameObject go, float height)
        {
            go.AddComponent<LayoutElement>().preferredHeight = height;
        }

        static Button MakeButton(Transform parent, string name, float fontSize, out TextMeshProUGUI label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.2f, 0.13f, 0.13f, 0.9f);
            label = MakeText(go.transform, "Text", fontSize);
            Stretch(label.rectTransform);
            label.color = Color.white;
            return go.GetComponent<Button>();
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.text = "-";
            return text;
        }
    }
}
