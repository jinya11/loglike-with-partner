using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모래폭풍 사막의 동굴 바위 (씬 배치, 프리팹 CaveRock). 바람이 불어오는 쪽을 막아 주는 피난처.
/// 바위 안쪽(동굴)과 바람이 불어가는 쪽 뒤편 shelterLength(m)까지는 바람/모래 피해가 없음.
/// 입구(로컬 +Z)를 바람이 불어가는 방향으로 두면 됨 — 씬 뷰에서 선택하면 피난 구역이 표시됨.
/// </summary>
public class ShelterRock : MonoBehaviour
{
    public static readonly List<ShelterRock> All = new List<ShelterRock>();

    [Tooltip("바위가 바람을 막는 폭의 절반(m)")] public float halfWidth = 3f;
    [Tooltip("바람 쪽 벽에서 동굴 안쪽까지 깊이(m)")] public float depth = 2.5f;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>p가 이 바위 덕분에 windDir(불어가는 방향) 바람을 피하는지.</summary>
    public bool Covers(Vector3 p, Vector3 windDir, float shelterLength)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        float along = Vector3.Dot(d, windDir);
        if (along < -depth || along > shelterLength) return false;
        Vector3 side = d - windDir * along;
        // 뒤로 갈수록 바람이 다시 감싸 들어와 폭이 조금 좁아짐
        float w = halfWidth * Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(along / Mathf.Max(0.01f, shelterLength)));
        return side.sqrMagnitude < w * w;
    }

    public static bool AnyCovers(Vector3 p, Vector3 windDir, float shelterLength)
    {
        for (int i = 0; i < All.Count; i++) if (All[i].Covers(p, windDir, shelterLength)) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized), Vector3.one);
        const float len = 7f;
        Gizmos.DrawWireCube(new Vector3(0f, 0.5f, (len - depth) * 0.5f), new Vector3(halfWidth * 2f, 1f, len + depth));
    }
}
