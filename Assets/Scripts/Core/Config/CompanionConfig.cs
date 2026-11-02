using UnityEngine;

/// <summary>감시자(동반자) 목록과 공통 기본 능력치.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Companion")]
public class CompanionConfig : ScriptableObject
{
    public CompanionDef[] companions;

    [Header("공통 기본 능력치 (런 시작 시 PlayerStats로 복사, 강화로 성장)")]
    public float companionDamage = 15f, companionInterval = 6f, companionRadius = 3f;
    public int companionStrikes = 1;

    public void ApplyTo(PlayerStats s)
    {
        s.companionDamage = companionDamage; s.companionInterval = companionInterval;
        s.companionRadius = companionRadius; s.companionStrikes = companionStrikes;
    }
}
