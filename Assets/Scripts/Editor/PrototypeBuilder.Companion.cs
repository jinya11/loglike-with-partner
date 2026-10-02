using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 동반자 선택 장소를 "현재 열린 로비 씬"에 추가 (씬 전체 재생성 없음, 저장하지 않음 → 직접 저장).
/// 메뉴: Roguelike/Add Companion Station
/// </summary>
public static partial class PrototypeBuilder
{
    [MenuItem("Roguelike/Add Companion Station")]
    public static void AddCompanionStation()
    {
        EnsureFonts();
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");

        // ---- 데이터: 거신 설명 보강 + 풍신 추가 ----
        var giant = cfg.companions[0];
        if (string.IsNullOrEmpty(giant.abilityText))
        {
            giant.abilityText = "• 일정 주기마다 플레이어 근처의 적에게 예고 후 낙뢰를 떨어뜨림\n• 낙뢰는 범위 피해 (특수 강화로 피해, 주기, 횟수 증가)\n• 강한 적을 확실하게 처리하는 데 특화";
            giant.ultimateName = "추후 공개"; giant.ultimateDesc = "고유 필살기 컷씬 예정";
            giant.previewScale = 0.07f;
        }
        var wind = BuildWindCompanion();
        int wi = System.Array.FindIndex(cfg.companions, c => c.name == "풍신");
        if (wi < 0)
            ArrayUtility.Add(ref cfg.companions, new CompanionDef
            {
                name = "풍신", desc = "돌풍으로 적을 한쪽으로 밀어냄", prefab = wind, previewScale = 0.07f,
                abilityText = "• 적이 가장 많이 몰린 쪽으로 돌풍을 일으켜 적들을 한쪽으로 멀리 밀어냄\n• 약한 피해 (거신 낙뢰의 30%)\n• 포위당했을 때 길을 여는 데 특화",
                ultimateName = "추후 공개", ultimateDesc = "고유 필살기 컷씬 예정",
            });
        else cfg.companions[wi].prefab = wind;
        EditorUtility.SetDirty(cfg);

        var labelPrefab = BuildWorldLabel();
        var hudPrefab = BuildCompanionSelectHud();
        AssetDatabase.SaveAssets();

        // ---- 씬: 사용자가 만든 플랫폼(Floor (1)) 중앙에 배치 ----
        var floor = GameObject.Find("Floor (1)");
        Vector3 c = floor ? new Vector3(floor.transform.position.x, 0f, floor.transform.position.z) : new Vector3(43f, 0f, 0f);
        var old = GameObject.Find("CompanionStation"); if (old) Object.DestroyImmediate(old);
        var oldHud = GameObject.Find("HUD_CompanionSelect"); if (oldHud) Object.DestroyImmediate(oldHud);

        var station = new GameObject("CompanionStation").transform;
        station.position = c;
        var label = (GameObject)PrefabUtility.InstantiatePrefab(labelPrefab, station);
        label.transform.localPosition = new Vector3(0f, 3.5f, 0f);

        var anchor = new GameObject("PreviewAnchor").transform;
        anchor.SetParent(station, false);
        anchor.localPosition = new Vector3(0f, 0f, 7f);
        anchor.localRotation = Quaternion.Euler(0f, 180f, 0f); // 카메라 쪽을 바라봄

        var camGo = new GameObject("ShowcaseCamera");
        camGo.transform.SetParent(station, false);
        camGo.transform.localPosition = new Vector3(2.6f, 1.9f, 0f);
        camGo.transform.localRotation = Quaternion.identity; // 정면(+Z)을 봄 → 동반자는 화면 왼쪽
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        cam.fieldOfView = 50f; cam.enabled = false;
        var keyLight = new GameObject("ShowcaseLight").AddComponent<Light>();
        keyLight.type = LightType.Spot; keyLight.range = 14f; keyLight.spotAngle = 50f; keyLight.intensity = 30f;
        keyLight.transform.SetParent(station, false);
        keyLight.transform.localPosition = new Vector3(-2f, 6f, 3f);
        keyLight.transform.LookAt(anchor.position + Vector3.up * 2f);

        var hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
        var refs = hud.GetComponent<CompanionSelectRefs>();
        var lobbyHud = GameObject.Find("HUD_Lobby");

        var cs = station.gameObject.AddComponent<CompanionStation>();
        var so = new SerializedObject(cs);
        so.FindProperty("showcaseCamera").objectReferenceValue = cam;
        so.FindProperty("previewAnchor").objectReferenceValue = anchor;
        so.FindProperty("panel").objectReferenceValue = refs.panel;
        so.FindProperty("nameText").objectReferenceValue = refs.nameText;
        so.FindProperty("abilityText").objectReferenceValue = refs.abilityText;
        so.FindProperty("ultimateText").objectReferenceValue = refs.ultimateText;
        so.FindProperty("indexText").objectReferenceValue = refs.indexText;
        so.FindProperty("currentBadge").objectReferenceValue = refs.currentBadge;
        if (lobbyHud) SetArray(so.FindProperty("hideCanvases"), new Object[] { lobbyHud.GetComponent<Canvas>() });
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
        Selection.activeGameObject = station.gameObject;
        Debug.Log("[PrototypeBuilder] 동반자 선택 장소 추가 (씬 저장은 직접)");
    }

    /// <summary>풍신: 날개(부채) 달린 청록색 거인.</summary>
    static GameObject BuildWindCompanion()
    {
        var root = new GameObject("Companion_Wind");
        var body = Mat("WindBody", new Color(0.16f, 0.32f, 0.34f));
        var fan = Mat("WindFan", new Color(0.55f, 0.9f, 0.85f));
        Prim(PrimitiveType.Capsule, "Torso", root.transform, new Vector3(0, 10f, 0), new Vector3(8f, 9f, 5f), body);
        var head = new GameObject("Head").transform; head.SetParent(root.transform, false); head.localPosition = new Vector3(0, 21f, -1f);
        Prim(PrimitiveType.Sphere, "Skull", head, Vector3.zero, new Vector3(6f, 7f, 6f), body);
        Prim(PrimitiveType.Cylinder, "Crest", head, new Vector3(0, 4f, -0.5f), new Vector3(1.2f, 2f, 1.2f), fan);
        var eyeMat = Mat("CompanionEye", Color.white);
        var eyeL = Prim(PrimitiveType.Sphere, "EyeL", head, new Vector3(-1.2f, 0.4f, 2.7f), new Vector3(1.5f, 0.6f, 0.8f), eyeMat).GetComponent<Renderer>();
        var eyeR = Prim(PrimitiveType.Sphere, "EyeR", head, new Vector3(1.2f, 0.4f, 2.7f), new Vector3(1.5f, 0.6f, 0.8f), eyeMat).GetComponent<Renderer>();
        for (int s = -1; s <= 1; s += 2)
        {
            var w = Prim(PrimitiveType.Cylinder, s < 0 ? "FanL" : "FanR", root.transform, new Vector3(s * 9f, 15f, 1f), new Vector3(9f, 0.3f, 9f), fan);
            w.transform.localRotation = Quaternion.Euler(0, 0, s * 70f);
        }
        var wc = root.AddComponent<WindCompanion>();
        var so = new SerializedObject(wc);
        so.FindProperty("head").objectReferenceValue = head;
        var eyes = so.FindProperty("eyes"); eyes.arraySize = 2;
        eyes.GetArrayElementAtIndex(0).objectReferenceValue = eyeL; eyes.GetArrayElementAtIndex(1).objectReferenceValue = eyeR;
        so.FindProperty("distanceFromEdge").floatValue = 14f;
        so.FindProperty("height").floatValue = -43f;
        so.FindProperty("eyeIdle").colorValue = new Color(0.5f, 1f, 0.9f);
        so.FindProperty("eyeActive").colorValue = Color.white;
        so.ApplyModifiedPropertiesWithoutUndo();
        root.transform.rotation = Quaternion.Euler(0, 180f, 0);
        root.transform.localScale = Vector3.one * 2.2f;
        return Save(root, "Companion_Wind");
    }

    /// <summary>플랫폼 위 월드 공간 문구 "동반자 선택 장소" (위치는 월드 고정, 항상 카메라를 향함).</summary>
    static GameObject BuildWorldLabel()
    {
        var go = new GameObject("WorldLabel_CompanionSelect", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(700, 140); rt.localScale = Vector3.one * 0.01f;
        go.AddComponent<Billboard>();
        Img(Stretch("Background", go.transform), new Color(0.05f, 0.04f, 0.07f, 0.7f));
        Txt("Label", go.transform, "동반자 선택 장소", 72, TextAlignmentOptions.Center, Gold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return Save(go, "UI/WorldLabel_CompanionSelect");
    }

    /// <summary>동반자 선택 화면 UI. 왼쪽은 비워 동반자 모습이 보이고, 오른쪽에 능력 설명.</summary>
    static GameObject BuildCompanionSelectHud()
    {
        var canvas = NewCanvas("HUD_CompanionSelect");
        canvas.GetComponent<Canvas>().sortingOrder = 10;
        var panel = UI("Panel", canvas.transform, new Vector2(0.52f, 0.06f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);
        Img(panel, new Color(0.06f, 0.055f, 0.08f, 0.88f));
        var p = panel.transform;
        Txt("Header", p, "동반자 선택", 26, TextAlignmentOptions.TopLeft, Gold, TL, TL, new Vector2(40, -36), new Vector2(600, 40), TL);
        var name = Txt("NameText", p, "{0}", 64, TextAlignmentOptions.TopLeft, Color.white, TL, TL, new Vector2(40, -80), new Vector2(700, 80), TL);
        var badge = Txt("CurrentBadge", p, "현재 동반자", 22, TextAlignmentOptions.TopLeft, new Color(0.5f, 1f, 0.8f), TL, TL, new Vector2(40, -165), new Vector2(400, 34), TL);
        Txt("AbilityHeader", p, "능력", 28, TextAlignmentOptions.TopLeft, Gold, TL, TL, new Vector2(40, -230), new Vector2(600, 40), TL);
        var ability = Txt("AbilityText", p, "능력 설명", 26, TextAlignmentOptions.TopLeft, Soft, TL, new Vector2(1, 1), new Vector2(40, -280), new Vector2(-80, 300), new Vector2(0, 1), false);
        Txt("UltimateHeader", p, "필살기", 28, TextAlignmentOptions.TopLeft, Gold, TL, TL, new Vector2(40, -600), new Vector2(600, 40), TL);
        var ult = Txt("UltimateText", p, "{0}\n{1}", 24, TextAlignmentOptions.TopLeft, Soft, TL, new Vector2(1, 1), new Vector2(40, -650), new Vector2(-80, 120), new Vector2(0, 1), false);
        var index = Txt("IndexText", p, "{0} / {1}", 24, TextAlignmentOptions.BottomLeft, Soft, Vector2.zero, Vector2.zero, new Vector2(40, 40), new Vector2(200, 40), Vector2.zero);
        Txt("Hint", p, "←/→ 다른 동반자    Enter 선택    Esc 취소", 24, TextAlignmentOptions.BottomRight, Color.white, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(700, 40), new Vector2(1, 0));
        panel.gameObject.SetActive(false);

        var refs = canvas.AddComponent<CompanionSelectRefs>();
        refs.panel = panel.gameObject; refs.nameText = name; refs.abilityText = ability; refs.ultimateText = ult; refs.indexText = index; refs.currentBadge = badge.gameObject;
        return Save(canvas, "UI/HUD_CompanionSelect");
    }
}
