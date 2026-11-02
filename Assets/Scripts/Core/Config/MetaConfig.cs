using UnityEngine;

/// <summary>런이 끝나도 남는 자원 / 거점 성장 설정.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Meta")]
public class MetaConfig : ScriptableObject
{
    public ResourceDef[] resources;
    [Tooltip("적 처치 시 해당 맵 자원 드롭 확률")] [Range(0, 1)] public float resourceDropChance = 0.06f;
    public ResourcePickup resourcePickupPrefab;
    public BaseUpgradeDef[] baseUpgrades;

    public ResourceDef Resource(ResourceType t)
    {
        if (resources != null) foreach (var r in resources) if (r.type == t) return r;
        return null;
    }
}
