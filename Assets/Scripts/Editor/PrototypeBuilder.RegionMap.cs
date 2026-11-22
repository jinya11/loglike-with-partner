using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 지역 지도 UI (HUD_RegionSelect) + 지역 풍경 그림. 메뉴: Roguelike/Build Region Map UI.
/// - 풍경 그림: RegionDef.mapArt 슬롯 (StageConfig). 지금은 UI/RegionArt/region_art_sheet.png를 잘라 쓴 스프라이트.
///   다른 그림으로 바꾸려면 mapArt에 스프라이트를 넣으면 됨 (Build Region Stages 재실행해도 유지).
/// - 화면: 분홍 노을 하늘 + 구름 바다 배경, 가운데 양피지 지도 패널, 둥근 카드(그림 + 왼쪽 위 번호·이름), 나무 판자 버튼.
/// </summary>
public static partial class PrototypeBuilder
{

    static readonly Color RmCream = new Color(1f, 0.95f, 0.85f), RmBrown = new Color(0.36f, 0.24f, 0.16f),
        RmGold = new Color(1f, 0.72f, 0.3f), RmWood = new Color(0.6f, 0.42f, 0.28f);

    [MenuItem("Roguelike/Build Region Map UI")]
    static void BuildRegionMapMenu()
    {
        var st = AssetDatabase.LoadAssetAtPath<StageConfig>(Root + "/Resources/Config/StageConfig.asset");
        st.regionSelectPrefab = BuildRegionSelectHud();
        EditorUtility.SetDirty(st);
        AssetDatabase.SaveAssets();
        Debug.Log("[PrototypeBuilder] 지역 지도 UI + 지역 그림 슬롯 갱신");
    }

    /// <summary>Build Region Stages가 지역 목록을 다시 만들 때 기존 그림을 유지하기 위해.</summary>
    static Sprite OldMapArt(StageConfig st, string scene)
    {
        if (st.centerRegion != null && st.centerRegion.scene == scene) return st.centerRegion.mapArt;
        if (st.regionPool != null) foreach (var d in st.regionPool) if (d != null && d.scene == scene) return d.mapArt;
        return null;
    }

    // ===================== 지도 UI 프리팹 =====================

    static GameObject BuildRegionSelectHud()
    {
        GrowthFonts();
        var round = AssetDatabase.LoadAssetAtPath<Sprite>(GrowthSpriteDir + "/G_Round.png");
        var sky = Tex("G_SunsetSky", 8, 256, (x, y) =>
        {
            float v = y / 255f;
            Color top = new Color(0.78f, 0.72f, 0.95f), mid = new Color(1f, 0.74f, 0.82f), bot = new Color(1f, 0.88f, 0.8f);
            return v > 0.45f ? Color.Lerp(mid, top, (v - 0.45f) / 0.55f) : Color.Lerp(bot, mid, v / 0.45f);
        }, Vector4.zero);
        var glow = AssetDatabase.LoadAssetAtPath<Sprite>(GrowthSpriteDir + "/G_Glow.png");

        var canvas = NewCanvas("HUD_RegionSelect");
        canvas.GetComponent<Canvas>().sortingOrder = 10;
        var panel = Stretch("RegionPanel", canvas.transform);
        Img(panel, Color.white).sprite = sky;
        // 구름 바다 (부드러운 빛 덩어리)
        for (int i = 0; i < 9; i++)
        {
            var c = UI("Cloud", panel, new Vector2(i / 8f, 0), new Vector2(i / 8f, 0), new Vector2(0, 40 + (i % 3) * 30), new Vector2(520, 300));
            var ci = Img(c, i % 2 == 0 ? new Color(1f, 0.85f, 0.92f, 0.9f) : new Color(0.88f, 0.84f, 1f, 0.85f)); ci.sprite = glow; ci.raycastTarget = false;
        }

        // 양피지 지도 패널
        var paper = UI("MapPaper", panel, C, C, new Vector2(0, -24), new Vector2(1140, 900));
        var pi = Img(paper, RmCream); pi.sprite = round; pi.type = Image.Type.Sliced;
        var po = paper.gameObject.AddComponent<Outline>(); po.effectColor = RmBrown; po.effectDistance = new Vector2(6, -6);
        var ribbon = UI("TitlePlank", paper, TC, TC, new Vector2(0, 34), new Vector2(460, 92));
        var ri = Img(ribbon, RmWood); ri.sprite = round; ri.type = Image.Type.Sliced;
        var ro = ribbon.gameObject.AddComponent<Outline>(); ro.effectColor = RmBrown; ro.effectDistance = new Vector2(4, -4);
        Txt("Title", ribbon, "지역 지도", 52, TextAlignmentOptions.Center, RmCream, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var grid = UI("Grid", paper, C, C, new Vector2(0, 40), new Vector2(1000, 660));
        int n = RegionSelectUI.GridNumbers.Length;
        var cells = new Button[n]; var names = new TMP_Text[n]; var bgs = new Image[n]; var borders = new Image[n];
        for (int i = 0; i < n; i++)
        {
            int num = RegionSelectUI.GridNumbers[i];
            var pos = new Vector2(((i % 3) - 1) * 330f, 215f - (i / 3) * 215f);
            var cell = UI($"Cell_{num}", grid, C, C, pos, new Vector2(312, 198));
            borders[i] = Img(cell, RmBrown); borders[i].sprite = round; borders[i].type = Image.Type.Sliced;
            var art = Stretch("Art", cell); art.offsetMin = new Vector2(7, 7); art.offsetMax = new Vector2(-7, -7);
            bgs[i] = Img(art, Color.white); bgs[i].preserveAspect = false;
            var band = UI("NameBand", cell, TL, TL, new Vector2(12, -12), new Vector2(236, 46), TL);
            var bi = Img(band, new Color(0.25f, 0.16f, 0.1f, 0.6f)); bi.sprite = round; bi.type = Image.Type.Sliced; bi.raycastTarget = false;
            names[i] = Txt("Name", band, $"{num}. 지역", 28, TextAlignmentOptions.Left, RmCream, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-16, 0));
            names[i].rectTransform.pivot = new Vector2(0, 0.5f); names[i].rectTransform.anchoredPosition = new Vector2(12, 0);
            names[i].outlineWidth = 0.15f; names[i].outlineColor = new Color32(60, 36, 24, 255);
            cells[i] = cell.gameObject.AddComponent<Button>(); cells[i].targetGraphic = bgs[i];
            var cb = cells[i].colors; cb.highlightedColor = new Color(1.1f, 1.08f, 1.05f); cb.disabledColor = new Color(0.45f, 0.42f, 0.42f); cells[i].colors = cb;
        }

        var selected = Txt("SelectedText", paper, "", 32, TextAlignmentOptions.Center, RmBrown, BC, BC, new Vector2(0, 150), new Vector2(900, 48));
        var start = UI("StartButton", paper, BC, BC, new Vector2(-170, 56), new Vector2(300, 76));
        var sImg = Img(start, RmGold); sImg.sprite = round; sImg.type = Image.Type.Sliced;
        var so1 = start.gameObject.AddComponent<Outline>(); so1.effectColor = RmBrown; so1.effectDistance = new Vector2(4, -4);
        var startBtn = start.gameObject.AddComponent<Button>(); startBtn.targetGraphic = sImg;
        Txt("Label", start, "출발 (F)", 30, TextAlignmentOptions.Center, RmBrown, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var close = UI("CloseButton", paper, BC, BC, new Vector2(170, 56), new Vector2(300, 76));
        var cImg = Img(close, RmWood); cImg.sprite = round; cImg.type = Image.Type.Sliced;
        var co = close.gameObject.AddComponent<Outline>(); co.effectColor = RmBrown; co.effectDistance = new Vector2(4, -4);
        var closeBtn = close.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = cImg;
        Txt("Label", close, "닫기 (Esc)", 30, TextAlignmentOptions.Center, RmCream, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var ui = canvas.AddComponent<RegionSelectUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        SetArray(so.FindProperty("cells"), cells);
        SetArray(so.FindProperty("cellNames"), names);
        so.FindProperty("cellDescs").arraySize = 0;
        SetArray(so.FindProperty("cellBackgrounds"), bgs);
        SetArray(so.FindProperty("cellBorders"), borders);
        so.FindProperty("selectedText").objectReferenceValue = selected;
        so.FindProperty("startButton").objectReferenceValue = startBtn;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("borderIdle").colorValue = RmBrown;
        so.FindProperty("borderSelected").colorValue = RmGold;
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(canvas, "UI/HUD_RegionSelect");
    }
}
