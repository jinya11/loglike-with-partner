using UnityEngine;

/// <summary>런이 끝나도 남는 자원 / 거점 성장 설정.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Meta")]
public class MetaConfig : ScriptableObject
{
    public ResourceDef[] resources;
    [Tooltip("적 처치 시 해당 맵 자원 드롭 확률")] [Range(0, 1)] public float resourceDropChance = 0.06f;
    public ResourcePickup resourcePickupPrefab;
    public BaseUpgradeDef[] baseUpgrades;

    [Header("교환소 (거점 재화 → 성장의 씨앗)")]
    [Tooltip("재화 종류별 교환 비율: amount개 → 씨앗 1개")] public ExchangeRate[] exchangeRates;
    [Tooltip("교환 창 프리팹 (로비 교환소에서 F)")] public ExchangeUI exchangePrefab;

    public ResourceDef Resource(ResourceType t)
    {
        if (resources != null) foreach (var r in resources) if (r.type == t) return r;
        return null;
    }
}

/// <summary>교환소 한 줄: 재화 amount개 → 성장의 씨앗 1개.</summary>
[System.Serializable]
public class ExchangeRate
{
    public ResourceType from;
    [Min(1)] public int amount = 3;
}
