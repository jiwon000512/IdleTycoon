using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using ZooTycoon.UI;

namespace ZooTycoon.Editor
{
    // 설계 21·22 점원 팝업 프리팹(ClerkPopupView): 편집 버튼 아래 점원 버튼 · 가운데 패널(목록 / 고용할까 창 / 협상) · 해고 토스트.
    // 2026-09-26 폴리싱(시안 D1 명찰 카드, 사용자 선택): 줄 = 초상 틀 + 상태 배지 | 이름 + 자리 알약 + 두 칸 표(일머리 게이지·월급) | 세로로 긴 버튼(가운데 높이).
    // 한 글에 정보 하나(「·」로 잇지 않는다). 부품·단위는 UIBaker와 같다(1 UI px = 4 캔버스 px). 굽기 전에 Import Visitor Sheets로 회색 웜뱃 정지 그림이 임포트돼 있어야 한다
    public static partial class UIBaker
    {
        const float k_Cell = 5f;   // 시안 D1 한 칸 = 캔버스 5px(clerk_* 조각은 PPU 20). 아래 좌표는 모두 시안 칸 수
        const float k_ClerkPanelWidth = 192 * k_Cell;
        const float k_ClerkPanelHeight = 282 * k_Cell;
        const float k_ClerkRowHeight = 49 * k_Cell;
        const float k_GaugeWidth = 28 * k_Cell;
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
            RectTransform panel = Panel(popup, "Panel", Sprite("clerk_panel"), Color.white);
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.sizeDelta = new Vector2(k_ClerkPanelWidth, k_ClerkPanelHeight);
            panel.anchoredPosition = new Vector2(0f, 4 * k_Cell);

            // 헤더: 제목 왼쪽 위, 닫기 17 오른쪽 위
            TextMeshProUGUI title = Text(panel, "Title", "Galmuri14", 14, k_Ink, TextAlignmentOptions.MidlineLeft);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            title.rectTransform.sizeDelta = new Vector2(-41 * k_Cell, 20 * k_Cell);
            title.rectTransform.anchoredPosition = new Vector2(11 * k_Cell, -10 * k_Cell);
            RectTransform closeRect = Panel(panel, "Close", Sprite("clerk_close"), Color.white);
            TopRight(closeRect, 8 * k_Cell, 8 * k_Cell, 17 * k_Cell, 17 * k_Cell);
            Button close = closeRect.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            closeRect.gameObject.AddComponent<PressScale>();

            // 몸통: 카드 줄이 시작하는 자리(위 47, 좌우 7, 아래 8)
            RectTransform body = Child(panel, "Body");
            Stretch(body, new Vector4(7 * k_Cell, 8 * k_Cell, 7 * k_Cell, 47 * k_Cell));

            // ---- 목록 상태: 가게 탭(몸통 위 28 자리) · 스크롤 줄 목록(카드 + 맨 아래 요약 줄) · 바닥 버튼 ----
            RectTransform list = Child(body, "List");
            Stretch(list, Vector4.zero);
            RectTransform tabRow = Child(list, "TabRow");
            Anchor(tabRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            tabRow.sizeDelta = new Vector2(0f, 16 * k_Cell);
            tabRow.anchoredPosition = new Vector2(0f, 19 * k_Cell);
            RectTransform tab = Panel(tabRow, "Tab", Sprite("clerk_tab"), Color.white);
            Anchor(tab, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            tab.sizeDelta = new Vector2(51 * k_Cell, 16 * k_Cell);
            TextMeshProUGUI tabLabel = Text(tab, "Label", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.Center);
            Stretch(tabLabel.rectTransform, Vector4.zero);

            RectTransform viewport = Child(list, "Viewport");
            Stretch(viewport, new Vector4(0f, 30 * k_Cell, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform rows = Child(viewport, "Rows");
            Anchor(rows, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            rows.anchoredPosition = Vector2.zero;
            rows.sizeDelta = new Vector2(0f, 0f);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = rows;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            VerticalLayoutGroup rowLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 4 * k_Cell;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ClerkRowView rowView = BakeClerkRow(rows);

            // 요약 줄(마지막 카드 바로 아래): 왼쪽 점원 수, 오른쪽 월급 합
            RectTransform summaryRow = Child(rows, "SummaryRow");
            summaryRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 20 * k_Cell;
            TextMeshProUGUI summaryLeft = Text(summaryRow, "SummaryLeft", "Galmuri9", 9, k_Muted, TextAlignmentOptions.MidlineLeft);
            summaryLeft.rectTransform.anchorMin = new Vector2(0f, 0f);
            summaryLeft.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            summaryLeft.rectTransform.offsetMin = new Vector2(1 * k_Cell, 0f);
            summaryLeft.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI summaryRight = Text(summaryRow, "SummaryRight", "Galmuri9", 9, k_Muted, TextAlignmentOptions.MidlineRight);
            summaryRight.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            summaryRight.rectTransform.anchorMax = new Vector2(1f, 1f);
            summaryRight.rectTransform.offsetMin = Vector2.zero;
            summaryRight.rectTransform.offsetMax = new Vector2(-8 * k_Cell, 0f);

            // 바닥: 글(시안에는 없어 비워 둔다) / 「새 후보 보기」 버튼(값 칸 포함)
            TextMeshProUGUI foot = Text(list, "Foot", "Galmuri9", 9, k_Muted, TextAlignmentOptions.Center);
            Anchor(foot.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            foot.rectTransform.sizeDelta = new Vector2(0f, 14 * k_Cell);
            foot.rectTransform.anchoredPosition = Vector2.zero;
            Button footButton = ButtonUi(list, "FootButton", "btn_secondary", 130 * U, 16 * U, out TextMeshProUGUI footLabel, "Galmuri11-Bold", 11, k_Ink);
            RectTransform footRect = footButton.GetComponent<RectTransform>();
            Anchor(footRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            footRect.anchoredPosition = new Vector2(0f, 4 * k_Cell);
            Stretch(footLabel.rectTransform, new Vector4(6 * U, 0f, 46 * U, 0f));
            footLabel.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform footPill = Panel(footRect, "Pill", Sprite("pill_cost"), Color.white);
            Anchor(footPill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            footPill.sizeDelta = new Vector2(40 * U, 12 * U);
            footPill.anchoredPosition = new Vector2(-3 * U, 0f);
            footPill.GetComponent<Image>().raycastTarget = false;
            TextMeshProUGUI footCost = Text(footPill, "Cost", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.MidlineRight);
            Stretch(footCost.rectTransform, new Vector4(14 * U, 0f, 4 * U, 0f));

            // ---- 고용할까 창(패널 위): 초상 · 「OO 고용할까?」 · 두 칸 표 · 버튼 둘 · 취소 ----
            RectTransform ask = Child(panel, "Ask");
            Stretch(ask, Vector4.zero);
            RectTransform askDim = Panel(ask, "Dim", Sprite("white"), k_Dim);
            Stretch(askDim, Vector4.zero);
            RectTransform box = Panel(ask, "Box", Sprite("sheet_frame"), Color.white);
            Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            box.sizeDelta = new Vector2(200 * U, 156 * U);
            RectTransform askIcon = Panel(box, "Icon", null, Color.white);
            Anchor(askIcon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            askIcon.sizeDelta = new Vector2(28 * U, 28 * U);
            askIcon.anchoredPosition = new Vector2(0f, -8 * U);
            askIcon.GetComponent<Image>().preserveAspect = true;
            TextMeshProUGUI askTitle = Text(box, "Title", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.Center);
            Anchor(askTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            askTitle.rectTransform.sizeDelta = new Vector2(0f, 14 * U);
            askTitle.rectTransform.anchoredPosition = new Vector2(0f, -40 * U);
            ClerkStatView askStat = BakeStatTable(box, new Vector2(0.5f, 1f), new Vector2(0f, -58 * U));
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
            Set(view, "m_summaryLeft", summaryLeft);
            Set(view, "m_summaryRight", summaryRight);
            Set(view, "m_summaryRow", summaryRow);
            Set(view, "m_rows", rows);
            Set(view, "m_rowTemplate", rowView);
            Set(view, "m_foot", foot);
            Set(view, "m_footButton", footButton);
            Set(view, "m_footButtonLabel", footLabel);
            Set(view, "m_footButtonCost", footCost);
            Set(view, "m_ask", ask.gameObject);
            Set(view, "m_askIcon", askIcon.GetComponent<Image>());
            Set(view, "m_askTitle", askTitle);
            Set(view, "m_askStat", askStat);
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

        // 명찰 카드 줄 178×49(시안 D1 그대로): 초상 틀 32×29(안에 웜뱃 정지 그림 180px, 빈 자리는 점선 틀 + 「+」) + 상태 배지 36×12 | 이름 + 자리 알약 33×12 | 두 칸 표 91×22 | 버튼 36×30(가운데 높이)
        static ClerkRowView BakeClerkRow(RectTransform parent)
        {
            RectTransform row = Panel(parent, "RowTemplate", Sprite("clerk_card"), Color.white);
            row.sizeDelta = new Vector2(0f, k_ClerkRowHeight);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = k_ClerkRowHeight;

            RectTransform frame = Panel(row, "Frame", Sprite("clerk_frame"), Color.white);
            TopLeft(frame, 5 * k_Cell, 5 * k_Cell, 32 * k_Cell, 29 * k_Cell);
            frame.GetComponent<Image>().raycastTarget = false;
            // 초상은 틀 안쪽(외곽선 1칸 안)에서만 보이게 가린다
            RectTransform portraitMask = Child(frame, "Mask");
            Stretch(portraitMask, new Vector4(k_Cell, k_Cell, k_Cell, k_Cell));
            portraitMask.gameObject.AddComponent<RectMask2D>();
            RectTransform portrait = Panel(portraitMask, "Portrait", null, Color.white);
            Anchor(portrait, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            portrait.sizeDelta = new Vector2(180f, 180f);   // 사용자 지정(2026-09-26): 180×180, 10 아래
            portrait.anchoredPosition = new Vector2(0f, -10f);
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            RectTransform empty = Panel(row, "Empty", Sprite("clerk_frame_empty"), Color.white);
            TopLeft(empty, 5 * k_Cell, 5 * k_Cell, 32 * k_Cell, 29 * k_Cell);
            Button emptyButton = empty.gameObject.AddComponent<Button>();
            emptyButton.transition = Selectable.Transition.None;
            empty.gameObject.AddComponent<PressScale>();
            TextMeshProUGUI plus = Text(empty, "Plus", "Galmuri14", 14, k_Ink, TextAlignmentOptions.Center);
            Stretch(plus.rectTransform, Vector4.zero);
            plus.text = "+";

            // 상태 배지: 흰 채움(색은 ClerkRowView가 상태색으로) 위에 진갈색 선
            RectTransform badge = Panel(row, "Badge", Sprite("clerk_badge_fill"), Color.white);
            TopLeft(badge, 4 * k_Cell, 33 * k_Cell, 36 * k_Cell, 12 * k_Cell);
            badge.GetComponent<Image>().raycastTarget = false;
            RectTransform badgeLine = Panel(badge, "Line", Sprite("clerk_badge_line"), Color.white);
            Stretch(badgeLine, Vector4.zero);
            badgeLine.GetComponent<Image>().raycastTarget = false;
            TextMeshProUGUI badgeText = Text(badge, "Text", "Galmuri9", 9, k_Cream, TextAlignmentOptions.Center);
            Stretch(badgeText.rectTransform, Vector4.zero);

            // 이름 + 자리 알약: 초상 틀과 8칸 띄우고(시안), 알약은 이름 길이만큼 뒤로 밀린다
            RectTransform nameRow = Child(row, "NameRow");
            TopLeft(nameRow, 45 * k_Cell, 6 * k_Cell, 90 * k_Cell, 15 * k_Cell);
            HorizontalLayoutGroup nameLayout = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            nameLayout.spacing = 5 * k_Cell;
            nameLayout.childAlignment = TextAnchor.MiddleLeft;
            nameLayout.childControlWidth = true;
            nameLayout.childControlHeight = false;
            nameLayout.childForceExpandWidth = false;
            nameLayout.childForceExpandHeight = false;
            TextMeshProUGUI name = Text(nameRow, "Name", "Galmuri14", 14, k_Ink, TextAlignmentOptions.MidlineLeft);
            name.rectTransform.sizeDelta = new Vector2(0f, 15 * k_Cell);
            RectTransform slotPill = Panel(nameRow, "SlotPill", Sprite("clerk_slot"), Color.white);
            slotPill.sizeDelta = new Vector2(33 * k_Cell, 12 * k_Cell);
            LayoutElement slotSize = slotPill.gameObject.AddComponent<LayoutElement>();
            slotSize.preferredWidth = 33 * k_Cell;
            slotSize.minWidth = 33 * k_Cell;
            slotPill.GetComponent<Image>().raycastTarget = false;
            TextMeshProUGUI slotText = Text(slotPill, "Text", "Galmuri9", 9, k_Muted, TextAlignmentOptions.Center);
            Stretch(slotText.rectTransform, Vector4.zero);

            ClerkStatView stat = BakeStatTable(row, new Vector2(0f, 1f), new Vector2(43 * k_Cell, -23 * k_Cell));

            // 버튼 36×30: 표 오른쪽 끝과 카드 안쪽 테두리 사이(42칸) 가운데, 세로는 초상 틀 위~배지 아래(5~45)의 가운데
            Button button = ButtonUi(row, "Button", "clerk_btn", 36 * k_Cell, 30 * k_Cell, out TextMeshProUGUI label, "Galmuri11-Bold", 11, k_Cream);
            TopRight(button.GetComponent<RectTransform>(), 5 * k_Cell, 10 * k_Cell, 36 * k_Cell, 30 * k_Cell);

            ClerkRowView view = row.gameObject.AddComponent<ClerkRowView>();
            Set(view, "m_portrait", portraitImage);
            Set(view, "m_emptyFrame", emptyButton);
            Set(view, "m_badge", badge.GetComponent<Image>());
            Set(view, "m_badgeText", badgeText);
            Set(view, "m_name", name);
            Set(view, "m_slotPill", slotPill.gameObject);
            Set(view, "m_slotText", slotText);
            Set(view, "m_stat", stat);
            Set(view, "m_button", button);
            Set(view, "m_buttonLabel", label);
            return view;
        }

        // 두 칸 표 91×22(시안 조각 clerk_table): 머리글 두 칸(보조 9) · 값 줄 = 게이지 30×7(채움 28×5) + 숫자 굵게 / 코인 7 + 월급 굵게. 피벗 = anchor, 위치 = position
        static ClerkStatView BakeStatTable(RectTransform parent, Vector2 anchor, Vector2 position)
        {
            RectTransform table = Panel(parent, "Stat", Sprite("clerk_table"), Color.white);
            Anchor(table, anchor, anchor, anchor);
            table.sizeDelta = new Vector2(91 * k_Cell, 22 * k_Cell);
            table.anchoredPosition = position;
            table.GetComponent<Image>().raycastTarget = false;

            TextMeshProUGUI skillHeader = Text(table, "SkillHeader", "Galmuri9", 9, k_Muted, TextAlignmentOptions.Center);
            TopLeft(skillHeader.rectTransform, 1 * k_Cell, 1 * k_Cell, 46 * k_Cell, 9 * k_Cell);
            TextMeshProUGUI wageHeader = Text(table, "WageHeader", "Galmuri9", 9, k_Muted, TextAlignmentOptions.Center);
            TopLeft(wageHeader.rectTransform, 49 * k_Cell, 1 * k_Cell, 40 * k_Cell, 9 * k_Cell);

            RectTransform gauge = Panel(table, "Gauge", Sprite("clerk_gauge"), Color.white);
            BottomLeft(gauge, 4 * k_Cell, 2 * k_Cell, 30 * k_Cell, 7 * k_Cell);
            gauge.GetComponent<Image>().raycastTarget = false;
            RectTransform fill = Panel(gauge, "Fill", Sprite("clerk_gauge_fill"), Color.white);
            BottomLeft(fill, 1 * k_Cell, 1 * k_Cell, k_GaugeWidth, 5 * k_Cell);
            fill.GetComponent<Image>().raycastTarget = false;
            TextMeshProUGUI skillText = Text(table, "SkillText", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.MidlineLeft);
            BottomLeft(skillText.rectTransform, 36 * k_Cell, 1 * k_Cell, 14 * k_Cell, 11 * k_Cell);

            RectTransform coin = Panel(table, "Coin", Sprite("clerk_coin"), Color.white);
            BottomLeft(coin, 56 * k_Cell, 2 * k_Cell, 7 * k_Cell, 7 * k_Cell);
            coin.GetComponent<Image>().raycastTarget = false;
            TextMeshProUGUI wageText = Text(table, "WageText", "Galmuri11-Bold", 11, k_Ink, TextAlignmentOptions.MidlineLeft);
            BottomLeft(wageText.rectTransform, 65 * k_Cell, 1 * k_Cell, 25 * k_Cell, 11 * k_Cell);

            ClerkStatView view = table.gameObject.AddComponent<ClerkStatView>();
            Set(view, "m_skillHeader", skillHeader);
            Set(view, "m_wageHeader", wageHeader);
            Set(view, "m_gaugeFill", fill);
            SetFloat(view, "m_gaugeWidth", k_GaugeWidth);
            Set(view, "m_skillText", skillText);
            Set(view, "m_wageText", wageText);
            return view;
        }

        // 왼쪽 위 / 오른쪽 위 / 왼쪽 아래 기준 고정 크기 사각(x·y는 그 모서리에서 안쪽으로)
        static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        static void TopRight(RectTransform rect, float x, float y, float width, float height)
        {
            Anchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(-x, -y);
        }

        static void BottomLeft(RectTransform rect, float x, float y, float width, float height)
        {
            Anchor(rect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        // 말풍선: 크림 상자 위 글자
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

        static void SetFloat(Object target, string field, float value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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
