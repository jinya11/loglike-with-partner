using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 감시자 스킬 장착 창 (화면 오른쪽). 로비에서 WatcherSkillConfig.menuKey(기본 U)로 열고 닫음. 보너스 스테이지(설탕 집)에서는 스킬 장착대(SkillStation, F)로 엶.
/// 스킬 칸을 클릭하면 장착: 빈 칸이 있으면 거기에, 2칸이 다 찼으면 먼저 장착한 것을 빼고 새 것을. 장착한 스킬을 다시 클릭하면 해제.
/// 해금된 스킬만 고를 수 있음 (해금 조건 로직은 아직 없음 — 표의 6개는 기본 해금). 설명은 한 줄.
/// </summary>
public class SkillSelectUI : MonoBehaviour
{
    static SkillSelectUI inst;
    /// <summary>로비처럼 단축키로 열 수 있는 곳인지 (LobbyDirector가 켬).</summary>
    public static bool HotkeyEnabled;
    public static bool IsOpen => inst && inst.panel && inst.panel.activeSelf;

    GameObject panel;
    class Row { public WatcherSkillDef def; public Image back; public TMP_Text badge; }
    Row[] rows;

    public static void Ensure() { if (!inst) inst = new GameObject("SkillSelectUI").AddComponent<SkillSelectUI>(); }
    public static void Toggle() { Ensure(); inst.SetOpen(!IsOpen); }

    void Awake() => Build();
    void OnDestroy() { if (inst == this) { inst = null; if (GameManager.I) GameManager.I.InputLocked = false; } }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 21;
        var cs = gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        SaveData.EnsureSkillLoadout();
        var cfg = GameManager.I.Config.skills;
        int n = cfg.skills.Length;
        float rowH = 74f, h = 70f + n * (rowH + 8f);
        var p = UiKit.Rect("Panel", transform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(460f, h));
        p.pivot = new Vector2(1f, 0.5f);
        UiKit.Image(p, UiKit.Square, new Color(0.07f, 0.065f, 0.08f, 0.92f));
        panel = p.gameObject;
        UiKit.Text(p, "Title", 28, new Color(0.95f, 0.8f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(420f, 40f)).text = "감시자 스킬";
        rows = new Row[n];
        for (int i = 0; i < n; i++)
        {
            var d = cfg.skills[i];
            var r = UiKit.Rect("Skill_" + d.type, p, new Vector2(0.5f, 1f), new Vector2(0f, -64f - i * (rowH + 8f)), new Vector2(430f, rowH));
            var back = UiKit.Image(r, UiKit.Square, new Color(0.16f, 0.15f, 0.19f));
            back.raycastTarget = true;
            var btn = r.gameObject.AddComponent<Button>();
            var t = d.type; btn.onClick.AddListener(() => Pick(t));
            var title = UiKit.Text(r, "Name", 24, Color.white, new Vector2(0f, 1f), new Vector2(14f, -6f), new Vector2(300f, 32f));
            title.alignment = TextAlignmentOptions.Left;
            title.text = $"{d.name}  <size=18><color=#B8B2C8>{d.category} · 코스트 {d.cost} · {d.cooldown:0.#}초</color></size>";
            var line = UiKit.Text(r, "Line", 17, new Color(0.82f, 0.8f, 0.86f), new Vector2(0f, 0f), new Vector2(14f, 8f), new Vector2(360f, 26f));
            line.alignment = TextAlignmentOptions.Left; line.fontStyle = FontStyles.Normal;
            line.text = d.oneLine;
            var badge = UiKit.Text(r, "Slot", 30, new Color(1f, 0.85f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(48f, 48f));
            badge.rectTransform.pivot = new Vector2(1f, 0.5f);
            rows[i] = new Row { def = d, back = back, badge = badge };
        }
        panel.SetActive(false);
        Refresh();
    }

    void SetOpen(bool on)
    {
        if (!panel) return;
        panel.SetActive(on);
        if (GameManager.I) GameManager.I.InputLocked = on;
        if (on) Refresh();
    }

    /// <summary>스킬 칸 클릭: 장착/해제/교체.</summary>
    void Pick(WatcherSkillType t)
    {
        var cfg = GameManager.I.Config.skills;
        var d = cfg.Get(t);
        if (!SaveData.SkillUnlocked(d)) return;
        int n = cfg.slotCount;
        for (int i = 0; i < n; i++)
            if (SaveData.EquippedSkill(i) == (int)t) { SaveData.SetEquippedSkill(i, -1); Compact(n); Refresh(); return; } // 해제
        for (int i = 0; i < n; i++)
            if (SaveData.EquippedSkill(i) < 0) { SaveData.SetEquippedSkill(i, (int)t); Refresh(); return; } // 빈 칸에
        // 다 찼으면 먼저 장착한 것(1번 칸)을 빼고 한 칸씩 당긴 뒤 마지막 칸에
        for (int i = 0; i < n - 1; i++) SaveData.SetEquippedSkill(i, SaveData.EquippedSkill(i + 1));
        SaveData.SetEquippedSkill(n - 1, (int)t);
        Refresh();
    }

    static void Compact(int n)
    {
        int w = 0;
        for (int i = 0; i < n; i++) { int s = SaveData.EquippedSkill(i); if (s >= 0) SaveData.SetEquippedSkill(w++, s); }
        for (; w < n; w++) SaveData.SetEquippedSkill(w, -1);
    }

    void Refresh()
    {
        if (rows == null) return;
        var cfg = GameManager.I.Config.skills;
        foreach (var r in rows)
        {
            int slot = -1;
            for (int i = 0; i < cfg.slotCount; i++) if (SaveData.EquippedSkill(i) == (int)r.def.type) slot = i;
            bool unlocked = SaveData.SkillUnlocked(r.def);
            r.badge.text = slot >= 0 && slot < cfg.slotKeys.Length ? UiKit.KeyName(cfg.slotKeys[slot]) : "";
            r.back.color = !unlocked ? new Color(0.1f, 0.1f, 0.11f) : slot >= 0 ? new Color(0.3f, 0.25f, 0.14f) : new Color(0.16f, 0.15f, 0.19f);
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || GameManager.I == null) return;
        if (IsOpen && kb.escapeKey.wasPressedThisFrame) { SetOpen(false); return; }
        if (kb[GameManager.I.Config.skills.menuKey].wasPressedThisFrame && (HotkeyEnabled || IsOpen)) SetOpen(!IsOpen);
    }
}
