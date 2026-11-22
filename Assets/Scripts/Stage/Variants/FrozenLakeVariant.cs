using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 설원 파생형 "얼어붙은 호수"의 규칙 컴포넌트 (전용 씬 Region_Snow_FrozenLake에 배치).
/// 배치는 모두 씬 오브젝트: 평평한 호수 지형(전용 TerrainData), 얼음판(IceTile 프리팹, 일부는 처음부터 금 감), 스페이스 연타 UI(IceEscape 프리팹).
/// 규칙:
///  - 금 간 얼음 위에 생명체나 적이 머물면 깨짐 → 잠깐 흔들린 뒤 구멍(물). 시간이 지나면 다시 얼음. 균열은 주기적으로 새로 생김.
///  - 구멍에 들어간 적은 즉사 (처치로 침).
///  - 생명체가 빠지면 움직이지 못하고 물에 반쯤 잠김 → 화면 중앙 UI, 스페이스 연타로 탈출 (가까운 얼음 위로).
///    제한 시간을 넘기면 큰 피해를 입고 끌려 나옴. 빠진 동안 차가운 물에 조금씩 피해.
///  - 생명체가 빠져나오면(성공/실패 모두) 그 구멍은 바로 다시 얼고, 균열은 떨어진 다른 얼음판으로 옮겨 감. 적이 빠져 생긴 구멍은 그대로(시간 지나면 얼음).
/// 수치는 VariantConfig.frozenLake.
/// </summary>
public class FrozenLakeVariant : MonoBehaviour
{
    [Tooltip("스페이스 연타 UI (씬에 배치된 Canvas 프리팹 IceEscape)")] public SpaceMashUI escapeUI;

    StageDirector stage;
    FrozenLakeSettings s;
    readonly Dictionary<Vector2Int, IceTile> grid = new Dictionary<Vector2Int, IceTile>(512);
    float crackTimer, trapTime, waterTick;
    int presses;
    IceTile trappedIn, wardTile;

    public bool WardTrapped => trappedIn != null;
    public int Presses => presses;

    public void Init(StageDirector st, FrozenLakeSettings settings)
    {
        stage = st; s = settings;
        grid.Clear();
        foreach (var t in IceTile.All)
        {
            t.transform.position = Ground.Snap(t.transform.position);
            grid[Key(t.transform.position)] = t;
        }
        // 시작 지점 발밑은 멀쩡한 얼음
        if (Ward.I != null && grid.TryGetValue(Key(Ward.I.Position), out var start)) start.Set(IceTile.State.Intact);
        crackTimer = s.crackInterval;
        Ward.Hazard = p => { var t = TileAt(p); return t && t.Current == IceTile.State.Hole; }; // 생명체는 구멍을 피해 다님
        if (escapeUI) escapeUI.Hide();
    }

    void OnDestroy() { Ward.Hazard = null; if (Ward.I != null) Ward.I.SetTrapped(false); }

    Vector2Int Key(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / s.tileSize), Mathf.RoundToInt(p.z / s.tileSize));

    IceTile TileAt(Vector3 p)
    {
        if (!grid.TryGetValue(Key(p), out var t) || !t) return null;
        // 칸 안쪽인지 (얼음판을 씬에서 옮겼을 수도 있으니 실제 위치 기준)
        Vector3 d = p - t.transform.position;
        float h = s.tileSize * 0.5f;
        return Mathf.Abs(d.x) <= h && Mathf.Abs(d.z) <= h ? t : null;
    }

    void Update()
    {
        if (s == null || GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        SpawnCracks(dt);
        LoadFromWard(dt);
        LoadFromEnemies(dt);
        TickTiles(dt);
        TickTrapped(dt);
        if ((navTick -= dt) <= 0f) { navTick = 0.25f; UpdateNavCost(); }
    }

    float navTick;
    /// <summary>생명체 길찾기: 구멍/깨지는 얼음은 크게 돌아가고, 금 간 얼음은 약간 피함 (직접 조작 중엔 그대로 감).</summary>
    void UpdateNavCost()
    {
        var nav = stage.Nav;
        if (nav == null) return;
        float half = s.tileSize * 0.5f - 0.05f;
        foreach (var t in IceTile.All)
        {
            float cost = t.Current switch { IceTile.State.Hole => 500f, IceTile.State.Breaking => 500f, IceTile.State.Cracked => 40f, _ => 0f };
            nav.SetExtraCost(t.transform.position, half, cost);
        }
    }

    // ---- 균열 ----
    void SpawnCracks(float dt)
    {
        if ((crackTimer -= dt) > 0f) return;
        crackTimer = s.crackInterval;
        int cracked = 0;
        foreach (var t in IceTile.All) if (t.Current == IceTile.State.Cracked) cracked++;
        if (cracked >= s.maxCracked || IceTile.All.Count == 0) return;
        Vector3 w = Ward.I != null ? Ward.I.Position : Vector3.zero;
        for (int i = 0; i < 12; i++)
        {
            var t = IceTile.All[Random.Range(0, IceTile.All.Count)];
            if (t.Current != IceTile.State.Intact) continue;
            Vector3 d = t.transform.position - w; d.y = 0f;
            if (d.sqrMagnitude < 4.5f * 4.5f) continue; // 바로 발밑엔 안 생김
            t.Set(IceTile.State.Cracked);
            Vfx.Pickup(t.transform.position + Vector3.up * 0.2f, s.crackColor, 6);
            return;
        }
    }

    // ---- 무게 ----
    void LoadFromWard(float dt)
    {
        var ward = Ward.I;
        if (ward == null || !ward.Alive || ward.Trapped) return;
        var t = TileAt(ward.Position);
        // 금 간 얼음에서 내려오면 그 칸에 쌓인 무게는 풀림 (빨리 건너면 안 깨짐, 머물면 깨짐)
        if (t != wardTile && wardTile && wardTile.Current == IceTile.State.Cracked) wardTile.load = 0f;
        wardTile = t;
        if (!t) return;
        if (t.Current == IceTile.State.Hole) { Trap(ward, t); return; }
        if (t.Current == IceTile.State.Cracked && (t.load += dt / Mathf.Max(0.05f, s.wardBreakTime)) >= 1f) StartBreak(t);
    }

    readonly List<Enemy> drown = new List<Enemy>(16);
    void LoadFromEnemies(float dt)
    {
        drown.Clear();
        foreach (var e in Enemy.Active)
        {
            var t = TileAt(e.transform.position);
            if (!t) continue;
            if (t.Current == IceTile.State.Hole) drown.Add(e);
            else if (t.Current == IceTile.State.Cracked && (t.load += dt / Mathf.Max(0.05f, s.enemyBreakTime)) >= 1f) StartBreak(t);
        }
        foreach (var e in drown) // 물에 빠진 적은 즉사 (환경사: 처치 수에는 안 침)
        {
            Vector3 p = e.transform.position;
            Splash(p, 10);
            e.EnvironmentKill();
        }
    }

    void StartBreak(IceTile t) { t.Set(IceTile.State.Breaking); SfxManager.Play(SfxManager.S.iceCrack, t.transform.position); }

    void TickTiles(float dt)
    {
        foreach (var t in IceTile.All)
        {
            switch (t.Current)
            {
                case IceTile.State.Breaking:
                    t.timer += dt;
                    t.Shake(t.timer / Mathf.Max(0.01f, s.breakDelay));
                    if (t.timer >= s.breakDelay)
                    {
                        float tm = 0f;
                        t.Set(IceTile.State.Hole); t.timer = tm; SfxManager.Play(SfxManager.S.iceBreak, t.transform.position);
                        Splash(t.transform.position, 18);
                        Vfx.Death(t.transform.position + Vector3.up * 0.1f, s.iceColor); // 얼음 조각
                    }
                    break;
                case IceTile.State.Hole:
                    if (s.refreezeTime <= 0f || t == trappedIn) break;
                    if ((t.timer += dt) >= s.refreezeTime)
                    {
                        t.Set(IceTile.State.Intact);
                        Vfx.Pickup(t.transform.position + Vector3.up * 0.2f, s.iceColor, 8);
                    }
                    break;
            }
        }
    }

    void Splash(Vector3 p, int n) => Vfx.Pickup(Ground.Snap(p) + Vector3.up * 0.15f, new Color(0.85f, 0.95f, 1f), n);

    // ---- 생명체가 빠짐 ----
    void Trap(Ward ward, IceTile t)
    {
        trappedIn = t; presses = 0; trapTime = 0f; waterTick = 1f;
        ward.Teleport(t.transform.position);
        ward.SetTrapped(true);
        Splash(t.transform.position, 24);
    }

    void TickTrapped(float dt)
    {
        var ward = Ward.I;
        if (trappedIn == null) { if (escapeUI) escapeUI.Hide(); return; }
        if (ward == null || !ward.Alive) { trappedIn = null; if (ward != null) ward.SetTrapped(false); if (escapeUI) escapeUI.Hide(); return; }
        trapTime += dt;
        var kb = GameManager.I.InputBlocked ? null : Keyboard.current;
        bool pressed = kb != null && kb.spaceKey.wasPressedThisFrame;
        if (pressed) { presses++; Splash(ward.Position, 3); }
        if ((waterTick -= dt) <= 0f) { waterTick = 1f; ward.Drain(s.inWaterDps); }
        if (escapeUI) escapeUI.Show(presses, s.escapePresses, 1f - trapTime / Mathf.Max(0.01f, s.escapeTime), pressed);
        if (presses >= s.escapePresses) { Release(ward); return; }
        if (trapTime >= s.escapeTime)
        {
            ward.Drain(GameManager.I.Stats.wardMaxHp * s.failDamageFrac); // 제한 시간 초과: 큰 피해 + 끌려 나옴
            Vfx.Hit(ward.Position + Vector3.up * 0.5f, new Color(1f, 0.35f, 0.35f));
            if (ward.Alive) Release(ward);
        }
    }

    void Release(Ward ward)
    {
        var from = trappedIn;
        trappedIn = null;
        ward.SetTrapped(false);
        ward.Teleport(NearestSolid(from.transform.position));
        ward.Shield(0.8f); // 나오자마자 다시 빠지거나 맞지 않게 잠깐
        Splash(from.transform.position, 16);
        if (escapeUI) escapeUI.Hide();
        if (s.relocateCrackOnEscape) RelocateCrack(from, ward.Position);
    }

    /// <summary>생명체가 빠졌던 구멍은 바로 다시 얼고, 균열은 떨어진 다른 멀쩡한 얼음판으로 옮겨 감.</summary>
    void RelocateCrack(IceTile hole, Vector3 wardPos)
    {
        hole.Set(IceTile.State.Intact);
        Vfx.Pickup(hole.transform.position + Vector3.up * 0.2f, s.iceColor, 12);

        float minSq = s.relocateMinDist * s.relocateMinDist;
        float maxSq = s.relocateMaxDist > 0f ? s.relocateMaxDist * s.relocateMaxDist : float.MaxValue;
        var pick = PickCrackTarget(hole, wardPos, minSq, maxSq) ?? PickCrackTarget(hole, wardPos, minSq, float.MaxValue);
        if (pick == null) return;
        pick.Set(IceTile.State.Cracked);
        Vfx.Pickup(pick.transform.position + Vector3.up * 0.2f, s.crackColor, 10);
    }

    readonly List<IceTile> candidates = new List<IceTile>(256);
    IceTile PickCrackTarget(IceTile hole, Vector3 wardPos, float minSq, float maxSq)
    {
        candidates.Clear();
        Vector3 hp = hole.transform.position;
        foreach (var t in IceTile.All)
        {
            if (t == hole || t.Current != IceTile.State.Intact) continue;
            Vector3 p = t.transform.position;
            float dw = Flat(p - wardPos), dh = Flat(p - hp);
            if (dw < minSq || dh < minSq || dw > maxSq) continue;
            candidates.Add(t);
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    static float Flat(Vector3 d) { d.y = 0f; return d.sqrMagnitude; }

    /// <summary>구멍이 아닌 가장 가까운 칸 (얼음 밖 땅 포함).</summary>
    Vector3 NearestSolid(Vector3 p)
    {
        var k = Key(p);
        for (int r = 1; r <= 4; r++)
        {
            Vector3 best = Vector3.zero; float bestSq = float.MaxValue; bool found = false;
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                    var c = new Vector2Int(k.x + dx, k.y + dz);
                    Vector3 cp = new Vector3(c.x * s.tileSize, 0f, c.y * s.tileSize);
                    if (grid.TryGetValue(c, out var t) && t && t.Current != IceTile.State.Intact) continue; // 금 간/깨지는/구멍 칸은 피함
                    if (!Arena.Contains(cp)) continue;
                    float sq = (cp - p).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = cp; found = true; }
                }
            if (found) return best;
        }
        return p + Vector3.forward * s.tileSize;
    }
}
