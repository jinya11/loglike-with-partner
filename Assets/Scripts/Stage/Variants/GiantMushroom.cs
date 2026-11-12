using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 거대 버섯 숲의 버섯 하나 (씬 배치, 프리팹 GiantMushroom): 줄기 + 위층 발판이 되는 갓.
/// 갓 윗면 높이는 규칙(GiantMushroomVariant.capY)이 모든 버섯에 같게 맞춤 (갓끼리 이어져야 위층을 걸어 다님).
/// 생명체가 아래층이면 갓이 반투명해짐 (아래층이 가려지지 않게).
/// </summary>
public class GiantMushroom : MonoBehaviour
{
    public static readonly List<GiantMushroom> All = new List<GiantMushroom>();

    [Tooltip("갓 반경(m) — 위층 발판 크기")] public float capRadius = 11f;
    [Tooltip("줄기 반경(m) — 아래층에서 생명체가 피해 감")] public float stemRadius = 1.4f;
    [Tooltip("갓 (높이를 맞춤)")] public Transform cap;
    [Tooltip("줄기 (높이에 맞춰 늘어남)")] public Transform stem;
    [Tooltip("투명해지는 갓 렌더러들")] public Renderer[] capRenderers;
    [Tooltip("불투명 / 반투명 갓 재질")] public Material capOpaque, capFaded;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>갓 윗면을 y 높이로, 줄기는 땅에서 갓까지.</summary>
    public void FitHeight(float y)
    {
        float ground = Application.isPlaying ? Ground.HeightAt(transform.position) : transform.position.y; // 에디터(씬 빌더)에선 놓인 높이 그대로
        var p = transform.position; p.y = ground; transform.position = p;
        float h = Mathf.Max(1f, y - ground);
        if (cap) { var c = cap.localPosition; c.y = h; cap.localPosition = c; }
        if (stem) { stem.localScale = new Vector3(stem.localScale.x, h * 0.5f, stem.localScale.z); var s = stem.localPosition; s.y = h * 0.5f; stem.localPosition = s; }
    }

    public bool OnCap(Vector3 p, float margin = 0.3f)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        float r = capRadius - margin;
        return d.sqrMagnitude <= r * r;
    }

    public bool InStem(Vector3 p, float margin = 0.4f)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        float r = stemRadius + margin;
        return d.sqrMagnitude <= r * r;
    }

    bool faded;
    public void SetFaded(bool on)
    {
        if (faded == on || capRenderers == null) return;
        faded = on;
        var m = on ? capFaded : capOpaque;
        if (!m) return;
        foreach (var r in capRenderers) if (r) r.sharedMaterial = m;
    }
}
