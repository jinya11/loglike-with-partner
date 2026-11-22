using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 생명체 성장 노드 화면 (프리팹 HUD_GrowthTree, WardConfig.growthTreePrefab). 로비에서 WardConfig.growthKey(기본 K)로 열고 Esc로 닫음.
/// 선택한 생명체의 CreatureGrowthConfig 노드를 NodeTemplate/LinkTemplate 복제로 보드에 배치 (위치 = config 정규화 좌표, 선행 노드끼리 점선).
/// 호버 = 필요 재화 툴팁, 클릭 = 그 노드로 줌인 + 오른쪽 설명 패널 → '강화하기'. 해금한 메인 노드는 장착/해제 (생명체당 하나).
/// 레이아웃/문구/색은 프리팹과 config에서 편집.
/// </summary>
public class GrowthTreeUI : MonoBehaviour
{
    static GrowthTreeUI inst;
    public static bool IsOpen => inst && inst.panel && inst.panel.activeSelf;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text title;
    [Header("생명체 전환 (제목 옆 < >)")]
    [SerializeField] Button prevButton;
    [SerializeField] Button nextButton;
    [Tooltip("전환 시 보드가 밀려 들어오는 거리 (px)")] [SerializeField] float slideDistance = 1400f;
    [Tooltip("보드 페이드 (선택)")] [SerializeField] CanvasGroup boardGroup;
    [Header("보드 (줌 대상)")]
    [SerializeField] RectTransform board;
    [SerializeField] Image silhouette;
    [Tooltip("점선 원본 (꺼 둔 상태, 피벗 왼쪽 가운데)")] [SerializeField] Image linkTemplate;
    [Tooltip("노드 원본 (꺼 둔 상태)")] [SerializeField] GrowthNodeView nodeTemplate;
    [Tooltip("빈 곳 클릭 = 선택 해제")] [SerializeField] Button backgroundButton;
    [SerializeField] Button closeButton;
    [Header("툴팁 (서식: {0}=필요 재화)")]
    [SerializeField] RectTransform tooltip;
    [SerializeField] TMP_Text tooltipText;
    [SerializeField] string tooltipOwned = "해금됨";
    [Tooltip("툴팁에 붙는 클리어 조건 (서식: {0}=이름 {1}=필요 {2}=현재)")] [SerializeField] string tooltipCondition = "\n{0} 클리어 {2}/{1}";
    [Header("설명 패널")]
    [SerializeField] RectTransform detailPanel;
    [SerializeField] TMP_Text detailKind, detailName, detailDesc;
    [Tooltip("서식: {0}=필요 재화 {1}=보유")] [SerializeField] TMP_Text detailCost;
    [SerializeField] Button upgradeButton;
    [SerializeField] TMP_Text upgradeLabel;
    [Tooltip("클리어 조건 (서식: {0}=이름 {1}=필요 횟수 {2}=현재 횟수). 조건 없는 노드는 숨김")] [SerializeField] TMP_Text detailCondition;
    [SerializeField] Color conditionMet = new Color(0.45f, 0.75f, 0.5f), conditionUnmet = new Color(0.9f, 0.45f, 0.5f);
    [SerializeField] string labelUpgrade = "강화하기", labelOwned = "해금 완료", labelLocked = "선행 노드 필요", labelEquip = "장착하기", labelUnequip = "장착 해제", labelCondition = "클리어 조건 필요";
    [SerializeField] string[] kindNames = { "메인", "스탯", "패시브", "감시자" };
    [Header("성장 재화 (오른쪽 위)")]
    [SerializeField] Button currencyButton;
    [SerializeField] GameObject currencyDropdown;
    [Tooltip("서식: {0}=보유량")] [SerializeField] TMP_Text currencyText;
    [Header("줌")]
    [SerializeField] float zoomScale = 1.7f, zoomSpeed = 9f;
    [Tooltip("줌인 시 노드가 올 화면 위치 (가운데 기준, 설명 패널 왼쪽)")] [SerializeField] Vector2 focusPoint = new Vector2(-330f, 0f);
    [SerializeField] Vector2 restPosition = new Vector2(0f, -20f);
    [Header("점선")]
    [SerializeField] float linkWidth = 6f;
    [SerializeField] Color linkLocked = new Color(0.75f, 0.68f, 0.78f, 0.55f), linkOwned = new Color(1f, 0.62f, 0.6f, 0.95f);

    readonly List<GrowthNodeView> views = new List<GrowthNodeView>(32);
    readonly List<Image> links = new List<Image>(40);
    readonly List<(int a, int b)> linkPairs = new List<(int, int)>(40);
    readonly Dictionary<string, int> indexById = new Dictionary<string, int>(32);
    CreatureGrowthConfig cfg;
    GrowthNodeView focus;
    string tooltipFmt, costFmt, currencyFmt, conditionFmt;
    readonly List<CreatureGrowthConfig> trees = new List<CreatureGrowthConfig>(4);
    int treeIndex;
    float slide;
    float scale = 1f, targetScale = 1f;
    Vector2 pos, targetPos;
    int openedFrame = -1;

    public static void Ensure()
    {
        if (inst || !GameManager.I) return;
        var prefab = GameManager.I.Config.ward.growthTreePrefab;
        if (!prefab) { Debug.LogWarning("WardConfig.growthTreePrefab 이 비어 있음 (Roguelike/Setup Rabbit Growth)"); return; }
        Instantiate(prefab);
    }

    void Awake()
    {
        inst = this;
        UiText.EnsureEventSystem();
        tooltipFmt = tooltipText.text; costFmt = detailCost.text; currencyFmt = currencyText.text;
        if (detailCondition) conditionFmt = detailCondition.text;
        if (prevButton) prevButton.onClick.AddListener(() => Switch(-1));
        if (nextButton) nextButton.onClick.AddListener(() => Switch(1));
        linkTemplate.gameObject.SetActive(false);
        nodeTemplate.gameObject.SetActive(false);
        upgradeButton.onClick.AddListener(OnUpgrade);
        backgroundButton.onClick.AddListener(() => Focus(null));
        currencyButton.onClick.AddListener(() => currencyDropdown.SetActive(!currencyDropdown.activeSelf));
        if (closeButton) closeButton.onClick.AddListener(() => SetOpen(false));
        panel.SetActive(false);
    }

    void OnEnable() => GrowthProgress.I.Changed += OnChanged;
    void OnDisable() => GrowthProgress.I.Changed -= OnChanged;

    void OnDestroy()
    {
        if (inst != this) return;
        inst = null;
        if (panel && panel.activeSelf && GameManager.I) GameManager.I.InputLocked = false;
    }

    void OnChanged() { if (IsOpen) RefreshAll(); }

    void SetOpen(bool on)
    {
        var gm = GameManager.I;
        if (on)
        {
            // 성장 설정이 있는 생명체 목록 (WardConfig.creatures 순서). 처음엔 지금 데려가는 생명체
            trees.Clear();
            var defs = gm.Config.ward.creatures;
            if (defs != null) foreach (var d in defs) if (d != null && d.growth) trees.Add(d.growth);
            if (trees.Count == 0) return;
            treeIndex = Mathf.Max(0, trees.IndexOf(gm.Creature?.growth));
            if (trees[treeIndex] != cfg) Build(trees[treeIndex]);
            UpdateArrows();
            slide = 0f;
            openedFrame = Time.frameCount;
        }
        panel.SetActive(on);
        gm.InputLocked = on;
        tooltip.gameObject.SetActive(false);
        currencyDropdown.SetActive(false);
        Focus(null);
        scale = targetScale; pos = targetPos; ApplyZoom();
        if (on) RefreshAll();
        else EventSystem.current?.SetSelectedGameObject(null);
    }

    // ---------------- 생성 ----------------

    void Build(CreatureGrowthConfig c)
    {
        cfg = c;
        title.text = c.title;
        board.sizeDelta = c.boardSize;
        if (silhouette) { silhouette.sprite = c.silhouette; silhouette.enabled = c.silhouette; }
        int n = c.nodes != null ? c.nodes.Length : 0;
        indexById.Clear();
        for (int i = 0; i < n; i++) if (c.nodes[i] != null && !string.IsNullOrEmpty(c.nodes[i].id)) indexById[c.nodes[i].id] = i;

        // 노드: 템플릿 복제 (재사용)
        while (views.Count < n)
        {
            var v = Instantiate(nodeTemplate, nodeTemplate.transform.parent);
            v.owner = this;
            var vv = v; v.button.onClick.AddListener(() => Focus(vv));
            views.Add(v);
        }
        for (int i = 0; i < views.Count; i++)
        {
            var v = views[i];
            bool on = i < n && c.nodes[i] != null;
            v.gameObject.SetActive(on);
            if (!on) continue;
            var d = v.def = c.nodes[i];
            v.name = "Node_" + d.id;
            v.Rt.anchoredPosition = (d.position - new Vector2(0.5f, 0.5f)) * c.boardSize;
            float s = v.SizeFor(d.kind);
            v.Rt.sizeDelta = new Vector2(s, s);
        }

        // 점선: 선행 노드마다 하나
        linkPairs.Clear();
        for (int i = 0; i < n; i++)
        {
            var d = c.nodes[i];
            if (d?.requires == null) continue;
            foreach (var r in d.requires)
                if (r != null && indexById.TryGetValue(r, out int j) && !linkPairs.Contains((i, j))) linkPairs.Add((j, i)); // 서로를 선행으로 둔 고리(원형 배치)는 한 번만
        }
        while (links.Count < linkPairs.Count) links.Add(Instantiate(linkTemplate, linkTemplate.transform.parent));
        for (int i = 0; i < links.Count; i++)
        {
            var l = links[i];
            bool on = i < linkPairs.Count;
            l.gameObject.SetActive(on);
            if (!on) continue;
            Vector2 a = views[linkPairs[i].a].Rt.anchoredPosition, b = views[linkPairs[i].b].Rt.anchoredPosition, d = b - a;
            var rt = l.rectTransform;
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(d.magnitude, linkWidth);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }

    // ---------------- 갱신 ----------------

    void RefreshAll()
    {
        var gp = GrowthProgress.I;
        string main = gp.SelectedMain(cfg);
        foreach (var v in views)
        {
            if (!v.gameObject.activeSelf) continue;
            var d = v.def;
            bool owned = gp.Owned(cfg, d), reach = gp.Reachable(cfg, d);
            Color k = cfg.KindColor(d.kind);
            v.dot.color = owned ? k : reach ? Color.Lerp(cfg.availableColor, k, 0.35f) : Color.Lerp(cfg.lockedColor, k, 0.3f); // 잠긴 노드도 종류 색이 살짝 보이게
            bool selected = d.kind == GrowthNodeKind.Main && owned && d.id == main;
            if (v.ring) v.ring.SetActive(selected);
            if (v.glow) { v.glow.gameObject.SetActive(selected || owned); var g = k; g.a = selected ? 0.6f : 0.18f; v.glow.color = g; }
        }
        for (int i = 0; i < linkPairs.Count; i++)
            links[i].color = gp.Owned(cfg, views[linkPairs[i].a].def) && gp.Owned(cfg, views[linkPairs[i].b].def) ? linkOwned : linkLocked;
        currencyText.text = UiText.Format(currencyFmt, gp.Seeds);
        RefreshDetail();
    }

    void RefreshDetail()
    {
        detailPanel.gameObject.SetActive(focus);
        if (!focus) return;
        var gp = GrowthProgress.I;
        var d = focus.def;
        bool owned = gp.Owned(cfg, d), reach = gp.Reachable(cfg, d);
        detailKind.text = (int)d.kind < kindNames.Length ? kindNames[(int)d.kind] : d.kind.ToString();
        detailKind.color = cfg.KindColor(d.kind);
        detailName.text = d.name;
        detailDesc.text = d.desc;
        detailCost.text = UiText.Format(costFmt, d.cost, gp.Seeds);
        detailCost.gameObject.SetActive(!owned);
        bool cond = gp.ConditionMet(cfg, d);
        if (detailCondition)
        {
            detailCondition.gameObject.SetActive(d.requiredClears > 0);
            if (d.requiredClears > 0)
            {
                detailCondition.text = UiText.Format(conditionFmt, cfg.displayName, d.requiredClears, Mathf.Min(gp.Clears(cfg), d.requiredClears));
                detailCondition.color = cond ? conditionMet : conditionUnmet;
            }
        }
        if (owned && d.kind == GrowthNodeKind.Main)
        {
            upgradeLabel.text = gp.SelectedMain(cfg) == d.id ? labelUnequip : labelEquip;
            upgradeButton.interactable = true;
        }
        else
        {
            upgradeLabel.text = owned ? labelOwned : !reach ? labelLocked : !cond ? labelCondition : labelUpgrade;
            upgradeButton.interactable = !owned && gp.CanUnlock(cfg, d);
        }
    }

    void OnUpgrade()
    {
        if (!focus) return;
        var gp = GrowthProgress.I;
        var d = focus.def;
        if (gp.Owned(cfg, d))
        {
            if (d.kind == GrowthNodeKind.Main) gp.SetMain(cfg, gp.SelectedMain(cfg) == d.id ? null : d.id); // 장착/해제 (서로 배타)
            return;
        }
        if (gp.Unlock(cfg, d)) GameManager.I.ResetRun(); // 로비 스탯에도 즉시 반영 (거점 성장과 같은 방식)
    }

    // ---------------- 줌 / 툴팁 ----------------

    void Focus(GrowthNodeView v)
    {
        focus = v;
        if (v) { targetScale = zoomScale; targetPos = focusPoint - v.Rt.anchoredPosition * zoomScale; }
        else { targetScale = 1f; targetPos = restPosition; }
        if (IsOpen) RefreshDetail();
    }

    public void ShowTooltip(GrowthNodeView v)
    {
        var gp = GrowthProgress.I;
        var d = v.def;
        string s = gp.Owned(cfg, d) ? tooltipOwned : UiText.Format(tooltipFmt, d.cost);
        if (d.requiredClears > 0 && !gp.Owned(cfg, d)) s += UiText.Format(tooltipCondition, cfg.displayName, d.requiredClears, Mathf.Min(gp.Clears(cfg), d.requiredClears));
        tooltipText.text = s;
        Vector2 pref = tooltipText.GetPreferredValues(s); // 조건 줄이 붙으면 툴팁을 글자에 맞춰 키움
        tooltip.sizeDelta = new Vector2(Mathf.Max(170f, pref.x + 64f), Mathf.Max(50f, pref.y + 18f));
        tooltip.gameObject.SetActive(true);
        tooltip.position = v.Rt.position;
        tooltip.anchoredPosition += new Vector2(0f, v.Rt.sizeDelta.y * 0.5f * scale + 12f);
    }

    public void HideTooltip(GrowthNodeView v) => tooltip.gameObject.SetActive(false);

    void ApplyZoom()
    {
        board.localScale = new Vector3(scale, scale, 1f);
        board.anchoredPosition = pos + new Vector2(slide, 0f);
        if (boardGroup) boardGroup.alpha = 1f - Mathf.Clamp01(Mathf.Abs(slide) / slideDistance);
    }

    // ---------------- 생명체 전환 ----------------

    /// <summary>제목 옆 &lt; &gt;: 이전/다음 생명체 트리로 슬라이드.</summary>
    void Switch(int dir)
    {
        int i = Mathf.Clamp(treeIndex + dir, 0, trees.Count - 1);
        if (i == treeIndex) return;
        treeIndex = i;
        tooltip.gameObject.SetActive(false);
        Build(trees[i]);
        Focus(null);
        scale = targetScale; pos = targetPos;
        slide = dir * slideDistance; // 다음 = 오른쪽에서 밀려 들어옴
        ApplyZoom();
        UpdateArrows();
        RefreshAll();
    }

    void UpdateArrows()
    {
        if (prevButton) prevButton.gameObject.SetActive(treeIndex > 0);
        if (nextButton) nextButton.gameObject.SetActive(treeIndex < trees.Count - 1);
    }

    void Update()
    {
        var gm = GameManager.I;
        var kb = Keyboard.current;
        if (gm == null || kb == null) return;
        bool toggle = kb[gm.Config.ward.growthKey].wasPressedThisFrame;
        if (!IsOpen)
        {
            if (toggle && !gm.InputBlocked) SetOpen(true);
            return;
        }
        if (Time.frameCount != openedFrame)
        {
            if (kb.escapeKey.wasPressedThisFrame) { if (focus) Focus(null); else { SetOpen(false); return; } }
            else if (toggle) { SetOpen(false); return; }
        }
        if (Mathf.Abs(scale - targetScale) > 0.001f || (pos - targetPos).sqrMagnitude > 0.05f || slide != 0f)
        {
            float k = 1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime);
            scale = Mathf.Lerp(scale, targetScale, k);
            pos = Vector2.Lerp(pos, targetPos, k);
            slide = Mathf.Abs(slide) < 1f ? 0f : Mathf.Lerp(slide, 0f, k); // 생명체 전환 슬라이드
            ApplyZoom();
            if (tooltip.gameObject.activeSelf) tooltip.gameObject.SetActive(false);
        }
    }
}
