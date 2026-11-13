using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버섯 숲 파생형 "발광 균사 동굴"의 규칙 컴포넌트 (전용 씬 Region_Mushroom_Mycelium에 배치).
/// 배치는 모두 씬 오브젝트: 어두운 동굴 조명, 균사 덩어리(MyceliumPatch: 가닥 + 실제 빛 + 포자 파티클), 회수 물품 포자 결정(SporeCrystal).
/// 규칙:
///  - 조작: 작은 생명체를 직접 조작 (WASD/화살표), 명령 전환 없음, 입력이 없으면 제자리. 감시자 표시 전부 없음, 폭격 불가.
///  - 적은 잠들어 있음 — 쫓지 않고 제자리 (접촉 피해 없음). 맵 전체에 고르게 생김 (생명체에게서 멀리, 다른 적과 떨어진 자리).
///  - 생명체가 균사를 밟으면 빛이 번져 나가고(실제 빛 범위 + 퍼지는 포자 고리), 빛이 닿은 적이 깨어남. 잠든 적에게 부딪히거나 맞히면 깨어남.
///  - 깨어난 적은 평소처럼 생명체를 쫓아옴.
///  - 생명체 자동 공격·저항 파동은 깨어난 적만 노리고, 깨어난 적이 없으면 아예 안 함. 동반자 능력도 깨어난 적이 있을 때만.
///  - 클리어: 포자 결정 crystalsToCollect개 회수 (처치 수와 상관없음). HUD는 "포자 n/N" (수치만).
/// 수치는 VariantConfig.mycelium.
/// </summary>
public class MyceliumCaveVariant : MonoBehaviour
{
    StageDirector stage;
    MyceliumCaveSettings s;
    readonly HashSet<Enemy> awake = new HashSet<Enemy>();
    readonly List<Enemy> tmp = new List<Enemy>(64);
    readonly List<Vector4> waves = new List<Vector4>(8); // xyz = 중심, w = 경과 시간
    GameObject commandHud;
    int collected, goal;

    public int AwakeCount { get; private set; }
    public int Collected => collected;

    public void Init(StageDirector st, MyceliumCaveSettings settings)
    {
        stage = st; s = settings;
        var ward = Ward.I;
        if (ward != null) ward.ForceControl(Ward.Command.Auto, Ward.Control.CreatureOnly); // 생명체 직접 조작 (WASD)
        Ward.ControlLocked = true;
        Ward.ManualOnly = true;                      // 입력이 없으면 제자리
        Ward.AttackAllowed = () => AwakeCount > 0;   // 발각됐을 때만 공격
        st.TargetFilter = e => awake.Contains(e);    // 공격 대상은 깨어난 적만
        Enemy.Damaged += OnEnemyDamaged;             // 어떤 공격이든 잠든 적이 맞으면 깨어남
        PlayerController.Hidden = true;              // 감시자 표시 없음
        BombardSkill.Disabled = true;                // 폭격 불가
        Enemy.GoalOverride = e => awake.Contains(e) ? (Vector3?)null : e.transform.position; // 잠든 적은 제자리
        var hud = FindAnyObjectByType<CommandHud>();
        if (hud) { commandHud = hud.gameObject; commandHud.SetActive(false); } // 명령/조작 전환 UI 숨김 (전환 불가)

        // 적 분포: 맵 전체에 고르게
        st.MaxAlive = s.maxEnemies;
        st.SpawnPoint = SpreadSpawn;

        // 클리어: 포자 결정 회수
        goal = Mathf.Min(s.crystalsToCollect, SporeCrystal.All.Count);
        st.IgnoreKillTarget = true;
        st.ExtraClearCondition = () => collected >= goal;
        st.ExtraObjective = () => $"포자 {collected}/{goal}";

        foreach (var p in MyceliumPatch.All) { p.transform.position = Ground.Snap(p.transform.position); p.Setup(s.idleLight, s.idleRange, s.burstLight); }
        foreach (var c in SporeCrystal.All) c.transform.position = Ground.Snap(c.transform.position);
    }

    void OnDestroy()
    {
        Ward.ControlLocked = false;
        Ward.ManualOnly = false;
        Ward.AttackAllowed = null;
        Enemy.Damaged -= OnEnemyDamaged;
        PlayerController.Hidden = false;
        BombardSkill.Disabled = false;
        Enemy.GoalOverride = null;
        if (stage) { stage.TargetFilter = null; stage.SpawnPoint = null; stage.IgnoreKillTarget = false; stage.ExtraClearCondition = null; stage.ExtraObjective = null; }
        if (commandHud) commandHud.SetActive(true);
    }

    void OnEnemyDamaged(Enemy e) { if (e && !awake.Contains(e)) Wake(e); }

    /// <summary>적이 생길 자리: 걸을 수 있는 곳 중 생명체에게서 멀고, 다른 적과 가장 떨어진 후보.</summary>
    Vector3 SpreadSpawn()
    {
        var nav = stage.Nav;
        Vector3 w = Ward.I != null ? Ward.I.Position : Vector3.zero;
        Vector3 best = Arena.RandomPoint(0.9f); float bestScore = -1f;
        float minW = s.spawnMinWardDistance * s.spawnMinWardDistance;
        for (int i = 0; i < Mathf.Max(1, s.spawnCandidates); i++)
        {
            Vector3 p = Arena.RandomPoint(0.9f);
            if (nav != null) p = nav.NearestFree(p);
            Vector3 dw = p - w; dw.y = 0f;
            if (dw.sqrMagnitude < minW) continue;
            float near = float.MaxValue;
            foreach (var e in Enemy.Active) { Vector3 d = e.transform.position - p; d.y = 0f; near = Mathf.Min(near, d.sqrMagnitude); }
            if (near > bestScore) { bestScore = near; best = p; }
        }
        return best;
    }

    void Update()
    {
        if (s == null || GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 죽거나 사라진 적은 깨어남 목록에서 뺌 (풀 재사용 대비)
        tmp.Clear();
        foreach (var e in awake) if (!e || !e.isActiveAndEnabled) tmp.Add(e);
        foreach (var e in tmp) awake.Remove(e);
        AwakeCount = awake.Count;

        var ward = Ward.I;
        if (ward == null || !ward.Alive) return;
        Vector3 w = ward.Position;

        // 포자 결정 회수
        float pr = s.pickupRadius * s.pickupRadius;
        for (int i = SporeCrystal.All.Count - 1; i >= 0; i--)
        {
            var c = SporeCrystal.All[i];
            Vector3 d = c.transform.position - w; d.y = 0f;
            if (d.sqrMagnitude > pr) continue;
            Vfx.Pickup(c.transform.position + Vector3.up, s.glowColor, 20);
            c.Collect();
            collected++;
            stage.CheckClear();
        }

        // 균사 밟기 → 빛 번짐 (실제 빛 + 퍼지는 포자 고리)
        foreach (var p in MyceliumPatch.All)
        {
            if (p.cooldown > 0f) { if ((p.cooldown -= dt) <= 0f) p.SetLit(true); continue; }
            float r = p.radius > 0f ? p.radius : s.patchRadius;
            Vector3 d = p.transform.position - w; d.y = 0f;
            if (d.sqrMagnitude > r * r) continue;
            p.cooldown = s.patchCooldown;
            p.Trigger(s.spreadRadius, s.spreadTime);
            Vector3 c = p.transform.position;
            waves.Add(new Vector4(c.x, c.y, c.z, 0f));
        }

        // 번지는 빛이 닿은 적이 깨어남
        for (int i = waves.Count - 1; i >= 0; i--)
        {
            var wv = waves[i];
            wv.w += dt;
            float rad = s.spreadRadius * Mathf.Clamp01(wv.w / Mathf.Max(0.05f, s.spreadTime));
            Vector3 c = new Vector3(wv.x, wv.y, wv.z);
            tmp.Clear(); tmp.AddRange(Enemy.Active);
            foreach (var e in tmp)
            {
                if (awake.Contains(e)) continue;
                Vector3 d = e.transform.position - c; d.y = 0f;
                if (d.sqrMagnitude <= rad * rad) Wake(e);
            }
            if (wv.w >= s.spreadTime) waves.RemoveAt(i); else waves[i] = wv;
        }

        // 잠든 적에게 부딪혀도 깨어남
        float bump = s.bumpWakeRadius * s.bumpWakeRadius;
        tmp.Clear(); tmp.AddRange(Enemy.Active);
        foreach (var e in tmp)
        {
            if (awake.Contains(e)) continue;
            Vector3 d = e.transform.position - w; d.y = 0f;
            if (d.sqrMagnitude <= bump) Wake(e);
        }
        AwakeCount = awake.Count;
    }

    void Wake(Enemy e)
    {
        if (!awake.Add(e)) return;
        AwakeCount = awake.Count;
        Vfx.Hit(e.transform.position + Vector3.up * 0.9f, s.glowColor);
        if (s.chainWakeRadius > 0f)
        {
            float r2 = s.chainWakeRadius * s.chainWakeRadius;
            var list = new List<Enemy>(Enemy.Active);
            foreach (var o in list) { if (awake.Contains(o)) continue; Vector3 d = o.transform.position - e.transform.position; d.y = 0f; if (d.sqrMagnitude <= r2) Wake(o); }
        }
    }

    /// <summary>적이 깨어 있는지 (테스트/디버그).</summary>
    public bool IsAwake(Enemy e) => awake.Contains(e);
}
