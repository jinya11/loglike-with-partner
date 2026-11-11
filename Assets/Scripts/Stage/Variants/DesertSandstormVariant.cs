using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사막 파생형 "모래폭풍 사막"의 규칙 컴포넌트 (전용 씬 Region_Desert_Sandstorm에 배치).
/// 배치는 모두 씬 오브젝트: 동굴 바위(ShelterRock, 프리팹 CaveRock), 폭풍 모래 입자(sand), 유물 프리팹(relicPrefab).
/// 주기: 잔잔 → 경보(모래가 일기 시작, 남은 시간 표시) → 폭풍 → 잔잔 ...
///  - 폭풍 시작 freezeLead초 전부터 폭풍이 끝날 때까지 적은 움직이지 못함 (Enemy.Frozen, 공격/능력도 없음).
///  - 폭풍 동안 바람이 windYaw 방향으로 생명체와 적을 밀고, 피난처 밖 생명체는 모래에 조금씩 피해. 동굴 바위 안/뒤는 바람이 없음.
///  - 폭풍이 지나가면 모래에 묻힌 유물이 드러남 → 감시자나 생명체가 다가가면 주움 (경험치 보석 + 지역 자원).
/// 수치는 VariantConfig.sandstorm.
/// </summary>
public class DesertSandstormVariant : MonoBehaviour
{
    [Tooltip("바람이 불어가는 방향(도, 0 = +Z). 동굴 바위 입구도 이쪽을 보게 두기")] public float windYaw = 90f;
    [Tooltip("폭풍 모래 입자 (씬 배치, 아레나 전체를 덮음)")] public ParticleSystem sand;
    [Tooltip("유물 프리팹 (모래 둔덕 + 유물). 자식 'Relic'이 빙글빙글 돎")] public GameObject relicPrefab;

    enum Phase { Calm, Warning, Storm }

    class Relic { public Transform root, spin; public float life, age; public Vector3 pos; }

    StageDirector stage;
    SandstormSettings s;
    Phase phase;
    float timer, dmgTick, intensity;
    Vector3 wind;
    int found;
    readonly List<Relic> relics = new List<Relic>();
    // 원래 안개 (폭풍 동안 모래빛으로 짙어졌다가 돌아옴)
    bool fog0; FogMode fogMode0; Color fogColor0; float fogStart0, fogEnd0;

    public bool Storming => phase == Phase.Storm;
    public int RelicsFound => found;
    public Vector3 Wind => wind;

    public void Init(StageDirector st, SandstormSettings settings)
    {
        stage = st; s = settings;
        wind = Quaternion.Euler(0f, windYaw, 0f) * Vector3.forward;
        phase = Phase.Calm; timer = s.firstDelay;
        fog0 = RenderSettings.fog; fogMode0 = RenderSettings.fogMode; fogColor0 = RenderSettings.fogColor;
        fogStart0 = RenderSettings.fogStartDistance; fogEnd0 = RenderSettings.fogEndDistance;
        if (!fog0) { fogStart0 = 200f; fogEnd0 = 400f; fogColor0 = s.sandColor; }
        // 바위를 지면에 붙임 (지형 높이 기준)
        foreach (var r in ShelterRock.All) r.transform.position = Ground.Snap(r.transform.position);
        if (sand)
        {
            var vel = sand.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            Vector3 v = wind * 16f;
            vel.x = new ParticleSystem.MinMaxCurve(v.x * 0.8f, v.x * 1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            vel.z = new ParticleSystem.MinMaxCurve(v.z * 0.8f, v.z * 1.2f);
            var main = sand.main; main.startColor = new Color(s.sandColor.r, s.sandColor.g, s.sandColor.b, 0.55f);
            // 바람 불어오는 쪽에서 생기도록 입자 상자를 바람 반대쪽으로 조금 옮김
            sand.transform.position = new Vector3(-wind.x * 12f, sand.transform.position.y, -wind.z * 12f);
            SetEmission(0f);
            sand.Play();
        }
    }

    void OnDestroy()
    {
        Enemy.Frozen = false;
        if (s == null) return;
        RenderSettings.fog = fog0; RenderSettings.fogMode = fogMode0; RenderSettings.fogColor = fogColor0;
        RenderSettings.fogStartDistance = fogStart0; RenderSettings.fogEndDistance = fogEnd0;
        if (HudExtras.I) HudExtras.I.SetStatus(null);
    }

    void Update()
    {
        if (s == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        timer -= dt;
        switch (phase)
        {
            case Phase.Calm:
                if (timer <= 0f) { phase = Phase.Warning; timer = s.warning; }
                break;
            case Phase.Warning:
                Enemy.Frozen = timer <= Mathf.Min(s.freezeLead, s.warning); // 폭풍 몇 초 전부터 적 정지
                if (timer <= 0f) { phase = Phase.Storm; timer = s.duration; Enemy.Frozen = true; dmgTick = 1f; }
                break;
            case Phase.Storm:
                Blow(dt);
                if (timer <= 0f) { phase = Phase.Calm; timer = s.calm; Enemy.Frozen = false; RevealRelics(); }
                break;
        }
        UpdateLook(dt);
        TickRelics(dt);
        UpdateStatus();
    }

    // ---- 바람 ----
    void Blow(float dt)
    {
        // 시작/끝은 부드럽게 (세기 0 → 1 → 0)
        float k = Mathf.Clamp01((s.duration - timer) / 1f) * Mathf.Clamp01(timer / 1.2f);
        Vector3 push = wind * s.windSpeed * k;
        var ward = Ward.I;
        if (ward != null && ward.Alive)
        {
            if (!ShelterRock.AnyCovers(ward.Position, wind, s.shelterLength))
            {
                ward.Knockback(push);
                if ((dmgTick -= dt) <= 0f) { dmgTick = 1f; if (s.stormWardDps > 0f && k > 0.5f) ward.TakeDamage(s.stormWardDps); }
            }
            else dmgTick = Mathf.Max(dmgTick, 0.5f);
        }
        Vector3 ep = push * s.enemyWindMul;
        foreach (var e in Enemy.Active)
            if (!ShelterRock.AnyCovers(e.transform.position, wind, s.shelterLength)) e.Push(ep);
    }

    void UpdateLook(float dt)
    {
        float target = phase == Phase.Storm ? 1f : phase == Phase.Warning ? 0.25f * (1f - timer / Mathf.Max(0.01f, s.warning)) : 0f;
        if (phase == Phase.Storm && timer < 1.2f) target = timer / 1.2f;
        intensity = Mathf.MoveTowards(intensity, target, dt * 1.5f);
        SetEmission(intensity);
        RenderSettings.fog = fog0 || intensity > 0.001f;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(fogColor0, s.sandColor, intensity);
        RenderSettings.fogStartDistance = Mathf.Lerp(fogStart0, 8f, intensity);
        RenderSettings.fogEndDistance = Mathf.Lerp(fogEnd0, s.stormFogEnd, intensity);
    }

    void SetEmission(float k)
    {
        if (!sand) return;
        var em = sand.emission; em.rateOverTime = s.sandRate * k;
    }

    // ---- 유물 ----
    void RevealRelics()
    {
        for (int i = 0; i < s.relicsPerStorm; i++)
        {
            Vector3 p = Arena.RandomPoint(0.85f);
            if (stage.Nav != null) p = stage.Nav.NearestFree(p);
            p = Ground.Snap(p);
            Transform root, spin;
            if (relicPrefab)
            {
                root = Instantiate(relicPrefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform).transform;
                spin = root.Find("Relic");
            }
            else
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                Destroy(root.GetComponent<Collider>());
                root.SetParent(transform, false); root.position = p + Vector3.up * 0.4f; root.localScale = Vector3.one * 0.4f;
                spin = root;
            }
            root.localScale = Vector3.zero;
            relics.Add(new Relic { root = root, spin = spin, life = s.relicLifetime, pos = p });
            Vfx.Pickup(p + Vector3.up * 0.3f, s.sandColor, 12);
        }
    }

    void TickRelics(float dt)
    {
        if (relics.Count == 0) return;
        Vector3 watcher = PlayerController.I ? PlayerController.I.Position : new Vector3(1e5f, 0f, 1e5f);
        var ward = Ward.I;
        float r2 = s.relicCollectRadius * s.relicCollectRadius;
        for (int i = relics.Count - 1; i >= 0; i--)
        {
            var r = relics[i];
            if (!r.root) { relics.RemoveAt(i); continue; }
            r.age += dt; r.life -= dt;
            // 모래에서 솟아올랐다가, 시간이 다 되면 다시 묻힘
            float grow = Mathf.Clamp01(r.age / 0.6f) * Mathf.Clamp01(r.life / 1f);
            r.root.localScale = Vector3.one * grow;
            if (r.spin && r.spin != r.root) r.spin.localRotation = Quaternion.Euler(0f, r.age * 90f, 0f);
            if (r.life <= 0f) { Destroy(r.root.gameObject); relics.RemoveAt(i); continue; }
            Vector3 w = watcher - r.pos; w.y = 0f;
            bool take = w.sqrMagnitude < r2;
            if (!take && ward != null && ward.Alive) { Vector3 d = ward.Position - r.pos; d.y = 0f; take = d.sqrMagnitude < r2; }
            if (!take) continue;
            found++;
            stage.DropReward(r.pos, s.relicGems, s.relicResource);
            Vfx.Pickup(r.pos + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.4f), 24);
            Destroy(r.root.gameObject);
            relics.RemoveAt(i);
        }
    }

    // ---- HUD: 수치만 ----
    void UpdateStatus()
    {
        if (!HudExtras.I) return;
        string storm = phase == Phase.Warning ? $"<color=#F0C878>모래폭풍 {Mathf.CeilToInt(timer)}</color>"
                     : phase == Phase.Storm ? "<color=#F0C878>모래폭풍</color>" : null;
        string relic = found > 0 || relics.Count > 0 ? $"유물 {found}" : null;
        HudExtras.I.SetStatus(storm != null && relic != null ? storm + "   " + relic : storm ?? relic);
    }
}
