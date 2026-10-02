using System.Collections.Generic;
using UnityEngine;

public enum ZoneType { Slow, Water, Damage, Heal }

/// <summary>
/// 바닥 구역 (눈밭/물가/용암/회복). 물리 트리거 대신 플레이어 위치를 폴링 (구역 수가 적어 저렴).
/// 형태는 이 Transform의 로컬 단위 원(지름 1) 또는 박스(1x1) — 스케일로 크기 조절.
/// </summary>
public class StageZone : MonoBehaviour
{
    public static readonly List<StageZone> All = new List<StageZone>(32);

    public ZoneType type;
    public bool box;
    [Tooltip("Slow/Water: 이동속도 배율, Damage: 초당 피해, Heal: 초당 회복")]
    public float value = 0.5f;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool Contains(Vector3 worldPos)
    {
        Vector3 l = transform.InverseTransformPoint(worldPos);
        return box ? Mathf.Abs(l.x) < 0.5f && Mathf.Abs(l.z) < 0.5f : l.x * l.x + l.z * l.z < 0.25f;
    }
}
