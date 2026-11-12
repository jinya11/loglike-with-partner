using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숲 파생형 "반딧불 숲": 밤의 숲. 흩어진 반딧불을 감시자(조준점)가 모아 등불 가까이 가면 등불이 켜지고,
/// 켜진 등불 반경은 밝아지며 안전지대가 됨 (적은 밀려나고 지속 피해, 생명체는 회복).
/// 기본형과 다른 점: 지형 분위기(밤, 조명) / 적 구성(공포형 증가, StageVariantDef.enemyBias) / 규칙(반딧불·등불·안전지대).
/// 전용 씬(Region_Forest_Firefly)에 배치된 규칙 컴포넌트: 등불·반딧불 영역·밤 조명은 씬 오브젝트, 이 컴포넌트는 규칙만. 수치는 VariantConfig.firefly.
/// </summary>
public class ForestFireflyVariant : MonoBehaviour
{
    static readonly Color FlyColor = new Color(0.75f, 1f, 0.35f), LanternOff = new Color(0.25f, 0.22f, 0.15f), LanternOn = new Color(1f, 0.85f, 0.45f);

    class Lantern
    {
        public Transform root;
        public Light light;
        public Renderer glass;
        public LineRenderer ring;
        public bool lit;
        public float pulse, left; // left = 남은 점등 시간
    }

    StageDirector stage;
    FireflyForestSettings s;
    readonly List<Transform> flies = new List<Transform>(64);
    readonly List<Vector3> flyBase = new List<Vector3>(64);
    readonly List<Lantern> lanterns = new List<Lantern>(8);
    Material flyMat, onMat, offMat;

    float tick, respawnTimer;
    int shownCarried = -1, shownLit = -1;

    /// <summary>감시자가 들고 있는 반딧불 수.</summary>
    public int Carried { get; private set; }
    public int LitCount { get; private set; }

    [Header("씬 배치용 재질 (빌더가 연결, 비우면 기본 생성)")]
    public Material fireflyMaterial, lanternOnMaterial, lanternOffMaterial;

    /// <summary>
    /// 씬에 배치된 등불(VariantLantern)과 반딧불 영역(FireflySpawnArea)을 모아 규칙 시작. 밤 조명/안개/추종 조명은 씬에 이미 있음.
    /// </summary>
    public void Init(StageDirector st, FireflyForestSettings settings)
    {
        stage = st; s = settings;
        flyMat = fireflyMaterial ? fireflyMaterial : Unlit(FlyColor * 2.2f);
        onMat = lanternOnMaterial ? lanternOnMaterial : Unlit(LanternOn * 2f);
        offMat = lanternOffMaterial ? lanternOffMaterial : Unlit(LanternOff);
        areas = FindObjectsByType<FireflySpawnArea>();
        foreach (var v in FindObjectsByType<VariantLantern>())
        {
            var p = Ground.Snap(v.transform.position);
            v.transform.position = p;
            var l = new Lantern { root = v.transform, light = v.lanternLight, glass = v.glass, ring = v.safeRing };
            if (l.light) { l.light.intensity = 0f; l.light.range = s.safeRadius * 1.6f; }
            if (l.glass) l.glass.sharedMaterial = offMat;
            if (l.ring)
            {
                // 안전지대 고리: 지형 높이를 따라 그림
                l.ring.useWorldSpace = true; l.ring.loop = true; l.ring.positionCount = 48;
                var pts = new Vector3[48];
                for (int i = 0; i < 48; i++)
                {
                    float a = i * Mathf.PI * 2f / 48f, x = p.x + Mathf.Cos(a) * s.safeRadius, z = p.z + Mathf.Sin(a) * s.safeRadius;
                    pts[i] = new Vector3(x, Ground.HeightAt(x, z) + 0.1f, z);
                }
                l.ring.SetPositions(pts);
                l.ring.startColor = l.ring.endColor = new Color(0.6f, 0.55f, 0.4f, 0.35f);
            }
            lanterns.Add(l);
        }
        SpawnFireflies();
    }

    FireflySpawnArea[] areas;

    static Material Unlit(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh ? sh : Shader.Find("Sprites/Default"));
        if (m.HasColor("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
        return m;
    }

    // ---- 반딧불 (씬의 FireflySpawnArea 안에서 생기고 다시 나타남) ----
    void SpawnFireflies() { for (int i = 0; i < s.fireflyCount; i++) SpawnFirefly(); }

    Vector3 FlySpot()
    {
        if (areas != null && areas.Length > 0)
        {
            float total = 0f; foreach (var a in areas) total += Mathf.Max(0.01f, a.weight);
            float pick = Random.value * total;
            foreach (var a in areas) { pick -= Mathf.Max(0.01f, a.weight); if (pick <= 0f) return a.RandomPoint(); }
            return areas[areas.Length - 1].RandomPoint();
        }
        return Arena.RandomPoint(0.9f);
    }

    void SpawnFirefly()
    {
        Vector3 p = FlySpot();
        if (stage.Nav != null) p = stage.Nav.NearestFree(p);
        p = Ground.Snap(p, Random.Range(0.9f, 1.8f));
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Firefly";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.position = p;
        go.transform.localScale = Vector3.one * 0.22f;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = flyMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flies.Add(go.transform); flyBase.Add(p);
    }

    void Update()
    {
        var gm = GameManager.I;
        var pc = PlayerController.I;
        if (pc == null) return;


        UpdateHud();
        if (gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;

        // 반딧불 다시 나타남 (등불이 꺼져도 다시 켤 수 있게)
        if (flies.Count < s.fireflyCount && (respawnTimer -= dt) <= 0f) { respawnTimer = s.fireflyRespawn; SpawnFirefly(); }

        // 반딧불: 둥실둥실 + 감시자가 다가가면 모음
        Vector3 w = pc.Position;
        float cr2 = s.collectRadius * s.collectRadius;
        for (int i = flies.Count - 1; i >= 0; i--)
        {
            var f = flies[i];
            Vector3 b = flyBase[i];
            f.position = b + new Vector3(Mathf.Sin(Time.time * 1.3f + i) * 0.4f, Mathf.Sin(Time.time * 2.1f + i * 1.7f) * 0.25f, Mathf.Cos(Time.time * 1.1f + i) * 0.4f);
            Vector3 d = f.position - w; d.y = 0f;
            if (d.sqrMagnitude > cr2) continue;
            Carried++;
            Vfx.Pickup(f.position, FlyColor, 6);
            Destroy(f.gameObject);
            flies.RemoveAt(i); flyBase.RemoveAt(i);
        }

        // 등불: 반딧불이 충분하면 감시자가 다가가 켬
        float lr2 = s.lightRadius * s.lightRadius;
        foreach (var l in lanterns)
        {
            if (l.lit)
            {
                l.pulse += dt;
                l.left -= dt;
                float fade = Mathf.Clamp01(l.left / 1f); // 꺼지기 직전 1초 동안 흐려짐
                l.light.intensity = (s.lanternIntensity + Mathf.Sin(l.pulse * 3f) * 0.5f) * fade;
                if (l.left <= 0f) // 지속 시간 끝: 꺼짐 → 반딧불을 다시 모아 켤 수 있음
                {
                    l.lit = false; LitCount--;
                    l.light.intensity = 0f;
                    l.glass.sharedMaterial = offMat;
                    l.ring.startColor = l.ring.endColor = new Color(0.6f, 0.55f, 0.4f, 0.35f);
                }
                continue;
            }
            Vector3 d = l.root.position - w; d.y = 0f;
            if (d.sqrMagnitude > lr2 || Carried < s.firefliesPerLantern) continue;
            Carried -= s.firefliesPerLantern;
            l.lit = true; LitCount++; l.left = s.lanternDuration;
            l.glass.sharedMaterial = onMat;
            l.ring.startColor = l.ring.endColor = new Color(1f, 0.85f, 0.45f, 0.8f);
            Vfx.Pillar(l.root.position, LanternOn);
        }

        // 안전지대: 적은 밀려나고 피해, 생명체 회복 (0.2초마다)
        if ((tick -= dt) > 0f) return;
        tick = 0.2f;
        float sr2 = s.safeRadius * s.safeRadius;
        foreach (var l in lanterns)
        {
            if (!l.lit) continue;
            Vector3 c = l.root.position;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var e = list[i];
                Vector3 d = e.transform.position - c; d.y = 0f;
                if (d.sqrMagnitude > sr2) continue;
                e.Hit(s.safeDps * 0.2f, c);
                if (e.isActiveAndEnabled) e.Push((d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward) * s.repelSpeed);
            }
            if (Ward.I != null)
            {
                Vector3 d = Ward.I.Position - c; d.y = 0f;
                var st = gm.Stats;
                if (d.sqrMagnitude < sr2 && !Ward.I.HealBlocked) st.wardHp =Mathf.Min(st.wardMaxHp, st.wardHp + s.wardRegen * 0.2f);
            }
        }
    }

    void UpdateHud()
    {
        if (!HudExtras.I || (Carried == shownCarried && LitCount == shownLit)) return;
        shownCarried = Carried; shownLit = LitCount;
        HudExtras.I.SetStatus($"<color=#C0FF70>반딧불 {Carried}/{s.firefliesPerLantern}</color>   등불 {LitCount}/{lanterns.Count}"); // 수치만 (설명 문구 없음)
    }

    void OnDestroy() { if (HudExtras.I) HudExtras.I.SetStatus(null); }
}
