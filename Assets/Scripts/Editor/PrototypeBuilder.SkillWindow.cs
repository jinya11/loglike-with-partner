using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 감시자 스킬 창 (HUD_SkillSelect) 패널을 새 배치로 다시 만듦. 메뉴: Roguelike/Build Skill Window.
/// 오른쪽 위 책 버튼(SkillBookButton)은 그대로 두고 Panel만 교체. 이후에는 프리팹을 직접 편집.
/// 배치: 크림색 창 / 왼쪽 분류별 스킬 카드 + 아래 설명·장착하기 / 오른쪽 장착 칸 2개. 아이콘 그림 없음(빈 자리).
/// </summary>
public static partial class PrototypeBuilder
{
    static readonly Color SkCream = new Color(1f, 0.96f, 0.88f), SkPaper = new Color(0.96f, 0.9f, 0.8f),
        SkBrown = new Color(0.36f, 0.24f, 0.16f), SkWood = new Color(0.78f, 0.55f, 0.33f), SkSelect = new Color(0.95f, 0.55f, 0.25f);

    [MenuItem("Roguelike/Build Skill Window")]
    static void BuildSkillWindow()
    {
        const string path = Root + "/Prefabs/UI/HUD_SkillSelect.prefab";
        GrowthFonts();
        var round = AssetDatabase.LoadAssetAtPath<Sprite>(GrowthSpriteDir + "/G_Round.png");
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var old = root.transform.Find("Panel");
            if (old) Object.DestroyImmediate(old.gameObject);
            var ui = root.GetComponent<SkillSelectUI>();

            var panel = Stretch("Panel", root.transform);
            panel.SetAsFirstSibling(); // 책 버튼이 위에
            Img(panel, new Color(0f, 0f, 0f, 0.55f));

            var win = UI("Window", panel, C, C, Vector2.zero, new Vector2(1360, 800));
            Box(win, SkCream, round, 4);
            Txt("Title", win, "감시자 스킬", 40, TextAlignmentOptions.Center, SkBrown, TC, TC, new Vector2(0, -22), new Vector2(600, 56), TC);
            var close = UI("CloseButton", win, Vector2.one, Vector2.one, new Vector2(-20, -20), new Vector2(56, 56), Vector2.one);
            var closeBtn = close.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = Box(close, SkWood, round, 3);
            Txt("X", close, "X", 28, TextAlignmentOptions.Center, SkCream, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 왼쪽: 분류 묶음
            var groups = UI("Groups", win, TL, TL, new Vector2(40, -96), new Vector2(940, 450), TL);
            var vl = groups.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 16; vl.childControlWidth = vl.childControlHeight = false; vl.childForceExpandWidth = vl.childForceExpandHeight = false;
            var group = UI("GroupTemplate", groups, TL, TL, Vector2.zero, new Vector2(940, 136), TL);
            Txt("Header", group, "분류", 28, TextAlignmentOptions.Center, SkBrown, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(110, 0), new Vector2(0, 0.5f));
            var cardsRt = UI("Cards", group, Vector2.zero, Vector2.one, new Vector2(60, 0), new Vector2(-120, 0));
            var hl = cardsRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 16; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = false; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var card = Card("CardTemplate", cardsRt, new Vector2(250, 124), round, true);
            card.line = Txt("Line", card.transform, "코스트 {0}", 20, TextAlignmentOptions.Left, SkBrown, TL, TL, new Vector2(102, -70), new Vector2(140, 28), TL, false);

            // 왼쪽 아래: 설명 + 장착하기
            var desc = UI("Desc", win, Vector2.zero, Vector2.zero, new Vector2(40, 36), new Vector2(940, 190), Vector2.zero);
            Box(desc, SkPaper, round, 3);
            var dName = Txt("Name", desc, "", 32, TextAlignmentOptions.Left, SkBrown, TL, TL, new Vector2(28, -18), new Vector2(600, 44), TL);
            var dInfo = Txt("Info", desc, "{0} · 코스트 {1} · 대기 {2:0.#}초", 20, TextAlignmentOptions.Left, SkBrown, TL, TL, new Vector2(28, -64), new Vector2(600, 30), TL, false);
            var dLine = Txt("Line", desc, "", 24, TextAlignmentOptions.TopLeft, SkBrown, TL, TL, new Vector2(28, -106), new Vector2(640, 70), TL, false);
            var eq = UI("EquipButton", desc, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 24), new Vector2(230, 70), new Vector2(1, 0));
            var eqBtn = eq.gameObject.AddComponent<Button>(); eqBtn.targetGraphic = Box(eq, SkWood, round, 3);
            var eqLabel = Txt("Label", eq, "장착하기", 26, TextAlignmentOptions.Center, SkCream, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 오른쪽: 장착 칸
            var side = UI("Equipped", win, Vector2.one, Vector2.one, new Vector2(-40, -96), new Vector2(300, 668), Vector2.one);
            Box(side, SkPaper, round, 3);
            Txt("Header", side, "장착", 30, TextAlignmentOptions.Center, SkBrown, TC, TC, new Vector2(0, -18), new Vector2(260, 44), TC);
            var slots = new SkillSelectRow[2];
            for (int i = 0; i < 2; i++)
            {
                var s = Card("Slot" + (i + 1), side, new Vector2(250, 124), round, false);
                var rt = (RectTransform)s.transform;
                rt.anchorMin = rt.anchorMax = TC; rt.pivot = TC; rt.anchoredPosition = new Vector2(0, -84 - i * 148);
                slots[i] = s;
            }

            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("groupTemplate").objectReferenceValue = group;
            so.FindProperty("cardTemplate").objectReferenceValue = card;
            var sp = so.FindProperty("slots"); sp.arraySize = 2;
            for (int i = 0; i < 2; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("descRoot").objectReferenceValue = desc.gameObject;
            so.FindProperty("descName").objectReferenceValue = dName;
            so.FindProperty("descInfo").objectReferenceValue = dInfo;
            so.FindProperty("descLine").objectReferenceValue = dLine;
            so.FindProperty("equipButton").objectReferenceValue = eqBtn;
            so.FindProperty("equipLabel").objectReferenceValue = eqLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            group.gameObject.SetActive(false);
            card.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[PrototypeBuilder] 스킬 창 (HUD_SkillSelect) 다시 만듦");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>크림/종이 상자: 둥근 사각 + 갈색 테두리.</summary>
    static Image Box(RectTransform rt, Color c, Sprite round, float outline)
    {
        var img = Img(rt, c); img.sprite = round; img.type = Image.Type.Sliced;
        var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = SkBrown; o.effectDistance = new Vector2(outline, -outline);
        return img;
    }

    /// <summary>스킬 카드 / 장착 칸: 선택 테두리, 바탕, 빈 아이콘 자리, 이름, 키 표시 (+카드는 추천·잠김).</summary>
    static SkillSelectRow Card(string name, Transform parent, Vector2 size, Sprite round, bool isCard)
    {
        var rt = UI(name, parent, TL, TL, Vector2.zero, size, TL);
        var row = rt.gameObject.AddComponent<SkillSelectRow>();
        var sel = UI("Selected", rt, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(14, 14));
        var selImg = Img(sel, SkSelect); selImg.sprite = round; selImg.type = Image.Type.Sliced; selImg.raycastTarget = false;
        sel.gameObject.SetActive(false);
        var bg = Stretch("Bg", rt);
        row.background = Box(bg, SkCream, round, 3);
        row.button = rt.gameObject.AddComponent<Button>(); row.button.targetGraphic = row.background;
        var icon = UI("Icon", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(72, 72), new Vector2(0, 0.5f));
        row.icon = Img(icon, new Color(1f, 1f, 1f, 0.6f)); row.icon.sprite = round; row.icon.type = Image.Type.Sliced; row.icon.raycastTarget = false;
        icon.gameObject.AddComponent<Outline>().effectColor = new Color(SkBrown.r, SkBrown.g, SkBrown.b, 0.5f);
        row.title = Txt("Name", rt, "", 26, TextAlignmentOptions.Left, SkBrown, TL, TL, new Vector2(102, isCard ? -30 : -46), new Vector2(140, 34), TL);
        row.badge = Txt("Key", rt, "", 24, TextAlignmentOptions.Center, SkBrown, Vector2.one, Vector2.one, new Vector2(-8, -6), new Vector2(36, 32), Vector2.one);
        row.selectedMark = sel.gameObject;
        if (!isCard) return row;
        var rec = UI("Recommend", rt, TL, TL, new Vector2(8, 14), new Vector2(64, 28), TL);
        var recImg = Img(rec, PinkBtn); recImg.sprite = round; recImg.type = Image.Type.Sliced; recImg.raycastTarget = false;
        Txt("Label", rec, "추천", 18, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rec.gameObject.SetActive(false);
        row.recommend = rec.gameObject;
        var lk = Stretch("Locked", rt);
        var lkImg = Img(lk, new Color(0.2f, 0.15f, 0.1f, 0.45f)); lkImg.sprite = round; lkImg.type = Image.Type.Sliced; lkImg.raycastTarget = false;
        Txt("Label", lk, "잠김", 24, TextAlignmentOptions.Center, SkCream, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        lk.gameObject.SetActive(false);
        row.lockMark = lk.gameObject;
        return row;
    }
}
