using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마법사 성장 노드의 전투 효과 (스테이지 단위 싱글톤, StageDirector 오브젝트에 붙음 → 씬과 함께 정리).
/// 메인(연쇄/연소/빙결/저주)은 기본 평타(MagicBoltAttack)에 덧붙음. '모든 효과' 패시브 = 네 효과 전부 + 연쇄로 맞은 적에게도 전부.
/// 감시자: 유도(디코이) 분류 스킬 발동 시 끌려온 적들에게 거대 마법 투사체. 수치는 WizardGrowthConfig.wizard.
/// </summary>
public class WizardGrowth : Singleton<WizardGrowth>
{
    class Burn { public Enemy enemy; public float left, tickLeft, perTick; }
    class Bolt { public LineRenderer line; public float until; }
    class Orb { public Transform t; public Enemy target; public Vector3 to; public float dmg; }

    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    const int ChainMax = 16;

    GrowthLoadout L;
    WizardGrowthSettings S;
    readonly List<Burn> burns = new List<Burn>(16);
    readonly Stack<Burn> freeBurns = new Stack<Burn>(16);
    readonly List<Bolt> bolts = new List<Bolt>(8);
    readonly Stack<Bolt> freeBolts = new Stack<Bolt>(8);
    readonly List<Orb> orbs = new List<Orb>(4);
    readonly Stack<Transform> freeOrbs = new Stack<Transform>(4);
    readonly Enemy[] chainHit = new Enemy[ChainMax];
    readonly Vector3[] zig = new Vector3[6];
    Material lineMat;
    MaterialPropertyBlock mpb;

    /// <summary>마법 탄 명중 콜백 (캐시: 발사마다 할당하지 않음).</summary>
    public System.Action<Enemy, Vector3, float> BoltHit;

    public static WizardGrowth Ensure(GrowthLoadout l)
    {
        if (!StageDirector.I || l == null || !l.Any || !(l.config is WizardGrowthConfig)) return null;
        var g = I ? I : StageDirector.I.gameObject.AddComponent<WizardGrowth>();
        g.Setup(l);
        return g;
    }

    void Setup(GrowthLoadout l)
    {
        WatcherSkills.Cast -= OnSkillCast;
        L = l; S = ((WizardGrowthConfig)l.config).wizard;
        BoltHit ??= OnBoltHit;
        if (L.arcaneOrbs) WatcherSkills.Cast += OnSkillCast;
    }

    void OnEnable() => StageDirector.EnemyDied += OnEnemyDied;
    void OnDisable() => StageDirector.EnemyDied -= OnEnemyDied;

    protected override void OnDestroy()
    {
        WatcherSkills.Cast -= OnSkillCast;
        if (lineMat) Destroy(lineMat);
        base.OnDestroy();
    }

    // ================= 평타 명중 =================

    void OnBoltHit(Enemy e, Vector3 at, float dmg)
    {
        ApplyEffects(e);
        if (L.Arcane(GrowthEffect.ArcaneChain)) Chain(e, dmg);
    }

    /// <summary>연소/빙결/저주 (켜져 있는 것만).</summary>
    void ApplyEffects(Enemy e)
    {
        if (!e.isActiveAndEnabled) return;
        Vector3 p = e.transform.position + Vector3.up;
        if (L.Arcane(GrowthEffect.ArcaneBurn)) { AddBurn(e); Mark(e, StatusKind.Burn, Time.time + S.burnDuration); }
        if (L.Arcane(GrowthEffect.ArcaneFreeze) && Random.value < S.freezeChance) { e.Freeze(S.freezeDuration); Vfx.Hit(p, S.freezeColor); Mark(e, StatusKind.Freeze, Time.time + S.freezeDuration); }
        if (L.Arcane(GrowthEffect.ArcaneCurse) && e.DamageTakenMul < S.curseDamageTaken && Random.value < S.curseChance) { e.DamageTakenMul = S.curseDamageTaken; Vfx.Hit(p, S.curseColor); Mark(e, StatusKind.Curse, float.MaxValue); }
    }

    void Chain(Enemy first, float dmg)
    {
        int n = 0;
        chainHit[n++] = first;
        Vector3 from = first.transform.position;
        float d = dmg * S.chainDamageMul, rr = S.chainRadius * S.chainRadius;
        int count = Mathf.Min(S.chainCount, ChainMax - 1);
        for (int k = 0; k < count; k++)
        {
            Enemy next = null; float best = rr;
            var list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (System.Array.IndexOf(chainHit, e, 0, n) >= 0) continue;
                Vector3 v = e.transform.position - from; v.y = 0f;
                float sq = v.sqrMagnitude;
                if (sq < best) { best = sq; next = e; }
            }
            if (!next) break;
            chainHit[n++] = next;
            Vector3 to = next.transform.position;
            Lightning(from, to);
            if (L.arcaneAll) ApplyEffects(next); // 모든 효과: 연쇄로 맞은 적에게도 전부
            next.Hit(d, to);
            from = to;
        }
        System.Array.Clear(chainHit, 0, n);
    }

    // ================= 연소 =================

    void AddBurn(Enemy e)
    {
        foreach (var b in burns) if (b.enemy == e) { b.left = S.burnDuration; return; } // 다시 맞으면 시간만 갱신
        var nb = freeBurns.Count > 0 ? freeBurns.Pop() : new Burn();
        nb.enemy = e; nb.left = S.burnDuration; nb.tickLeft = S.burnTick;
        nb.perTick = S.burnTotalDamage / Mathf.Max(1f, Mathf.Round(S.burnDuration / Mathf.Max(0.05f, S.burnTick)));
        burns.Add(nb);
    }

    void RemoveBurn(int i)
    {
        var b = burns[i];
        burns[i] = burns[burns.Count - 1]; burns.RemoveAt(burns.Count - 1);
        b.enemy = null; freeBurns.Push(b);
    }

    void TickBurns(float dt)
    {
        for (int i = burns.Count - 1; i >= 0; i--)
        {
            var b = burns[i];
            if (!b.enemy || !b.enemy.isActiveAndEnabled || b.left <= 0f) { RemoveBurn(i); continue; }
            b.left -= dt;
            if ((b.tickLeft -= dt) > 0f) continue;
            b.tickLeft += S.burnTick;
            var e = b.enemy;
            e.Hit(b.perTick, e.transform.position); // 같은 자리에서 → 넉백 없음
            if (e.isActiveAndEnabled) Vfx.Hit(e.transform.position + Vector3.up * 0.9f, S.burnColor); // 불꽃 튐
            if (i < burns.Count && burns[i] == b && !e.isActiveAndEnabled) RemoveBurn(i);
        }
    }

    void OnEnemyDied(Enemy e, bool counted)
    {
        for (int i = burns.Count - 1; i >= 0; i--) if (burns[i].enemy == e) RemoveBurn(i);
        for (int i = marks.Count - 1; i >= 0; i--) if (marks[i].enemy == e) RemoveMark(i);
        for (int i = 0; i < orbs.Count; i++) if (orbs[i].target == e) orbs[i].target = null; // 남은 자리로 계속 날아감
    }

    // ================= 번개 표시 =================

    void Lightning(Vector3 a, Vector3 b)
    {
        var bolt = freeBolts.Count > 0 ? freeBolts.Pop() : NewBolt();
        a.y += 0.8f; b.y += 0.8f;
        Vector3 side = Vector3.Cross(b - a, Vector3.up).normalized;
        for (int i = 0; i < zig.Length; i++)
        {
            float k = i / (zig.Length - 1f);
            float off = i == 0 || i == zig.Length - 1 ? 0f : Random.Range(-0.35f, 0.35f);
            zig[i] = Vector3.Lerp(a, b, k) + side * off + Vector3.up * Random.Range(-0.15f, 0.15f);
        }
        bolt.line.SetPositions(zig);
        bolt.line.startColor = bolt.line.endColor = S.chainColor;
        bolt.line.gameObject.SetActive(true);
        bolt.until = Time.time + 0.15f;
        bolts.Add(bolt);
    }

    Bolt NewBolt()
    {
        if (!lineMat) lineMat = new Material(Shader.Find("Sprites/Default"));
        var go = new GameObject("ChainBolt");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = zig.Length; lr.widthMultiplier = 0.12f; lr.sharedMaterial = lineMat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
        return new Bolt { line = lr };
    }

    void TickBolts()
    {
        float now = Time.time;
        for (int i = bolts.Count - 1; i >= 0; i--)
        {
            if (now < bolts[i].until) continue;
            var b = bolts[i];
            b.line.gameObject.SetActive(false);
            bolts[i] = bolts[bolts.Count - 1]; bolts.RemoveAt(bolts.Count - 1);
            freeBolts.Push(b);
        }
    }

    // ================= 감시자: 거대 마법 투사체 =================

    void OnSkillCast(WatcherSkillType t, WatcherSkillCategory c)
    {
        if (c == WatcherSkillCategory.유도 && isActiveAndEnabled && PlayerController.I) StartCoroutine(OrbRoutine(PlayerController.I.Position));
    }

    /// <summary>디코이/조종으로 끌려온 적(스킬을 쓴 자리 주변)을 골라 마법사 위에서 거대 투사체를 쏨.</summary>
    IEnumerator OrbRoutine(Vector3 center)
    {
        yield return new WaitForSeconds(S.orbDelay);
        if (!Ward.I) yield break;
        float dmg = GameManager.I.Stats.damage * S.orbDamageMul, rr = S.orbSearchRadius * S.orbSearchRadius;
        Vector3 from = Ward.I.Position + Vector3.up * 2.2f;
        int shots = 0;
        var list = Enemy.Active;
        for (int i = 0; i < list.Count && shots < S.orbCount; i++)
        {
            var e = list[i];
            Vector3 v = e.transform.position - center; v.y = 0f;
            if (v.sqrMagnitude > rr) continue;
            FireOrb(from, e, dmg);
            shots++;
        }
    }

    void FireOrb(Vector3 from, Enemy target, float dmg)
    {
        var t = freeOrbs.Count > 0 ? freeOrbs.Pop() : NewOrb();
        t.position = from;
        t.localScale = Vector3.one * S.orbSize;
        t.gameObject.SetActive(true);
        orbs.Add(new Orb { t = t, target = target, to = target.transform.position, dmg = dmg });
    }

    Transform NewOrb()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ArcaneOrb";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<Renderer>();
        var fxr = GameManager.I.Config.stage.fxPrefab ? GameManager.I.Config.stage.fxPrefab.GetComponent<Renderer>() : null;
        if (fxr) r.sharedMaterial = fxr.sharedMaterial;
        mpb ??= new MaterialPropertyBlock();
        mpb.SetColor(ColorId, S.orbColor);
        r.SetPropertyBlock(mpb);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    void TickOrbs(float dt)
    {
        for (int i = orbs.Count - 1; i >= 0; i--)
        {
            var o = orbs[i];
            if (o.target && o.target.isActiveAndEnabled) o.to = o.target.transform.position; // 따라감
            Vector3 dest = o.to + Vector3.up * 0.5f;
            o.t.position = Vector3.MoveTowards(o.t.position, dest, S.orbSpeed * dt);
            if ((o.t.position - dest).sqrMagnitude > 0.04f) continue;
            float r = S.orbRadius * GameManager.I.Stats.areaMul;
            Vector3 p = Ground.Snap(o.to);
            Vfx.Explosion(p, r, S.orbColor);
            SfxManager.Play(SfxManager.S.bombardImpact, p);
            StageDirector.I.DamageArea(p, r, o.dmg);
            o.t.gameObject.SetActive(false);
            freeOrbs.Push(o.t);
            orbs[i] = orbs[orbs.Count - 1]; orbs.RemoveAt(orbs.Count - 1);
        }
    }

    // ================= 디버프 표시 (적마다 종류별 1개, 풀 재사용) =================
    // 빙결 = 적을 감싼 하늘색 얼음 덩어리, 연소 = 머리 위에서 일렁이는 살구색 불씨(+ 피해마다 불꽃 튐), 저주 = 발밑에서 도는 라벤더 고리

    enum StatusKind { Freeze, Burn, Curse }
    class StatusMark { public Enemy enemy; public StatusKind kind; public float until; public Transform t; public float phase; }

    readonly List<StatusMark> marks = new List<StatusMark>(16);
    readonly Stack<Transform>[] freeMarkObjs = { new Stack<Transform>(8), new Stack<Transform>(8), new Stack<Transform>(8) };
    readonly Stack<StatusMark> freeMarks = new Stack<StatusMark>(16);

    void Mark(Enemy e, StatusKind k, float until)
    {
        foreach (var m in marks) if (m.enemy == e && m.kind == k) { m.until = Mathf.Max(m.until, until); return; } // 이미 있으면 시간만 갱신
        var mk = freeMarks.Count > 0 ? freeMarks.Pop() : new StatusMark();
        mk.enemy = e; mk.kind = k; mk.until = until; mk.phase = Random.value * 6f;
        var pool = freeMarkObjs[(int)k];
        mk.t = pool.Count > 0 ? pool.Pop() : NewMarkObj(k);
        mk.t.gameObject.SetActive(true);
        marks.Add(mk);
        PlaceMark(mk, Time.time);
    }

    Transform NewMarkObj(StatusKind k)
    {
        var go = GameObject.CreatePrimitive(k == StatusKind.Freeze ? PrimitiveType.Cube : k == StatusKind.Burn ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
        go.name = "Status_" + k;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<Renderer>();
        var fxr = GameManager.I.Config.stage.fxPrefab ? GameManager.I.Config.stage.fxPrefab.GetComponent<Renderer>() : null;
        if (fxr) r.sharedMaterial = fxr.sharedMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        mpb ??= new MaterialPropertyBlock();
        Color c = k == StatusKind.Freeze ? S.freezeColor : k == StatusKind.Burn ? S.burnColor : S.curseColor;
        c.a = k == StatusKind.Freeze ? 0.45f : 0.75f;
        mpb.SetColor(ColorId, c);
        r.SetPropertyBlock(mpb);
        if (k == StatusKind.Freeze) go.transform.localScale = new Vector3(1.1f, 1.35f, 1.1f);
        else if (k == StatusKind.Curse) go.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);
        return go.transform;
    }

    void PlaceMark(StatusMark m, float now)
    {
        Vector3 p = m.enemy.transform.position;
        float w = now * 6f + m.phase;
        switch (m.kind)
        {
            case StatusKind.Freeze:
                m.t.SetPositionAndRotation(p + Vector3.up * 0.65f, Quaternion.Euler(0f, 30f + m.phase * 20f, 8f));
                break;
            case StatusKind.Burn:
                m.t.position = p + Vector3.up * (1.45f + Mathf.Sin(w) * 0.08f);
                m.t.localScale = Vector3.one * (0.32f + 0.06f * Mathf.Sin(w * 1.7f));
                break;
            default: // Curse
                m.t.SetPositionAndRotation(p + Vector3.up * 0.05f, Quaternion.Euler(0f, w * 15f, 0f));
                break;
        }
    }

    void RemoveMark(int i)
    {
        var m = marks[i];
        marks[i] = marks[marks.Count - 1]; marks.RemoveAt(marks.Count - 1);
        m.t.gameObject.SetActive(false);
        freeMarkObjs[(int)m.kind].Push(m.t);
        m.enemy = null; m.t = null;
        freeMarks.Push(m);
    }

    void TickMarks()
    {
        float now = Time.time;
        for (int i = marks.Count - 1; i >= 0; i--)
        {
            var m = marks[i];
            if (!m.enemy || !m.enemy.isActiveAndEnabled || now > m.until) { RemoveMark(i); continue; }
            PlaceMark(m, now);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        TickBurns(dt);
        TickBolts();
        TickOrbs(dt);
        TickMarks();
    }
}
