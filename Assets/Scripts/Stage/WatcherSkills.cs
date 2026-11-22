using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 감시자 스킬 (전투 스테이지마다 StageDirector가 추가). 장착한 2칸을 키(WatcherSkillConfig.slotKeys, 기본 1/2)로 사용.
/// 코스트: 런 동안 GameManager.SkillCost에 유지 (씬이 바뀌어도 그대로), 적을 처치하면 조금씩 충전, 최대 maxCost.
/// 재사용 대기: 이 컴포넌트에만 있어서 씬이 바뀌면 초기화.
/// 모든 수치는 WatcherSkillConfig (GameConfig.skills). 스킬 변경은 로비/보너스 스테이지의 장착 창에서만.
/// </summary>
public class WatcherSkills : MonoBehaviour
{
    public static WatcherSkills I { get; private set; }
    /// <summary>폭격 분류 스킬이 발동됨 (생명체 성장: 토끼 당근 폭격 등).</summary>
    public static event System.Action<WatcherSkillType> BombardCast;
    public static void RaiseBombardCast(WatcherSkillType t) => BombardCast?.Invoke(t);
    /// <summary>스킬이 발동됨 (종류, 분류). 생명체 성장: 마법사는 유도(디코이) 분류와 연계.</summary>
    public static event System.Action<WatcherSkillType, WatcherSkillCategory> Cast;

    GameManager gm;
    WatcherSkillConfig cfg;
    StageDirector stage;
    readonly Dictionary<WatcherSkillType, float> readyAt = new Dictionary<WatcherSkillType, float>();
    bool busy; // 조종/3중 폭격 지정 중

    public WatcherSkillConfig Config => cfg;
    /// <summary>스킬 사용 불가 (발광 균사 동굴처럼 감시자가 없는 스테이지).</summary>
    public static bool Blocked => BombardSkill.Disabled;

    void Awake()
    {
        I = this;
        gm = GameManager.I;
        cfg = gm.Config.skills;
        stage = GetComponent<StageDirector>();
        SaveData.EnsureSkillLoadout();
        cfg.SanitizeLoadout();
        StageDirector.EnemyDied += OnEnemyDied;
        Ward.HitByEnemy += OnWardHit;
        Ward.AttackIntercept = ParryIntercept;
        Enemy.Lure = Lure;
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        StageDirector.EnemyDied -= OnEnemyDied;
        Ward.HitByEnemy -= OnWardHit;
        if (Ward.AttackIntercept == (System.Func<Enemy, float, bool>)ParryIntercept) Ward.AttackIntercept = null;
        Ward.AttackIntercept = null;
        Enemy.Lure = null;
        if (busy) { Time.timeScale = 1f; if (CameraFollow.I) CameraFollow.I.SetOverview(false); }
    }

    // ---- 장착 / 상태 ----
    public static WatcherSkillDef Equipped(int slot)
    {
        var c = GameManager.I ? GameManager.I.Config.skills : null;
        if (c == null) return null;
        int t = SaveData.EquippedSkill(slot);
        return t < 0 ? null : c.Get((WatcherSkillType)t);
    }

    public static bool IsEquipped(WatcherSkillType t)
    {
        var c = GameManager.I ? GameManager.I.Config.skills : null;
        if (c == null) return false;
        for (int i = 0; i < c.slotCount; i++) if (SaveData.EquippedSkill(i) == (int)t) return true;
        return false;
    }

    public static bool IsEquippedAndReady(WatcherSkillType t) => I != null && !Blocked && IsEquipped(t) && I.CanUse(t);

    public float CooldownLeft(WatcherSkillType t) => readyAt.TryGetValue(t, out var r) ? Mathf.Max(0f, r - Time.time) : 0f;

    public bool CanUse(WatcherSkillType t)
    {
        var d = cfg.Get(t);
        return d != null && CooldownLeft(t) <= 0f && gm.SkillCost >= d.cost;
    }

    /// <summary>코스트·재사용 대기 소모. 모자라면 false.</summary>
    public bool Consume(WatcherSkillType t)
    {
        if (!CanUse(t)) return false;
        gm.SkillCost -= cfg.Get(t).cost;
        readyAt[t] = Time.time + cfg.Cooldown(t);
        return true;
    }

    /// <summary>발동을 취소했을 때 되돌림 (폭격 조준 취소 등).</summary>
    public void Refund(WatcherSkillType t)
    {
        var d = cfg.Get(t);
        if (d == null) return;
        gm.SkillCost = Mathf.Min(cfg.maxCost, gm.SkillCost + d.cost);
        readyAt[t] = 0f;
    }

    void OnEnemyDied(Enemy e, bool counted)
    {
        if (!counted && !cfg.chargeFromEnvironmentKills) return;
        gm.SkillCost = Mathf.Min(cfg.maxCost, gm.SkillCost + cfg.costPerKill);
    }

    void Update()
    {
        TickDecoys();
        TickChaos();
        TickThorns();
        if (Blocked || busy || gm.State != GameState.Playing || gm.InputBlocked) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        for (int i = 0; i < cfg.slotCount && i < cfg.slotKeys.Length; i++)
        {
            if (!kb[cfg.slotKeys[i]].wasPressedThisFrame) continue;
            var d = Equipped(i);
            if (d != null) Use(d.type);
        }
    }

    /// <summary>스킬 사용 (코스트/대기 확인 → 발동). 패링은 성공했을 때만 소모.</summary>
    public void Use(WatcherSkillType t)
    {
        if (Blocked || busy) return;
        if (t == WatcherSkillType.Parry) { if (CanUse(t)) StartParry(); return; }
        if (t == WatcherSkillType.Control && PickChaosTargets(false) == 0) { Vfx.Hit(WatcherPos + Vector3.up, new Color(0.6f, 0.6f, 0.65f)); return; } // 주변에 적이 없으면 안 씀 (코스트/대기 그대로)
        if (!Consume(t)) { Vfx.Hit(WatcherPos + Vector3.up, new Color(0.6f, 0.6f, 0.65f)); return; }
        switch (t)
        {
            case WatcherSkillType.Decoy: PlaceDecoy(); break;
            case WatcherSkillType.Control: PickChaosTargets(true); break;
            case WatcherSkillType.TripleBombard: StartCoroutine(TripleBombardRoutine()); break;
            case WatcherSkillType.Bombard: if (BombardSkill.I) BombardSkill.I.Trigger(); break;
            case WatcherSkillType.Thorns: StartThorns(); break;
        }
        var cat = cfg.Get(t)?.category;
        if (cat.HasValue) Cast?.Invoke(t, cat.Value);
        if (cat == WatcherSkillCategory.폭격) RaiseBombardCast(t);
    }

    static Vector3 WatcherPos => PlayerController.I ? Ground.Snap(Arena.Clamp(PlayerController.I.Position)) : Vector3.zero;

    /// <summary>적 끌어당기기 (Enemy.Lure): 혼돈 적은 다른 적에게, 디코이 범위 안의 적은 디코이로.</summary>
    Vector3? Lure(Enemy e)
    {
        if (chaos.ContainsKey(e)) return NearestOther(e, out _);
        foreach (var d in decoys)
        {
            if (Time.time < d.armAt) continue;
            if (d.lured != null) { if (d.lured.Contains(e)) return d.pos; continue; } // 최대 수 제한: 작동 순간 고른 적만
            Vector3 v = e.transform.position - d.pos; v.y = 0f;
            if (v.sqrMagnitude <= d.radius * d.radius) return d.pos;
        }
        return null;
    }

    // =====================================================================
    // 디코이: 감시자 위치에 장치 설치 → armDelay 뒤 범위 안의 적을 장치로 유도
    // =====================================================================
    class Decoy { public Vector3 pos; public float armAt, until, radius; public GameObject go; public HashSet<Enemy> lured; public bool picked; }
    readonly List<Enemy> decoyPick = new List<Enemy>(32);

    /// <summary>작동 순간 범위 안에서 디코이에 가까운 적부터 maxLured마리만 유도 대상으로 (0 = 무제한: 범위 안 적 모두, 나중에 들어온 적도). 죽어도 다시 채우지 않음.</summary>
    void PickLured(Decoy d)
    {
        d.picked = true;
        int max = cfg.decoy.maxLured;
        if (max <= 0) return;
        decoyPick.Clear();
        float r2 = d.radius * d.radius;
        foreach (var e in Enemy.Active) { Vector3 v = e.transform.position - d.pos; v.y = 0f; if (v.sqrMagnitude <= r2) decoyPick.Add(e); }
        decoyPick.Sort((a, b) => (a.transform.position - d.pos).sqrMagnitude.CompareTo((b.transform.position - d.pos).sqrMagnitude));
        d.lured = new HashSet<Enemy>();
        for (int i = 0; i < decoyPick.Count && i < max; i++) d.lured.Add(decoyPick[i]);
    }
    readonly List<Decoy> decoys = new List<Decoy>();

    void PlaceDecoy()
    {
        var s = cfg.decoy;
        Vector3 p = WatcherPos;
        float r = s.radius * cfg.EffectMul(WatcherSkillType.Decoy);
        var d = new Decoy { pos = p, armAt = Time.time + s.armDelay, until = Time.time + s.armDelay + s.duration, radius = r, go = SkillProps.Decoy(p, r) };
        decoys.Add(d);
        Vfx.Pillar(p, new Color(1f, 0.85f, 0.4f));
    }

    void TickDecoys()
    {
        for (int i = decoys.Count - 1; i >= 0; i--)
        {
            var d = decoys[i];
            if (!d.picked && Time.time >= d.armAt) PickLured(d);
            if (d.go) SkillProps.PulseDecoy(d.go, Time.time >= d.armAt);
            if (Time.time < d.until) continue;
            if (d.go) Destroy(d.go);
            decoys.RemoveAt(i);
        }
    }

    // =====================================================================
    // 조종: 감시자 주변 반경 안의 적 중 무작위로 targets마리를 혼돈 상태로 (확정). 혼돈 적이 다른 적을 때리면 자신은 즉사
    //       (처치 수 제외), 맞은 적은 낮은 피해, 그 피해로 죽으면 처치 수 포함
    // =====================================================================
    readonly Dictionary<Enemy, float> chaos = new Dictionary<Enemy, float>();

    readonly List<Enemy> chaosPool = new List<Enemy>(32);

    /// <summary>감시자 주변 반경 안의 (혼돈이 아닌) 적 중 무작위로 targets마리. apply = false면 후보 수만 셈.</summary>
    int PickChaosTargets(bool apply)
    {
        var s = cfg.control;
        Vector3 c = WatcherPos;
        float r2 = s.radius * s.radius;
        chaosPool.Clear();
        foreach (var e in Enemy.Active)
        {
            if (chaos.ContainsKey(e)) continue;
            Vector3 v = e.transform.position - c; v.y = 0f;
            if (v.sqrMagnitude <= r2) chaosPool.Add(e);
        }
        if (!apply) return chaosPool.Count;
        float dur = s.chaosDuration * cfg.EffectMul(WatcherSkillType.Control);
        int n = Mathf.Min(Mathf.Max(1, s.targets), chaosPool.Count);
        for (int i = 0; i < n; i++)
        {
            int k = Random.Range(i, chaosPool.Count);
            (chaosPool[i], chaosPool[k]) = (chaosPool[k], chaosPool[i]);
            var e = chaosPool[i];
            chaos[e] = Time.time + dur;
            Vfx.Pillar(e.transform.position, new Color(0.8f, 0.5f, 1f));
        }
        return n;
    }

    Vector3? NearestOther(Enemy self, out Enemy other)
    {
        other = null; float best = float.MaxValue;
        Vector3 p = self.transform.position;
        foreach (var e in Enemy.Active)
        {
            if (e == self || chaos.ContainsKey(e)) continue;
            Vector3 v = e.transform.position - p; v.y = 0f;
            if (v.sqrMagnitude < best) { best = v.sqrMagnitude; other = e; }
        }
        return other ? other.transform.position : (Vector3?)p;
    }

    readonly List<Enemy> tmp = new List<Enemy>(16);
    void TickChaos()
    {
        if (chaos.Count == 0) return;
        float hit2 = cfg.control.chaosHitRange * cfg.control.chaosHitRange;
        tmp.Clear(); tmp.AddRange(chaos.Keys);
        foreach (var e in tmp)
        {
            if (!e || !e.isActiveAndEnabled || Time.time > chaos[e]) { chaos.Remove(e); continue; }
            if (Random.value < Time.deltaTime * 3f) Vfx.Pickup(e.transform.position + Vector3.up, new Color(0.8f, 0.5f, 1f), 1);
            NearestOther(e, out var victim);
            if (!victim) continue;
            Vector3 v = victim.transform.position - e.transform.position; v.y = 0f;
            if (v.sqrMagnitude > hit2) continue;
            chaos.Remove(e);
            Vector3 at = e.transform.position;
            victim.Hit(e.ContactDamage * cfg.control.chaosDamageMul, at); // 그 피해로 죽으면 처치 수 포함
            e.EnvironmentKill();                                           // 혼돈 적 자신은 즉사 (처치 수 제외)
        }
    }

    // =====================================================================
    // 3중 폭격: 탑뷰 강제 + 느려진 시간 → 클릭한 곳마다 delay 뒤 폭격 (points곳)
    // =====================================================================
    IEnumerator TripleBombardRoutine()
    {
        var s = cfg.tripleBombard;
        busy = true;
        float prevScale = Time.timeScale;
        Time.timeScale = s.aimTimeScale;
        gm.InputLocked = true;
        if (CameraFollow.I) CameraFollow.I.SetOverview(true);
        int placed = 0;
        float r = s.radius * GameManager.I.Stats.areaMul;
        float dmg = GameManager.I.Stats.companionDamage * s.damageMul * cfg.EffectMul(WatcherSkillType.TripleBombard);
        yield return null;
        while (placed < s.points && gm.State == GameState.Playing)
        {
            var mouse = Mouse.current; var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) break;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && GroundPoint(mouse, out var g))
            {
                g = Ground.Snap(Arena.Clamp(g));
                placed++;
                StartCoroutine(Blast(g, r, dmg, s.delay));
            }
            yield return null;
        }
        if (placed == 0) Refund(WatcherSkillType.TripleBombard); // 하나도 안 골랐으면 되돌림
        if (CameraFollow.I) CameraFollow.I.SetOverview(false);
        gm.InputLocked = false;
        if (gm.State == GameState.Playing) Time.timeScale = prevScale > 0f ? prevScale : 1f;
        busy = false;
    }

    IEnumerator Blast(Vector3 p, float r, float dmg, float delay)
    {
        var mark = SkillProps.Ring(p, r, new Color(1f, 0.55f, 0.3f));
        yield return new WaitForSeconds(delay);
        if (mark) Destroy(mark);
        Vfx.Explosion(p, r, new Color(1f, 0.6f, 0.3f)); SfxManager.Play(SfxManager.S.bombardImpact, p);
        stage.DamageArea(p, r, dmg);
    }

    static bool GroundPoint(Mouse mouse, out Vector3 p)
    {
        p = default;
        var cf = CameraFollow.I;
        if (cf == null || cf.Cam == null) return false;
        var ray = cf.Cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float dist)) return false;
        p = ray.GetPoint(dist);
        return true;
    }

    // =====================================================================
    // 가시: 생명체를 때린 적에게 받은 피해 × reflectFrac 반사 (reflects회). 반사로 죽은 자리에 가시 (spikeLife초, 밟은 적 피해)
    // =====================================================================
    int thornsLeft; float thornsUntil, thornsFx;
    class Spike { public Vector3 pos; public float until, tick; public GameObject go; }
    readonly List<Spike> spikes = new List<Spike>();

    void StartThorns()
    {
        thornsLeft = cfg.thorns.reflects;
        thornsUntil = Time.time + cfg.thorns.maxDuration;
        if (Ward.I) Vfx.Pillar(Ward.I.Position, new Color(0.55f, 0.9f, 0.55f));
    }

    void OnWardHit(Enemy e, float taken)
    {
        if (thornsLeft <= 0 || Time.time > thornsUntil || !e || !e.isActiveAndEnabled) return;
        thornsLeft--;
        Vector3 at = e.transform.position;
        e.Hit(taken * cfg.thorns.reflectFrac * cfg.EffectMul(WatcherSkillType.Thorns), Ward.I ? Ward.I.Position : at);
        if (!e.isActiveAndEnabled) // 반사로 죽음 → 가시
            spikes.Add(new Spike { pos = Ground.Snap(at), until = Time.time + cfg.thorns.spikeLife, go = SkillProps.Spikes(Ground.Snap(at), cfg.thorns.spikeRadius) });
    }

    void TickThorns()
    {
        if (thornsLeft > 0 && Time.time <= thornsUntil && Ward.I && (thornsFx -= Time.deltaTime) <= 0f) { thornsFx = 0.4f; Vfx.Pickup(Ward.I.Position + Vector3.up * 0.6f, new Color(0.55f, 0.9f, 0.55f), 2); }
        float r2 = cfg.thorns.spikeRadius * cfg.thorns.spikeRadius;
        for (int i = spikes.Count - 1; i >= 0; i--)
        {
            var s = spikes[i];
            if (Time.time > s.until) { if (s.go) Destroy(s.go); spikes.RemoveAt(i); continue; }
            if ((s.tick -= Time.deltaTime) > 0f) continue;
            s.tick = 0.5f;
            tmp.Clear(); tmp.AddRange(Enemy.Active);
            foreach (var e in tmp) { Vector3 v = e.transform.position - s.pos; v.y = 0f; if (v.sqrMagnitude <= r2) e.Hit(cfg.thorns.spikeDps * 0.5f, s.pos); }
        }
    }

    // =====================================================================
    // 패링: 키를 누른 뒤 window초 안에 적이 생명체를 공격하면 성공 → 그 공격 막음, 그 적 사망(처치 수 포함),
    //       생명체 invulnerable초 무적, 주변 적 넉백. 성공했을 때만 코스트·대기 소모
    // =====================================================================
    float parryUntil = -1f;

    void StartParry()
    {
        parryUntil = Time.time + cfg.parry.window;
        if (Ward.I) Vfx.Pickup(Ward.I.Position + Vector3.up * 0.8f, new Color(0.9f, 0.95f, 1f), 6);
    }

    bool ParryIntercept(Enemy e, float dmg)
    {
        if (Time.time > parryUntil || !Ward.I || !Consume(WatcherSkillType.Parry)) return false;
        parryUntil = -1f;
        var w = Ward.I;
        w.Shield(cfg.parry.invulnerable * cfg.EffectMul(WatcherSkillType.Parry));
        Vector3 at = e.transform.position;
        e.Hit(1e6f, w.Position);
        stage.RadialForce(w.Position, cfg.parry.knockRadius, cfg.parry.knockSpeed, 0f);
        Vfx.Nova(w.Position, cfg.parry.knockRadius, new Color(0.9f, 0.95f, 1f));
        return true;
    }
}
