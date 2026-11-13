using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 강 파생형 "상류의 댐"의 규칙 컴포넌트 (전용 씬 Region_River_Dam에 배치).
/// 배치는 모두 씬 오브젝트: 세로로 긴 강바닥 발판(지형·보더 없음, 둘레는 강물), 위쪽 끝의 댐 벽과 수문 장치(SluiceGate), 물살(FloodWave).
/// 규칙:
///  - 적은 아래쪽(하류) 끝에서 나타나 위쪽 수문 장치로 올라와 부수려 함 (생명체가 가까우면 생명체를 노림). 발판 밖(강물)에 빠지면 환경사.
///  - 수문 장치는 시간이 지나면 차오르고, 다 차오르면 감시자가 가까이 가서 상호작용(F)으로 엶 → 물살이 아래로 내려가며 지나간 적을 쓸어 감 (환경사: 처치 수 제외).
///  - 감시자 조작이 메인: 감시자만 조작(WASD)으로 고정, 작은 생명체는 수문 앞 자리에서 움직이지 않고 자동 공격 (포털이 열리면 포털로).
///  - 클리어: defendTime초 동안 장치를 하나라도 지키고 + 처치 목표(× killTargetMul). 장치가 모두 부서지면 패배.
///  - HUD: 남은 장치 수 / 남은 방어 시간 (수치만).
/// 수치는 VariantConfig.dam.
/// </summary>
public class RiverDamVariant : MonoBehaviour
{
    [Tooltip("물살 (씬 오브젝트, 꺼 두었다가 수문이 열리면 내려감)")] public Transform floodWave;

    StageDirector stage;
    RiverDamSettings s;
    float elapsed, gateZ, halfW, halfL, checkTick;
    bool lost;
    readonly List<Enemy> tmp = new List<Enemy>(64);
    HashSet<Enemy> seen = new HashSet<Enemy>(), seenNext = new HashSet<Enemy>();

    class Flood { public float x, front, half; public Transform wave; }
    readonly List<Flood> floods = new List<Flood>(4);

    public int Intact { get { int n = 0; foreach (var g in SluiceGate.Gates) if (!g.Broken) n++; return n; } }

    public void Init(StageDirector st, RiverDamSettings settings)
    {
        stage = st; s = settings;
        halfW = s.deckWidth * 0.5f; halfL = s.deckLength * 0.5f;
        gateZ = halfL - s.gateInset;
        float hp = st.EnemyHp * s.gateHpMul;
        foreach (var g in SluiceGate.Gates) { g.rule = this; g.Init(hp); }
        if (floodWave) floodWave.gameObject.SetActive(false);

        // 감시자 조작이 메인: 생명체는 수문 앞 자리에서 지키기만
        var ward = Ward.I;
        if (ward != null)
        {
            ward.ForceControl(Ward.Command.Auto, Ward.Control.WatcherOnly);
            ward.Teleport(new Vector3(0f, 0f, gateZ - 5f));
        }
        Ward.ControlLocked = true;
        Ward.ManualOnly = true;
        Ward.Hazard = p => !OnDeck(p, 0.4f);
        var pc = PlayerController.I;
        if (pc) { var cc = pc.GetComponent<CharacterController>(); if (cc) cc.enabled = false; pc.transform.position = new Vector3(0f, pc.transform.position.y, gateZ - 9f); if (cc) cc.enabled = true; }

        Enemy.GoalOverride = EnemyGoal;
        st.SpawnPoint = () => new Vector3(Random.Range(-halfW + 2f, halfW - 2f), 0f, Random.Range(-halfL + 1.5f, -halfL + s.spawnDepth));
        st.PortalFilter = p => OnDeck(p, 3f) && p.z < gateZ - 4f;
        st.PortalFallback = () => new Vector3(0f, 0f, gateZ - 12f);
        st.MulKillTarget(s.killTargetMul);
        st.ExtraClearCondition = () => elapsed >= s.defendTime && Intact > 0;
        st.ExtraObjective = () =>
        {
            float left = Mathf.Max(0f, s.defendTime - elapsed);
            return left > 0f ? $"수문 {Intact}/{SluiceGate.Gates.Count}   {(int)left / 60}:{(int)left % 60:00}" : $"수문 {Intact}/{SluiceGate.Gates.Count}";
        };
        BuildNav();
    }

    void OnDestroy()
    {
        Ward.ControlLocked = false; Ward.ManualOnly = false; Ward.Hazard = null;
        Enemy.GoalOverride = null;
        if (stage) { stage.SpawnPoint = null; stage.PortalFilter = null; stage.PortalFallback = null; stage.ExtraClearCondition = null; stage.ExtraObjective = null; }
    }

    bool OnDeck(Vector3 p, float margin = 0f) => Mathf.Abs(p.x) <= halfW - margin && Mathf.Abs(p.z) <= halfL - margin;

    /// <summary>길찾기: 발판 밖(강물)은 막힘.</summary>
    void BuildNav()
    {
        var nav = stage.Nav;
        if (nav == null) return;
        nav.SetAllBlocked(true);
        for (float z = -halfL + 0.6f; z <= halfL - 0.6f; z += 1.2f)
            for (float x = -halfW + 0.6f; x <= halfW - 0.6f; x += 1.2f)
                nav.SetBlocked(new Vector3(x, 0f, z), 0.05f, false);
    }

    SluiceGate NearestGate(Vector3 p)
    {
        SluiceGate best = null; float bestSq = float.MaxValue;
        foreach (var g in SluiceGate.Gates) { if (g.Broken) continue; Vector3 d = g.transform.position - p; d.y = 0f; if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = g; } }
        return best;
    }

    /// <summary>적: 생명체가 가까우면 생명체, 아니면 가장 가까운 수문 장치로 (붙으면 제자리에서 부숨).</summary>
    Vector3? EnemyGoal(Enemy e)
    {
        Vector3 p = e.transform.position;
        var ward = Ward.I;
        if (ward != null && ward.Alive) { Vector3 dw = ward.Position - p; dw.y = 0f; if (dw.sqrMagnitude < s.wardAggro * s.wardAggro) return null; }
        var g = NearestGate(p);
        if (!g) return null;
        Vector3 d = g.transform.position - p; d.y = 0f;
        if (d.sqrMagnitude <= s.gateReach * s.gateReach) return p; // 붙어서 부수는 중
        return g.transform.position - d.normalized * (s.gateReach * 0.7f);
    }

    /// <summary>수문을 엶 → 물살이 내려감.</summary>
    public void Open(SluiceGate g)
    {
        if (!g.Ready || GameManager.I.State != GameState.Playing) return;
        g.charge = 0f;
        g.openLeft = s.floodTime + 0.6f;
        var f = new Flood { x = g.transform.position.x, front = gateZ, half = s.floodHalfWidth > 0f ? s.floodHalfWidth : halfW };
        if (floodWave)
        {
            f.wave = Instantiate(floodWave, floodWave.parent);
            f.wave.gameObject.SetActive(true);
            f.wave.position = new Vector3(s.floodHalfWidth > 0f ? f.x : 0f, 0f, gateZ);
            f.wave.localScale = new Vector3(f.half * 2f, f.wave.localScale.y, f.wave.localScale.z);
        }
        floods.Add(f);
        Vfx.Pickup(g.transform.position + Vector3.up * 1.5f, s.waterColor, 24);
        g.Refresh();
    }

    void Update()
    {
        if (s == null || GameManager.I.State != GameState.Playing || lost) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        elapsed += dt;
        if (elapsed >= s.defendTime && (checkTick -= dt) <= 0f) { checkTick = 0.5f; stage.CheckClear(); }

        // 수문 장치: 차오름 / 붙은 적에게 부서짐
        foreach (var g in SluiceGate.Gates)
        {
            if (g.Broken) continue;
            if (g.openLeft > 0f) g.openLeft -= dt;
            else if (g.charge < 1f) g.charge = Mathf.Min(1f, g.charge + dt / Mathf.Max(0.1f, s.gateCharge));
            int n = 0;
            foreach (var e in Enemy.Active) { Vector3 d = e.transform.position - g.transform.position; d.y = 0f; if (d.sqrMagnitude <= s.gateReach * s.gateReach) n++; }
            if (n > 0)
            {
                g.hp -= n * s.gateDpsPerEnemy * dt;
                if (Random.value < dt * 4f) Vfx.Hit(g.transform.position + Vector3.up * 1.2f, s.gateColor);
                if (g.hp <= 0f) { g.hp = 0f; g.ShowBroken(); Vfx.Death(g.transform.position + Vector3.up, s.gateColor); }
            }
            g.Refresh();
        }
        if (SluiceGate.Gates.Count > 0 && Intact == 0) { lost = true; GameManager.I.GameOver(); return; } // 장치를 모두 잃으면 패배

        TickFloods(dt);

        // 강물에 빠진 적은 환경사 (막 생긴 적은 발판 위로)
        seenNext.Clear(); tmp.Clear(); tmp.AddRange(Enemy.Active);
        foreach (var e in tmp)
        {
            seenNext.Add(e);
            Vector3 p = e.transform.position;
            if (OnDeck(p)) continue;
            if (!seen.Contains(e)) { var q = new Vector3(Mathf.Clamp(p.x, -halfW + 1f, halfW - 1f), p.y, Mathf.Clamp(p.z, -halfL + 1f, halfL - 1f)); e.transform.position = q; if (e.TryGetComponent(out Rigidbody rb)) rb.position = q; continue; }
            Splash(p, 8);
            e.EnvironmentKill();
        }
        (seen, seenNext) = (seenNext, seen);
    }

    /// <summary>물살: 수문 줄에서 아래 끝까지 내려가며 지나간 적을 쓸어 감 (환경사).</summary>
    void TickFloods(float dt)
    {
        float speed = s.deckLength / Mathf.Max(0.2f, s.floodTime);
        for (int i = floods.Count - 1; i >= 0; i--)
        {
            var f = floods[i];
            float prev = f.front;
            f.front -= speed * dt;
            if (f.wave) f.wave.position = new Vector3(f.wave.position.x, 0f, f.front);
            tmp.Clear(); tmp.AddRange(Enemy.Active);
            foreach (var e in tmp)
            {
                Vector3 p = e.transform.position;
                if (p.z > prev || p.z < f.front - 1f || Mathf.Abs(p.x - (s.floodHalfWidth > 0f ? f.x : 0f)) > f.half) continue;
                Splash(p, 10);
                e.EnvironmentKill();
            }
            if (f.front < -halfL - 4f) { if (f.wave) Destroy(f.wave.gameObject); floods.RemoveAt(i); }
        }
    }

    void Splash(Vector3 p, int n) => Vfx.Pickup(new Vector3(p.x, 0.3f, p.z), new Color(0.85f, 0.95f, 1f), n);
}
