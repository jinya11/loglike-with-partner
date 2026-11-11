using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숲 파생형 "반딧불 숲": 밤의 숲. 흩어진 반딧불을 감시자(조준점)가 모아 등불 가까이 가면 등불이 켜지고,
/// 켜진 등불 반경은 밝아지며 안전지대가 됨 (적은 밀려나고 지속 피해, 생명체는 회복).
/// 기본형과 다른 점: 지형 분위기(밤, 조명) / 적 구성(공포형 증가, StageVariantDef.enemyBias) / 규칙(반딧불·등불·안전지대).
/// StageDirector가 파생형일 때 붙임. 수치는 StageConfig.firefly.
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
        public float pulse;
    }

    StageDirector stage;
    FireflyForestSettings s;
    readonly List<Transform> flies = new List<Transform>(64);
    readonly List<Vector3> flyBase = new List<Vector3>(64);
    readonly List<Lantern> lanterns = new List<Lantern>(8);
    Material flyMat, onMat, offMat, postMat;
    Light playerLight, wardLight;
    float tick;
    int shownCarried = -1, shownLit = -1;

    /// <summary>감시자가 들고 있는 반딧불 수.</summary>
    public int Carried { get; private set; }
    public int LitCount { get; private set; }

    public void Init(StageDirector st, FireflyForestSettings settings)
    {
        stage = st; s = settings;
        flyMat = Unlit(FlyColor * 2.2f);
        onMat = Unlit(LanternOn * 2f);
        offMat = Unlit(LanternOff);
        postMat = Unlit(new Color(0.12f, 0.1f, 0.09f));
        MakeNight();
        SpawnFireflies();
        SpawnLanterns();
    }

    static Material Unlit(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh ? sh : Shader.Find("Sprites/Default"));
        if (m.HasColor("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
        return m;
    }

    // ---- 밤 ----
    void MakeNight()
    {
        foreach (var l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional) { l.intensity *= s.sunMul; l.color = new Color(0.55f, 0.65f, 1f); }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = s.ambient;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.06f);
        RenderSettings.fogStartDistance = 18f; RenderSettings.fogEndDistance = 75f;
        RenderSettings.reflectionIntensity = 0.1f;
        var cam = Camera.main;
        if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor; }
        // 감시자/생명체 주변만 희미하게 보임
        playerLight = PointLight("WatcherLight", 11f, 2.2f, new Color(0.65f, 0.75f, 1f));
        wardLight = PointLight("WardLight", 6f, 1.6f, new Color(0.75f, 1f, 0.75f));
    }

    Light PointLight(string name, float range, float intensity, Color c)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(transform, false);
        l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = c; l.shadows = LightShadows.None;
        return l;
    }

    // ---- 반딧불 ----
    void SpawnFireflies()
    {
        for (int i = 0; i < s.fireflyCount; i++)
        {
            var p = Arena.RandomPoint(0.9f);
            if (stage.Nav != null) p = stage.Nav.NearestFree(p);
            if (p.magnitude < 6f) { i--; continue; }
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
    }

    // ---- 등불 ----
    void SpawnLanterns()
    {
        var used = new List<Vector3>();
        for (int i = 0, tries = 0; i < s.lanternCount && tries < 400; tries++)
        {
            var p = Arena.RandomPoint(0.8f);
            if (stage.Nav != null) p = stage.Nav.NearestFree(p);
            if (p.magnitude < 10f || !Ground.IsFlat(p, 1f, 0.5f, 22f)) continue;
            bool near = false;
            foreach (var u in used) if ((u - p).sqrMagnitude < 22f * 22f) { near = true; break; }
            if (near && tries < 300) continue;
            used.Add(p);
            lanterns.Add(MakeLantern(Ground.Snap(p)));
            i++;
        }
    }

    Lantern MakeLantern(Vector3 p)
    {
        var root = new GameObject("Lantern").transform;
        root.SetParent(transform, false);
        root.position = p;
        Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1.1f, 0f), new Vector3(0.25f, 1.1f, 0.25f), postMat);
        Part(PrimitiveType.Cube, root, new Vector3(0f, 2.3f, 0f), new Vector3(0.75f, 0.12f, 0.75f), postMat);
        var glass = Part(PrimitiveType.Sphere, root, new Vector3(0f, 2.75f, 0f), Vector3.one * 0.65f, offMat);
        var l = new GameObject("LanternLight").AddComponent<Light>();
        l.transform.SetParent(root, false); l.transform.localPosition = new Vector3(0f, 3f, 0f);
        l.type = LightType.Point; l.range = s.safeRadius * 1.6f; l.intensity = 0f; l.color = LanternOn; l.shadows = LightShadows.None;
        var ring = new GameObject("SafeRing").AddComponent<LineRenderer>();
        ring.transform.SetParent(root, false);
        ring.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        ring.useWorldSpace = true; ring.loop = true; ring.widthMultiplier = 0.12f; ring.positionCount = 48;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var pts = new Vector3[48];
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2f / 48f, x = p.x + Mathf.Cos(a) * s.safeRadius, z = p.z + Mathf.Sin(a) * s.safeRadius;
            pts[i] = new Vector3(x, Ground.HeightAt(x, z) + 0.1f, z);
        }
        ring.SetPositions(pts);
        ring.startColor = ring.endColor = new Color(0.6f, 0.55f, 0.4f, 0.35f);
        return new Lantern { root = root, light = l, glass = glass, ring = ring };
    }

    Renderer Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(t);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = m;
        return r;
    }

    void Update()
    {
        var gm = GameManager.I;
        var pc = PlayerController.I;
        if (pc == null) return;
        if (playerLight) playerLight.transform.position = pc.Position + Vector3.up * 4f;
        if (wardLight && Ward.I != null) wardLight.transform.position = Ward.I.Position + Vector3.up * 2.2f;
        UpdateHud();
        if (gm.State != GameState.Playing) return;
        float dt = Time.deltaTime;

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
            if (l.lit) { l.pulse += dt; l.light.intensity = s.lanternIntensity + Mathf.Sin(l.pulse * 3f) * 0.5f; continue; }
            Vector3 d = l.root.position - w; d.y = 0f;
            if (d.sqrMagnitude > lr2 || Carried < s.firefliesPerLantern) continue;
            Carried -= s.firefliesPerLantern;
            l.lit = true; LitCount++;
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
                if (d.sqrMagnitude < sr2) st.wardHp = Mathf.Min(st.wardMaxHp, st.wardHp + s.wardRegen * 0.2f);
            }
        }
    }

    void UpdateHud()
    {
        if (!HudExtras.I || (Carried == shownCarried && LitCount == shownLit)) return;
        shownCarried = Carried; shownLit = LitCount;
        HudExtras.I.SetStatus($"<color=#C0FF70>반딧불 {Carried}</color> / 등불 하나에 {s.firefliesPerLantern}   ·   켜진 등불 {LitCount}/{lanterns.Count}  (등불 근처는 안전지대)");
    }

    void OnDestroy() { if (HudExtras.I) HudExtras.I.SetStatus(null); }
}
