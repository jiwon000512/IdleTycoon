using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using TMPro;
using ZooTycoon.Core;
using ZooTycoon.World;

namespace ZooTycoon.Editor
{
    // 설계 08 v0.5 · 굴 격자 설계 v0.5 · 손님 동선 설계 v0.2: 빵집 프리팹을 통째로 다시 만든다. 사물 자리 숫자는 Core ShopLayout과 같은 값을 쓴다.
    // 사물 프리팹(진열대·오븐·계산대) + 표시(파기 태그·빈 자리) + Shop(ShopView·손님 스포너·흙 배경·굴 그림·입구 아치) + ShopCustomer.
    // 스프라이트는 한 칸 2px·PPU 80(Sprites/World/Shop, Resources/Sprites/Shop/Breads, 원본 Shop/Source~). 굴 그림 재료(타일·빈 자리)는 한 칸 1px·PPU 40
    public static class ShopBaker
    {
        const string k_SpriteDir = "Assets/Sprites/World/Shop/";
        const string k_BreadDir = "Assets/Resources/Sprites/Shop/Breads/";
        const string k_PrefabDir = "Assets/Prefabs/Shop/";
        const string k_ShadowPath = "Assets/Sprites/World/shadow.png";
        const string k_CoinPrefabPath = "Assets/Prefabs/CoinPopup.prefab";
        const string k_FontPath = "Assets/Fonts/Galmuri/Galmuri11-Bold.asset";
        const float k_TagPpu = 40f;
        const float k_Ppu = 80f;
        // 가게 유닛(웜뱃·손님) 기본 크기: 한 칸 2px, PPU 80 = 42칸 캐릭터 약 1.05유닛. 스케일은 1로 두고 크기는 PPU로 정한다
        const float k_UnitPpu = 80f;
        const int k_TimerFrames = 16;
        const int k_SmokeFrames = 12;
        const int k_BackdropOrder = -2100;
        const int k_BurrowOrder = -2000;
        const int k_ArchOrder = -1995;
        const int k_MarkerOrder = -1980;
        // 설계 09: 웜뱃이 든 빵 층 수·높이·크기·층 간격(층 로컬). 높이·앞뒤 순서는 실행 중 ShopWombat이 방향에 맞춰 옮긴다
        const int k_CarryLayers = 5;
        const float k_CarryHeight = 1.05f;
        const float k_CarryScale = 0.6f;
        const float k_CarryStep = 0.3f;
        // 흙 배경: 굴 원점에서 이만큼 왼쪽 위에서 시작해 두 배 크기(타일 32칸 주기에 맞는 값)
        const float k_BackdropHalf = 32f;

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

            foreach (string old in new[] { "ShopEntrance", "ShopShelfRow", "ShopOvenRow" })
            {
                AssetDatabase.DeleteAsset(k_PrefabDir + old + ".prefab");
            }

            ShelfView shelf = BakeShelf();
            OvenView oven = BakeOven();
            CounterView counter = BakeCounter();
            MarkerView digTag = BakeDigTag();
            SpriteRenderer slotMarker = BakeSlotMarker();
            BakeCoinPopup();
            ShopCustomer customer = BakeCustomer();
            BakeShop(shelf, oven, counter, digTag, slotMarker, customer);
            AssetDatabase.SaveAssets();
            return "Shop: Shelf·Oven·ShopCounter·DigTag·SlotMarker·Shop·ShopCustomer (UI 프리팹은 ZooTycoon/Bake/UI)";
        }

        static void ImportSprites()
        {
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 bottom = new Vector2(0.5f, 0f);
            Vector2 center = new Vector2(0.5f, 0.5f);

            Import(k_SpriteDir + "arch.png", top);

            foreach (string name in new[] { "shelf", "oven", "oven_2", "counter", "bubble", "angry" })
            {
                Import(k_SpriteDir + name + ".png", bottom);
            }

            foreach (string side in new[] { "front", "back", "side" })
            {
                foreach (string suffix in new[] { "", "_1", "_2", "_3", "_walk_0", "_walk_1", "_walk_2", "_walk_3" })
                {
                    Import(k_SpriteDir + "wombat_" + side + suffix + ".png", bottom, k_UnitPpu);
                }
            }

            for (int i = 0; i < k_TimerFrames; i++)
            {
                Import(k_SpriteDir + TimerFrame(i) + ".png", center);
            }

            // 설계 10: 오븐 상태 표시(Source~/make_oven_idle.py). 타이머 자리
            Import(k_SpriteDir + "oven_empty_mark.png", center);
            Import(k_SpriteDir + "oven_ready_mark.png", center);

            // 오븐 연출: 불빛은 몸통과 같은 크기·하단 가운데, 연기는 하단 가운데 = 굴뚝 입구
            foreach (string name in FxFrames())
            {
                Import(k_SpriteDir + name + ".png", bottom);
            }

            Import(k_SpriteDir + "slot_empty.png", center, k_TagPpu);
            // 타일: 굴 그림이 픽셀을 읽고, 흙 배경은 Tiled로 깐다(왼쪽 위 피벗)
            Import(k_SpriteDir + "floor_tile.png", new Vector2(0f, 1f), k_TagPpu, true);
            Import(k_SpriteDir + "wall_tile.png", new Vector2(0f, 1f), k_TagPpu, true);

            foreach (string name in new[] { "b01", "b02", "b03" })
            {
                Import(k_BreadDir + name + ".png", center);
            }
        }

        static void Import(string path, Vector2 pivot, float ppu = k_Ppu, bool tile = false)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.isReadable = tile;
            ti.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            TextureImporterSettings settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(k_SpriteDir + name + ".png");
        }

        // ---------- 사물 ----------

        // 진열대 하나. 피벗 = 밑변 가운데. 손님이 서는 자리는 Core ShopLayout
        static ShelfView BakeShelf()
        {
            GameObject go = new GameObject("Shelf");
            go.AddComponent<SortingGroup>();
            SpriteRenderer body = Renderer(go.transform, "Body", Load("shelf"), Vector3.zero, 0);
            SpriteRenderer icon = Renderer(go.transform, "Icon", null, new Vector3(0f, 0.62f, 0f), 1);
            TextMeshPro text = WorldText(go.transform, "Stock", new Vector3(0f, 1.4f, 0f), 2);

            ShelfView view = go.AddComponent<ShelfView>();
            Set(view, "m_body", body);
            Set(view, "m_icon", icon);
            Set(view, "m_stockText", text);
            return Save(go, view, "Shelf");
        }

        static OvenView BakeOven()
        {
            GameObject go = new GameObject("Oven");
            go.AddComponent<SortingGroup>();
            SpriteRenderer body = Renderer(go.transform, "Body", Load("oven"), Vector3.zero, 0);
            // 오븐 연출: 아궁이 불빛은 몸통 바로 위, 굴뚝 연기는 그 위·타이머 뒤. 연기 높이는 OvenView.SetLook이 흙/벽돌에 맞춘다
            SpriteRenderer fire = Renderer(go.transform, "Fire", null, Vector3.zero, 1);
            fire.enabled = false;
            SpriteAnimator fireAnimator = fire.gameObject.AddComponent<SpriteAnimator>();
            Set(fireAnimator, "m_renderer", fire);
            SpriteRenderer smoke = Renderer(go.transform, "Smoke", null, new Vector3(0f, 1.25f, 0f), 2);
            smoke.enabled = false;
            SpriteAnimator smokeAnimator = smoke.gameObject.AddComponent<SpriteAnimator>();
            Set(smokeAnimator, "m_renderer", smoke);
            // 굽기 타이머(20칸 원): 오븐 윗변(1.3) 위 3칸 띄움
            Sprite[] timerFrames = new Sprite[k_TimerFrames];

            for (int i = 0; i < k_TimerFrames; i++)
            {
                timerFrames[i] = Load(TimerFrame(i));
            }

            SpriteRenderer timer = Renderer(go.transform, "Timer", timerFrames[0], new Vector3(0f, 1.62f, 0f), 3);
            // 설계 10: 빈 오븐 화살표·다 구움 원판은 타이머 자리, 개수 글자는 원판 오른쪽 아래
            SpriteRenderer emptyMark = Renderer(go.transform, "EmptyMark", Load("oven_empty_mark"), new Vector3(0f, 1.62f, 0f), 3);
            emptyMark.enabled = false;
            SpriteRenderer readyMark = Renderer(go.transform, "ReadyMark", Load("oven_ready_mark"), new Vector3(0f, 1.62f, 0f), 3);
            readyMark.enabled = false;
            TextMeshPro ready = WorldText(go.transform, "Ready", new Vector3(0.4f, 1.42f, 0f), 4);

            OvenView view = go.AddComponent<OvenView>();
            Set(view, "m_body", body);
            Set(view, "m_timer", timer);
            Set(view, "m_fire", fire);
            Set(view, "m_fireAnimator", fireAnimator);
            SetSprites(view, "m_baseFire", Frames("oven_fire", "_0", "_1", "_2", "_3"));
            SetSprites(view, "m_upgradedFire", Frames("oven_2_fire", "_0", "_1", "_2", "_3"));
            Set(view, "m_smoke", smoke);
            Set(view, "m_smokeAnimator", smokeAnimator);
            SetSprites(view, "m_graySmoke", Frames("smoke_gray", SmokeSuffixes()));
            SetSprites(view, "m_whiteSmoke", Frames("smoke_white", SmokeSuffixes()));
            SetSprites(view, "m_timerFrames", timerFrames);
            Set(view, "m_readyText", ready);
            Set(view, "m_emptyMark", emptyMark);
            Set(view, "m_readyMark", readyMark);
            Set(view, "m_baseBody", Load("oven"));
            Set(view, "m_upgradedBody", Load("oven_2"));
            return Save(go, view, "Oven");
        }

        // 계산대. 피벗 = 계산대 줄 윗변 가운데(옛 구역과 같은 오프셋)
        static CounterView BakeCounter()
        {
            GameObject root = new GameObject("ShopCounter");
            CounterView counter = root.AddComponent<CounterView>();
            SpriteRenderer counterBody = Renderer(root.transform, "Counter", Load("counter"), new Vector3(0f, -ShopLayout.k_CounterDrop, 0f), 0);
            Set(counter, "m_body", counterBody);
            // 웜뱃 정면·뒷모습: 가게 유닛 기본 크기(k_UnitPpu), 스케일 1. 숨쉬기 0 → 1 → 2 → 3(Source~/make_breath_frames.py)
            SpriteRenderer wombat = Renderer(root.transform, "Wombat", Load("wombat_front"), new Vector3(0f, -ShopLayout.k_WombatDrop, 0f), 0);
            SpriteAnimator animator = wombat.gameObject.AddComponent<SpriteAnimator>();
            Set(animator, "m_renderer", wombat);
            // 걷기(v0.6): 딛기 → 왼발 → 딛기 → 오른발(Source~/make_walk_frames.py)
            ShopWombat mover = wombat.gameObject.AddComponent<ShopWombat>();
            Set(mover, "m_animator", animator);
            Set(mover, "m_renderer", wombat);
            // 옆모습(손님 동선 설계 v0.2): 오른쪽 보는 그림, 왼쪽은 뒤집기. 실제 아트 전까지 앞모습 사본(더미)
            foreach (string side in new[] { "front", "back", "side" })
            {
                SetSprites(mover, "m_" + side + "Idle", Frames("wombat_" + side, "", "_1", "_2", "_3"));
                SetSprites(mover, "m_" + side + "Walk", Frames("wombat_" + side, "_walk_0", "_walk_1", "_walk_2", "_walk_3"));
            }
            // 설계 09: 든 빵 층(아래부터 5개, 앞발에서 위로). 크기는 부모 Carry가 정하고 층은 스케일 1(튀기 연출이 1로 되돌린다)
            GameObject carry = Child(wombat.transform, "Carry", new Vector3(0f, k_CarryHeight, 0f));
            carry.transform.localScale = Vector3.one * k_CarryScale;
            SerializedObject so = new SerializedObject(mover);
            SerializedProperty layers = so.FindProperty("m_carry");
            layers.arraySize = k_CarryLayers;

            for (int i = 0; i < k_CarryLayers; i++)
            {
                SpriteRenderer layer = Renderer(carry.transform, "Layer" + i, null, new Vector3(0f, i * k_CarryStep, 0f), 51 + i);
                layer.enabled = false;
                layers.GetArrayElementAtIndex(i).objectReferenceValue = layer;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Set(counter, "m_wombat", mover);
            return Save(root, counter, "ShopCounter");
        }

        // 파기 비용 태그: 칸 가운데에 태그만. 탭 영역 = 태그
        static MarkerView BakeDigTag()
        {
            GameObject root = new GameObject("DigTag");
            (SpriteRenderer bg, TextMeshPro text) = Tag(root.transform, 0f);
            MarkerView view = root.AddComponent<MarkerView>();
            Set(view, "m_body", bg);
            Set(view, "m_text", text);
            return Save(root, view, "DigTag");
        }

        // 빈 자리: 점선 발자국만(2026-09-23 「빈 자리」 글자 태그 삭제)
        static SpriteRenderer BakeSlotMarker()
        {
            GameObject root = new GameObject("SlotMarker");
            SpriteRenderer body = root.AddComponent<SpriteRenderer>();
            body.sprite = Load("slot_empty");
            body.sortingOrder = k_MarkerOrder;
            return Save(root, body, "SlotMarker");
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

            // 코인은 가게 그림 위에(오븐·손님 뒤로 가려지지 않게), 금액 글자는 코인 윗테 위에(코인 반높이 0.275 + 글자 반높이 약 0.14)
            root.GetComponent<SpriteRenderer>().sortingOrder = 59;
            TextMeshPro amount = WorldText(root.transform, "Amount", new Vector3(0f, 0.42f, 0f), 60);
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

            // 「!!」 말풍선(빵을 못 찾고 떠날 때), 집은 빵, 결제 뒤 하트. 머리 높이는 외형마다 달라 실행 중에 맞춘다
            SpriteRenderer bubble = Renderer(root.transform, "Bubble", Load("angry"), new Vector3(0f, 0.8f, 0f), 50);
            SpriteRenderer carry = Renderer(root.transform, "Carry", null, new Vector3(0f, 0.9f, 0f), 51);
            carry.transform.localScale = Vector3.one * 0.7f;
            TextMeshPro emote = WorldText(root.transform, "Emote", new Vector3(0f, 1f, 0f), 52);
            emote.fontSize = 3.5f;
            emote.color = new Color(0.93f, 0.33f, 0.4f);

            ShopCustomer customer = root.AddComponent<ShopCustomer>();
            Set(customer, "m_modelRoot", model.transform);
            Set(customer, "m_spriteRenderer", sprite);
            Set(customer, "m_shadowRenderer", shadow);
            Set(customer, "m_animator", animator);
            Set(customer, "m_coinPrefab", AssetDatabase.LoadAssetAtPath<CoinPopup>(k_CoinPrefabPath));
            Set(customer, "m_bubble", bubble);
            Set(customer, "m_carry", carry);
            Set(customer, "m_emote", emote);
            return Save(root, customer, "ShopCustomer");
        }

        // Shop 루트: 흙 배경(무한 벽 타일) + 굴 그림(실행 중 생성) + 입구 아치
        static void BakeShop(ShelfView shelf, OvenView oven, CounterView counter, MarkerView digTag, SpriteRenderer slotMarker, ShopCustomer customer)
        {
            GameObject root = new GameObject("Shop");
            SpriteRenderer backdrop = Renderer(root.transform, "Backdrop", Load("wall_tile"), new Vector3(-k_BackdropHalf, k_BackdropHalf, 0f), k_BackdropOrder);
            backdrop.drawMode = SpriteDrawMode.Tiled;
            backdrop.tileMode = SpriteTileMode.Continuous;
            backdrop.size = new Vector2(k_BackdropHalf * 2f, k_BackdropHalf * 2f);
            SpriteRenderer burrow = Renderer(root.transform, "Burrow", null, Vector3.zero, k_BurrowOrder);
            // 아치 그림 53칸 높이가 입구 줄(80칸) 밑변에 닿게: 윗변 = 27칸 = 0.675유닛 아래
            Renderer(root.transform, "Arch", Load("arch"), new Vector3(0f, -0.675f, 0f), k_ArchOrder);

            ShopView view = root.AddComponent<ShopView>();
            Set(view, "m_shelfPrefab", shelf);
            Set(view, "m_ovenPrefab", oven);
            Set(view, "m_counterPrefab", counter);
            Set(view, "m_digTagPrefab", digTag);
            Set(view, "m_slotMarkerPrefab", slotMarker);
            Set(view, "m_burrow", burrow);
            Set(view, "m_floorTile", AssetDatabase.LoadAssetAtPath<Texture2D>(k_SpriteDir + "floor_tile.png"));
            Set(view, "m_wallTile", AssetDatabase.LoadAssetAtPath<Texture2D>(k_SpriteDir + "wall_tile.png"));
            ShopCustomerSpawner spawner = root.AddComponent<ShopCustomerSpawner>();
            Set(spawner, "m_prefab", customer);
            Save(root, view, "Shop");
        }

        // ---------- 공용 ----------

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

        // 비용 태그(UI 규칙 4장: 진갈색 바탕·크림 글자 9px, 월드 스프라이트 PPU 40 = 1px가 한 칸)
        static (SpriteRenderer, TextMeshPro) Tag(Transform parent, float y)
        {
            GameObject tag = Child(parent, "Tag", new Vector3(0f, y, 0f));
            SpriteRenderer bg = Renderer(tag.transform, "Bg", AssetDatabase.LoadAssetAtPath<Sprite>(k_SpriteDir + "tag_cost.png"), Vector3.zero, k_MarkerOrder + 3);
            bg.drawMode = SpriteDrawMode.Sliced;
            bg.size = new Vector2(2.1f, 0.35f);
            TextMeshPro text = WorldText(tag.transform, "Text", Vector3.zero, k_MarkerOrder + 4);
            text.rectTransform.sizeDelta = new Vector2(2.1f, 0.35f);
            text.fontSize = 2.75f;
            text.color = new Color32(0xF4, 0xDF, 0xBF, 255);
            return (bg, text);
        }

        static TextMeshPro WorldText(Transform parent, string name, Vector3 position, int order)
        {
            GameObject go = Child(parent, name, position);
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontPath);
            text.fontSize = 2.75f;
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

        static string TimerFrame(int i)
        {
            return "oven_timer_" + i.ToString("00");
        }

        // 연기 12프레임 이름 뒤붙이(_00~_11)
        static string[] SmokeSuffixes()
        {
            string[] suffixes = new string[k_SmokeFrames];

            for (int i = 0; i < k_SmokeFrames; i++)
            {
                suffixes[i] = "_" + i.ToString("00");
            }

            return suffixes;
        }

        static System.Collections.Generic.IEnumerable<string> FxFrames()
        {
            foreach (string oven in new[] { "oven_fire", "oven_2_fire" })
            {
                for (int i = 0; i < 4; i++)
                {
                    yield return oven + "_" + i;
                }
            }

            foreach (string smoke in new[] { "smoke_gray", "smoke_white" })
            {
                foreach (string suffix in SmokeSuffixes())
                {
                    yield return smoke + suffix;
                }
            }
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
    }
}
