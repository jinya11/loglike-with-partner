using UnityEngine;

/// <summary>
/// 파티클 이펙트 (스테이지 싱글톤). 성질별 ParticleSystem 2개를 공유하고 Emit으로만 분출
/// → 이펙트마다 오브젝트/풀이 필요 없고 전체가 드로우콜 2개. 기본 도형(원형 입자)만 사용.
/// </summary>
public class Vfx : Singleton<Vfx>
{
    static Texture2D dot;
    static ParticleSystem.EmitParams ep;
    ParticleSystem burst, drift; // burst: 중력+감속 (파편/불꽃), drift: 무중력 (연기/반짝임/오라)

    protected override void Awake()
    {
        base.Awake();
        var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = Dot(), enableInstancing = true };
        burst = Make("FxBurst", mat, 1.8f, 3f);
        drift = Make("FxDrift", mat, 0f, 0.6f);
    }

    ParticleSystem Make(string name, Material mat, float gravity, float drag)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = true; m.playOnAwake = false; m.maxParticles = 1500;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.gravityModifier = gravity;
        m.scalingMode = ParticleSystemScalingMode.Shape;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = drag; lv.multiplyDragByParticleSize = false;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    /// <summary>부드러운 원형 입자 텍스처 (런타임 1회 생성).</summary>
    static Texture2D Dot()
    {
        if (dot) return dot;
        const int N = 32;
        dot = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        dot.SetPixels32(px);
        dot.Apply(false, true);
        return dot;
    }

    /// <summary>pos에서 n개 분출. up: 위쪽 성분 비율, spread: 수평 확산.</summary>
    static void Emit(ParticleSystem ps, Vector3 pos, Color c, int n, float speed, float size, float life, float up = 0.5f, float jitter = 0.1f)
    {
        ep.startColor = c;
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Random.onUnitSphere;
            d.y = Mathf.Abs(d.y) * up + up * 0.5f;
            ep.position = pos + Random.insideUnitSphere * jitter;
            ep.velocity = d * (speed * Random.Range(0.5f, 1f));
            ep.startSize = size * Random.Range(0.7f, 1.3f);
            ep.startLifetime = life * Random.Range(0.7f, 1.2f);
            ps.Emit(ep, 1);
        }
    }

    /// <summary>수평 원형으로 퍼지는 고리.</summary>
    static void Ring(ParticleSystem ps, Vector3 pos, Color c, int n, float radius, float speed, float size, float life)
    {
        ep.startColor = c; ep.startSize = size; ep.startLifetime = life;
        float step = Mathf.PI * 2f / n;
        for (int i = 0; i < n; i++)
        {
            var d = new Vector3(Mathf.Cos(step * i), 0f, Mathf.Sin(step * i));
            ep.position = pos + d * radius;
            ep.velocity = d * speed;
            ps.Emit(ep, 1);
        }
    }

    static Color Bright(Color c) => Color.Lerp(c, Color.white, 0.5f);

    // ---- 프리셋 (호출부에서 I null 체크 불필요) ----

    /// <summary>투사체/공격 적중 불꽃.</summary>
    public static void Hit(Vector3 p, Color c)
    {
        if (!I) return;
        Emit(I.burst, p, Bright(c), 4, 6f, 0.12f, 0.25f, 0.6f);
    }

    /// <summary>적 처치: 몸 색 파편 + 흰 섬광 + 연기.</summary>
    public static void Death(Vector3 p, Color c)
    {
        if (!I) return;
        p.y += 0.4f;
        Emit(I.burst, p, c, 10, 7f, 0.22f, 0.5f, 0.8f, 0.2f);
        Emit(I.drift, p, new Color(1f, 1f, 1f, 0.9f), 1, 0f, 1.4f, 0.12f, 0f, 0f);
        Emit(I.drift, p, new Color(0.35f, 0.35f, 0.4f, 0.5f), 3, 1.2f, 0.6f, 0.6f, 1f, 0.25f);
    }

    /// <summary>폭격/광역 폭발: 섬광 + 불꽃 파편 + 바닥 고리 + 연기.</summary>
    public static void Explosion(Vector3 p, float radius, Color c)
    {
        if (!I) return;
        Vector3 up = p + Vector3.up * 0.3f;
        Emit(I.drift, up, new Color(1f, 1f, 0.9f, 1f), 1, 0f, radius * 2.2f, 0.12f, 0f, 0f);
        Emit(I.burst, up, Bright(c), 8 + (int)(radius * 4f), 6f + radius * 3f, 0.2f, 0.55f, 1f, radius * 0.3f);
        Ring(I.drift, p + Vector3.up * 0.1f, c, 14, radius * 0.3f, radius * 3f, 0.35f, 0.3f);
        Emit(I.drift, up, new Color(0.3f, 0.28f, 0.28f, 0.45f), 4, 1.5f, radius * 0.8f, 0.9f, 1f, radius * 0.4f);
    }

    /// <summary>스킬 시전 고리 (Q 등).</summary>
    public static void Nova(Vector3 p, float radius, Color c)
    {
        if (!I) return;
        Ring(I.drift, p + Vector3.up * 0.15f, Bright(c), 24, 0.4f, radius * 4f, 0.3f, 0.25f);
        Emit(I.burst, p + Vector3.up * 0.3f, c, 8, radius * 2f, 0.15f, 0.35f, 0.4f, 0.3f);
    }

    /// <summary>바람: dir 방향으로 흘러가는 줄기.</summary>
    public static void Gust(Vector3 p, Vector3 dir, float radius, Color c)
    {
        if (!I) return;
        ep.startColor = c; ep.startLifetime = 0.4f;
        Vector3 side = new Vector3(-dir.z, 0f, dir.x);
        for (int i = 0; i < 16; i++)
        {
            ep.position = p + side * Random.Range(-radius, radius) + Vector3.up * Random.Range(0.1f, 0.8f);
            ep.velocity = dir * (radius * Random.Range(4f, 7f));
            ep.startSize = Random.Range(0.12f, 0.25f);
            I.drift.Emit(ep, 1);
        }
    }

    /// <summary>회오리: 바깥에서 중심으로 빨려드는 고리.</summary>
    public static void Vortex(Vector3 p, float radius, Color c)
    {
        if (!I) return;
        Ring(I.drift, p + Vector3.up * Random.Range(0.1f, 1.5f), c, 12, radius, -radius * 2.5f, 0.22f, 0.4f);
    }

    /// <summary>디버프 표시 (생명체 위). Slow: 떨어지는 결정, Bleed: 핏방울, Fear: 솟는 보랏빛.</summary>
    public static void Debuff(Vector3 p, EnemyAbility kind, int n = 3)
    {
        if (!I) return;
        p.y += 0.6f;
        switch (kind)
        {
            case EnemyAbility.Slow: Emit(I.drift, p + Vector3.up * 0.5f, new Color(0.5f, 0.75f, 1f, 0.9f), n, 0.4f, 0.14f, 0.6f, -1.5f, 0.35f); break;
            case EnemyAbility.Bleed: Emit(I.burst, p, new Color(0.8f, 0.05f, 0.1f), n, 2.5f, 0.13f, 0.45f, 0.6f, 0.2f); break;
            case EnemyAbility.Fear: Emit(I.drift, p, new Color(0.65f, 0.35f, 1f, 0.8f), n, 1.4f, 0.2f, 0.6f, 1.5f, 0.3f); break;
            case EnemyAbility.Charge: Emit(I.burst, p, new Color(1f, 0.6f, 0.2f), n * 3, 6f, 0.16f, 0.35f, 0.5f, 0.2f); break;
        }
    }

    /// <summary>보호막 전개: 위로 감싸는 하늘색 고리 2겹.</summary>
    public static void Shield(Vector3 p, Color c)
    {
        if (!I) return;
        Ring(I.drift, p + Vector3.up * 0.1f, c, 18, 0.9f, -0.6f, 0.22f, 0.45f);
        Emit(I.drift, p + Vector3.up * 0.5f, Bright(c), 10, 1.8f, 0.16f, 0.6f, 1.2f, 0.6f);
    }

    /// <summary>구제(포위 탈출): 초록 충격파 + 솟는 빛.</summary>
    public static void Rescue(Vector3 p, float radius, Color c)
    {
        if (!I) return;
        Ring(I.drift, p + Vector3.up * 0.15f, c, 32, 0.5f, radius * 3f, 0.4f, 0.35f);
        Emit(I.drift, p + Vector3.up * 0.3f, Bright(c), 14, 3f, 0.2f, 0.7f, 2f, 0.4f);
    }

    /// <summary>획득 반짝임 (경험치/자원).</summary>
    public static void Pickup(Vector3 p, Color c, int n = 3)
    {
        if (!I) return;
        Emit(I.drift, p + Vector3.up * 0.4f, Bright(c), n, 1.6f, 0.15f, 0.4f, 1.5f, 0.15f);
    }

    /// <summary>레벨업/포털 개방: 금빛 기둥.</summary>
    public static void Pillar(Vector3 p, Color c)
    {
        if (!I) return;
        Ring(I.drift, p + Vector3.up * 0.1f, c, 20, 0.6f, 2.5f, 0.3f, 0.4f);
        Emit(I.drift, p + Vector3.up * 0.2f, Bright(c), 18, 5f, 0.2f, 0.7f, 3f, 0.4f);
    }
}
