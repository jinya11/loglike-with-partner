using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 논 한 구획 (자연 농지 씬에 배치). 이 오브젝트의 위치/Y 회전/size 영역에 벼를 곧은 줄로 심어 GPU 인스턴싱으로 그림.
/// [ExecuteAlways]: 에디터에서도 그려짐 → 씬에서 구획을 옮기거나 크기/줄 방향(Y 회전)/색을 바꾸면 벼가 바로 따라옴 (QA/레벨 디자인용).
/// 높이는 씬의 Terrain을 따름. 폭격 등으로 원 범위를 베면(CutCircle) 그 자리 벼가 사라짐 (플레이 중에만, 씬에는 저장 안 됨).
/// </summary>
[ExecuteAlways]
public class RicePaddy : MonoBehaviour
{
    public static readonly List<RicePaddy> All = new List<RicePaddy>(64);
    const int Chunk = 1023;

    [Header("구획")]
    [Tooltip("구획 크기 (가로 X = 줄 방향, 세로 Z). 줄 방향은 이 오브젝트의 Y 회전")] public Vector2 size = new Vector2(12.4f, 12.4f);
    [Tooltip("줄 간격 (m) — 포기 폭보다 넓게 해서 물 댄 논바닥이 보이게")] public float rowSpacing = 0.8f;
    [Tooltip("줄 안 포기 간격 (m)")] public float plantSpacing = 0.45f;
    [Tooltip("벼 높이 (m). 적 키가 0.5~0.9m라 0.55~0.72면 아래쪽만 묻힘")] public float height = 0.62f;
    [Tooltip("위치 흔들림 (m) / 회전 흔들림 (도) / 크기 흔들림 (비율)")] public float jitter = 0.03f, yawJitter = 10f, sizeJitter = 0.05f;
    [Tooltip("익은 황금빛 구획 (끄면 초록)")] public bool golden;
    [Tooltip("배치 무작위 시드 (같으면 매번 같은 모양)")] public int seed = 1;

    [Header("모델")]
    [Tooltip("벼 한 포기 메시 (Meshy)")] public Mesh riceMesh;
    public Material goldMaterial, greenMaterial;
    [Tooltip("벼 한 포기의 폭 (m) — 모델을 이 크기로 맞춤")] public float plantWidth = 0.65f;
    [Tooltip("모델 위쪽이 Z축이면 켬 (Meshy FBX에 따라)")] public bool meshUpIsZ;

    readonly List<Matrix4x4[]> chunks = new List<Matrix4x4[]>(4);
    readonly List<int> counts = new List<int>(4);
    readonly List<Vector3> plantPos = new List<Vector3>(512);
    readonly List<bool> alive = new List<bool>(512);
    Bounds bounds;
    bool dirty = true;
    int cutCount;
    Vector3 lastPos; Quaternion lastRot; Vector3 lastScale;

    public int PlantCount { get { int n = 0; foreach (var c in counts) n += c; return n; } }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        dirty = true;
        RenderPipelineManager.beginCameraRendering += Draw;
    }

    void OnDisable()
    {
        All.Remove(this);
        RenderPipelineManager.beginCameraRendering -= Draw;
    }

    void OnValidate() { dirty = true; }

    /// <summary>모든 구획에서 원(XZ) 안의 벼를 벰. 벤 포기 수 반환.</summary>
    public static int CutAll(Vector3 center, float radius)
    {
        int n = 0;
        foreach (var p in All) n += p.Cut(center, radius);
        return n;
    }

    int Cut(Vector3 center, float r)
    {
        if (dirty) Rebuild();
        float r2 = r * r; int n = 0;
        for (int i = 0; i < plantPos.Count; i++)
        {
            if (!alive[i]) continue;
            float dx = plantPos[i].x - center.x, dz = plantPos[i].z - center.z;
            if (dx * dx + dz * dz > r2) continue;
            alive[i] = false; n++;
        }
        if (n > 0) { cutCount += n; BuildMatrices(); }
        return n;
    }

    float GroundAt(float x, float z)
    {
        if (Application.isPlaying && Ground.HasTerrain) return Ground.HeightAt(x, z);
        var t = Terrain.activeTerrain;
        return t ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : 0f;
    }

    /// <summary>줄 배치 다시 계산 (구획을 옮기거나 값을 바꾸면 자동).</summary>
    public void Rebuild()
    {
        dirty = false;
        lastPos = transform.position; lastRot = transform.rotation; lastScale = transform.lossyScale;
        plantPos.Clear(); alive.Clear();
        if (!riceMesh) { chunks.Clear(); counts.Clear(); return; }
        var rng = new System.Random(seed);
        float sx = Mathf.Max(0.2f, plantSpacing), sz = Mathf.Max(0.3f, rowSpacing);
        int nx = Mathf.Max(1, Mathf.FloorToInt(size.x / sx)), nz = Mathf.Max(1, Mathf.FloorToInt(size.y / sz));
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                float lx = (x + 0.5f) * sx - nx * sx * 0.5f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
                float lz = (z + 0.5f) * sz - nz * sz * 0.5f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
                var w = transform.position + transform.rotation * new Vector3(lx, 0f, lz);
                w.y = GroundAt(w.x, w.z) - 0.03f;
                plantPos.Add(w); alive.Add(true);
            }
        BuildMatrices();
    }

    void BuildMatrices()
    {
        chunks.Clear(); counts.Clear();
        if (!riceMesh) return;
        var b = riceMesh.bounds;
        const float eps = 1e-6f;
        Quaternion fix; Vector3 scale, pivot;
        if (meshUpIsZ) { fix = Quaternion.Euler(-90f, 0f, 0f); scale = new Vector3(plantWidth / Mathf.Max(eps, b.size.x), height / Mathf.Max(eps, b.size.z), plantWidth / Mathf.Max(eps, b.size.y)); pivot = new Vector3(-b.center.x, -b.center.y, -b.min.z); }
        else { fix = Quaternion.identity; float wd = Mathf.Max(eps, Mathf.Max(b.size.x, b.size.z)); scale = new Vector3(plantWidth / wd, height / Mathf.Max(eps, b.size.y), plantWidth / wd); pivot = new Vector3(-b.center.x, -b.min.y, -b.center.z); }
        var rng = new System.Random(seed * 7 + 3);
        float baseYaw = transform.eulerAngles.y;
        bounds = new Bounds(transform.position, Vector3.one);
        Matrix4x4[] cur = null; int c = 0;
        for (int i = 0; i < plantPos.Count; i++)
        {
            float jy = ((float)rng.NextDouble() * 2f - 1f) * yawJitter, js = 1f + ((float)rng.NextDouble() * 2f - 1f) * sizeJitter;
            if (!alive[i]) continue;
            if (cur == null || c == Chunk) { if (cur != null) { chunks.Add(cur); counts.Add(c); } cur = new Matrix4x4[Chunk]; c = 0; }
            var sc = new Vector3(scale.x * js, scale.y * js, scale.z * js);
            cur[c++] = Matrix4x4.TRS(plantPos[i], Quaternion.Euler(0f, baseYaw + jy, 0f), sc) * Matrix4x4.Rotate(fix) * Matrix4x4.Translate(pivot);
            bounds.Encapsulate(plantPos[i] + Vector3.up * height);
            bounds.Encapsulate(plantPos[i]);
        }
        if (cur != null && c > 0) { chunks.Add(cur); counts.Add(c); }
        bounds.Expand(plantWidth);
    }

    void Update()
    {
        // 에디터에서 구획을 옮기거나 돌리면 다시 심기
        if (transform.position != lastPos || transform.rotation != lastRot || transform.lossyScale != lastScale) dirty = true;
        if (dirty && !(Application.isPlaying && cutCount > 0)) Rebuild();
    }

    void Draw(ScriptableRenderContext ctx, Camera cam)
    {
        if (!isActiveAndEnabled) return;
        if (dirty) Rebuild();
        var mat = golden ? goldMaterial : greenMaterial;
        if (!mat || !riceMesh || chunks.Count == 0) return;
        var rp = new RenderParams(mat) { camera = cam, worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true };
        for (int i = 0; i < chunks.Count; i++) Graphics.RenderMeshInstanced(rp, riceMesh, 0, chunks[i], counts[i]);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = golden ? new Color(1f, 0.85f, 0.3f, 0.8f) : new Color(0.5f, 1f, 0.4f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * 0.5f, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 1f, size.y));
    }
}
