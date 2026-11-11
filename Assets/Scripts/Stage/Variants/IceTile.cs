using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 얼어붙은 호수의 얼음판 한 칸 (씬 배치, 프리팹 IceTile). 씬에서 'cracked'를 켜면 처음부터 금 간 얼음.
/// 상태: 멀쩡함 → 금 감 → (밟으면) 깨지는 중 → 구멍(물) → (시간이 지나면) 다시 얼음. 규칙은 FrozenLakeVariant.
/// </summary>
public class IceTile : MonoBehaviour
{
    public enum State { Intact, Cracked, Breaking, Hole }

    public static readonly List<IceTile> All = new List<IceTile>();

    [Tooltip("처음부터 금 간 얼음")] public bool cracked;
    [Tooltip("얼음 몸체")] public Transform ice;
    [Tooltip("균열 표시 (금 갔을 때 보임)")] public GameObject cracks;
    [Tooltip("구멍이 됐을 때 보이는 물")] public GameObject water;

    public State Current { get; private set; }
    /// <summary>금 간 얼음 위에 머문 누적 시간 (생명체/적 무게).</summary>
    [System.NonSerialized] public float load;
    [System.NonSerialized] public float timer;
    Vector3 iceBase;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Awake()
    {
        if (ice) iceBase = ice.localPosition;
        Set(cracked ? State.Cracked : State.Intact);
    }

    void OnValidate() { if (!Application.isPlaying && cracks) cracks.SetActive(cracked); }

    public void Set(State s)
    {
        Current = s; load = 0f; timer = 0f;
        if (ice) { ice.gameObject.SetActive(s != State.Hole); ice.localPosition = iceBase; }
        if (cracks) cracks.SetActive(s == State.Cracked || s == State.Breaking);
        if (water) water.SetActive(s == State.Hole);
    }

    /// <summary>깨지는 중 흔들림.</summary>
    public void Shake(float k)
    {
        if (!ice) return;
        ice.localPosition = iceBase + new Vector3(Random.Range(-1f, 1f), -0.5f * k, Random.Range(-1f, 1f)) * 0.06f * (0.3f + k);
    }
}
