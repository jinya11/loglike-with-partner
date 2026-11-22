using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 HUD 보조 UI (HUD_Stage 프리팹 안 "HudExtras" — 위치/색/크기는 프리팹에서 편집):
/// - 화면 하단: 레벨업 자동 보상 획득 알림(토스트) + 지금까지 얻은 보상 아이콘 목록(색 칩 + 횟수, ChipTemplate 복제)
/// - 화면 위 중앙: 이번 스테이지 특징 이름 (제한 시간 특징이면 남은 시간)
/// - 하단: 파생 스테이지 상태 한 줄
/// </summary>
public class HudExtras : MonoBehaviour
{
    [SerializeField] TMP_Text toast;
    [SerializeField] CanvasGroup toastGroup;
    [SerializeField] TMP_Text traitLabel;
    [SerializeField] TMP_Text statusLabel;
    [Tooltip("보상 아이콘 줄 (가운데 정렬 기준점)")]
    [SerializeField] RectTransform chipRow;
    [Tooltip("보상 아이콘 원본 (배경 Image + 자식 TMP 글자, 꺼 둔 상태로 둠)")]
    [SerializeField] Image chipTemplate;
    [SerializeField] float chipGap = 6f;
    [SerializeField] float toastTime = 2.2f, toastFade = 0.5f;

    readonly Queue<string> toastQueue = new Queue<string>(8);
    float toastLeft;
    readonly List<(Image bg, TMP_Text label)> chips = new List<(Image, TMP_Text)>(16);
    int shownRewardCount = -1, shownStacks = -1;
    int shownTraitCount = -1;
    int shownTime = int.MinValue;
    bool shownEnraged;

    public static HudExtras I { get; private set; }

    /// <summary>하단 알림 한 줄 (파생 포털 열림 등).</summary>
    public void Notice(string text) { toastQueue.Enqueue(text); }

    /// <summary>파생 스테이지 상태 한 줄 (하단, 아이콘 목록 위). null이면 숨김.</summary>
    public void SetStatus(string text)
    {
        if (!statusLabel) return;
        statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        if (!string.IsNullOrEmpty(text)) statusLabel.text = text;
    }

    void Awake()
    {
        I = this;
        chipTemplate.gameObject.SetActive(false);
        toast.gameObject.SetActive(false);
        traitLabel.gameObject.SetActive(false);
        statusLabel.gameObject.SetActive(false);
        var gm = GameManager.I;
        if (gm) gm.RewardGained += OnReward;
    }

    void OnDestroy()
    {
        if (GameManager.I) GameManager.I.RewardGained -= OnReward;
        if (I == this) I = null;
    }

    void OnReward(LevelRewardDef r, int level)
    {
        toastQueue.Enqueue($"Lv {level}  <color=#{ColorUtility.ToHtmlStringRGB(r.color)}>{r.desc}</color>");
        if (toastQueue.Count > 6) toastQueue.Dequeue();
    }

    /// <summary>표시 초기화 (HUD가 다시 보일 때).</summary>
    public void ResetShown()
    {
        shownRewardCount = shownStacks = -1; shownTraitCount = -1; shownTime = int.MinValue;
        toastQueue.Clear(); toastLeft = 0f;
        if (toast) toast.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        var gm = GameManager.I;
        if (!gm) return;
        UpdateToast();
        UpdateChips(gm);
        UpdateTrait();
    }

    void UpdateToast()
    {
        if (toastLeft <= 0f && toastQueue.Count > 0)
        {
            toast.text = toastQueue.Dequeue();
            toastLeft = toastQueue.Count > 0 ? toastTime * 0.6f : toastTime; // 여러 개 밀리면 빨리 넘김
            toast.gameObject.SetActive(true);
        }
        if (toastLeft <= 0f) return;
        toastLeft -= Time.unscaledDeltaTime;
        toastGroup.alpha = Mathf.Clamp01(toastLeft / toastFade);
        if (toastLeft <= 0f) toast.gameObject.SetActive(false);
    }

    void UpdateChips(GameManager gm)
    {
        int stacks = 0;
        foreach (var kv in gm.RewardStacks) stacks += kv.Value;
        if (gm.RewardOrder.Count == shownRewardCount && stacks == shownStacks) return;
        shownRewardCount = gm.RewardOrder.Count; shownStacks = stacks;
        while (chips.Count < gm.RewardOrder.Count)
        {
            var img = Instantiate(chipTemplate, chipRow);
            img.name = "RewardIcon";
            chips.Add((img, img.GetComponentInChildren<TMP_Text>(true)));
        }
        int n = gm.RewardOrder.Count;
        float chipW = ((RectTransform)chipTemplate.transform).sizeDelta.x;
        float total = n * chipW + Mathf.Max(0, n - 1) * chipGap, x0 = -total * 0.5f + chipW * 0.5f;
        for (int i = 0; i < chips.Count; i++)
        {
            bool on = i < n;
            chips[i].bg.gameObject.SetActive(on);
            if (!on) continue;
            var r = gm.RewardOrder[i];
            var c = r.color;
            chips[i].bg.color = new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.85f);
            ((RectTransform)chips[i].bg.transform).anchoredPosition = new Vector2(x0 + i * (chipW + chipGap), 0f);
            int cnt = gm.RewardStacks.TryGetValue(r, out int k) ? k : 0;
            chips[i].label.text = $"<color=#{ColorUtility.ToHtmlStringRGB(r.color)}>●</color> {r.name} ×{cnt}";
        }
    }

    /// <summary>누적 스테이지 증강 목록 (위 중앙, 카테고리별 색). 제한 시간 증강이면 남은 시간.</summary>
    void UpdateTrait()
    {
        var stage = StageDirector.I;
        int count = stage ? stage.Traits.Count : 0;
        int time = stage && stage.TimeLeft >= 0f ? Mathf.CeilToInt(stage.TimeLeft) : -1;
        bool enraged = stage && stage.Enraged;
        if (count == shownTraitCount && time == shownTime && enraged == shownEnraged) return;
        shownTraitCount = count; shownTime = time; shownEnraged = enraged;
        traitLabel.gameObject.SetActive(count > 0);
        if (count == 0) return;
        var sb = new System.Text.StringBuilder("스테이지 증강  ");
        for (int i = 0; i < count; i++)
        {
            var t = stage.Traits[i];
            if (i > 0) sb.Append("  ·  ");
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(StageTraitDef.CategoryColor(t.category))).Append('>')
              .Append('[').Append(StageTraitDef.CategoryName(t.category)).Append("]</color> ")
              .Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(t.color)).Append('>').Append(t.name).Append("</color>");
        }
        if (enraged) sb.Append("\n<color=#FF5555>시간 초과 — 적 광폭화!</color>");
        else if (time >= 0) sb.Append($"\n남은 시간 {time / 60}:{time % 60:00}");
        traitLabel.text = sb.ToString();
    }
}
