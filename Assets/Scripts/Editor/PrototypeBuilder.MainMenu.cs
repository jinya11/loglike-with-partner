using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀(메인 메뉴) + ESC 귀환 확인 창. 메뉴: Roguelike/Setup Main Menu (재실행해도 안전).
/// 만드는 것: HUD_Confirm / HUD_MainMenu 프리팹, MainMenuConfig 에셋(없을 때만), BGM 임포트 설정,
/// Assets/Scenes/MainMenu.unity (없을 때만, Additive 임시로 만들고 닫음 — 열린 씬은 건드리지 않음), 빌드 0번 등록.
/// </summary>
public static partial class PrototypeBuilder
{
    const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string MainMenuBgmPath = "Assets/Audio/Bgm/Velvet_Ears_And_Sunny_Lanes.mp3";
    const string MainMenuConfigPath = Root + "/Resources/Config/MainMenuConfig.asset";

    [MenuItem("Roguelike/Setup Main Menu")]
    static void SetupMainMenu()
    {
        GrowthFonts();
        var round = AssetDatabase.LoadAssetAtPath<Sprite>(GrowthSpriteDir + "/G_Round.png");
        var glow = AssetDatabase.LoadAssetAtPath<Sprite>(GrowthSpriteDir + "/G_Glow.png");
        var bg = AssetDatabase.LoadAssetAtPath<Sprite>(GrowthSpriteDir + "/G_Bg.png");

        // BGM: 스트리밍 + Vorbis (반복은 AudioSource.loop)
        if (AssetImporter.GetAtPath(MainMenuBgmPath) is AudioImporter imp)
        {
            var ss = imp.defaultSampleSettings;
            if (ss.loadType != AudioClipLoadType.Streaming || ss.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                ss.loadType = AudioClipLoadType.Streaming; ss.compressionFormat = AudioCompressionFormat.Vorbis; ss.quality = 0.7f;
                imp.defaultSampleSettings = ss; imp.SaveAndReimport();
            }
        }

        BuildConfirmPopup(round);
        BuildMainMenuUI(round, glow, bg);

        var cfg = AssetDatabase.LoadAssetAtPath<MainMenuConfig>(MainMenuConfigPath);
        if (!cfg) { cfg = ScriptableObject.CreateInstance<MainMenuConfig>(); AssetDatabase.CreateAsset(cfg, MainMenuConfigPath); }
        cfg.confirmPrefab = AssetDatabase.LoadAssetAtPath<ConfirmPopup>(Root + "/Prefabs/UI/HUD_Confirm.prefab");
        EditorUtility.SetDirty(cfg);

        // 설정 창 + 기본값 / 화면 분할 config (없을 때만 생성, 수치는 덮어쓰지 않음)
        BuildSettingsUI(round, AssetDatabase.LoadAssetAtPath<Sprite>(GrowthSpriteDir + "/G_Dot.png"));
        var scfg = AssetDatabase.LoadAssetAtPath<SettingsConfig>(Root + "/Resources/Config/SettingsConfig.asset");
        if (!scfg) { scfg = ScriptableObject.CreateInstance<SettingsConfig>(); AssetDatabase.CreateAsset(scfg, Root + "/Resources/Config/SettingsConfig.asset"); }
        scfg.settingsPrefab = AssetDatabase.LoadAssetAtPath<SettingsUI>(Root + "/Prefabs/UI/HUD_Settings.prefab");
        EditorUtility.SetDirty(scfg);
        if (!AssetDatabase.LoadAssetAtPath<ScreenSplitConfig>(Root + "/Resources/Config/ScreenSplitConfig.asset"))
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ScreenSplitConfig>(), Root + "/Resources/Config/ScreenSplitConfig.asset");
        AssetDatabase.SaveAssets();

        if (!System.IO.File.Exists(MainMenuScenePath))
            BuildMainMenuScene(AssetDatabase.LoadAssetAtPath<MainMenuUI>(Root + "/Prefabs/UI/HUD_MainMenu.prefab"),
                AssetDatabase.LoadAssetAtPath<ConfirmPopup>(Root + "/Prefabs/UI/HUD_Confirm.prefab"), cfg);

        // 빌드 설정: 타이틀을 0번으로 (나머지 순서 유지)
        var list = new List<EditorBuildSettingsScene>();
        list.Add(new EditorBuildSettingsScene(MainMenuScenePath, true));
        foreach (var s in EditorBuildSettings.scenes) if (s.path != MainMenuScenePath) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("[PrototypeBuilder] 메인 메뉴 준비 완료");
    }

    static ConfirmPopup BuildConfirmPopup(Sprite round)
    {
        var canvas = NewCanvas("HUD_Confirm");
        canvas.GetComponent<Canvas>().sortingOrder = 100;
        var panel = Stretch("Panel", canvas.transform);
        Img(panel, new Color(0.36f, 0.22f, 0.32f, 0.45f));

        var win = UI("Window", panel, C, C, Vector2.zero, new Vector2(680, 340));
        var winImg = Img(win, new Color(1f, 0.985f, 0.97f, 0.98f)); winImg.sprite = round; winImg.type = Image.Type.Sliced;
        SoftShadow(win);
        var msg = Txt("Message", win, "거점으로 돌아가시겠습니까?", 40, TextAlignmentOptions.Center, Brown, TC, TC, new Vector2(0, -40), new Vector2(600, 150), TC);
        var yes = MenuButton("YesButton", win, new Vector2(-135, -90), new Vector2(230, 84), "예", PinkBtn, Color.white, round, 36);
        var no = MenuButton("NoButton", win, new Vector2(135, -90), new Vector2(230, 84), "아니오", new Color(1f, 0.9f, 0.9f), Brown, round, 36);
        panel.gameObject.SetActive(false);

        var ui = canvas.AddComponent<ConfirmPopup>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("message").objectReferenceValue = msg;
        so.FindProperty("yesButton").objectReferenceValue = yes;
        so.FindProperty("noButton").objectReferenceValue = no;
        so.ApplyModifiedPropertiesWithoutUndo();
        var p = PrefabUtility.SaveAsPrefabAsset(canvas, Root + "/Prefabs/UI/HUD_Confirm.prefab");
        Object.DestroyImmediate(canvas);
        return p.GetComponent<ConfirmPopup>();
    }

    static MainMenuUI BuildMainMenuUI(Sprite round, Sprite glow, Sprite bg)
    {
        var canvas = NewCanvas("HUD_MainMenu");
        canvas.GetComponent<Canvas>().sortingOrder = 50;
        var root = Stretch("Background", canvas.transform);
        Img(root, Color.white).sprite = bg; // 라벤더 하늘 → 분홍 노을 → 크림
        Deco(root, glow, new Vector2(0, -420), 1700, new Color(1f, 0.8f, 0.68f, 0.75f));   // 지는 해
        Deco(root, glow, new Vector2(-640, 300), 1100, new Color(1f, 0.72f, 0.84f, 0.55f)); // 분홍 구름
        Deco(root, glow, new Vector2(660, 220), 1200, new Color(0.8f, 0.74f, 1f, 0.5f));    // 라벤더 구름
        Deco(root, glow, new Vector2(-380, -380), 900, new Color(1f, 0.92f, 0.96f, 0.6f));
        Deco(root, glow, new Vector2(460, -360), 1000, new Color(0.95f, 0.85f, 1f, 0.55f));

        var title = Txt("Title", root, "로그라이크", 132, TextAlignmentOptions.Center, Color.white, C, C, new Vector2(0, 250), new Vector2(1400, 200));
        var sh = title.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0.85f, 0.45f, 0.6f, 0.55f); sh.effectDistance = new Vector2(0, -6);

        var cont = MenuButton("ContinueButton", root, new Vector2(0, -20), new Vector2(460, 96), "계속하기", PinkBtn, Color.white, round, 42);
        var neu = MenuButton("NewGameButton", root, new Vector2(0, -136), new Vector2(460, 96), "새 게임", PinkBtn, Color.white, round, 42);
        var settingsBtn = MenuButton("SettingsButton", root, new Vector2(0, -252), new Vector2(460, 96), "설정", Cream, Brown, round, 42);
        var quit = MenuButton("QuitButton", root, new Vector2(0, -368), new Vector2(460, 96), "게임 종료", Cream, Brown, round, 42);

        var fadeRt = Stretch("Fade", canvas.transform);
        Img(fadeRt, new Color(1f, 0.9f, 0.93f));
        var fade = fadeRt.gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 0f; fade.blocksRaycasts = false; fade.interactable = false;

        var ui = canvas.AddComponent<MainMenuUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("continueButton").objectReferenceValue = cont;
        so.FindProperty("newGameButton").objectReferenceValue = neu;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
        so.FindProperty("fade").objectReferenceValue = fade;
        so.ApplyModifiedPropertiesWithoutUndo();
        var p = PrefabUtility.SaveAsPrefabAsset(canvas, Root + "/Prefabs/UI/HUD_MainMenu.prefab");
        Object.DestroyImmediate(canvas);
        return p.GetComponent<MainMenuUI>();
    }

    static void BuildSettingsUI(Sprite round, Sprite dot)
    {
        var canvas = NewCanvas("HUD_Settings");
        canvas.GetComponent<Canvas>().sortingOrder = 90;

        // 작은 화면 테두리 (창 아래에 그려지도록 첫 자식, 위치는 런타임에 viewport를 따라감)
        var frame = Stretch("PipFrame", canvas.transform);
        var sc = ScreenSplitConfig.I; float w = sc.borderWidth;
        FrameEdge(frame, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 0), new Vector2(2 * w, w));
        FrameEdge(frame, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(2 * w, w));
        FrameEdge(frame, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0.5f), new Vector2(w, 0));
        FrameEdge(frame, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(w, 0));

        // 현재 화면을 연하게 덮는 반투명 패널
        var panel = Stretch("Panel", canvas.transform);
        Img(panel, new Color(0.98f, 0.84f, 0.92f, 0.45f));
        var win = UI("Window", panel, C, C, new Vector2(0, -20), new Vector2(860, 820));
        var winImg = Img(win, new Color(1f, 0.985f, 0.97f, 1f)); winImg.sprite = round; winImg.type = Image.Type.Sliced;
        SoftShadow(win);
        var plate = UI("TitlePlate", win, TC, TC, new Vector2(0, 40), new Vector2(300, 88));
        var plateImg = Img(plate, PinkBtn); plateImg.sprite = round; plateImg.type = Image.Type.Sliced;
        SoftShadow(plate);
        Txt("Title", plate, "설정", 44, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var master = SettingSlider(Row(win, "MasterRow", "마스터 음량", 250), round, dot);
        var sfx = SettingSlider(Row(win, "SfxRow", "효과음", 160), round, dot);
        var amb = SettingSlider(Row(win, "AmbientRow", "환경음", 70), round, dot);
        var split = SettingToggle(Row(win, "SplitRow", "화면 분할", -30), round, dot);
        var modeRow = Row(win, "ModeRow", "분할 방식", -120);
        var half = MenuButton("Half", modeRow, new Vector2(40, 0), new Vector2(210, 64), "2등분", Cream, Brown, round, 30);
        var pip = MenuButton("Pip", modeRow, new Vector2(270, 0), new Vector2(210, 64), "작은 화면", Cream, Brown, round, 30);
        var targetRow = Row(win, "TargetRow", "작은 화면 대상", -210);
        var tw = MenuButton("Watcher", targetRow, new Vector2(40, 0), new Vector2(210, 64), "감시자", Cream, Brown, round, 30);
        var tc = MenuButton("Creature", targetRow, new Vector2(270, 0), new Vector2(210, 64), "생명체", Cream, Brown, round, 30);

        var ret = MenuButton("ReturnButton", win, new Vector2(-150, -330), new Vector2(320, 84), "거점으로 돌아가기", new Color(1f, 0.9f, 0.9f), Brown, round, 32);
        var close = MenuButton("CloseButton", win, new Vector2(185, -330), new Vector2(260, 84), "닫기", PinkBtn, Color.white, round, 34);
        panel.gameObject.SetActive(false);

        var ui = canvas.AddComponent<SettingsUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("master").objectReferenceValue = master;
        so.FindProperty("sfx").objectReferenceValue = sfx;
        so.FindProperty("ambient").objectReferenceValue = amb;
        so.FindProperty("split").objectReferenceValue = split;
        so.FindProperty("splitOn").objectReferenceValue = split.transform.Find("On").gameObject;
        so.FindProperty("modeHalf").objectReferenceValue = half;
        so.FindProperty("modePip").objectReferenceValue = pip;
        so.FindProperty("targetWatcher").objectReferenceValue = tw;
        so.FindProperty("targetCreature").objectReferenceValue = tc;
        so.FindProperty("modeRow").objectReferenceValue = modeRow.gameObject.AddComponent<CanvasGroup>();
        so.FindProperty("targetRow").objectReferenceValue = targetRow.gameObject.AddComponent<CanvasGroup>();
        so.FindProperty("returnButton").objectReferenceValue = ret;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("returnLabel").objectReferenceValue = ret.GetComponentInChildren<TMP_Text>();
        so.FindProperty("pipFrame").objectReferenceValue = frame;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(canvas, Root + "/Prefabs/UI/HUD_Settings.prefab");
        Object.DestroyImmediate(canvas);
    }

    static void FrameEdge(RectTransform frame, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
    {
        var rt = UI(name, frame, aMin, aMax, Vector2.zero, size, pivot);
        Img(rt, Color.white).raycastTarget = false;
    }

    /// <summary>설정 한 줄 (왼쪽 이름 + 오른쪽 조작부, 창 가운데 기준 y).</summary>
    static RectTransform Row(RectTransform win, string name, string label, float y)
    {
        var row = UI(name, win, C, C, new Vector2(0, y), new Vector2(780, 80));
        Txt("Label", row, label, 34, TextAlignmentOptions.Left, Brown, C, C, new Vector2(-230, 0), new Vector2(320, 70));
        return row;
    }

    static Slider SettingSlider(RectTransform row, Sprite round, Sprite dot)
    {
        var rt = UI("Slider", row, C, C, new Vector2(150, 0), new Vector2(440, 44));
        var s = rt.gameObject.AddComponent<Slider>();
        var bg = UI("Background", rt, new Vector2(0, 0.3f), new Vector2(1, 0.7f), Vector2.zero, Vector2.zero);
        var bi = Img(bg, new Color(1f, 0.88f, 0.9f)); bi.sprite = round; bi.type = Image.Type.Sliced; bi.pixelsPerUnitMultiplier = 3f;
        var fa = UI("Fill Area", rt, new Vector2(0, 0.3f), new Vector2(1, 0.7f), Vector2.zero, Vector2.zero);
        var fill = UI("Fill", fa, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var fi = Img(fill, PinkBtn); fi.sprite = round; fi.type = Image.Type.Sliced; fi.pixelsPerUnitMultiplier = 3f;
        var ha = UI("Handle Slide Area", rt, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-36, 0));
        var h = UI("Handle", ha, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(40, 0));
        var hi = Img(h, Color.white); hi.sprite = dot;
        SoftShadow(h);
        s.fillRect = fill; s.handleRect = h; s.targetGraphic = hi;
        s.direction = Slider.Direction.LeftToRight; s.minValue = 0f; s.maxValue = 1f; s.value = 1f;
        return s;
    }

    /// <summary>켜기/끄기 스위치 (켜짐 = 분홍 알약 + 오른쪽 손잡이).</summary>
    static Toggle SettingToggle(RectTransform row, Sprite round, Sprite dot)
    {
        var rt = UI("Toggle", row, C, C, new Vector2(310, 0), new Vector2(110, 56));
        var bg = Img(rt, new Color(0.9f, 0.86f, 0.88f)); bg.sprite = round; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1.2f;
        var offKnob = UI("Knob", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(44, 44));
        Img(offKnob, Color.white).sprite = dot;
        var on = UI("On", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var onImg = Img(on, PinkBtn); onImg.sprite = round; onImg.type = Image.Type.Sliced; onImg.pixelsPerUnitMultiplier = 1.2f;
        var onKnob = UI("Knob", on, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(44, 44));
        Img(onKnob, Color.white).sprite = dot;
        var t = rt.gameObject.AddComponent<Toggle>();
        t.targetGraphic = bg; t.isOn = true; // 켜짐 표시(On)는 SettingsUI가 SetActive
        return t;
    }

    static Button MenuButton(string name, Transform parent, Vector2 pos, Vector2 size, string label, Color bgc, Color fg, Sprite round, int fontSize)
    {
        var rt = UI(name, parent, C, C, pos, size);
        var img = Img(rt, bgc); img.sprite = round; img.type = Image.Type.Sliced;
        SoftShadow(rt);
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img;
        var cb = b.colors;
        cb.highlightedColor = new Color(1f, 0.95f, 0.97f); cb.pressedColor = new Color(0.92f, 0.85f, 0.9f);
        cb.disabledColor = new Color(0.78f, 0.76f, 0.8f, 0.6f);
        b.colors = cb;
        Txt("Label", rt, label, fontSize, TextAlignmentOptions.Center, fg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return b;
    }

    static void BuildMainMenuScene(MainMenuUI menuPrefab, ConfirmPopup confirmPrefab, MainMenuConfig cfg)
    {
        var prev = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try { FillMainMenuScene(scene, menuPrefab, confirmPrefab, cfg); EditorSceneManager.SaveScene(scene, MainMenuScenePath); }
        finally
        {
            if (prev.IsValid()) SceneManager.SetActiveScene(prev);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void FillMainMenuScene(Scene scene, MainMenuUI menuPrefab, ConfirmPopup confirmPrefab, MainMenuConfig cfg)
    {
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(1f, 0.87f, 0.91f);
        camGo.AddComponent<AudioListener>();

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        var bgmGo = new GameObject("MainMenuBgm");
        var src = bgmGo.AddComponent<AudioSource>();
        src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MainMenuBgmPath);
        src.loop = true; src.playOnAwake = true; src.spatialBlend = 0f; src.volume = cfg.bgmVolume;

        var menu = (GameObject)PrefabUtility.InstantiatePrefab(menuPrefab.gameObject, scene);
        var conf = (GameObject)PrefabUtility.InstantiatePrefab(confirmPrefab.gameObject, scene);
        var so = new SerializedObject(menu.GetComponent<MainMenuUI>());
        so.FindProperty("confirm").objectReferenceValue = conf.GetComponent<ConfirmPopup>();
        so.FindProperty("bgm").objectReferenceValue = src;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
