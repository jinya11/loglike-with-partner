using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 교환소 창 (프리팹 HUD_Exchange, MetaConfig.exchangePrefab). 로비 교환소(ExchangeStation)에서 F로 엶, Esc/X로 닫음.
/// MetaConfig.exchangeRates 한 줄마다 카드(RowTemplate 복제): '재화 N개 → 성장의 씨앗 1개' + 교환 버튼 (부족하면 비활성).
/// 위쪽 보유 재화 바 = 교환 대상 재화들 + 성장의 씨앗. 문구 서식은 프리팹 텍스트에서 편집.
/// </summary>
public class ExchangeUI : MonoBehaviour
{
    static ExchangeUI inst;
    public static bool IsOpen => inst && inst.panel.activeSelf;

    [SerializeField] GameObject panel;
    [Tooltip("카드 원본 (꺼 둔 상태). 부모(스크롤 Content)의 레이아웃이 세로로 나열")]
    [SerializeField] ExchangeRow rowTemplate;
    [Tooltip("보유 재화 바")] [SerializeField] TMP_Text walletText;
    [Tooltip("보유 재화 한 항목 서식: {0}=이름 {1}=개수")] [SerializeField] string walletItemFmt = "{0} <b>{1}</b>";
    [SerializeField] string walletSeparator = "    ";
    [SerializeField] Button closeButton;

    ExchangeRow[] rows;
    readonly StringBuilder sb = new StringBuilder(128);
    int openedFrame = -1;

    public static void Open()
    {
        if (!inst)
        {
            var prefab = GameManager.I.Config.meta.exchangePrefab;
            if (!prefab) { Debug.LogWarning("MetaConfig.exchangePrefab 이 비어 있음 (Roguelike/Setup Exchange Stand)"); return; }
            Instantiate(prefab);
        }
        inst.Show();
    }

    void Awake()
    {
        inst = this;
        UiText.EnsureEventSystem();
        closeButton.onClick.AddListener(Close);
        BuildRows();
        panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (inst != this) return;
        inst = null;
        if (panel && panel.activeSelf && GameManager.I) GameManager.I.InputLocked = false;
    }

    void OnEnable() => SaveData.ResourceChanged += OnResourceChanged;
    void OnDisable() => SaveData.ResourceChanged -= OnResourceChanged;
    void OnResourceChanged(ResourceType t, int v) { if (IsOpen) Refresh(); }

    void BuildRows()
    {
        var meta = GameManager.I.Config.meta;
        var rates = meta.exchangeRates ?? new ExchangeRate[0];
        string labelFmt = rowTemplate.label.text, haveFmt = rowTemplate.have.text;
        rowTemplate.gameObject.SetActive(false);
        rows = new ExchangeRow[rates.Length];
        for (int i = 0; i < rates.Length; i++)
        {
            var r = Instantiate(rowTemplate, rowTemplate.transform.parent);
            var rate = r.rate = rates[i];
            r.name = "Row_" + rate.from;
            r.haveFmt = haveFmt;
            var def = meta.Resource(rate.from);
            r.label.text = UiText.Format(labelFmt, def != null ? def.name : rate.from.ToString(), rate.amount);
            r.button.onClick.AddListener(() => Trade(rate));
            r.gameObject.SetActive(true);
            rows[i] = r;
        }
    }

    void Show()
    {
        panel.SetActive(true);
        GameManager.I.InputLocked = true;
        openedFrame = Time.frameCount;
        Refresh();
    }

    void Close()
    {
        panel.SetActive(false);
        GameManager.I.InputLocked = false;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    /// <summary>재화 amount개 → 성장의 씨앗 1개.</summary>
    static void Trade(ExchangeRate rate)
    {
        if (rate == null || rate.amount <= 0 || SaveData.Get(rate.from) < rate.amount) return;
        SaveData.Add(rate.from, -rate.amount);
        SaveData.Add(GrowthProgress.Currency, 1);
        GrowthProgress.I.NotifyChanged();
    }

    void Refresh()
    {
        var meta = GameManager.I.Config.meta;
        sb.Clear();
        foreach (var r in rows)
        {
            int have = SaveData.Get(r.rate.from);
            r.have.text = UiText.Format(r.haveFmt, have);
            r.button.interactable = r.rate.amount > 0 && have >= r.rate.amount;
            AppendWallet(meta, r.rate.from, have);
        }
        sb.Append('\n'); // 성장의 씨앗은 둘째 줄
        AppendWallet(meta, GrowthProgress.Currency, SaveData.Get(GrowthProgress.Currency));
        walletText.text = sb.ToString();
    }

    void AppendWallet(MetaConfig meta, ResourceType t, int n)
    {
        if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append(walletSeparator);
        var def = meta.Resource(t);
        sb.Append(UiText.Format(walletItemFmt, def != null ? def.name : t.ToString(), n));
    }

    void Update()
    {
        if (!panel.activeSelf || Time.frameCount == openedFrame) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
    }
}
