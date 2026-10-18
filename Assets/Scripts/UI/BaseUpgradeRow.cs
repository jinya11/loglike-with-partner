using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>거점 성장 한 줄. 이름/설명은 프리팹 문구 그대로, 레벨/비용만 서식으로 채움.</summary>
public class BaseUpgradeRow : MonoBehaviour
{
    [Tooltip("GameConfig.baseUpgrades의 id")]
    [SerializeField] string upgradeId;
    [Tooltip("서식: {0}=현재 레벨, {1}=최대 레벨")] [SerializeField] TMP_Text levelText;
    [Tooltip("서식: {0}=자원 이름, {1}=필요 개수, {2}=보유 개수")] [SerializeField] TMP_Text costText;
    [SerializeField] string maxedText = "최대 레벨";
    [SerializeField] Button buyButton;

    string levelFmt, costFmt;
    BaseUpgradeDef def;

    public event System.Action Bought;

    void Awake()
    {
        levelFmt = levelText.text; costFmt = costText.text;
        buyButton.onClick.AddListener(Buy);
    }

    BaseUpgradeDef Def
    {
        get
        {
            if (def != null) return def;
            foreach (var b in GameManager.I.Config.baseUpgrades) if (b.id == upgradeId) return def = b;
            return null;
        }
    }

    public void Refresh()
    {
        var d = Def;
        if (d == null) { gameObject.SetActive(false); return; }
        int lv = SaveData.Level(d.id);
        levelText.SetText(levelFmt, lv, d.maxLevel);
        if (lv >= d.maxLevel)
        {
            costText.text = maxedText;
            buyButton.interactable = false;
            return;
        }
        int need = d.CostAt(lv), have = SaveData.Get(d.cost.type);
        var res = GameManager.I.Config.Resource(d.cost.type);
        costText.text = UiText.Format(costFmt, res != null ? res.name : d.cost.type.ToString(), need, have);
        buyButton.interactable = have >= need;
    }

    void Buy()
    {
        var d = Def;
        if (d == null) return;
        int lv = SaveData.Level(d.id);
        if (lv >= d.maxLevel) return;
        var cost = new[] { new ResourceCost { type = d.cost.type, amount = d.CostAt(lv) } };
        if (!SaveData.Spend(cost)) return;
        SaveData.SetLevel(d.id, lv + 1);
        GameManager.I.ResetRun(); // 로비 스탯에도 즉시 반영
        Bought?.Invoke();
    }
}
