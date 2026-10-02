using UnityEngine;

/// <summary>스테이지 내부 랜덤 배치 규칙 (수량/위치 조율).</summary>
[CreateAssetMenu(menuName = "Roguelike/Stage Layout")]
public class StageLayout : ScriptableObject
{
    [Tooltip("중심부 비우는 반경 (플레이어 시작/포털)")] public float centerSafeRadius = 5f;
    [Tooltip("0이면 매번 랜덤")] public int seed;
    public int maxAttempts = 30;
    public PresetEntry[] entries;
}
