using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 감시자 스킬 HUD (화면 아래 가운데): 장착 칸(키, 이름, 재사용 대기) + 원형 코스트 (원이 가득 차면 +1, 가운데 숫자 = 쓸 수 있는 코스트).
/// 프리팹 HUD_SkillBar (StageConfig.skillHudPrefab) — StageDirector가 전투 스테이지마다 생성. 위치/색/크기는 프리팹에서 편집.
/// 스킬을 쓸 수 없는 스테이지(감시자 없음)에서는 숨김.
/// </summary>
public class SkillHud : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public GameObject root;
        [Tooltip("재사용 대기 표시 (Filled / Radial360)")] public Image cool;
        public TMP_Text key, name, time;
    }

    [Tooltip("숨김/표시 대상 (스킬 칸 + 코스트)")]
    [SerializeField] GameObject root;
    [SerializeField] Slot[] slots;
    [Tooltip("코스트 원 채움 (Filled / Radial360)")]
    [SerializeField] Image costFill;
    [SerializeField] TMP_Text costText;
    [SerializeField] Color nameAffordable = Color.white, nameTooExpensive = new Color(0.6f, 0.6f, 0.65f);

    int slotCount;

    void Start()
    {
        var cfg = GameManager.I.Config.skills;
        slotCount = Mathf.Min(slots.Length, Mathf.Max(1, cfg.slotCount));
        for (int i = 0; i < slots.Length; i++)
        {
            bool on = i < slotCount;
            if (slots[i].root) slots[i].root.SetActive(on);
            if (on) slots[i].key.text = i < cfg.slotKeys.Length ? UiText.KeyName(cfg.slotKeys[i]) : "";
        }
    }

    void Update()
    {
        var ws = WatcherSkills.I;
        bool show = ws != null && !WatcherSkills.Blocked;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) return;
        var gm = GameManager.I; var cfg = gm.Config.skills;
        for (int i = 0; i < slotCount; i++)
        {
            var d = WatcherSkills.Equipped(i); var s = slots[i];
            if (d == null) { s.name.text = ""; s.time.text = ""; s.cool.fillAmount = 0f; continue; }
            s.name.text = d.name;
            float left = ws.CooldownLeft(d.type), total = cfg.Cooldown(d.type);
            s.cool.fillAmount = total > 0f ? left / total : 0f;
            if (left > 0f) s.time.SetText("{0}", Mathf.CeilToInt(left)); else s.time.text = "";
            s.name.color = gm.SkillCost >= d.cost ? nameAffordable : nameTooExpensive;
        }
        float cost = Mathf.Min(gm.SkillCost, cfg.maxCost);
        int whole = Mathf.FloorToInt(cost + 0.0001f);
        costText.SetText("{0}", whole); // 숫자 전용 SetText: 문자열 할당 없음
        costFill.fillAmount = whole >= cfg.maxCost ? 1f : cost - whole;
    }
}
