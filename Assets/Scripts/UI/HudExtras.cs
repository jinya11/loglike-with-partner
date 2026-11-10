using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 HUD 보조 UI (코드로 생성, Hud가 Awake에서 붙임):
/// - 화면 하단: 레벨업 자동 보상 획득 알림(토스트) + 지금까지 얻은 보상 아이콘 목록(색 칩 + 횟수)
/// - 화면 위 중앙: 이번 스테이지 특징 이름 (제한 시간 특징이면 남은 시간)
/// 글꼴은 HUD의 기존 텍스트에서 가져옴.
/// </summary>
public class HudExtras : MonoBehaviour
{
    const float ToastTime = 2.2f, ToastFade = 0.5f, ChipW = 176f, ChipH = 30f, ChipGap = 6f;

    TMP_FontAsset font;
    RectTransform root, chipRow;
    TMP_Text toast, traitLabel;
    CanvasGroup toastGroup;
    readonly Queue<string> toastQueue = new Queue<string>(8);
    float toastLeft;
    readonly List<(Image bg, TMP_Text label)> chips = new List<(Image, TMP_Text)>(16);
    int shownRewardCount = -1, shownStacks = -1;
    StageTraitDef shownTrait;
    int shownTime = int.MinValue;
    bool shownEnraged;

    public void Init(TMP_FontAsset f)
    {
        font = f;
        root = (RectTransform)new GameObject("HudExtras", typeof(RectTransform)).transform;
        root.SetParent(transform, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;

        chipRow = (RectTransform)new GameObject("RewardIcons", typeof(RectTransform)).transform;
        chipRow.SetParent(root, false);
        chipRow.anchorMin = chipRow.anchorMax = new Vector2(0.5f, 0f);
        chipRow.pivot = new Vector2(0.5f, 0f);
        chipRow.anchoredPosition = new Vector2(0f, 16f);
        chipRow.sizeDelta = new Vector2(1600f, ChipH);

        toast = Text("RewardToast", root, 30, new Color(1f, 0.92f, 0.55f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(1200f, 44f));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toast.gameObject.SetActive(false);

        traitLabel = Text("StageTraitLabel", root, 24, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(1000f, 36f));
        traitLabel.gameObject.SetActive(false);

        var gm = GameManager.I;
        if (gm) gm.RewardGained += OnReward;
    }

    void OnDestroy() { if (GameManager.I) GameManager.I.RewardGained -= OnReward; }

    TMP_Text Text(string name, Transform parent, float size, Color c, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        t.outlineWidth = 0.15f; t.outlineColor = new Color(0f, 0f, 0f, 0.8f);
        return t;
    }

    void OnReward(LevelRewardDef r, int level)
    {
        toastQueue.Enqueue($"Lv {level}  <color=#{ColorUtility.ToHtmlStringRGB(r.color)}>{r.desc}</color>");
        if (toastQueue.Count > 6) toastQueue.Dequeue();
    }

    /// <summary>표시 초기화 (HUD가 다시 보일 때).</summary>
    public void ResetShown()
    {
        shownRewardCount = shownStacks = -1; shownTrait = null; shownTime = int.MinValue;
        toastQueue.Clear(); toastLeft = 0f;
        if (toast) toast.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        var gm = GameManager.I;
        if (!gm || !root) return;
        UpdateToast();
        UpdateChips(gm);
        UpdateTrait();
    }

    void UpdateToast()
    {
        if (toastLeft <= 0f && toastQueue.Count > 0)
        {
            toast.text = toastQueue.Dequeue();
            toastLeft = toastQueue.Count > 0 ? ToastTime * 0.6f : ToastTime; // 여러 개 밀리면 빨리 넘김
            toast.gameObject.SetActive(true);
        }
        if (toastLeft <= 0f) return;
        toastLeft -= Time.unscaledDeltaTime;
        toastGroup.alpha = Mathf.Clamp01(toastLeft / ToastFade);
        if (toastLeft <= 0f) toast.gameObject.SetActive(false);
    }

    void UpdateChips(GameManager gm)
    {
        int stacks = 0;
        foreach (var kv in gm.RewardStacks) stacks += kv.Value;
        if (gm.RewardOrder.Count == shownRewardCount && stacks == shownStacks) return;
        shownRewardCount = gm.RewardOrder.Count; shownStacks = stacks;
        while (chips.Count < gm.RewardOrder.Count) chips.Add(NewChip());
        int n = gm.RewardOrder.Count;
        float total = n * ChipW + Mathf.Max(0, n - 1) * ChipGap, x0 = -total * 0.5f + ChipW * 0.5f;
        for (int i = 0; i < chips.Count; i++)
        {
            bool on = i < n;
            chips[i].bg.gameObject.SetActive(on);
            if (!on) continue;
            var r = gm.RewardOrder[i];
            var c = r.color; c.a = 0.85f;
            chips[i].bg.color = new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.85f);
            ((RectTransform)chips[i].bg.transform).anchoredPosition = new Vector2(x0 + i * (ChipW + ChipGap), 0f);
            int cnt = gm.RewardStacks.TryGetValue(r, out int k) ? k : 0;
            chips[i].label.text = $"<color=#{ColorUtility.ToHtmlStringRGB(r.color)}>●</color> {r.name} ×{cnt}";
        }
    }

    (Image, TMP_Text) NewChip()
    {
        var rt = (RectTransform)new GameObject("RewardIcon", typeof(RectTransform)).transform;
        rt.SetParent(chipRow, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(ChipW, ChipH);
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        var t = Text("Label", rt, 16, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ChipW - 6f, ChipH));
        ((RectTransform)t.transform).pivot = new Vector2(0.5f, 0.5f);
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
        return (img, t);
    }

    void UpdateTrait()
    {
        var stage = StageDirector.I;
        var trait = stage ? stage.Trait : null;
        int time = stage && stage.TimeLeft >= 0f ? Mathf.CeilToInt(stage.TimeLeft) : -1;
        bool enraged = stage && stage.Enraged;
        if (trait == shownTrait && time == shownTime && enraged == shownEnraged) return;
        shownTrait = trait; shownTime = time; shownEnraged = enraged;
        traitLabel.gameObject.SetActive(trait != null);
        if (trait == null) return;
        string s = $"스테이지 특징: <color=#{ColorUtility.ToHtmlStringRGB(trait.color)}>{trait.name}</color>";
        if (enraged) s += "   <color=#FF5555>시간 초과 — 적 광폭화!</color>";
        else if (time >= 0) s += $"   남은 시간 {time / 60}:{time % 60:00}";
        traitLabel.text = s;
    }
}
