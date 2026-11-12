using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 칼데라의 플랫폼 한 칸 (씬 배치, 프리팹 CalderaPlatform). 윗면 = 높이 0 (지형 없음, Ground 평지).
/// 'vanishes'면 용암 수위가 바뀔 때 가라앉았다가 다시 떠오르는 플랫폼 (식은 껍질 색 + 빛나는 틈으로 미리 구분).
/// 기본은 규칙(CalderaVariant)이 런타임에 무작위로 다시 고름 (CalderaSettings.randomVanish). 언제 가라앉는지도 규칙이 정함.
/// </summary>
public class CalderaPlatform : MonoBehaviour
{
    public static readonly List<CalderaPlatform> All = new List<CalderaPlatform>();

    [Tooltip("용암 수위가 바뀔 때 가라앉는 플랫폼 (무작위 고르기를 끄면 이 지정 그대로)")] public bool vanishes;
    [Tooltip("기둥 몸체 (가라앉을 때 이것만 내려감)")] public Transform body;
    [Tooltip("빛나는 틈 (가라앉는 플랫폼만)")] public GameObject veins;
    [Tooltip("기둥 렌더러 (재질 교체용)")] public Renderer column;
    [Tooltip("사라지지 않는 플랫폼 / 가라앉는 플랫폼 재질")] public Material rockMaterial, crustMaterial;

    /// <summary>위에 설 수 있는지 (가라앉는 중/잠긴 동안 false).</summary>
    public bool Solid { get; private set; } = true;
    /// <summary>곧 가라앉음 (경고 중).</summary>
    public bool Warning { get; private set; }
    Vector3 bodyBase;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
    void Awake() { if (body) bodyBase = body.localPosition; }

    void OnValidate() { if (!Application.isPlaying && veins) veins.SetActive(vanishes); }

    /// <summary>가라앉는 플랫폼인지 바꾸고 겉모습(색, 틈)도 맞춤.</summary>
    public void SetVanishes(bool on) { vanishes = on; Look(on); }

    void Look(bool on)
    {
        if (veins) veins.SetActive(on);
        var m = on ? crustMaterial : rockMaterial;
        if (column && m) column.sharedMaterial = m;
    }

    /// <summary>offset = 몸체 높이 변화(아래로 음수), shake = 경고 흔들림 0~1.</summary>
    public void Pose(float offset, float shake, bool solid, bool warning)
    {
        Solid = solid; Warning = warning;
        if (!body) return;
        Vector3 j = shake > 0f ? new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.07f * shake : Vector3.zero;
        body.localPosition = bodyBase + new Vector3(j.x, offset - 0.05f * shake, j.z);
    }
}
