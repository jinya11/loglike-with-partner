using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버섯 숲 파생형 "거대 버섯 숲"의 규칙 컴포넌트 (전용 씬 Region_Mushroom_Giant에 배치).
/// 배치는 모두 씬 오브젝트: 거대 버섯(GiantMushroom: 줄기 + 갓), 오르내리는 곳(ClimbSpot: 줄기 옆 덩굴 사다리).
/// 규칙:
///  - 전장이 두 층: 아래층(지면) / 위층(버섯 갓 윗면, 모든 갓이 같은 높이 capY라 겹친 갓끼리 걸어서 이어짐).
///  - 층 이동: 생명체가 오르내리는 곳 가까이 있을 때 상호작용 키(F)로 다른 층으로 (감시자가 가까이 있어야 키 표시).
///  - 위층의 생명체는 갓 밖으로 걸어 나가지 않음(구멍 회피와 같은 방식). 넉백 등으로 밀려나면 아래층으로 떨어짐.
///  - 적: 같은 층이면 평소처럼 쫓아옴. 다른 층이면 가장 가까운 오르내리는 곳으로 가서 잠깐 뒤 층을 옮김. 갓 밖으로 밀려나면 아래층으로 떨어짐.
///  - 다른 층끼리는 공격·접촉 피해 없음 (StageDirector.LevelTolerance).
///  - 가림: 생명체가 아래층이면 갓이 반투명해짐.
/// 수치는 VariantConfig.giantMushroom.
/// </summary>
public class GiantMushroomVariant : MonoBehaviour
{
    [Tooltip("위층(갓 윗면) 높이 — 씬 빌더가 정함. 0이면 시작할 때 지면 기준으로 계산")] public float capY;

    StageDirector stage;
    GiantMushroomSettings s;
    bool wardUp;
    readonly Dictionary<Enemy, float> climbing = new Dictionary<Enemy, float>(32);
    HashSet<Enemy> seen = new HashSet<Enemy>(), seenNext = new HashSet<Enemy>();
    readonly List<Enemy> tmp = new List<Enemy>(32);

    public bool WardUp => wardUp;
    public float CapY => capY;

    public void Init(StageDirector st, GiantMushroomSettings settings)
    {
        stage = st; s = settings;
        if (capY <= 0f)
        {
            float top = 0f;
            foreach (var m in GiantMushroom.All) top = Mathf.Max(top, Ground.HeightAt(m.transform.position));
            capY = top + s.capHeight;
        }
        foreach (var m in GiantMushroom.All) m.FitHeight(capY);
        foreach (var c in ClimbSpot.Spots)
        {
            c.rule = this;
            c.transform.position = Ground.Snap(c.transform.position);
            c.FitHeight(capY);
        }
        st.LevelTolerance = s.levelTolerance;
        Ward.Hazard = p => wardUp ? !OnCap(p) : InStem(p); // 위층: 갓 밖으로 안 나감 / 아래층: 줄기 피함
        Enemy.GoalOverride = EnemyGoal;
        st.PortalFilter = p => !OnCap(p) && !InStem(p); // 포털은 아래층, 갓 밑이 아닌 곳
        SetWardFloor(false);
    }

    void OnDestroy()
    {
        Ward.Hazard = null;
        Enemy.GoalOverride = null;
        if (stage) { stage.LevelTolerance = float.PositiveInfinity; stage.PortalFilter = null; }
        if (Ward.I != null) Ward.I.FloorY = float.NaN;
    }

    public bool OnCap(Vector3 p) { foreach (var m in GiantMushroom.All) if (m.OnCap(p)) return true; return false; }
    bool InStem(Vector3 p) { foreach (var m in GiantMushroom.All) if (m.InStem(p)) return true; return false; }
    static bool Up(Enemy e) => !float.IsNaN(e.FloorY);

    ClimbSpot NearestSpot(Vector3 p)
    {
        ClimbSpot best = null; float bestSq = float.MaxValue;
        foreach (var c in ClimbSpot.Spots) { Vector3 d = c.Point - p; d.y = 0f; if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = c; } }
        return best;
    }

    /// <summary>생명체가 오르내리는 곳 가까이 있으면 다른 층으로 (상호작용 키).</summary>
    public void TryClimb(ClimbSpot c)
    {
        var ward = Ward.I;
        if (ward == null || !ward.Alive || GameManager.I.State != GameState.Playing) return;
        Vector3 d = c.Point - ward.Position; d.y = 0f;
        if (d.sqrMagnitude > s.climbRange * s.climbRange) return;
        Vfx.Pickup(ward.Position + Vector3.up * 0.5f, s.climbColor, 10);
        SetWardFloor(!wardUp);
        ward.Teleport(c.Point);
        Vfx.Pickup(ward.Position + Vector3.up * 0.5f, s.climbColor, 10);
    }

    void SetWardFloor(bool up)
    {
        wardUp = up;
        if (Ward.I != null) Ward.I.FloorY = up ? capY : float.NaN;
        foreach (var m in GiantMushroom.All) m.SetFaded(!up);
        RebuildNav();
    }

    /// <summary>길찾기: 위층이면 갓 위만 걸을 수 있음, 아래층이면 전부 (줄기는 생명체가 직접 피함).</summary>
    void RebuildNav()
    {
        var nav = stage.Nav;
        if (nav == null) return;
        if (!wardUp) { nav.SetAllBlocked(false); return; }
        nav.SetAllBlocked(true);
        foreach (var m in GiantMushroom.All)
        {
            Vector3 c = m.transform.position;
            float r = m.capRadius - 0.9f;
            for (float x = -r; x <= r; x += 0.6f)
                for (float z = -r; z <= r; z += 0.6f)
                    if (x * x + z * z <= r * r) nav.SetBlocked(c + new Vector3(x, 0f, z), 0.05f, false);
        }
    }

    /// <summary>적의 목표: 다른 층의 생명체를 쫓을 땐 가장 가까운 오르내리는 곳.</summary>
    Vector3? EnemyGoal(Enemy e)
    {
        if (Ward.I == null || Up(e) == wardUp) return null;
        var c = NearestSpot(e.transform.position);
        return c ? c.Point : (Vector3?)null;
    }

    void Update()
    {
        if (s == null || GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        var ward = Ward.I;
        if (ward != null && wardUp && !OnCap(ward.Position)) { SetWardFloor(false); ward.Teleport(ward.Position); } // 밀려나 떨어짐
        if (ward != null && wardUp && stage.PortalOpen) // 포털은 아래층: 클리어하면 가장 가까운 오르내리는 곳으로 내려감
        {
            var c = NearestSpot(ward.Position);
            if (c) { Vfx.Pickup(ward.Position + Vector3.up * 0.5f, s.climbColor, 10); SetWardFloor(false); ward.Teleport(c.Point); }
        }

        seenNext.Clear(); tmp.Clear(); tmp.AddRange(Enemy.Active);
        foreach (var e in tmp)
        {
            seenNext.Add(e);
            Vector3 p = e.transform.position;
            if (!seen.Contains(e)) // 막 생긴 적: 생명체가 위층이고 갓 위에 생겼으면 일부는 위층에
            {
                if (wardUp && OnCap(p) && Random.value < s.upperSpawnShare) SetEnemyFloor(e, true, p);
                continue;
            }
            if (Up(e) && !OnCap(p)) { SetEnemyFloor(e, false, p); continue; } // 갓 밖으로 밀려나 떨어짐
            if (Up(e) == wardUp) { climbing.Remove(e); continue; }
            var c = NearestSpot(p);
            if (!c) continue;
            Vector3 d = c.Point - p; d.y = 0f;
            if (d.sqrMagnitude > 1.4f * 1.4f) { climbing.Remove(e); continue; }
            climbing.TryGetValue(e, out float t);
            if ((t += dt) >= s.enemyClimbDelay) { climbing.Remove(e); SetEnemyFloor(e, !Up(e), c.Point); }
            else climbing[e] = t;
        }
        (seen, seenNext) = (seenNext, seen);
    }

    void SetEnemyFloor(Enemy e, bool up, Vector3 at)
    {
        e.FloorY = up ? capY : float.NaN;
        Vector3 p = new Vector3(at.x, up ? capY : Ground.HeightAt(at), at.z);
        e.transform.position = p;
        if (e.TryGetComponent(out Rigidbody rb)) { rb.position = p; rb.linearVelocity = Vector3.zero; }
    }
}
