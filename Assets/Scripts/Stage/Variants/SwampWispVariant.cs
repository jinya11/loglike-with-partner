using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 늪 파생형 "도깨비불 늪"의 규칙 컴포넌트 (전용 씬 Region_Swamp_Wisp에 배치).
/// 배치는 모두 씬 오브젝트: 도깨비불(SwampWisp 프리팹)이 놓인 자리 둘레를 떠다님.
/// 규칙: 도깨비불에 닿으면 스테이지 안 무작위 안전 위치로 순간이동 (작은 생명체 / 적, 각각 VariantConfig.wisp에서 끌 수 있음).
///  - 닿은 도깨비불은 잠깐 꺼짐, 순간이동한 대상은 잠깐 다시 순간이동되지 않음.
///  - 안전 위치: 걸을 수 있는 칸(길찾기 격자), 경계 안쪽, 바닥 피해 구역 밖, 도깨비불에서 떨어진 곳. 생명체는 적에게서 먼 곳을 우선.
/// 수치는 VariantConfig.wisp.
/// </summary>
public class SwampWispVariant : MonoBehaviour
{
    StageDirector stage;
    WispSwampSettings s;
    float time;
    readonly Dictionary<Object, float> immuneUntil = new Dictionary<Object, float>(64);
    readonly List<Enemy> hits = new List<Enemy>(8);

    public void Init(StageDirector st, WispSwampSettings settings)
    {
        stage = st; s = settings;
        int i = 0;
        foreach (var w in SwampWisp.All) w.Begin(++i);
        // 생명체는 도깨비불을 쫓지 않고 피해 다님: 켜진 도깨비불 둘레로는 걸어 들어가지 않음 (길찾기도 돌아감)
        Ward.Hazard = p => NearWisp(p, s.wardAvoidRadius);
    }

    void OnDestroy() { Ward.Hazard = null; }

    bool NearWisp(Vector3 p, float r)
    {
        float r2 = r * r;
        foreach (var w in SwampWisp.All)
        {
            if (!w.Active) continue;
            Vector3 d = w.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude <= r2) return true;
        }
        return false;
    }

    void Update()
    {
        if (s == null || GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        time += dt;
        foreach (var w in SwampWisp.All) w.Drift(time, s.driftSpeed, s.wanderRadius, s.hoverHeight, dt);
        TouchWard();
        TouchEnemies();
        TickDots(dt);
        if ((navTick -= dt) <= 0f) { navTick = 0.25f; UpdateNavCost(); }
    }

    // ---- 길찾기: 도깨비불 둘레는 크게 돌아감 ----
    float navTick;
    readonly Dictionary<SwampWisp, Vector3> navMarked = new Dictionary<SwampWisp, Vector3>(16);
    void UpdateNavCost()
    {
        var nav = stage.Nav;
        if (nav == null) return;
        float r = s.wardAvoidRadius + 1f;
        foreach (var kv in navMarked) nav.SetExtraCost(kv.Value, r, 0f);
        navMarked.Clear();
        foreach (var w in SwampWisp.All)
        {
            if (!w.Active) continue;
            nav.SetExtraCost(w.transform.position, r, 300f);
            navMarked[w] = w.transform.position;
        }
    }

    // ---- 순간이동 후 지속 피해 ----
    class Dot { public Object target; public float left, tick; }
    readonly List<Dot> dots = new List<Dot>(16);
    const float DotStep = 0.5f;

    void AddDot(Object target)
    {
        if (s.teleportDotTotal <= 0f || s.teleportDotDuration <= 0f) return;
        foreach (var d in dots) if (d.target == target) { d.left = s.teleportDotDuration; d.tick = DotStep; return; } // 다시 걸리면 시간만 새로
        dots.Add(new Dot { target = target, left = s.teleportDotDuration, tick = DotStep });
    }

    void TickDots(float dt)
    {
        float perTick = s.teleportDotTotal * DotStep / Mathf.Max(0.01f, s.teleportDotDuration);
        for (int i = dots.Count - 1; i >= 0; i--)
        {
            var d = dots[i];
            if (!d.target) { dots.RemoveAt(i); continue; }
            d.left -= dt;
            if ((d.tick -= dt) <= 0f)
            {
                d.tick += DotStep;
                if (d.target is Ward w) { if (w.Alive) { w.Drain(perTick); Vfx.Pickup(w.Position + Vector3.up * 0.7f, s.wispColor, 2); } }
                else if (d.target is Enemy e && e.isActiveAndEnabled) e.EnvironmentDamage(perTick); // 이 피해로 죽으면 환경사 (처치 수에 안 침)
            }
            if (d.left <= 0f) dots.RemoveAt(i);
        }
    }

    bool Immune(Object o) => immuneUntil.TryGetValue(o, out var t) && t > time;

    SwampWisp Touching(Vector3 p)
    {
        float r2 = s.touchRadius * s.touchRadius;
        foreach (var w in SwampWisp.All)
        {
            if (!w.Active) continue;
            Vector3 d = w.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude <= r2) return w;
        }
        return null;
    }

    void TouchWard()
    {
        var ward = Ward.I;
        if (!s.affectWard || ward == null || !ward.Alive || ward.Trapped || Immune(ward)) return;
        var w = Touching(ward.Position);
        if (!w) return;
        Vector3 from = ward.Position, to = SafePoint(true, from);
        Flash(from); Flash(to);
        ward.Teleport(to);
        Spend(w, ward);
        if (s.dotWard) AddDot(ward);
    }

    void TouchEnemies()
    {
        if (!s.affectEnemies) return;
        hits.Clear();
        foreach (var e in Enemy.Active) if (!Immune(e) && Touching(e.transform.position)) hits.Add(e);
        foreach (var e in hits)
        {
            var w = Touching(e.transform.position);
            if (!w) continue;
            Vector3 to = Ground.Snap(SafePoint(false, e.transform.position));
            Flash(e.transform.position); Flash(to);
            e.transform.position = to;
            if (e.TryGetComponent(out Rigidbody rb)) { rb.position = to; rb.linearVelocity = Vector3.zero; }
            Spend(w, e);
            if (s.dotEnemies) AddDot(e);
        }
    }

    void Spend(SwampWisp w, Object target)
    {
        w.cooldown = s.wispCooldown;
        immuneUntil[target] = time + s.targetCooldown;
    }

    void Flash(Vector3 p) => Vfx.Pickup(Ground.Snap(p) + Vector3.up * 0.6f, s.wispColor, 10);

    /// <summary>스테이지 안 무작위 안전 위치. forWard면 적에게서 먼 곳을 우선 (못 찾으면 후보 중 가장 먼 곳, 후보가 없으면 fallback).</summary>
    Vector3 SafePoint(bool forWard, Vector3 fallback)
    {
        var nav = stage.Nav;
        Vector3 best = Vector3.zero; float bestScore = float.MinValue;
        float clear = s.wanderRadius + s.wispClearance, clear2 = clear * clear, safe = s.wardSafeDistance;
        for (int i = 0; i < 40; i++)
        {
            Vector3 p = Arena.RandomPoint(0.85f);
            if (nav != null) p = nav.NearestFree(p);
            if (!Arena.Contains(p, 3f) || InDamageZone(p) || (Ward.Hazard != null && Ward.Hazard(p))) continue;
            bool nearWisp = false;
            foreach (var w in SwampWisp.All) { Vector3 d = w.home - p; d.y = 0f; if (d.sqrMagnitude < clear2) { nearWisp = true; break; } } // 도깨비불이 떠다니는 범위 전체 + 여유
            if (nearWisp) continue;
            if (!forWard) return p;
            float near = float.MaxValue;
            foreach (var e in Enemy.Active) { Vector3 d = e.transform.position - p; d.y = 0f; near = Mathf.Min(near, d.magnitude); }
            if (near >= safe) return p;
            if (near > bestScore) { bestScore = near; best = p; }
        }
        return bestScore > float.MinValue ? best : fallback;
    }

    static bool InDamageZone(Vector3 p)
    {
        foreach (var z in StageZone.All) if (z.type == ZoneType.Damage && z.Contains(p)) return true;
        return false;
    }
}
