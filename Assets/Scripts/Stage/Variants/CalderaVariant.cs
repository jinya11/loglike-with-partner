using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화산 파생형 "칼데라"의 규칙 컴포넌트 (전용 씬 Region_Volcano_Caldera에 배치).
/// 배치는 모두 씬 오브젝트: 고리 모양으로 깔린 플랫폼(CalderaPlatform 프리팹), 용암 면(Lava). 지형(Terrain)과 보더 없음 — 경계는 용암.
/// 규칙:
///  - 용암 수위가 주기적으로 오르내림 (높음 유지 → 내려감 → 낮음 유지 → 올라감).
///  - 'vanishes' 플랫폼(고리 안쪽·바깥 가장자리)은 수위가 바뀔 때(기본: 낮아질 때) 흔들리며 경고한 뒤 가라앉고, 반대로 바뀔 때 다시 떠오름 → 고리 폭이 바뀜.
///  - 용암에 들어간 적은 즉사 (처치로 침). 생명체는 큰 피해를 입고 가까운 플랫폼으로 돌아옴.
///  - 길찾기: 용암 칸은 막힘 (적도 돌아옴), 곧 가라앉을 플랫폼은 생명체가 피함.
/// 수치는 VariantConfig.caldera.
/// </summary>
public class CalderaVariant : MonoBehaviour
{
    [Tooltip("용암 면 (씬 오브젝트, 수위에 따라 높이만 바뀜)")] public Transform lava;

    StageDirector stage;
    CalderaSettings s;
    readonly Dictionary<Vector2Int, CalderaPlatform> grid = new Dictionary<Vector2Int, CalderaPlatform>(1024);
    float cycleTime;
    bool navDirty = true;
    HashSet<Enemy> seen = new HashSet<Enemy>(), seenNext = new HashSet<Enemy>();
    readonly List<Enemy> burn = new List<Enemy>(16);

    /// <summary>현재 용암 표면 높이.</summary>
    public float LavaLevel { get; private set; }
    float Cycle => s.highHold + s.lowHold + 2f * Mathf.Max(0.1f, s.transition);

    public void Init(StageDirector st, CalderaSettings settings)
    {
        stage = st; s = settings;
        grid.Clear();
        foreach (var p in CalderaPlatform.All) grid[Key(p.transform.position)] = p;
        cycleTime = 0f;
        LavaLevel = s.highLevel;
        Apply(0f);
        st.PortalFilter = p => Stable(p);
        st.PortalFallback = () => StableNear(Vector3.back * (s.innerRadius + s.outerRadius) * 0.5f);
        // 생명체는 용암 호수 한가운데가 아니라 고리 위 (사라지지 않는 플랫폼)에서 시작
        if (Ward.I != null) Ward.I.Teleport(StableNear(Ward.I.Position + Vector3.back * (s.innerRadius + s.outerRadius) * 0.5f));
        RebuildNav();
    }

    void OnDestroy()
    {
        if (stage) { stage.PortalFilter = null; stage.PortalFallback = null; }
    }

    Vector2Int Key(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / s.tileSize), Mathf.RoundToInt(p.z / s.tileSize));

    CalderaPlatform At(Vector3 p) => grid.TryGetValue(Key(p), out var t) && t ? t : null;

    /// <summary>지금 설 수 있는 바닥인지.</summary>
    public bool OnSolid(Vector3 p) { var t = At(p); return t && t.Solid; }

    /// <summary>사라지지 않는 플랫폼 위인지 (포털 자리 등).</summary>
    bool Stable(Vector3 p) { var t = At(p); return t && !t.vanishes; }

    Vector3 StableNear(Vector3 p) => NearestPlatform(p, t => !t.vanishes);

    Vector3 NearestPlatform(Vector3 p, System.Func<CalderaPlatform, bool> ok)
    {
        CalderaPlatform best = null; float bestSq = float.MaxValue;
        foreach (var t in CalderaPlatform.All)
        {
            if (!ok(t)) continue;
            Vector3 d = t.transform.position - p; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = t; }
        }
        return best ? new Vector3(best.transform.position.x, 0f, best.transform.position.z) : p;
    }

    void Update()
    {
        if (s == null || GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        cycleTime = (cycleTime + dt) % Cycle;
        Apply(dt);
        if (navDirty) RebuildNav();
        TickWard();
        TickEnemies();
    }

    // ---- 수위 주기 ----
    /// <summary>주기 안 위치 → 용암 높이 + 가라앉는 플랫폼 자세.</summary>
    void Apply(float dt)
    {
        float tr = Mathf.Max(0.1f, s.transition);
        float t = cycleTime;
        // 0: 높음 유지, 1: 내려감, 2: 낮음 유지, 3: 올라감
        int phase; float e;
        if (t < s.highHold) { phase = 0; e = t; }
        else if ((t -= s.highHold) < tr) { phase = 1; e = t; }
        else if ((t -= tr) < s.lowHold) { phase = 2; e = t; }
        else { phase = 3; e = t - s.lowHold; }

        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / tr));
        LavaLevel = phase switch { 0 => s.highLevel, 1 => Mathf.Lerp(s.highLevel, s.lowLevel, k), 2 => s.lowLevel, _ => Mathf.Lerp(s.lowLevel, s.highLevel, k) };
        if (lava) { var lp = lava.position; lp.y = LavaLevel; lava.position = lp; }

        // 가라앉는 쪽 전환 / 떠오르는 쪽 전환 / 잠겨 있는 유지 구간
        bool low = s.vanishAt == CalderaSettings.VanishAt.Low;
        int sinkPhase = low ? 1 : 3, downPhase = low ? 2 : 0, risePhase = low ? 3 : 1;
        float down = (low ? s.lowLevel : s.highLevel) - 1.2f; // 잠긴 플랫폼 윗면 (용암 아래)
        float warn = Mathf.Clamp(s.warning, 0f, tr - 0.05f);

        float offset = 0f, shake = 0f; bool solid = true, warning = false;
        if (phase == sinkPhase)
        {
            if (e < warn) { shake = e / Mathf.Max(0.01f, warn); warning = true; }
            else { offset = Mathf.Lerp(0f, down, Mathf.SmoothStep(0f, 1f, (e - warn) / Mathf.Max(0.01f, tr - warn))); solid = false; }
        }
        else if (phase == downPhase) { offset = down; solid = false; }
        else if (phase == risePhase) { offset = Mathf.Lerp(down, 0f, Mathf.SmoothStep(0f, 1f, e / tr)); solid = false; }

        foreach (var p in CalderaPlatform.All)
        {
            if (!p.vanishes) continue;
            if (p.Solid != solid || p.Warning != warning) navDirty = true;
            bool wasSolid = p.Solid;
            p.Pose(offset, shake, solid, warning);
            if (wasSolid && !solid && dt > 0f) Vfx.Pickup(p.transform.position + Vector3.up * 0.2f, s.lavaColor, 4); // 가라앉기 시작
        }
    }

    // ---- 길찾기: 용암 칸은 막힘, 곧 가라앉을 플랫폼은 피함 ----
    void RebuildNav()
    {
        navDirty = false;
        var nav = stage.Nav;
        if (nav == null) return;
        nav.SetAllBlocked(true);
        float half = s.tileSize * 0.5f;
        float inset = Mathf.Max(0.05f, half - 0.6f); // 이웃 칸(용암)에 걸친 격자 칸은 막힌 채로
        foreach (var p in CalderaPlatform.All)
        {
            nav.SetExtraCost(p.transform.position, half, p.Warning ? 500f : 0f);
            if (p.Solid) nav.SetBlocked(p.transform.position, inset, false);
        }
    }

    // ---- 용암에 빠짐 ----
    void TickWard()
    {
        var ward = Ward.I;
        if (ward == null || !ward.Alive || ward.Trapped || OnSolid(ward.Position)) return;
        Vector3 at = ward.Position;
        Burst(at, 22);
        ward.Drain(GameManager.I.Stats.wardMaxHp * s.wardLavaDamageFrac);
        Vfx.Hit(at + Vector3.up * 0.5f, new Color(1f, 0.35f, 0.25f));
        if (!ward.Alive) return;
        ward.Teleport(NearestPlatform(at, t => t.Solid && !t.Warning));
        ward.Shield(s.wardShield);
    }

    void TickEnemies()
    {
        burn.Clear();
        seenNext.Clear();
        foreach (var e in Enemy.Active)
        {
            seenNext.Add(e);
            Vector3 p = e.transform.position;
            if (OnSolid(p)) continue;
            if (!seen.Contains(e)) { e.transform.position = NearestPlatform(p, t => t.Solid); continue; } // 막 생겨난 적: 용암 위면 가까운 플랫폼으로
            burn.Add(e);
        }
        (seen, seenNext) = (seenNext, seen);
        foreach (var e in burn) // 용암에 들어간 적은 즉사
        {
            Vector3 p = e.transform.position;
            Burst(p, 10);
            e.Hit(1e6f, p + Vector3.up);
        }
    }

    void Burst(Vector3 p, int n)
    {
        Vector3 at = new Vector3(p.x, LavaLevel + 0.15f, p.z);
        Vfx.Pickup(at, s.lavaColor, n);
        Vfx.Death(at, new Color(1f, 0.7f, 0.3f));
    }
}
