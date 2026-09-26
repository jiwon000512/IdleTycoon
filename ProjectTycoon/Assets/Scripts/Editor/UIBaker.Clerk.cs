using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using ZooTycoon.UI;

namespace ZooTycoon.Editor
{
    // 설계 21 점원 팝업 프리팹(ClerkPopupView): 편집 버튼 아래 점원 버튼 · 가운데 패널(목록 / 고용할까 창 / 협상) · 해고 토스트.
    // 부품·단위는 UIBaker와 같다(1 UI px = 4 캔버스 px). 굽기 전에 Import Visitor Sheets로 회색 웜뱃 정지 그림(버튼 아이콘)이 임포트돼 있어야 한다
    public static partial class UIBaker
    {
        const float k_ClerkPanelWidth = 240 * U;
        const float k_ClerkPanelHeight = 340 * U;
        const float k_ClerkRowHeight = 30 * U;
        const string k_ClerkIconPath = "Assets/Resources/Sprites/Visitors/WombatGray/WombatGray.png";
        static readonly Color k_Scene = new Color32(0x6B, 0x4A, 0x31, 255);
        static readonly Color k_ZoneRed = new Color32(0xA6, 0x4B, 0x3C, 255);
        static readonly Color k_ZoneWhite = new Color32(0xEF, 0xE7, 0xDA, 255);
        static readonly Color k_ZoneGreen = new Color32(0x3E, 0x9A, 0x5A, 255);

        static void BakeClerkPopup()
        {
            GameObject root = Root("ClerkPopupView");

            // 점원 버튼: 편집 버튼(오른쪽 위 28) 바로 아래
            RectTransform openRect = Panel(root.transform, "OpenButton", Sprite("btn_act"), Color.white);
            Anchor(openRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            openRect.sizeDelta = new Vector2(28 * U, 28 * U);
            openRect.anchoredPosition = new Vector2(-3 * U, -34 * U);
            Button open = openRect.gameObject.AddComponent<Button>();
            openRect.gameObject.AddComponent<PressScale>();
            RectTransform openIcon = Panel(openRect, "Icon", AssetDatabase.LoadAssetAtPath<Sprite>(k_ClerkIconPath), Color.white);
            Anchor(openIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            openIcon.sizeDelta = new Vector2(18 * U, 18 * U);
            openIcon.GetComponent<Image>().preserveAspect = true;
            openIcon.GetComponent<Image>().raycastTarget = false;

            // 팝업 뿌리: 어둡게 + 가운데 패널
            RectTransform popup = Child(root.transform, "Popup");
            Stretch(popup, Vector4.zero);
            RectTransform dimRect = Panel(popup, "Dim", Sprite("white"), k_Dim);
            Stretch(dimRect, Vector4.zero);
            Button dim = dimRect.gameObject.AddComponent<Button>();
            dim.transition = Selectable.Transition.None;
            RectTransform panel = Panel(popup, "Panel", Sprite("sheet_frame"), Color.white);
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.sizeDelta = new Vector2(k_ClerkPanelWidth, k_ClerkPanelHeight);

            // 헤더
            RectTransform header = Child(panel, "Header");
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            header.sizeDelta = new Vector2(0f, 24 * U);
            header.anchoredPosition = new Vector2(0f, -8 * U);
            TextMeshProUGUI title = Text(header, "Title", "Galmuri14", 14, k_Ink, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector4(12 * U, 0f, 40 * U, 0f));
            RectTransform closeRect = Panel(header, "Close", Sprite("btn_close"), Color.white);
            Anchor(closeRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            closeRect.sizeDelta = new Vector2(19 * U, 19 * U);
            closeRect.anchoredPosition = new Vector2(-10 * U, 0f);
            Button close = closeRect.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            closeRect.gameObject.AddComponent<PressScale>();

            // 몸통(헤더 아래)
            RectTransform body = Child(panel, "Body");
            Stretch(body, new Vector4(10 * U, 10 * U, 10 * U, 36 * U));

            // ---- 목록 상태 ----
            RectTransform list = Child(body, "List");
            Stretch(list, Vector4.zero);
            RectTransform tabRow = Child(list, "TabRow");
            Anchor(tabRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            tabRow.sizeDelta = new Vector2(0f, 16 * U);
            RectTransform tab = Panel(tabRow, "Tab", Sprite("chip_hi"), Color.white);
            Anchor(tab, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            tab.sizeDelta = new Vector2(56 * U, 16 * U);
            TextMeshProUGUI tabLabel = Text(tab, "Label", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.Center);
            Stretch(tabLabel.rectTransform, Vector4.zero);
            TextMeshProUGUI summary = Text(list, "Summary", "Galmuri9", 9, k_Muted, TextAlignmentOptions.MidlineRight);
            Anchor(summary.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            summary.rectTransform.sizeDelta = new Vector2(0f, 16 * U);
            summary.rectTransform.anchoredPosition = Vector2.zero;

            RectTransform rows = Child(list, "Rows");
            Anchor(rows, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            rows.anchoredPosition = new Vector2(0f, -20 * U);
            rows.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup rowLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 4 * U;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ClerkRowView rowView = BakeClerkRow(rows);

            TextMeshProUGUI foot = Text(list, "Foot", "Galmuri9", 9, k_Muted, TextAlignmentOptions.Center);
            Anchor(foot.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            foot.rectTransform.sizeDelta = new Vector2(0f, 14 * U);
            foot.rectTransform.anchoredPosition = Vector2.zero;
            Button footButton = ButtonUi(list, "FootButton", "btn_secondary", 120 * U, 16 * U, out TextMeshProUGUI footLabel, "Galmuri11-Bold", 11, k_Ink);
            RectTransform footRect = footButton.GetComponent<RectTransform>();
            Anchor(footRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            footRect.anchoredPosition = Vector2.zero;

            // ---- 고용할까 창(패널 위) ----
            RectTransform ask = Child(panel, "Ask");
            Stretch(ask, Vector4.zero);
            RectTransform askDim = Panel(ask, "Dim", Sprite("white"), k_Dim);
            Stretch(askDim, Vector4.zero);
            RectTransform box = Panel(ask, "Box", Sprite("sheet_frame"), Color.white);
            Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            box.sizeDelta = new Vector2(200 * U, 124 * U);
            RectTransform askIcon = Panel(box, "Icon", null, Color.white);
            Anchor(askIcon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            askIcon.sizeDelta = new Vector2(28 * U, 28 * U);
            askIcon.anchoredPosition = new Vector2(0f, -8 * U);
            askIcon.GetComponent<Image>().preserveAspect = true;
            TextMeshProUGUI askTitle = Text(box, "Title", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.Center);
            Anchor(askTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            askTitle.rectTransform.sizeDelta = new Vector2(0f, 14 * U);
            askTitle.rectTransform.anchoredPosition = new Vector2(0f, -40 * U);
            TextMeshProUGUI askSub = Text(box, "Sub", "Galmuri9", 9, k_Muted, TextAlignmentOptions.Center);
            Anchor(askSub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            askSub.rectTransform.sizeDelta = new Vector2(0f, 12 * U);
            askSub.rectTransform.anchoredPosition = new Vector2(0f, -56 * U);
            Button plain = ButtonUi(box, "Plain", "btn_secondary", 80 * U, 16 * U, out TextMeshProUGUI plainLabel, "Galmuri11-Bold", 11, k_Ink);
            RectTransform plainRect = plain.GetComponent<RectTransform>();
            Anchor(plainRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f));
            plainRect.anchoredPosition = new Vector2(-3 * U, 26 * U);
            Button negotiate = ButtonUi(box, "Negotiate", "btn_primary", 80 * U, 16 * U, out TextMeshProUGUI negotiateLabel, "Galmuri11-Bold", 11, k_Cream);
            RectTransform negotiateRect = negotiate.GetComponent<RectTransform>();
            Anchor(negotiateRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f));
            negotiateRect.anchoredPosition = new Vector2(3 * U, 26 * U);
            RectTransform cancelRect = Panel(box, "Cancel", null, Color.clear);
            Anchor(cancelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            cancelRect.sizeDelta = new Vector2(60 * U, 14 * U);
            cancelRect.anchoredPosition = new Vector2(0f, 6 * U);
            Button cancel = cancelRect.gameObject.AddComponent<Button>();
            cancel.transition = Selectable.Transition.None;
            TextMeshProUGUI cancelLabel = Text(cancelRect, "Text", "Galmuri9", 9, k_Muted, TextAlignmentOptions.Center);
            Stretch(cancelLabel.rectTransform, Vector4.zero);

            // ---- 협상 상태 ----
            RectTransform nego = Child(body, "Nego");
            Stretch(nego, Vector4.zero);
            RectTransform scene = Panel(nego, "Scene", Sprite("white"), k_Scene);
            Anchor(scene, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            scene.sizeDelta = new Vector2(0f, 140 * U);
            scene.anchoredPosition = Vector2.zero;
            RectTransform wombat = Panel(scene, "Wombat", AssetDatabase.LoadAssetAtPath<Sprite>(k_ShopSpriteDir + "wombat_side.png"), Color.white);
            Anchor(wombat, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            wombat.sizeDelta = new Vector2(44 * U, 44 * U);
            wombat.anchoredPosition = new Vector2(-46 * U, 16 * U);
            RectTransform candidate = Panel(scene, "Candidate", null, Color.white);
            Anchor(candidate, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            candidate.sizeDelta = new Vector2(44 * U, 44 * U);
            candidate.anchoredPosition = new Vector2(46 * U, 16 * U);
            candidate.localScale = new Vector3(-1f, 1f, 1f);
            TextMeshProUGUI left = Bubble(scene, "LeftBubble", new Vector2(-46 * U, 80 * U));
            TextMeshProUGUI right = Bubble(scene, "RightBubble", new Vector2(46 * U, 80 * U));
            TextMeshProUGUI hint = Text(nego, "Hint", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.Center);
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            hint.rectTransform.sizeDelta = new Vector2(0f, 16 * U);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -150 * U);

            RectTransform bar = Panel(nego, "Bar", Sprite("white"), k_Ink);
            Anchor(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            bar.sizeDelta = new Vector2(0f, 18 * U);
            bar.anchoredPosition = new Vector2(0f, -172 * U);
            RectTransform inner = Child(bar, "Inner");
            Stretch(inner, new Vector4(1 * U, 1 * U, 1 * U, 1 * U));
            Color[] zoneColors = { k_ZoneRed, k_ZoneWhite, k_ZoneGreen, k_ZoneWhite, k_ZoneRed };
            RectTransform[] zones = new RectTransform[zoneColors.Length];

            for (int i = 0; i < zones.Length; i++)
            {
                zones[i] = Panel(inner, "Zone" + i, Sprite("white"), zoneColors[i]);
                zones[i].GetComponent<Image>().raycastTarget = false;
            }

            RectTransform marker = Panel(inner, "Marker", Sprite("white"), k_Ink);
            Anchor(marker, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            marker.sizeDelta = new Vector2(2 * U, 4 * U);
            marker.GetComponent<Image>().raycastTarget = false;
            RectTransform tapRect = Panel(nego, "TapArea", null, Color.clear);
            Stretch(tapRect, Vector4.zero);
            Button tap = tapRect.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;

            // ---- 토스트(화면 위, 팝업 밖) ----
            RectTransform toast = Panel(root.transform, "Toast", Sprite("pill_hud"), k_Shadow);
            Anchor(toast, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            toast.sizeDelta = new Vector2(220 * U, 20 * U);
            toast.anchoredPosition = new Vector2(0f, -44 * U);
            toast.GetComponent<Image>().raycastTarget = false;
            TextMeshProUGUI toastText = Text(toast, "Text", "Galmuri11-Bold", 11, k_Cream, TextAlignmentOptions.Center);
            Stretch(toastText.rectTransform, Vector4.zero);

            ClerkPopupView view = root.AddComponent<ClerkPopupView>();
            Set(view, "m_openButton", open);
            Set(view, "m_root", popup.gameObject);
            Set(view, "m_dim", dim);
            Set(view, "m_closeButton", close);
            Set(view, "m_title", title);
            Set(view, "m_list", list.gameObject);
            Set(view, "m_tabRow", tabRow.gameObject);
            Set(view, "m_tabLabel", tabLabel);
            Set(view, "m_summary", summary);
            Set(view, "m_rows", rows);
            Set(view, "m_rowTemplate", rowView);
            Set(view, "m_foot", foot);
            Set(view, "m_footButton", footButton);
            Set(view, "m_footButtonLabel", footLabel);
            Set(view, "m_ask", ask.gameObject);
            Set(view, "m_askIcon", askIcon.GetComponent<Image>());
            Set(view, "m_askTitle", askTitle);
            Set(view, "m_askSub", askSub);
            Set(view, "m_askPlain", plain);
            Set(view, "m_askPlainLabel", plainLabel);
            Set(view, "m_askNegotiate", negotiate);
            Set(view, "m_askNegotiateLabel", negotiateLabel);
            Set(view, "m_askCancel", cancel);
            Set(view, "m_askCancelLabel", cancelLabel);
            Set(view, "m_nego", nego.gameObject);
            Set(view, "m_negoWombat", wombat.GetComponent<Image>());
            Set(view, "m_negoCandidate", candidate.GetComponent<Image>());
            Set(view, "m_negoLeft", left);
            Set(view, "m_negoRight", right);
            Set(view, "m_negoHint", hint);
            Set(view, "m_marker", marker);
            SetRects(view, "m_zones", zones);
            Set(view, "m_tapArea", tap);
            Set(view, "m_toast", toast.gameObject);
            Set(view, "m_toastText", toastText);
            ask.gameObject.SetActive(false);
            nego.gameObject.SetActive(false);
            toast.gameObject.SetActive(false);
            popup.gameObject.SetActive(false);
            Save(root, "ClerkPopupView");
        }

        // 줄: 왼쪽 그림(24) · 이름(위)·상태(아래) · 오른쪽 버튼(40×14)
        static ClerkRowView BakeClerkRow(RectTransform parent)
        {
            RectTransform row = Panel(parent, "RowTemplate", Sprite("row"), Color.white);
            row.sizeDelta = new Vector2(0f, k_ClerkRowHeight);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = k_ClerkRowHeight;
            RectTransform icon = Panel(row, "Icon", null, Color.white);
            Anchor(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            icon.sizeDelta = new Vector2(24 * U, 24 * U);
            icon.anchoredPosition = new Vector2(4 * U, 0f);
            Image iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            TextMeshProUGUI name = Text(row, "Name", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.TopLeft);
            Stretch(name.rectTransform, new Vector4(32 * U, 0f, 50 * U, 3 * U));
            TextMeshProUGUI sub = Text(row, "Sub", "Galmuri9", 9, k_Muted, TextAlignmentOptions.BottomLeft);
            Stretch(sub.rectTransform, new Vector4(32 * U, 3 * U, 50 * U, 0f));
            Button button = ButtonUi(row, "Button", "btn_primary", 40 * U, 14 * U, out TextMeshProUGUI label, "Galmuri11-Bold", 11, k_Cream);
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            Anchor(buttonRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            buttonRect.anchoredPosition = new Vector2(-4 * U, 0f);
            ClerkRowView view = row.gameObject.AddComponent<ClerkRowView>();
            Set(view, "m_icon", iconImage);
            Set(view, "m_name", name);
            Set(view, "m_sub", sub);
            Set(view, "m_button", button);
            Set(view, "m_buttonLabel", label);
            return view;
        }

        // 말풍선: 크림 알약 위 글자
        static TextMeshProUGUI Bubble(Transform parent, string name, Vector2 position)
        {
            RectTransform bubble = Panel(parent, name, Sprite("row"), Color.white);
            Anchor(bubble, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            bubble.sizeDelta = new Vector2(80 * U, 16 * U);
            bubble.anchoredPosition = position;
            bubble.GetComponent<Image>().raycastTarget = false;
            TextMeshProUGUI text = Text(bubble, "Text", "Galmuri9", 9, k_Ink, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector4.zero);
            return text;
        }

        static void SetRects(Object target, string field, RectTransform[] rects)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = rects.Length;

            for (int i = 0; i < rects.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = rects[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
