using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using ZooTycoon.UI;

namespace ZooTycoon.Editor
{
    // UI-디자인-규칙 v1.0 + 사물 터치 기획 + 설계 09: 상단 바·가게 HUD(조이스틱·상호작용 버튼)·사물 시트 프리팹을 규칙 숫자로 조립한다(손으로 만든 프리팹 없음).
    // 단위: 1 UI px = 캔버스 4px(U). 스프라이트는 Sprites/UI(PPU 25, 9-slice), 폰트는 Fonts/Galmuri
    public static class UIBaker
    {
        const float U = 4f;
        const string k_SpriteDir = "Assets/Sprites/UI/";
        const string k_FontDir = "Assets/Fonts/Galmuri/";
        const string k_UiDir = "Assets/Resources/UI/";
        // 가게 HUD 조이스틱 받침·상호작용 버튼: 같은 크기, 화면 가운데를 기준으로 좌우 대칭(가운데 x 거리, 화면 아래에서 가운데 높이)
        const float k_ControlSize = 44 * U;
        const float k_ControlOffsetX = 68 * U;
        const float k_ControlCenterY = 62 * U;
        // 설계 09 v0.4: 행동 아이콘(actions.json icon). 실행 중에는 View가 경로로 읽는다
        const string k_ActionIconDir = "Assets/Resources/Sprites/Actions/";

        static readonly Color k_Ink = new Color32(0x2E, 0x23, 0x20, 255);
        static readonly Color k_Muted = new Color32(0x5C, 0x4C, 0x42, 255);
        static readonly Color k_Cream = new Color32(0xFB, 0xF4, 0xE6, 255);
        static readonly Color k_TopBar = new Color32(0x2F, 0x4A, 0x3E, 255);
        static readonly Color k_Dim = new Color32(0x3A, 0x24, 0x22, 115);

        [MenuItem("ZooTycoon/Bake/UI")]
        public static void Bake()
        {
            BakeTopBar();
            BakeShopHud();
            BakeObjectSheet();
            AssetDatabase.SaveAssets();
            Debug.Log("UI prefabs: TopBarView, ShopHudView, ObjectSheetView");
        }

        // ---------- 상단 바: 높이 20, 왼쪽 코인 pill(높이 16) ----------
        static void BakeTopBar()
        {
            GameObject root = Root("TopBarView");
            RectTransform bar = Panel(root.transform, "Bar", null, k_TopBar);
            Anchor(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            bar.sizeDelta = new Vector2(0f, 20 * U);
            bar.anchoredPosition = Vector2.zero;

            RectTransform pill = Panel(bar, "CoinPill", Sprite("pill_topbar"), Color.white);
            Anchor(pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            pill.sizeDelta = new Vector2(70 * U, 16 * U);
            pill.anchoredPosition = new Vector2(4 * U, 0f);
            TextMeshProUGUI coins = Text(pill, "CoinsText", "Galmuri11-Bold", 11, k_Cream, TextAlignmentOptions.MidlineRight);
            Stretch(coins.rectTransform, new Vector4(17 * U, 0f, 4 * U, 0f));

            TopBarView view = root.AddComponent<TopBarView>();
            Set(view, "m_coinsText", coins);
            Save(root, "TopBarView");
        }

        // ---------- 가게 HUD(설계 09): 상단 바 아래 가게 이름 + 아래쪽 60% 조이스틱 영역 + 오른쪽 아래 상호작용 버튼(행동 아이콘) ----------
        static void BakeShopHud()
        {
            GameObject root = Root("ShopHudView");

            TextMeshProUGUI title = Text(root.transform, "TitleText", "Galmuri14", 14, k_Cream, TextAlignmentOptions.MidlineLeft);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            title.rectTransform.sizeDelta = new Vector2(120 * U, 20 * U);
            title.rectTransform.anchoredPosition = new Vector2(4 * U, -(24 * U));

            // 조이스틱: 투명 영역(누르는 곳) → 받침(쉬는 자리 = 상호작용 버튼과 좌우 대칭) → 손잡이.
            // 영역 피벗과 받침 앵커를 같은 점(아래 가운데)에 둬야 누른 곳 = 받침 위치가 된다
            RectTransform area = Panel(root.transform, "JoystickArea", null, Color.clear);
            Anchor(area, Vector2.zero, new Vector2(1f, 0.6f), new Vector2(0.5f, 0f));
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;
            RectTransform stickBase = Panel(area, "Base", Sprite("joystick_base"), Color.white);
            Anchor(stickBase, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            stickBase.sizeDelta = new Vector2(k_ControlSize, k_ControlSize);
            stickBase.anchoredPosition = new Vector2(-k_ControlOffsetX, k_ControlCenterY);
            stickBase.GetComponent<Image>().raycastTarget = false;
            CanvasGroup group = stickBase.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            RectTransform knob = Panel(stickBase, "Knob", Sprite("joystick_knob"), Color.white);
            Anchor(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            knob.sizeDelta = new Vector2(20 * U, 20 * U);
            knob.GetComponent<Image>().raycastTarget = false;
            Joystick joystick = area.gameObject.AddComponent<Joystick>();
            Set(joystick, "m_base", stickBase);
            Set(joystick, "m_knob", knob);
            Set(joystick, "m_group", group);

            // 상호작용 버튼: 둥근 바탕 + 가운데 아이콘. 눌림·비활성은 색 틴트
            RectTransform buttonRect = Panel(root.transform, "InteractButton", Sprite("btn_act"), Color.white);
            Anchor(buttonRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            buttonRect.sizeDelta = new Vector2(k_ControlSize, k_ControlSize);
            buttonRect.anchoredPosition = new Vector2(k_ControlOffsetX, k_ControlCenterY);
            Button button = buttonRect.gameObject.AddComponent<Button>();
            buttonRect.gameObject.AddComponent<PressScale>();
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f);
            button.colors = colors;
            RectTransform icon = Panel(buttonRect, "Icon", AssetDatabase.LoadAssetAtPath<Sprite>(k_ActionIconDir + "open.png"), Color.white);
            Anchor(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            icon.sizeDelta = new Vector2(18 * U, 18 * U);
            icon.GetComponent<Image>().raycastTarget = false;

            ShopHudView view = root.AddComponent<ShopHudView>();
            Set(view, "m_titleText", title);
            Set(view, "m_joystick", joystick);
            Set(view, "m_interactButton", button);
            Set(view, "m_interactIcon", icon.GetComponent<Image>());
            Save(root, "ShopHudView");
        }

        // ---------- 사물 시트: 딤 + 하단 패널(나무 틀) → 헤더 / 칩 줄 / 행 목록 ----------
        static void BakeObjectSheet()
        {
            GameObject root = Root("ObjectSheetView");

            RectTransform dimRect = Panel(root.transform, "Dim", Sprite("white"), k_Dim);
            Stretch(dimRect, Vector4.zero);
            Button dim = dimRect.gameObject.AddComponent<Button>();
            dim.transition = Selectable.Transition.None;

            RectTransform panel = Panel(root.transform, "Panel", Sprite("sheet_frame"), Color.white);
            Anchor(panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            panel.sizeDelta = Vector2.zero;
            panel.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)(12 * U), (int)(12 * U), (int)(10 * U), (int)(12 * U));
            layout.spacing = 4 * U;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 헤더
            RectTransform header = Child(panel, "Header");
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 20 * U;
            TextMeshProUGUI title = Text(header, "Title", "Galmuri14", 14, k_Ink, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector4(0f, 0f, 100 * U, 0f));
            TextMeshProUGUI status = Text(header, "Status", "Galmuri9", 9, k_Muted, TextAlignmentOptions.MidlineRight);
            Stretch(status.rectTransform, new Vector4(80 * U, 0f, 26 * U, 0f));
            RectTransform closeRect = Panel(header, "Close", Sprite("btn_close"), Color.white);
            Anchor(closeRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            closeRect.sizeDelta = new Vector2(19 * U, 19 * U);
            Button close = closeRect.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            closeRect.gameObject.AddComponent<PressScale>();

            // 칩 줄
            RectTransform chipRow = Child(panel, "ChipRow");
            HorizontalLayoutGroup chipLayout = chipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 4 * U;
            chipLayout.childControlWidth = false;
            chipLayout.childControlHeight = false;
            chipLayout.childForceExpandWidth = false;
            chipLayout.childForceExpandHeight = false;
            chipLayout.childAlignment = TextAnchor.UpperLeft;
            chipRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 52 * U;
            Button chip = ButtonUi(chipRow, "ChipTemplate", "chip", 56 * U, 52 * U, out _, null, 0, k_Ink);
            chip.gameObject.AddComponent<CanvasGroup>();
            RectTransform icon = Panel(chip.transform, "Icon", null, Color.white);
            icon.GetComponent<Image>().preserveAspect = true;
            Anchor(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            icon.anchoredPosition = new Vector2(0f, -(4 * U));
            icon.sizeDelta = new Vector2(26 * U, 22 * U);
            TextMeshProUGUI label = Text(chip.transform, "Label", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.Bottom);
            Stretch(label.rectTransform, new Vector4(2 * U, 14 * U, 2 * U, 0f));
            TextMeshProUGUI sub = Text(chip.transform, "Sub", "Galmuri9", 9, k_Muted, TextAlignmentOptions.Bottom);
            Stretch(sub.rectTransform, new Vector4(2 * U, 4 * U, 2 * U, 0f));

            // 행 목록
            RectTransform rowList = Child(panel, "RowList");
            VerticalLayoutGroup rowLayout = rowList.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 4 * U;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Button row = ButtonUi(rowList, "RowTemplate", "row", 0f, 28 * U, out _, null, 0, k_Ink);
            row.gameObject.AddComponent<CanvasGroup>();
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 28 * U;
            TextMeshProUGUI name = Text(row.transform, "Name", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.TopLeft);
            Stretch(name.rectTransform, new Vector4(8 * U, 0f, 60 * U, 3 * U));
            TextMeshProUGUI effect = Text(row.transform, "Effect", "Galmuri9", 9, k_Muted, TextAlignmentOptions.BottomLeft);
            Stretch(effect.rectTransform, new Vector4(8 * U, 3 * U, 60 * U, 0f));
            RectTransform pill = Panel(row.transform, "Pill", Sprite("pill_cost"), Color.white);
            Anchor(pill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            pill.sizeDelta = new Vector2(50 * U, 12 * U);
            pill.anchoredPosition = new Vector2(-(6 * U), 0f);
            TextMeshProUGUI cost = Text(pill, "Cost", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.MidlineRight);
            Stretch(cost.rectTransform, new Vector4(14 * U, 0f, 4 * U, 0f));

            ObjectSheetView view = root.AddComponent<ObjectSheetView>();
            Set(view, "m_dim", dim);
            Set(view, "m_panel", panel);
            Set(view, "m_titleText", title);
            Set(view, "m_statusText", status);
            Set(view, "m_closeButton", close);
            Set(view, "m_chipRow", chipRow);
            Set(view, "m_chipTemplate", chip);
            Set(view, "m_chipSprite", Sprite("chip"));
            Set(view, "m_chipHighlightSprite", Sprite("chip_hi"));
            Set(view, "m_rowList", rowList);
            Set(view, "m_rowTemplate", row);
            panel.gameObject.SetActive(false);
            dimRect.gameObject.SetActive(false);
            Save(root, "ObjectSheetView");
        }

        // ---------- 부품 ----------

        static GameObject Root(string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.SetActive(false);
            Stretch(root.GetComponent<RectTransform>(), Vector4.zero);
            return root;
        }

        static RectTransform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static RectTransform Panel(Transform parent, string name, Sprite sprite, Color color)
        {
            RectTransform rect = Child(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            return rect;
        }

        // 버튼: 9-slice 배경 + 눌림·비활성 스프라이트 교체(규칙 3장: 눌림 = 1px 아래 + 위 밝음 없음)
        static Button ButtonUi(Transform parent, string name, string sprite, float width, float height, out TextMeshProUGUI label, string font, int size, Color color)
        {
            RectTransform rect = Panel(parent, name, Sprite(sprite), Color.white);
            rect.sizeDelta = new Vector2(width, height);
            Button button = rect.gameObject.AddComponent<Button>();
            rect.gameObject.AddComponent<PressScale>();
            Sprite pressed = AssetDatabase.LoadAssetAtPath<Sprite>(k_SpriteDir + sprite + "_pressed.png");
            Sprite disabled = AssetDatabase.LoadAssetAtPath<Sprite>(k_SpriteDir + sprite + "_disabled.png");

            if (pressed != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState state = new SpriteState { pressedSprite = pressed, disabledSprite = disabled != null ? disabled : pressed, highlightedSprite = Sprite(sprite), selectedSprite = Sprite(sprite) };
                button.spriteState = state;
            }
            else
            {
                button.transition = Selectable.Transition.None;
            }

            label = null;

            if (font != null)
            {
                label = Text(rect, "Text", font, size, color, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, Vector4.zero);
            }

            return button;
        }

        static TextMeshProUGUI Text(Transform parent, string name, string font, int size, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = Child(parent, name);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontDir + font + ".asset");
            text.fontSize = size * U;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = "-";
            return text;
        }

        static Sprite Sprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(k_SpriteDir + name + ".png");
        }

        static void Stretch(RectTransform rect, Vector4 padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding.x, padding.y);
            rect.offsetMax = new Vector2(-padding.z, -padding.w);
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
        }

        static void Set(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Save(GameObject root, string name)
        {
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, k_UiDir + name + ".prefab");
            Object.DestroyImmediate(root);
        }
    }
}
