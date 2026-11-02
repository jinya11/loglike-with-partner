using System.Collections.Generic;
using UnityEngine;

// 섬 밑 구름 바다의 빈틈에서 주기적으로 연한 빛기둥이 솟았다가 사라지게 한다.
// 기둥은 카메라를 향해 Y축으로만 회전하는 세로 쿼드 + 구름 위 둥근 번짐 쿼드로 이루어진다.
public class LightPillars : MonoBehaviour
{
    [Header("참조")]
    public Material material;
    [Tooltip("구름 덩어리들의 부모(CloudSea). 덩어리와 겹치지 않는 틈을 골라 기둥을 세운다.")]
    public Transform cloudRoot;

    [Header("등장 범위")]
    public Vector3 areaCenter = new Vector3(18f, 0f, 120f);
    public Vector2 areaSize = new Vector2(420f, 320f);
    [Tooltip("이 지점 반경 안(섬 바로 밑)에는 세우지 않는다")]
    public Vector3 avoidCenter = new Vector3(18f, 0f, 6f);
    public float avoidRadius = 70f;
    public float baseY = -66f;

    [Header("타이밍")]
    public int maxActive = 2;
    public Vector2 interval = new Vector2(2.5f, 6f);
    public float fadeIn = 1.8f, hold = 3f, fadeOut = 2.6f;

    [Header("모양")]
    public Vector2 height = new Vector2(260f, 420f);
    public Vector2 width = new Vector2(12f, 18f);
    public float groundGlowSize = 34f;
    [Range(0f, 2f)] public float maxIntensity = 1.1f;
    public Color[] glowColors =
    {
        new Color(1.3f, 0.7f, 0.95f),  // 분홍
        new Color(1.25f, 0.85f, 0.65f), // 살구
        new Color(0.85f, 0.8f, 1.35f),  // 라벤더
    };

    class Pillar
    {
        public Transform root, beam;
        public Renderer beamR, glowR;
        public float start, life;
        public bool active;
    }

    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    readonly List<Pillar> pool = new List<Pillar>();
    readonly List<(Vector3 c, float r)> puffs = new List<(Vector3, float)>();
    MaterialPropertyBlock mpb;
    float nextSpawn;
    Camera cam;

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        if (cloudRoot != null)
            foreach (Transform p in cloudRoot)
                puffs.Add((p.position, Mathf.Max(p.lossyScale.x, p.lossyScale.z) * 0.5f));
        for (int i = 0; i < maxActive + 1; i++) pool.Add(Create(transform, material));
        nextSpawn = Time.time + Random.Range(0.5f, 2f);
    }

    void Update()
    {
        if (Time.time >= nextSpawn)
        {
            nextSpawn = Time.time + Random.Range(interval.x, interval.y);
            if (CountActive() < maxActive && TryFindGap(out var pos)) Spawn(pos);
        }

        if (cam == null) cam = Camera.main;
        foreach (var p in pool)
        {
            if (!p.active) continue;
            float t = Time.time - p.start;
            if (t >= p.life) { p.active = false; p.root.gameObject.SetActive(false); continue; }
            float k = t < fadeIn ? Smooth(t / fadeIn)
                    : t < fadeIn + hold ? 1f
                    : 1f - Smooth((t - fadeIn - hold) / fadeOut);
            SetIntensity(p, k * maxIntensity);
            if (cam != null) FaceCamera(p.beam, cam.transform.position);
        }
    }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    int CountActive()
    {
        int n = 0;
        foreach (var p in pool) if (p.active) n++;
        return n;
    }

    // 구름 덩어리와 겹치지 않고 섬 바로 밑이 아닌 지점을 몇 번 시도해서 찾는다
    bool TryFindGap(out Vector3 pos)
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var p = areaCenter + new Vector3(Random.Range(-0.5f, 0.5f) * areaSize.x, 0f, Random.Range(-0.5f, 0.5f) * areaSize.y);
            p.y = baseY;
            var flat = new Vector2(p.x - avoidCenter.x, p.z - avoidCenter.z);
            if (flat.magnitude < avoidRadius) continue;
            bool blocked = false;
            foreach (var (c, r) in puffs)
            {
                float dx = p.x - c.x, dz = p.z - c.z;
                if (dx * dx + dz * dz < (r * 0.75f) * (r * 0.75f)) { blocked = true; break; }
            }
            if (blocked) continue;
            pos = p;
            return true;
        }
        pos = default;
        return false;
    }

    void Spawn(Vector3 pos)
    {
        Pillar p = null;
        foreach (var x in pool) if (!x.active) { p = x; break; }
        if (p == null) return;
        p.active = true;
        p.start = Time.time;
        p.life = fadeIn + hold + fadeOut;
        p.root.position = pos;
        p.root.gameObject.SetActive(true);
        Shape(p, Random.Range(height.x, height.y), Random.Range(width.x, width.y), groundGlowSize);
        var c = glowColors.Length > 0 ? glowColors[Random.Range(0, glowColors.Length)] : Color.white;
        SetColor(p, c);
        SetIntensity(p, 0f);
    }

    void SetIntensity(Pillar p, float v)
    {
        foreach (var r in new[] { p.beamR, p.glowR })
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(IntensityId, v);
            r.SetPropertyBlock(mpb);
        }
    }

    void SetColor(Pillar p, Color c)
    {
        foreach (var r in new[] { p.beamR, p.glowR })
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }

    static void FaceCamera(Transform beam, Vector3 camPos)
    {
        var d = camPos - beam.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.001f) beam.rotation = Quaternion.LookRotation(-d.normalized, Vector3.up);
    }

    static void Shape(Pillar p, float h, float w, float glow)
    {
        p.beam.localScale = new Vector3(w, h, 1f);
        p.beam.GetChild(0).localPosition = new Vector3(0f, 0.5f, 0f);
        p.glowR.transform.localScale = new Vector3(glow, glow, 1f);
    }

    static void Kill(Object o)
    {
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }

    // 빛기둥 한 개(세로 빔 + 바닥 번짐). 에디터 미리보기에서도 쓰도록 static.
    static Pillar Create(Transform parent, Material mat)
    {
        var root = new GameObject("Pillar").transform;
        root.SetParent(parent, false);

        var beam = new GameObject("Beam").transform;
        beam.SetParent(root, false);
        var beamQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Kill(beamQuad.GetComponent<Collider>());
        beamQuad.transform.SetParent(beam, false);
        var beamR = beamQuad.GetComponent<Renderer>();
        beamR.sharedMaterial = mat;
        beamR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beamR.receiveShadows = false;

        var glowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Kill(glowQuad.GetComponent<Collider>());
        glowQuad.name = "GroundGlow";
        glowQuad.transform.SetParent(root, false);
        glowQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glowQuad.transform.localPosition = new Vector3(0f, 2f, 0f);
        var glowR = glowQuad.GetComponent<Renderer>();
        glowR.sharedMaterial = mat;
        glowR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowR.receiveShadows = false;
        var block = new MaterialPropertyBlock();
        glowR.GetPropertyBlock(block);
        block.SetFloat("_Mode", 1f);
        glowR.SetPropertyBlock(block);

        root.gameObject.SetActive(false);
        return new Pillar { root = root, beam = beam, beamR = beamR, glowR = glowR };
    }

#if UNITY_EDITOR
    // 에디터 캡처용: 지금 설정으로 기둥 하나를 최대 밝기로 세운다(호출한 쪽이 지움)
    public GameObject PreviewAt(Vector3 pos, Vector3 camPos, Color color)
    {
        mpb ??= new MaterialPropertyBlock();
        var p = Create(transform, material);
        p.root.position = pos;
        p.root.gameObject.SetActive(true);
        Shape(p, height.y, width.y, groundGlowSize);
        SetColor(p, color);
        SetIntensity(p, maxIntensity);
        FaceCamera(p.beam, camPos);
        return p.root.gameObject;
    }
#endif
}
