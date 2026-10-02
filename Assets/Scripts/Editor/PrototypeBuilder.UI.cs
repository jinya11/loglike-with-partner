using TMPro;
using UnityEditor;
using UnityEngine.TextCore.LowLevel;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>uGUI HUD 프리팹 생성 (HUD_Stage, HUD_Lobby). 생성 후에는 프리팹을 직접 편집.</summary>
public static partial class PrototypeBuilder
{
    static TMP_FontAsset fontRegular, fontBold;
    static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f), Panel = new Color(0.07f, 0.065f, 0.08f, 1f),
        Gold = new Color(0.95f, 0.8f, 0.5f), Soft = new Color(0.82f, 0.8f, 0.85f);
    static readonly Vector2 TL = new Vector2(0, 1), TC = new Vector2(0.5f, 1), C = new Vector2(0.5f, 0.5f), BC = new Vector2(0.5f, 0);

    static GameObject BuildStageHud()
    {
        EnsureFonts();
        var canvas = NewCanvas("HUD_Stage");
        var t = canvas.transform;

        var hpFill = Bar("HpBar", t, new Vector2(20, -20), new Vector2(420, 30), new Color(0.9f, 0.2f, 0.2f));
        var hpText = Txt("HpText", hpFill.parent, "HP {0} / {1}", 20, TextAlignmentOptions.Left, Color.white, Vector2.zero, Vector2.one, new Vector2(10, 0), Vector2.zero);
        var xpFill = Bar("XpBar", t, new Vector2(20, -56), new Vector2(420, 14), new Color(0.3f, 0.9f, 1f));
        var info = Txt("InfoText", t, "Lv {0}    스테이지 {1}/{2} {3}    처치 {4}/{5}", 22, TextAlignmentOptions.Left, Color.white, TL, TL, new Vector2(20, -78), new Vector2(1100, 32), TL);
        var skill = Txt("SkillText", t, "[Q] 충격파 {0}    [E] 대시 {1}", 22, TextAlignmentOptions.Left, Color.white, TL, TL, new Vector2(20, -112), new Vector2(560, 32), TL);
        var jumpHint = Txt("JumpHint", t, "[Space] 점프", 22, TextAlignmentOptions.Left, Gold, TL, TL, new Vector2(580, -112), new Vector2(300, 32), TL);
        var bonusBadge = Txt("BonusBadge", t, "보너스 스테이지", 22, TextAlignmentOptions.Left, new Color(1f, 0.7f, 0.85f), TL, TL, new Vector2(460, -20), new Vector2(400, 32), TL);
        jumpHint.gameObject.SetActive(false); bonusBadge.gameObject.SetActive(false);
        var portal = Txt("PortalMessage", t, "포털이 열렸습니다. 중앙에서 F로 이동하세요", 38, TextAlignmentOptions.Center, Gold, TC, TC, new Vector2(0, -170), new Vector2(1400, 60));

        var stagePrompt = Txt("PromptText", t, "", 30, TextAlignmentOptions.Center, Color.white, BC, BC, new Vector2(0, 90), new Vector2(1500, 56));

        // 레벨업
        var lv = Stretch("LevelUpPanel", t); Img(lv, Dim);
        var lvNormal = Txt("NormalTitle", lv, "레벨 업", 48, TextAlignmentOptions.Center, Gold, C, C, new Vector2(0, 210), new Vector2(1200, 70));
        var lvSpecial = Txt("SpecialTitle", lv, "거신의 축복 (특수 강화)", 48, TextAlignmentOptions.Center, new Color(0.8f, 0.65f, 1f), C, C, new Vector2(0, 210), new Vector2(1200, 70));
        lvSpecial.gameObject.SetActive(false);
        var buttons = new Button[3]; var texts = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            var b = UI("Choice" + (i + 1), lv, C, C, new Vector2((i - 1) * 340, -10), new Vector2(310, 220));
            var img = Img(b, new Color(0.18f, 0.17f, 0.22f, 0.95f));
            buttons[i] = b.gameObject.AddComponent<Button>(); buttons[i].targetGraphic = img;
            var cb = buttons[i].colors; cb.highlightedColor = new Color(1.4f, 1.3f, 1f); buttons[i].colors = cb;
            texts[i] = Txt("Label", b, "[{0}] {1}\n\n{2}", 24, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-30, -30));
        }
        lv.gameObject.SetActive(false);

        // 결과
        var res = Stretch("ResultPanel", t); Img(res, Dim);
        var resDefeat = Txt("DefeatTitle", res, "사망", 64, TextAlignmentOptions.Center, Color.white, C, C, new Vector2(0, 40), new Vector2(1200, 90));
        var resVictory = Txt("VictoryTitle", res, "모든 스테이지 클리어!", 64, TextAlignmentOptions.Center, Gold, C, C, new Vector2(0, 40), new Vector2(1200, 90));
        resVictory.gameObject.SetActive(false);
        Txt("Hint", res, "R 키로 로비 복귀", 30, TextAlignmentOptions.Center, Soft, C, C, new Vector2(0, -50), new Vector2(1200, 50));
        res.gameObject.SetActive(false);
        portal.gameObject.SetActive(false);

        var hud = canvas.AddComponent<Hud>();
        var so = new SerializedObject(hud);
        so.FindProperty("hpFill").objectReferenceValue = hpFill;
        so.FindProperty("hpText").objectReferenceValue = hpText;
        so.FindProperty("xpFill").objectReferenceValue = xpFill;
        so.FindProperty("infoText").objectReferenceValue = info;
        so.FindProperty("skillText").objectReferenceValue = skill;
        so.FindProperty("portalMessage").objectReferenceValue = portal.gameObject;
        so.FindProperty("promptText").objectReferenceValue = stagePrompt;
        so.FindProperty("levelUpPanel").objectReferenceValue = lv.gameObject;
        so.FindProperty("normalTitle").objectReferenceValue = lvNormal.gameObject;
        so.FindProperty("specialTitle").objectReferenceValue = lvSpecial.gameObject;
        so.FindProperty("jumpHint").objectReferenceValue = jumpHint.gameObject;
        so.FindProperty("bonusBadge").objectReferenceValue = bonusBadge.gameObject;
        so.FindProperty("resultPanel").objectReferenceValue = res.gameObject;
        so.FindProperty("victoryTitle").objectReferenceValue = resVictory.gameObject;
        so.FindProperty("defeatTitle").objectReferenceValue = resDefeat.gameObject;
        SetArray(so.FindProperty("choiceButtons"), buttons);
        SetArray(so.FindProperty("choiceTexts"), texts);
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(canvas, "UI/HUD_Stage");
    }

    static GameConfig cfgForUI;

    static GameObject BuildLobbyHud()
    {
        var canvas = NewCanvas("HUD_Lobby");
        var t = canvas.transform;
        var info = Txt("InfoText", t, "무기: {0}\n동반자: {1}\n각성한 석판: {2}/{3}\n북쪽 거대 석판에서 난이도 설정\n\nWASD 이동  E 대시  F 상호작용", 22, TextAlignmentOptions.TopLeft, Color.white, TL, TL, new Vector2(20, -20), new Vector2(900, 240), TL);
        var prompt = Txt("PromptText", t, "[F] 상호작용", 30, TextAlignmentOptions.Center, Color.white, BC, BC, new Vector2(0, 90), new Vector2(1500, 56));

        // 난이도의 석판: 로비가 보이지 않는 불투명 전체 화면
        var panel = Stretch("DifficultyPanel", t); Img(panel, Panel);
        var line = UI("TopLine", panel, TC, TC, new Vector2(0, -50), new Vector2(1500, 3)); Img(line, Gold);
        Txt("Title", panel, "난이도의 석판", 62, TextAlignmentOptions.Center, Gold, TC, TC, new Vector2(0, -115), new Vector2(1200, 80));
        Txt("Subtitle", panel, "봉인을 풀수록 시련이 강해집니다. 각성한 석판은 다음 런 전체에 적용됩니다.", 22, TextAlignmentOptions.Center, Soft, TC, TC, new Vector2(0, -175), new Vector2(1400, 40));

        // 석판 카드: 프리팹 안에 개별 배치 (각각 위치/모양 편집 가능, 이름/설명은 GameConfig.modifiers에서 채움)
        var cardRoot = UI("Cards", panel, C, C, new Vector2(0, 10), new Vector2(1160, 420));
        var mods = cfgForUI.modifiers;
        var cards = new ModifierCard[mods.Length];
        for (int i = 0; i < mods.Length; i++)
        {
            var pos = new Vector2(((i % 3) - 1) * 395f, 110f - (i / 3) * 220f);
            var card = UI($"Card_{i + 1}_{mods[i].name}", cardRoot, C, C, pos, new Vector2(370, 195));
            var border = Img(card, new Color(0.35f, 0.33f, 0.38f));
            var bgRt = Stretch("Background", card); bgRt.offsetMin = new Vector2(3, 3); bgRt.offsetMax = new Vector2(-3, -3);
            var bg = Img(bgRt, new Color(0.16f, 0.15f, 0.18f));
            Txt("Name", card, $"{i + 1}. {mods[i].name}", 28, TextAlignmentOptions.TopLeft, Color.white, TL, TL, new Vector2(22, -20), new Vector2(330, 40), TL);
            Txt("Desc", card, mods[i].desc, 20, TextAlignmentOptions.TopLeft, Soft, TL, TL, new Vector2(22, -70), new Vector2(330, 60), TL, false);
            var cOff = Txt("StateOff", card, "봉인됨", 22, TextAlignmentOptions.BottomRight, new Color(0.5f, 0.48f, 0.55f), Vector2.zero, Vector2.one, new Vector2(-22, 18), new Vector2(-44, -36));
            var cOn = Txt("StateOn", card, "각성됨", 22, TextAlignmentOptions.BottomRight, new Color(1f, 0.45f, 0.3f), Vector2.zero, Vector2.one, new Vector2(-22, 18), new Vector2(-44, -36));
            cOn.gameObject.SetActive(false);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
            var mc = cards[i] = card.gameObject.AddComponent<ModifierCard>();
            var cso = new SerializedObject(mc);
            cso.FindProperty("index").intValue = i;
            cso.FindProperty("stateOn").objectReferenceValue = cOn.gameObject;
            cso.FindProperty("stateOff").objectReferenceValue = cOff.gameObject;
            cso.FindProperty("border").objectReferenceValue = border;
            cso.FindProperty("background").objectReferenceValue = bg;
            cso.FindProperty("button").objectReferenceValue = btn;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
        var count = Txt("ActiveCount", panel, "각성한 석판 {0} / {1}", 26, TextAlignmentOptions.Center, Soft, C, C, new Vector2(0, -255), new Vector2(800, 44));
        var close = UI("CloseButton", panel, BC, BC, new Vector2(0, 70), new Vector2(300, 64));
        var closeImg = Img(close, new Color(0.25f, 0.23f, 0.28f));
        var closeBtn = close.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = closeImg;
        Txt("Label", close, "닫기 (F / Esc)", 24, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        panel.gameObject.SetActive(false); // 편집 시 Hierarchy에서 활성화해서 보기 (런타임에 자동으로 숨김)

        var ld = canvas.AddComponent<LobbyDirector>();
        var so = new SerializedObject(ld);
        so.FindProperty("infoText").objectReferenceValue = info;
        so.FindProperty("promptText").objectReferenceValue = prompt;
        so.FindProperty("difficultyPanel").objectReferenceValue = panel.gameObject;
        SetArray(so.FindProperty("cards"), cards);
        so.FindProperty("activeCountText").objectReferenceValue = count;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(canvas, "UI/HUD_Lobby");
    }

    static void AddUI(GameObject hudPrefab)
    {
        PrefabUtility.InstantiatePrefab(hudPrefab);
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    // ---- UI 헬퍼 ----
    static GameObject NewCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    static RectTransform UI(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(string name, Transform parent) => UI(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    static Image Img(RectTransform rt, Color c)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        return img;
    }

    static TMP_Text Txt(string name, Transform parent, string s, int size, TextAlignmentOptions align, Color c, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sizeDelta, Vector2? pivot = null, bool bold = true)
    {
        var rt = UI(name, parent, aMin, aMax, pos, sizeDelta, pivot);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = bold ? fontBold : fontRegular;
        t.text = s; t.fontSize = size; t.alignment = align; t.color = c;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>페이퍼로지 TTF로 동적 TMP 폰트 에셋 생성 (한글은 런타임에 필요한 글자만 아틀라스에 추가).</summary>
    static void EnsureFonts()
    {
        fontRegular = FontAsset("Paperlogy-4Regular");
        fontBold = FontAsset("Paperlogy-7Bold");
        var settings = TMP_Settings.instance;
        if (settings)
        {
            var so = new SerializedObject(settings);
            var p = so.FindProperty("m_defaultFontAsset");
            if (p != null) { p.objectReferenceValue = fontRegular; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }

    static TMP_FontAsset FontAsset(string ttf)
    {
        string path = $"{Root}/Fonts/{ttf} SDF.asset";
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (fa) return fa;
        var src = AssetDatabase.LoadAssetAtPath<Font>($"{Root}/Fonts/{ttf}.ttf");
        fa = TMP_FontAsset.CreateFontAsset(src, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        fa.name = ttf + " SDF";
        AssetDatabase.CreateAsset(fa, path);
        fa.atlasTextures[0].name = ttf + " Atlas";
        fa.material.name = ttf + " Material";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        AssetDatabase.SaveAssets();
        return fa;
    }
    /// <summary>배경 + 채움 바. 채움 RectTransform 반환 (anchorMax.x = 비율).</summary>
    static RectTransform Bar(string name, Transform parent, Vector2 pos, Vector2 size, Color fill)
    {
        var bg = UI(name, parent, TL, TL, pos, size, TL);
        Img(bg, new Color(0, 0, 0, 0.6f));
        var f = Stretch("Fill", bg);
        Img(f, fill).raycastTarget = false;
        return f;
    }

    static void SetArray(SerializedProperty p, Object[] values)
    {
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
