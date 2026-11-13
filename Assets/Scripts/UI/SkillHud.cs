using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 감시자 스킬 HUD (화면 아래 가운데): 장착 칸 2개(키, 이름, 재사용 대기) + 원형 코스트 (원이 가득 차면 +1, 가운데 숫자 = 쓸 수 있는 코스트).
/// StageDirector가 전투 스테이지마다 만듦. 스킬을 쓸 수 없는 스테이지(감시자 없음)에서는 숨김.
/// </summary>
public class SkillHud : MonoBehaviour
{
    class Slot { public Image back, cool; public TMP_Text key, name, time; }
    Slot[] slots;
    Image costFill;
    TMP_Text costText;
    GameObject root;

    void Start() => Build();

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 9;
        var cs = gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
        root = UiKit.Rect("Skills", transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(420f, 120f)).gameObject;
        var cfg = GameManager.I.Config.skills;
        int n = Mathf.Max(1, cfg.slotCount);
        slots = new Slot[n];
        for (int i = 0; i < n; i++)
        {
            var r = UiKit.Rect("Slot" + i, root.transform, new Vector2(0.5f, 0f), new Vector2(-150f + i * 110f, 0f), new Vector2(96f, 96f));
            var s = new Slot();
            s.back = UiKit.Image(r, UiKit.Square, new Color(0.08f, 0.08f, 0.1f, 0.8f));
            s.cool = UiKit.Image(UiKit.Rect("Cool", r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f), true), UiKit.Square, new Color(0f, 0f, 0f, 0.6f));
            s.cool.type = Image.Type.Filled; s.cool.fillMethod = Image.FillMethod.Radial360; s.cool.fillClockwise = false;
            s.key = UiKit.Text(r, "Key", 22, new Color(1f, 0.85f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(90f, 28f));
            s.name = UiKit.Text(r, "Name", 18, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(92f, 40f));
            s.time = UiKit.Text(r, "Time", 26, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(90f, 30f));
            string keyName = i < cfg.slotKeys.Length ? UiKit.KeyName(cfg.slotKeys[i]) : "";
            s.key.text = keyName;
            slots[i] = s;
        }
        // 원형 코스트
        var c = UiKit.Rect("Cost", root.transform, new Vector2(0.5f, 0f), new Vector2(130f + (n - 2) * 110f, 4f), new Vector2(104f, 104f));
        UiKit.Image(c, UiKit.Circle, new Color(0.08f, 0.08f, 0.1f, 0.8f));
        costFill = UiKit.Image(UiKit.Rect("Fill", c, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f), true), UiKit.Circle, new Color(0.55f, 0.85f, 1f, 0.85f));
        costFill.type = Image.Type.Filled; costFill.fillMethod = Image.FillMethod.Radial360; costFill.fillOrigin = (int)Image.Origin360.Top;
        UiKit.Image(UiKit.Rect("Hole", c, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66f, 66f), true), UiKit.Circle, new Color(0.08f, 0.08f, 0.1f, 1f));
        costText = UiKit.Text(c, "Count", 38, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 60f));
    }

    void Update()
    {
        if (root == null) return;
        var ws = WatcherSkills.I;
        bool show = ws != null && !WatcherSkills.Blocked;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) return;
        var gm = GameManager.I; var cfg = gm.Config.skills;
        for (int i = 0; i < slots.Length; i++)
        {
            var d = WatcherSkills.Equipped(i); var s = slots[i];
            if (d == null) { s.name.text = ""; s.time.text = ""; s.cool.fillAmount = 0f; continue; }
            s.name.text = d.name;
            float left = ws.CooldownLeft(d.type), total = cfg.Cooldown(d.type);
            s.cool.fillAmount = total > 0f ? left / total : 0f;
            s.time.text = left > 0f ? Mathf.CeilToInt(left).ToString() : "";
            bool afford = gm.SkillCost >= d.cost;
            s.name.color = afford ? Color.white : new Color(0.6f, 0.6f, 0.65f);
        }
        float cost = Mathf.Min(gm.SkillCost, cfg.maxCost);
        int whole = Mathf.FloorToInt(cost + 0.0001f);
        costText.text = whole.ToString();
        costFill.fillAmount = whole >= cfg.maxCost ? 1f : cost - whole;
    }
}

/// <summary>코드로 만드는 간단한 uGUI 조각 (스킬 HUD / 장착 창 공용).</summary>
public static class UiKit
{
    static Sprite square, circle;
    public static Sprite Square => square ? square : square = Make(false);
    public static Sprite Circle => circle ? circle : circle = Make(true);

    static Sprite Make(bool round)
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float a = 1f;
                if (round) { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)); a = Mathf.Clamp01(n / 2f - d); }
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, bool center = false)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = center ? new Vector2(0.5f, 0.5f) : anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    public static Image Image(RectTransform rt, Sprite s, Color c)
    {
        var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
        img.sprite = s; img.color = c; img.raycastTarget = false;
        return img;
    }

    public static TMP_Text Text(RectTransform parent, string name, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var rt = Rect(name, parent, anchor, pos, box);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.fontStyle = FontStyles.Bold;
        t.enableWordWrapping = true;
        return t;
    }

    public static string KeyName(UnityEngine.InputSystem.Key k)
    {
        string s = k.ToString();
        return s.StartsWith("Digit") ? s.Substring(5) : s;
    }
}
