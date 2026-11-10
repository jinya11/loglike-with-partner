using UnityEngine;

/// <summary>감시자(동반자) 목록과 공통 기본 능력치.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Companion")]
public class CompanionConfig : ScriptableObject
{
    public CompanionDef[] companions;

    [Header("공통 기본 능력치 (런 시작 시 PlayerStats로 복사, 강화로 성장)")]
    public float companionDamage = 15f, companionInterval = 6f, companionRadius = 3f;
    public int companionStrikes = 1;

    [Header("폭격 (R)")]
    [Tooltip("켜면 감시자(조준점)가 폭격 마커: R을 누르면 감시자가 서 있는 위치에 바로 폭격. 끄면 기존 방식(탑뷰 조준 후 클릭)")]
    public bool bombardAtWatcher = true;
    [Tooltip("폭격이 준비되면 감시자 발밑에 폭격 범위 표시")] public bool showBombardRange = true;
    [Tooltip("자동(감시자 자동 조작)일 때 이 수 이상의 적이 감시자 폭격 범위 안에 있으면 폭격")] public int autoBombardMinEnemies = 5;

    public void ApplyTo(PlayerStats s)
    {
        s.companionDamage = companionDamage; s.companionInterval = companionInterval;
        s.companionRadius = companionRadius; s.companionStrikes = companionStrikes;
    }
}
