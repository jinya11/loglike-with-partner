using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 감시자 스킬 장착 창. 로비에서 WatcherSkillConfig.menuKey(기본 U) 또는 오른쪽 위 책 버튼으로 열고 닫음. 보너스 스테이지(설탕 집)에서는 스킬 장착대(SkillStation, F)로 엶.
/// 왼쪽: 분류(유도/폭격/방어)별 스킬 카드. 카드 클릭 = 선택(아래 설명 + 장착하기 버튼), 더블클릭 = 바로 장착/해제.
/// 오른쪽: 장착 칸 2개. 칸 클릭 = 그 스킬 선택, 더블클릭 = 해제.
/// 장착 규칙: 빈 칸이 있으면 거기에, 다 찼으면 1번 칸을 빼고 한 칸씩 당긴 뒤 마지막 칸에. 같은 분류는 함께 장착 불가.
/// 프리팹 HUD_SkillSelect (StageConfig.skillSelectPrefab, 메뉴 Roguelike/Build Skill Window). 카드·분류 묶음은 템플릿 복제.
/// </summary>
public class SkillSelectUI : MonoBehaviour
{
    static SkillSelectUI inst;
    /// <summary>로비처럼 단축키로 열 수 있는 곳인지 (LobbyDirector가 켬).</summary>
    public static bool HotkeyEnabled;
    public static bool IsOpen => inst && inst.panel && inst.panel.activeSelf;

    [SerializeField] GameObject panel;
    [Tooltip("분류 묶음 원본 (꺼 둠): 자식 Header(TMP), Cards(카드가 들어갈 곳)")]
    [SerializeField] RectTransform groupTemplate;
    [Tooltip("스킬 카드 원본 (꺼 둠)")]
    [SerializeField] SkillSelectRow cardTemplate;
    [Tooltip("장착 칸 (칸 수만큼)")]
    [SerializeField] SkillSelectRow[] slots;
    [Tooltip("오른쪽 위 책 아이콘 버튼 (클릭 = 메뉴 키와 같음)")]
    [SerializeField] Button bookButton;
    [SerializeField] Button closeButton;

    [Header("설명")]
    [SerializeField] GameObject descRoot;
    [SerializeField] TMP_Text descName;
    [Tooltip("서식: {0}=분류 {1}=코스트 {2}=재사용 대기(초)")]
    [SerializeField] TMP_Text descInfo;
    [SerializeField] TMP_Text descLine;
    [SerializeField] Button equipButton;
    [SerializeField] TMP_Text equipLabel;
    [SerializeField] string equipText = "장착하기", unequipText = "해제하기", lockedText = "잠김", categoryText = "같은 분류 장착 중";

    [Header("색")]
    [SerializeField] Color cardIdle = new Color(1f, 0.96f, 0.88f), cardEquipped = new Color(1f, 0.85f, 0.55f), cardLocked = new Color(0.72f, 0.68f, 0.64f), cardBlocked = new Color(0.86f, 0.82f, 0.76f);
    [SerializeField] Color slotEmpty = new Color(0.9f, 0.84f, 0.74f), slotFilled = new Color(1f, 0.85f, 0.55f);

    SkillSelectRow[] cards;
    string infoFmt;
    int selected = -1; // 선택한 스킬 (WatcherSkillType), -1 = 없음

    WatcherSkillConfig Cfg => GameManager.I.Config.skills;

    public static void Ensure()
    {
        if (inst) return;
        var prefab = GameManager.I ? GameManager.I.Config.stage.skillSelectPrefab : null;
        if (!prefab) { Debug.LogWarning("StageConfig.skillSelectPrefab 이 비어 있음"); return; }
        Instantiate(prefab);
    }
    public static void Toggle() { Ensure(); if (inst) inst.SetOpen(!IsOpen); }

    void Awake()
    {
        inst = this;
        UiText.EnsureEventSystem();
        SaveData.EnsureSkillLoadout();
        Cfg.SanitizeLoadout(); // 예전에 저장된 같은 분류 2개 정리
        infoFmt = descInfo ? descInfo.text : "";
        BuildCards();
        for (int i = 0; i < slots.Length; i++)
        {
            int k = i;
            slots[i].button.onClick.AddListener(() => { int s = SaveData.EquippedSkill(k); if (s >= 0) Select(s); });
            slots[i].onDoubleClick = () => { int s = SaveData.EquippedSkill(k); if (s >= 0) Toggle((WatcherSkillType)s); };
        }
        if (equipButton) equipButton.onClick.AddListener(() => { if (selected >= 0) Toggle((WatcherSkillType)selected); });
        if (bookButton) bookButton.onClick.AddListener(() => SetOpen(!IsOpen));
        if (closeButton) closeButton.onClick.AddListener(() => SetOpen(false));
        panel.SetActive(false);
        Refresh();
    }

    void OnDestroy() { if (inst == this) { inst = null; if (GameManager.I) GameManager.I.InputLocked = false; } }

    /// <summary>분류마다 묶음을 복제하고 그 안에 스킬 카드를 복제.</summary>
    void BuildCards()
    {
        var skills = Cfg.skills;
        groupTemplate.gameObject.SetActive(false);
        cardTemplate.gameObject.SetActive(false);
        cards = new SkillSelectRow[skills.Length];
        foreach (WatcherSkillCategory cat in System.Enum.GetValues(typeof(WatcherSkillCategory)))
        {
            RectTransform group = null;
            for (int i = 0; i < skills.Length; i++)
            {
                var d = skills[i];
                if (d == null || d.category != cat) continue;
                if (!group)
                {
                    group = Instantiate(groupTemplate, groupTemplate.parent);
                    group.name = "Group_" + cat;
                    group.gameObject.SetActive(true);
                    var h = group.Find("Header")?.GetComponent<TMP_Text>();
                    if (h) h.text = cat.ToString();
                }
                var c = Instantiate(cardTemplate, group.Find("Cards"));
                c.name = "Skill_" + d.type;
                c.gameObject.SetActive(true);
                c.def = d;
                c.title.text = d.name;
                if (c.line) c.line.text = UiText.Format(c.line.text, d.cost);
                int t = (int)d.type;
                c.button.onClick.AddListener(() => Select(t));
                c.onDoubleClick = () => Toggle((WatcherSkillType)t);
                cards[i] = c;
            }
        }
    }

    void SetOpen(bool on)
    {
        if (!panel) return;
        panel.SetActive(on);
        if (GameManager.I) GameManager.I.InputLocked = on;
        if (on) Refresh();
    }

    void Select(int t) { selected = t; Refresh(); }

    int SlotOf(WatcherSkillType t)
    {
        for (int i = 0; i < Cfg.slotCount; i++) if (SaveData.EquippedSkill(i) == (int)t) return i;
        return -1;
    }

    /// <summary>장착 / 해제 / 교체.</summary>
    void Toggle(WatcherSkillType t)
    {
        var cfg = Cfg;
        selected = (int)t;
        if (!SaveData.SkillUnlocked(cfg.Get(t))) { Refresh(); return; }
        int n = cfg.slotCount;
        if (SlotOf(t) >= 0) { SaveData.SetEquippedSkill(SlotOf(t), -1); Compact(n); Refresh(); return; } // 해제
        if (cfg.CategoryTaken(t)) { Refresh(); return; } // 같은 분류는 함께 장착 불가
        for (int i = 0; i < n; i++)
            if (SaveData.EquippedSkill(i) < 0) { SaveData.SetEquippedSkill(i, (int)t); Refresh(); return; } // 빈 칸에
        // 다 찼으면 1번 칸을 빼고 한 칸씩 당긴 뒤 마지막 칸에
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
        if (cards == null) return;
        var cfg = Cfg;
        var g = GameManager.I.Creature?.growth; // 선택한 생명체가 추천하는 스킬 분류
        foreach (var c in cards)
        {
            if (!c) continue;
            int slot = SlotOf(c.def.type);
            bool unlocked = SaveData.SkillUnlocked(c.def);
            bool blocked = slot < 0 && cfg.CategoryTaken(c.def.type);
            c.background.color = !unlocked ? cardLocked : slot >= 0 ? cardEquipped : blocked ? cardBlocked : cardIdle;
            if (c.badge) c.badge.text = slot >= 0 && slot < cfg.slotKeys.Length ? UiText.KeyName(cfg.slotKeys[slot]) : "";
            if (c.lockMark) c.lockMark.SetActive(!unlocked);
            if (c.selectedMark) c.selectedMark.SetActive((int)c.def.type == selected);
            if (c.recommend) c.recommend.SetActive(g && g.recommendSkills && c.def.category == g.recommendedCategory);
        }
        for (int i = 0; i < slots.Length; i++)
        {
            int s = i < cfg.slotCount ? SaveData.EquippedSkill(i) : -1;
            var d = s >= 0 ? cfg.Get((WatcherSkillType)s) : null;
            var v = slots[i];
            v.background.color = d != null ? slotFilled : slotEmpty;
            v.title.text = d != null ? d.name : "";
            if (v.line) v.line.gameObject.SetActive(d == null);
            if (v.badge) v.badge.text = i < cfg.slotKeys.Length ? UiText.KeyName(cfg.slotKeys[i]) : "";
            if (v.selectedMark) v.selectedMark.SetActive(d != null && s == selected);
        }
        var sel = selected >= 0 ? cfg.Get((WatcherSkillType)selected) : null;
        if (descRoot) descRoot.SetActive(sel != null);
        if (sel == null) return;
        descName.text = sel.name;
        descInfo.text = UiText.Format(infoFmt, sel.category, sel.cost, cfg.Cooldown(sel.type));
        descLine.text = sel.oneLine;
        bool on = SaveData.SkillUnlocked(sel), eq = SlotOf(sel.type) >= 0, cat = !eq && cfg.CategoryTaken(sel.type);
        equipButton.interactable = on && !cat;
        equipLabel.text = !on ? lockedText : eq ? unequipText : cat ? categoryText : equipText;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || GameManager.I == null) return;
        if (IsOpen && kb.escapeKey.wasPressedThisFrame) { SetOpen(false); return; }
        if (kb[Cfg.menuKey].wasPressedThisFrame && (HotkeyEnabled || IsOpen)) SetOpen(!IsOpen);
    }
}
