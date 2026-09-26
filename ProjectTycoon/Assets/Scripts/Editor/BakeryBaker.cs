using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using TMPro;
using ZooTycoon.Core;
using ZooTycoon.World;

namespace ZooTycoon.Editor
{
    // 설계 08 v0.5 · 굴 격자 설계 v0.5 · 손님 동선 설계 v0.2: 빵집 프리팹을 통째로 다시 만든다. 사물 자리 숫자는 Core BakeryLayout과 같은 값을 쓴다.
    // 사물 프리팹(진열대·오븐·계산대) + 표시(파기 태그·빈 자리) + Shop(BakeryView·손님 스포너·흙 배경·굴 그림·입구 아치) + VisitorView.
    // 스프라이트는 한 칸 2px·PPU 80(Sprites/World/Shop, Resources/Sprites/Shop/Breads, 원본 Shop/Source~). 굴 그림 재료(타일·빈 자리)는 한 칸 1px·PPU 40
    public static class BakeryBaker
    {
        const string k_SpriteDir = "Assets/Sprites/World/Shop/";
        const string k_BreadDir = "Assets/Resources/Sprites/Shop/Breads/";
        const string k_PrefabDir = "Assets/Prefabs/Bakery/";
        const string k_ShadowPath = "Assets/Sprites/World/shadow.png";
        const string k_PlazaDir = "Assets/Sprites/World/Plaza/";
        const string k_DecorDir = "Assets/Resources/Sprites/Decor/";
        // 설계 11 광장 빵집 문(원점 = 구멍 밑변 가운데, 유닛): 차양은 아치 윗부분을 덮고, 간판은 문 왼쪽 띠 가운데
        const float k_AwningHeight = 1.0f;
        const float k_SignOffsetX = 1.3f;
        const float k_SignHeight = 0.5f;
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
        const int k_ShadowOrder = -900;
        // 설계 09: 웜뱃이 든 빵 층 수·높이·크기·층 간격(층 로컬). 높이·앞뒤 순서는 실행 중 WombatView이 방향에 맞춰 옮긴다
        const int k_CarryLayers = 5;
        const float k_CarryHeight = 1.05f;
        const float k_CarryScale = 0.6f;
        const float k_CarryStep = 0.3f;
        // 흙 배경: 굴 원점에서 이만큼 왼쪽 위에서 시작해 두 배 크기(타일 32칸 주기에 맞는 값)
        const float k_BackdropHalf = 32f;

        [MenuItem("ZooTycoon/Bake/Bakery")]
        public static void Bake()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            ImportSprites();

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Bakery"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Bakery");
            }

            foreach (string old in new[] { "ShopEntrance", "ShopShelfRow", "ShopOvenRow" })
            {
                AssetDatabase.DeleteAsset(k_PrefabDir + old + ".prefab");
            }

            ShelfView shelf = BakeShelf();
            ShelfSignView shelfSign = BakeShelfSign();
            OvenView oven = BakeOven();
            CounterView counter = BakeCounter();
            MarkerView digTag = BakeDigTag();
            BakeCoinPopup();
            VisitorView customer = BakeCustomer();
            BakeShop(shelf, shelfSign, oven, counter, digTag, customer);
            BakePlaza(customer);
            AssetDatabase.SaveAssets();
            return "Bakery: Shelf·ShelfSign·Oven·Counter·DigTag·SlotMarker·Bakery·Visitor·Plaza (UI 프리팹은 ZooTycoon/Bake/UI)";
        }

        static void ImportSprites()
        {
            Vector2 bottom = new Vector2(0.5f, 0f);
            Vector2 center = new Vector2(0.5f, 0.5f);

            // 굴 환경 A2: 피벗 = 구멍 밑변 = 띠 밑변(입구 줄 바닥 윗변)
            Import(k_SpriteDir + "arch.png", bottom);

            foreach (string name in new[] { "shelf", "shelf_sign", "oven", "oven_2", "counter", "wait" })
            {
                Import(k_SpriteDir + name + ".png", bottom);
            }

            // 설계 22: 글자 말풍선 상자(9-slice, 꼬리 아래 가운데 = 피벗)와 이모지 말풍선 시트(9칸 52px, BubbleTable 칸 번호)
            Import(k_SpriteDir + "bubble.png", bottom, k_Ppu, false, new Vector4(8f, 16f, 8f, 8f));
            VisitorSheetImporter.Import(k_SpriteDir + "wait_sheet.png", true, 52);
            VisitorSheetImporter.Import(k_SpriteDir + "bubble_sheet.png", true, 52);

            foreach (string side in new[] { "front", "back", "side" })
            {
                foreach (string suffix in new[] { "", "_1", "_2", "_3", "_walk_0", "_walk_1", "_walk_2", "_walk_3" })
                {
                    string path = k_SpriteDir + "wombat_" + side + suffix + ".png";
                    Import(path, CellBottom(path), k_UnitPpu);
                }
            }

            for (int i = 0; i < k_TimerFrames; i++)
            {
                Import(k_SpriteDir + TimerFrame(i) + ".png", center);
                Import(k_SpriteDir + CounterTimerFrame(i) + ".png", center);
            }

            // 설계 10: 오븐 상태 표시(Source~/make_oven_idle.py). 타이머 자리
            Import(k_SpriteDir + "oven_empty_mark.png", center);
            Import(k_SpriteDir + "oven_ready_mark.png", center);

            // 오븐 연출: 불빛은 몸통과 같은 크기·하단 가운데, 연기는 하단 가운데 = 굴뚝 입구
            foreach (string name in FxFrames())
            {
                Import(k_SpriteDir + name + ".png", bottom);
            }

            // 발밑 그림자: 격자에 맞춘 최종 모양(Source~/make_shadow.py), 흰색이라 렌더러가 검정 35%로 칠한다
            Import(k_ShadowPath, center);
            Import(k_SpriteDir + "slot_empty.png", center, k_TagPpu);

            // 설계 11 광장: 차양·계단은 아래 가운데, 간판은 가운데. 장식은 Resources(DecorationTable 경로), 아래 가운데
            Import(k_PlazaDir + "awning.png", bottom);
            Import(k_PlazaDir + "sign.png", center);
            Import(k_PlazaDir + "stairs.png", bottom);

            foreach (string path in System.IO.Directory.GetFiles(k_DecorDir, "*.png"))
            {
                Import(path.Replace('\\', '/'), bottom);
            }
            // 타일: 굴 그림이 픽셀을 읽고, 흙 배경은 Tiled로 깐다(왼쪽 위 피벗)
            Import(k_SpriteDir + "floor_tile.png", new Vector2(0f, 1f), k_TagPpu, true);
            Import(k_SpriteDir + "wall_tile.png", new Vector2(0f, 1f), k_TagPpu, true);
            Import(k_SpriteDir + "wall_face.png", new Vector2(0f, 1f), k_TagPpu, true);

            foreach (string name in new[] { "b01", "b02", "b03" })
            {
                Import(k_BreadDir + name + ".png", center);
            }
        }

        static void Import(string path, Vector2 pivot, float ppu = k_Ppu, bool tile = false, Vector4 border = default)
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
            settings.spriteBorder = border;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        // 발끝 가운데 피벗을 한 칸(2px) 경계로. 폭이 홀수 칸이면(웜뱃 옆모습 74·78px) 가운데가 칸 중간이라 그림자 격자와 반 칸 어긋난다
        static Vector2 CellBottom(string path)
        {
            ((TextureImporter)AssetImporter.GetAtPath(path)).GetSourceTextureWidthAndHeight(out int width, out int _);
            return new Vector2(width / 4 * 2f / width, 0f);
        }

        static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(k_SpriteDir + name + ".png");
        }

        // 시트의 잘린 칸들을 이름(_0, _1, …) 순으로
        static Sprite[] LoadFrames(string name)
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(k_SpriteDir + name + ".png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
        }

        // ---------- 사물 ----------

        // 진열대 하나. 피벗 = 밑변 가운데. 손님이 서는 자리는 Core BakeryLayout
        static ShelfView BakeShelf()
        {
            GameObject go = new GameObject("Shelf");
            go.AddComponent<SortingGroup>();
            SpriteRenderer body = Renderer(go.transform, "Body", Load("shelf"), Vector3.zero, 0);
            SpriteRenderer icon = Renderer(go.transform, "Icon", null, new Vector3(0f, 0.62f, 0f), 1);

            ShelfView view = go.AddComponent<ShelfView>();
            Set(view, "m_body", body);
            Set(view, "m_icon", icon);
            return Save(go, view, "Shelf");
        }

        // 진열대 재고 표지판(칠판 입간판, Source~/make_shelf_sign.py). 피벗 = 다리 밑변 가운데, 자리는 Core BakeryLayout.ShelfSignBase.
        // 분필 숫자는 칠판 가운데(발끝에서 15.5칸 위)
        static ShelfSignView BakeShelfSign()
        {
            GameObject go = new GameObject("ShelfSign");
            go.AddComponent<SortingGroup>();
            Renderer(go.transform, "Body", Load("shelf_sign"), Vector3.zero, 0);
            TextMeshPro text = WorldText(go.transform, "Stock", new Vector3(0f, 15.5f / BurrowShape.k_PixelsPerUnit, 0f), 1);
            text.rectTransform.sizeDelta = new Vector2(0.4f, 0.35f);

            ShelfSignView view = go.AddComponent<ShelfSignView>();
            Set(view, "m_stockText", text);
            return Save(go, view, "ShelfSign");
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
            GameObject root = new GameObject("Counter");
            CounterView counter = root.AddComponent<CounterView>();
            // 몸체 + 출력기는 SortingGroup(밑변 = CounterBase)으로 묶는다. 안 묶으면 출력기의 order 3이 전역이라 계산대 앞에 선 웜뱃 위에 그려진다(2026-09-25). 웜뱃은 그룹 밖(root 자식)
            // 설계 18: 프리팹 원점 = 계산대 밑변(진열대·오븐과 같다). 옛 「계산대 줄 윗변」 원점은 자유 배치에서 그림이 1.55 아래로 어긋났다
            GameObject body = Child(root.transform, "Body", Vector3.zero);
            body.AddComponent<SortingGroup>();
            SpriteRenderer counterBody = Renderer(body.transform, "Counter", Load("counter"), Vector3.zero, 0);
            Set(counter, "m_body", counterBody);
            Set(counter, "m_wombat", BakeWombat(root.transform, new Vector3(0f, 0.4f, 0f)));
            // 2026-09-25 사용자 선택 C: 영수증 출력기(Source~/make_counter_timer.py). 늘 계산대 위에 있고 계산 중에 영수증이 올라온다.
            // 자리는 계산대 왼쪽, 출력기 밑 외곽선이 금전등록기 밑변과 같은 줄(계산대 밑에서 15칸 위)에 서게 가운데 = (−0.6, +0.725)
            Sprite[] timerFrames = new Sprite[k_TimerFrames];

            for (int i = 0; i < k_TimerFrames; i++)
            {
                timerFrames[i] = Load(CounterTimerFrame(i));
            }

            SpriteRenderer timer = Renderer(body.transform, "Timer", timerFrames[0], new Vector3(-0.6f, 0.725f, 0f), 3);
            Set(counter, "m_timer", timer);
            SetSprites(counter, "m_timerFrames", timerFrames);
            return Save(root, counter, "Counter");
        }

        // 웜뱃(빵집 계산대·광장 공용). 정면·뒷모습: 가게 유닛 기본 크기(k_UnitPpu), 스케일 1. 숨쉬기 0 → 1 → 2 → 3(Source~/make_breath_frames.py)
        static WombatView BakeWombat(Transform parent, Vector3 position)
        {
            SpriteRenderer wombat = Renderer(parent, "Wombat", Load("wombat_front"), position, 0);
            SpriteAnimator animator = wombat.gameObject.AddComponent<SpriteAnimator>();
            Set(animator, "m_renderer", wombat);
            SpriteRenderer shadow = Shadow(wombat.transform);
            // 걷기(v0.6): 딛기 → 왼발 → 딛기 → 오른발(Source~/make_walk_frames.py)
            WombatView mover = wombat.gameObject.AddComponent<WombatView>();
            Set(mover, "m_animator", animator);
            Set(mover, "m_renderer", wombat);
            Set(mover, "m_shadow", shadow);
            // 옆모습(손님 동선 설계 v0.2): 오른쪽 보는 그림, 왼쪽은 뒤집기. 실제 아트 전까지 앞모습 사본(더미)
            foreach (string side in new[] { "front", "back", "side" })
            {
                SetSprites(mover, "m_" + side + "Idle", Frames("wombat_" + side, "", "_1", "_2", "_3"));
                SetSprites(mover, "m_" + side + "Walk", Frames("wombat_" + side, "_walk_0", "_walk_1", "_walk_2", "_walk_3"));
            }

            // 설계 22: 머리 위 이모지 말풍선·글자 말풍선(웜뱃 키 1.1)
            BakeBubbles(wombat.transform, mover, k_WombatHeight);
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
            return mover;
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

        // 손님 몸체: 루트(SpriteAnimator) → ModelRoot(크기) → Sprite + Shadow(납작한 타원). 외형은 VisitorTable 행이 실행 중에 채운다
        static VisitorView BakeCustomer()
        {
            GameObject root = new GameObject("Visitor");
            GameObject model = Child(root.transform, "ModelRoot", Vector3.zero);
            SpriteRenderer sprite = Renderer(model.transform, "Sprite", null, Vector3.zero, 0);
            SpriteRenderer shadow = Shadow(model.transform);
            SpriteAnimator animator = root.AddComponent<SpriteAnimator>();
            Set(animator, "m_renderer", sprite);

            // 집은 빵. 머리 위 말풍선(이모지 시트·글자 상자)은 BakeBubbles, 머리 높이는 외형마다 달라 실행 중에 맞춘다
            SpriteRenderer carry = Renderer(root.transform, "Carry", null, new Vector3(0f, 0.9f, 0f), 51);
            carry.transform.localScale = Vector3.one * 0.7f;

            VisitorView customer = root.AddComponent<VisitorView>();
            Set(customer, "m_modelRoot", model.transform);
            Set(customer, "m_spriteRenderer", sprite);
            Set(customer, "m_shadowRenderer", shadow);
            Set(customer, "m_animator", animator);
            Set(customer, "m_coinPrefab", AssetDatabase.LoadAssetAtPath<CoinPopup>(k_CoinPrefabPath));
            Set(customer, "m_carry", carry);
            BakeBubbles(root.transform, customer, 0.8f);
            return Save(root, customer, "Visitor");
        }

        // 설계 22: 머리 위 이모지 말풍선(bubble_sheet 칸, order 50)과 글자 말풍선(bubble 9-slice 상자 53 + 글 54). 필드 이름은 VisitorView·WombatView 공통
        const float k_WombatHeight = 1.15f;

        static void BakeBubbles(Transform parent, Object view, float height)
        {
            Sprite[] frames = LoadFrames("bubble_sheet");
            SpriteRenderer bubble = Renderer(parent, "Bubble", frames[0], new Vector3(0f, height, 0f), 50);
            SpriteRenderer say = Renderer(parent, "Say", Load("bubble"), new Vector3(0f, height, 0f), 53);
            say.drawMode = SpriteDrawMode.Sliced;
            say.size = new Vector2(0.8f, 0.6f);
            TextMeshPro sayText = WorldText(say.transform, "Text", new Vector3(0f, 0.4f, 0f), 54);
            sayText.fontSize = 2.5f;
            sayText.rectTransform.sizeDelta = new Vector2(3f, 0.4f);
            Set(view, "m_bubble", bubble);
            SetSprites(view, "m_bubbleFrames", frames);
            Set(view, "m_say", say);
            Set(view, "m_sayText", sayText);
        }

        // Shop 루트: 흙 배경(무한 벽 타일) + 굴 그림(실행 중 생성) + 입구 아치
        static void BakeShop(ShelfView shelf, ShelfSignView shelfSign, OvenView oven, CounterView counter, MarkerView digTag, VisitorView customer)
        {
            GameObject root = new GameObject("Bakery");
            SpriteRenderer backdrop = Renderer(root.transform, "Backdrop", Load("wall_tile"), new Vector3(-k_BackdropHalf, k_BackdropHalf, 0f), k_BackdropOrder);
            backdrop.drawMode = SpriteDrawMode.Tiled;
            backdrop.tileMode = SpriteTileMode.Continuous;
            backdrop.size = new Vector2(k_BackdropHalf * 2f, k_BackdropHalf * 2f);
            SpriteRenderer burrow = Renderer(root.transform, "Burrow", null, Vector3.zero, k_BurrowOrder);
            // 아치 그림 53칸 높이가 입구 줄(80칸) 밑변에 닿게: 윗변 = 27칸 = 0.675유닛 아래
            SpriteRenderer arch = Renderer(root.transform, "Arch", Load("arch"), new Vector3(0f, -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit, 0f), k_ArchOrder);

            BakeryView view = root.AddComponent<BakeryView>();
            Set(view, "m_shelfPrefab", shelf);
            Set(view, "m_shelfSignPrefab", shelfSign);
            Set(view, "m_ovenPrefab", oven);
            Set(view, "m_counterPrefab", counter);
            Set(view, "m_digTagPrefab", digTag);
            Set(view, "m_ghostShelf", Load("shelf"));
            Set(view, "m_ghostOven", Load("oven"));
            Set(view, "m_ghostCounter", Load("counter"));
            Set(view, "m_burrow", burrow);
            Set(view, "m_arch", arch.transform);
            SetBurrowTextures(view);
            BakeryVisitorSpawner spawner = root.AddComponent<BakeryVisitorSpawner>();
            Set(spawner, "m_prefab", customer);
            Save(root, view, "Bakery");
        }

        // 설계 11: 굴 밖 광장. 굴 그림·빵집 문·계단·장식 자리는 실행 중 PlazaView가 Core 배치(PlazaLayout)대로 놓는다
        static void BakePlaza(VisitorView customer)
        {
            GameObject root = new GameObject("Plaza");
            SpriteRenderer backdrop = Renderer(root.transform, "Backdrop", Load("wall_tile"), new Vector3(-k_BackdropHalf, k_BackdropHalf, 0f), k_BackdropOrder);
            backdrop.drawMode = SpriteDrawMode.Tiled;
            backdrop.tileMode = SpriteTileMode.Continuous;
            backdrop.size = new Vector2(k_BackdropHalf * 2f, k_BackdropHalf * 2f);
            SpriteRenderer burrow = Renderer(root.transform, "Burrow", null, Vector3.zero, k_BurrowOrder);

            GameObject door = Child(root.transform, "BakeryDoor", Vector3.zero);
            Renderer(door.transform, "Arch", Load("arch"), Vector3.zero, k_ArchOrder);
            Renderer(door.transform, "Awning", AssetDatabase.LoadAssetAtPath<Sprite>(k_PlazaDir + "awning.png"), new Vector3(0f, k_AwningHeight, 0f), k_ArchOrder + 1);
            Renderer(door.transform, "Sign", AssetDatabase.LoadAssetAtPath<Sprite>(k_PlazaDir + "sign.png"), new Vector3(-k_SignOffsetX, k_SignHeight, 0f), k_ArchOrder + 1);
            TextMeshPro sign = WorldText(door.transform, "SignText", new Vector3(-k_SignOffsetX, k_SignHeight, 0f), k_ArchOrder + 2);
            sign.rectTransform.sizeDelta = new Vector2(1f, 0.35f);
            sign.color = new Color32(0xF4, 0xDF, 0xBF, 255);
            SpriteRenderer stairs = Renderer(root.transform, "Stairs", AssetDatabase.LoadAssetAtPath<Sprite>(k_PlazaDir + "stairs.png"), Vector3.zero, k_ArchOrder);
            WombatView wombat = BakeWombat(root.transform, Vector3.zero);

            PlazaView view = root.AddComponent<PlazaView>();
            Set(view, "m_burrow", burrow);
            SetBurrowTextures(view);
            Set(view, "m_door", door.transform);
            Set(view, "m_sign", sign);
            Set(view, "m_stairs", stairs.transform);
            Set(view, "m_wombat", wombat);
            PlazaVisitorSpawner spawner = root.AddComponent<PlazaVisitorSpawner>();
            Set(spawner, "m_prefab", customer);
            Save(root, view, "Plaza");
        }

        static void SetBurrowTextures(Object view)
        {
            Set(view, "m_floorTile", AssetDatabase.LoadAssetAtPath<Texture2D>(k_SpriteDir + "floor_tile.png"));
            Set(view, "m_wallTile", AssetDatabase.LoadAssetAtPath<Texture2D>(k_SpriteDir + "wall_tile.png"));
            Set(view, "m_wallFace", AssetDatabase.LoadAssetAtPath<Texture2D>(k_SpriteDir + "wall_face.png"));
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

        // 발밑 그림자(웜뱃·손님 공용): 발끝 가운데, 몸 뒤. 흰 그림을 검정 35%로. 손님은 이 알파에 투명도를 곱한다
        static SpriteRenderer Shadow(Transform parent)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(k_ShadowPath);
            SpriteRenderer shadow = Renderer(parent, "Shadow", sprite, Vector3.zero, k_ShadowOrder);
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            return shadow;
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
            text.textWrappingMode = TextWrappingModes.NoWrap;
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

        static string CounterTimerFrame(int i)
        {
            return "counter_timer_c_" + i.ToString("00");
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
