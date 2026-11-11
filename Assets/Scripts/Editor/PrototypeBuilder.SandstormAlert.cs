using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>모래폭풍 경보 UI 프리팹 (Canvas). 스프라이트(링/원/비네트)는 코드로 그린 PNG.</summary>
public static partial class PrototypeBuilder
{
    const string VariantUiDir = Root + "/Materials/Variants/UI";

    [MenuItem("Roguelike/QA/모래폭풍 사막: 경보 UI 씬에 넣기")]
    static void AddSandstormAlertToOpenScene()
    {
        var rule = Object.FindAnyObjectByType<DesertSandstormVariant>();
        if (!rule) { EditorUtility.DisplayDialog("경보 UI", "모래폭풍 사막 씬을 먼저 여세요.", "확인"); return; }
        PlaceSandstormAlert(rule);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rule.gameObject.scene);
    }

    static void PlaceSandstormAlert(DesertSandstormVariant rule)
    {
        if (rule.alert) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(SandstormAlertPrefab(), rule.transform);
        rule.alert = go.GetComponent<SandstormAlertUI>();
        EditorUtility.SetDirty(rule);
    }

    static Sprite UiSprite(string name, int n, System.Func<float, float, float> alpha)
    {
        EnsureFolder(Root + "/Materials/Variants", "UI");
        string path = $"{VariantUiDir}/{name}.png";
        if (!System.IO.File.Exists(path))
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f))));
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static GameObject SandstormAlertPrefab()
    {
        string path = $"{VariantPrefabDir}/SandstormAlert.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var ring = UiSprite("Ring", 256, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01((d - 0.78f) * 60f) * Mathf.Clamp01((1f - d) * 60f); });
        var disc = UiSprite("Disc", 256, (x, y) => Mathf.Clamp01((0.98f - Mathf.Sqrt(x * x + y * y)) * 60f));
        var vignette = UiSprite("Vignette", 256, (x, y) => { float e = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)); return Mathf.Pow(Mathf.Clamp01((e - 0.55f) / 0.45f), 1.6f); });
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Paperlogy-7Bold SDF.asset");

        var root = new GameObject("SandstormAlert", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
        var ui = root.AddComponent<SandstormAlertUI>();

        var content = UiRect("Content", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ui.group = content.gameObject.AddComponent<CanvasGroup>(); ui.group.blocksRaycasts = false; ui.group.interactable = false;
        ui.edge = UiImage("EdgePulse", content, vignette, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var panel = UiRect("Panel", content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(320f, 280f));
        ui.panel = panel;
        var center = new Vector2(0.5f, 0.5f);
        var back = UiImage("GaugeBack", panel, ring, center, center, new Vector2(0f, 20f), new Vector2(170f, 170f));
        back.color = new Color(0f, 0f, 0f, 0.35f);
        ui.gauge = UiImage("Gauge", panel, ring, center, center, new Vector2(0f, 20f), new Vector2(170f, 170f));
        ui.gauge.type = Image.Type.Filled; ui.gauge.fillMethod = Image.FillMethod.Radial360;
        ui.gauge.fillOrigin = (int)Image.Origin360.Top; ui.gauge.fillClockwise = false;
        ui.icon = UiImage("Icon", panel, disc, center, center, new Vector2(0f, 20f), new Vector2(128f, 128f));
        var shade = UiImage("IconShade", ui.icon.rectTransform, disc, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        shade.color = new Color(0.25f, 0.12f, 0.05f, 0.35f);
        ui.count = UiText("Count", ui.icon.rectTransform, font, 78, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ui.label = UiText("Label", panel, font, 34, center, center, new Vector2(-40f, -98f), new Vector2(220f, 46f));
        ui.label.text = "모래폭풍";
        // 바람에 밀릴 방향 화살표 (라벨 오른쪽)
        var arrow = UiRect("WindArrow", panel, center, center, new Vector2(110f, -98f), new Vector2(70f, 70f));
        UiBar(arrow, new Vector2(-6f, 0f), new Vector2(46f, 10f), 0f);
        UiBar(arrow, new Vector2(12f, 9f), new Vector2(28f, 10f), -42f);
        UiBar(arrow, new Vector2(12f, -9f), new Vector2(28f, 10f), 42f);
        ui.arrow = arrow;

        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }

    static RectTransform UiRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static Image UiImage(string name, Transform parent, Sprite sp, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var img = UiRect(name, parent, aMin, aMax, pos, size).gameObject.AddComponent<Image>();
        img.sprite = sp; img.raycastTarget = false;
        return img;
    }

    static TMP_Text UiText(string name, Transform parent, TMP_FontAsset font, float size, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz)
    {
        var t = UiRect(name, parent, aMin, aMax, pos, sz).gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.outlineWidth = 0.2f; t.outlineColor = new Color(0f, 0f, 0f, 0.8f);
        t.text = "5";
        return t;
    }

    static void UiBar(Transform parent, Vector2 pos, Vector2 size, float rot)
    {
        var img = UiImage("Bar", parent, null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
        img.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
    }
}
